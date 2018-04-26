using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.ProBuilder;

namespace UnityEngine.ProBuilder.MeshOperations
{
	public static class AppendPolygon
	{
		const int k_MaxHoleIterations = 2048;

		/// <summary>
		/// Create a new face connecting the vertices selected by indices.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="indices"></param>
		/// <param name="unordered"></param>
		/// <param name="face"></param>
		/// <returns></returns>
		public static ActionResult CreatePolygon(this ProBuilderMesh pb, IList<int> indices, bool unordered, out Face face)
		{
			IntArray[] sharedIndices = pb.sharedIndicesInternal;
			Dictionary<int, int> lookup = sharedIndices.ToDictionary();
			HashSet<int> common = IntArrayUtility.GetCommonIndices(lookup, indices);
			List<Vertex> vertices = new List<Vertex>(Vertex.GetVertices(pb));
			List<Vertex> appendVertices = new List<Vertex>();

			foreach(int i in common)
			{
				int index = sharedIndices[i][0];
				appendVertices.Add(new Vertex(vertices[index]));
			}

			FaceRebuildData data = FaceWithVertices(appendVertices, unordered);

			if(data != null)
			{
				data.sharedIndices = common.ToList();
				List<Face> faces = new List<Face>(pb.facesInternal);
				FaceRebuildData.Apply(new FaceRebuildData[] { data }, vertices, faces, lookup, null);
				pb.SetVertices(vertices);
				pb.SetFaces(faces.ToArray());
				pb.SetSharedIndices(lookup);
				face = data.face;

				return new ActionResult(Status.Success, "Create Polygon");
			}

			face = null;

			const string insufficientPoints = "Too Few Unique Points Selected";
			const string badWinding = "Points not ordered correctly";

			return new ActionResult(Status.Failure, unordered ? insufficientPoints : badWinding);
		}

		/// <summary>
		/// Create a poly shape from a set of points on a plane. The points must be ordered.
		/// </summary>
		/// <param name="poly"></param>
		/// <returns>An action result indicating the status of the operation.</returns>
		internal static ActionResult CreateShapeFromPolygon(this PolyShape poly)
		{
			return poly.mesh.CreateShapeFromPolygon(poly.points, poly.extrude, poly.flipNormals);
		}

		/// <summary>
		/// Rebuild a pb_Object from an ordered set of points.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="points"></param>
		/// <param name="extrude"></param>
		/// <param name="flipNormals"></param>
		/// <returns></returns>
		public static ActionResult CreateShapeFromPolygon(this ProBuilderMesh pb, IList<Vector3> points, float extrude, bool flipNormals)
		{
			if (points.Count < 3)
			{
				pb.Clear();
				pb.ToMesh();
				pb.Refresh();
				return new ActionResult(Status.NoChange, "Too Few Points");
			}

			Vector3[] vertices = points.ToArray();
			List<int> triangles;

			Log.PushLogLevel(LogLevel.Error);

			if(Triangulation.TriangulateVertices(vertices, out triangles, false))
			{
				int[] indices = triangles.ToArray();

				if(ProBuilderMath.PolygonArea(vertices, indices) < Mathf.Epsilon )
				{
					pb.Clear();
					Log.PopLogLevel();
					return new ActionResult(Status.Failure, "Polygon Area < Epsilon");
				}

				pb.Clear();
				pb.GeometryWithVerticesFaces(vertices, new Face[] { new Face(indices) });

				Vector3 nrm = ProBuilderMath.Normal(pb, pb.facesInternal[0]);

				if(Vector3.Dot(Vector3.up, nrm) > 0f)
					pb.facesInternal[0].ReverseIndices();

				pb.DuplicateAndFlip(pb.facesInternal);

				pb.Extrude(new Face[] { pb.facesInternal[1] }, ExtrudeMethod.IndividualFaces, extrude);

				if((extrude < 0f && !flipNormals) || (extrude > 0f && flipNormals))
					pb.ReverseWindingOrder(pb.facesInternal);

				pb.ToMesh();
				pb.Refresh();
			}
			else
			{
				Log.PopLogLevel();
				return new ActionResult(Status.Failure, "Failed Triangulating Points");
			}

			Log.PopLogLevel();

			return new ActionResult(Status.Success, "Create Polygon Shape");
		}

		/// <summary>
		/// Create a new face given a set of unordered vertices (or ordered, if unordered param is set to false).
		/// </summary>
		/// <param name="vertices"></param>
		/// <param name="unordered"></param>
		/// <returns></returns>
		internal static FaceRebuildData FaceWithVertices(List<Vertex> vertices, bool unordered = true)
		{
			List<int> triangles;

			if(Triangulation.TriangulateVertices(vertices, out triangles, unordered))
			{
				FaceRebuildData data = new FaceRebuildData();
				data.vertices = vertices;
				data.face = new Face(triangles.ToArray());
				return data;
			}

			return null;
		}

		/// <summary>
		/// Given a path of vertices, inserts a new vertex in the center inserts triangles along the path.
		/// </summary>
		/// <param name="path"></param>
		/// <returns></returns>
		internal static List<FaceRebuildData> TentCapWithVertices(List<Vertex> path)
		{
			int count = path.Count;
			Vertex center = Vertex.Average(path);
			List<FaceRebuildData> faces = new List<FaceRebuildData>();

			for(int i = 0; i < count; i++)
			{
				List<Vertex> vertices = new List<Vertex>()
				{
					path[i],
					center,
					path[(i+1)%count]
				};

				FaceRebuildData data = new FaceRebuildData();
				data.vertices = vertices;
				data.face = new Face(new int[] {0 , 1, 2});

				faces.Add(data);
			}

			return faces;
		}

		/// <summary>
		/// Find any holes touching one of the passed vertex indices.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="indices"></param>
		/// <returns></returns>
		internal static List<List<Edge>> FindHoles(ProBuilderMesh pb, IList<int> indices)
		{
			Dictionary<int, int> lookup = pb.sharedIndicesInternal.ToDictionary();
			HashSet<int> common = IntArrayUtility.GetCommonIndices(lookup, indices);
			List<List<Edge>> holes = new List<List<Edge>>();
			List<WingedEdge> wings = WingedEdge.GetWingedEdges(pb);

			foreach(List<WingedEdge> hole in AppendPolygon.FindHoles(wings, common))
				holes.Add( hole.Select(x => x.edge.local).ToList() );

			return holes;
		}

		/// <summary>
		/// Find any holes touching one of the passed common indices.
		/// </summary>
		/// <param name="wings"></param>
		/// <param name="common"></param>
		/// <returns></returns>
		internal static List<List<WingedEdge>> FindHoles(List<WingedEdge> wings, HashSet<int> common)
		{
			HashSet<WingedEdge> used = new HashSet<WingedEdge>();
			List<List<WingedEdge>> holes = new List<List<WingedEdge>>();

			for(int i = 0; i < wings.Count; i++)
			{
				WingedEdge c = wings[i];

				// if this edge has been added to a hole already, or the edge isn't in the approved list of indices,
				// or if there's an opposite face, this edge doesn't belong to a hole.  move along.
				if(c.opposite != null || used.Contains(c) || !(common.Contains(c.edge.common.x) || common.Contains(c.edge.common.y)))
					continue;

				List<WingedEdge> hole = new List<WingedEdge>();
				WingedEdge it = c;
				int ind = it.edge.common.x;

				int counter = 0;

				while(it != null && counter++ < k_MaxHoleIterations)
				{
					used.Add(it);
					hole.Add(it);

					ind = it.edge.common.x == ind ? it.edge.common.y : it.edge.common.x;
					it = FindNextEdgeInHole(it, ind);

					if(it == c)
						break;
				}

				List<SimpleTuple<int, int>> splits = new List<SimpleTuple<int, int>>();

				// check previous wings for y == x (closed loop).
				for(int n = 0; n < hole.Count; n++)
				{
					WingedEdge wing = hole[n];

					for(int p = n - 1; p > -1; p--)
					{
						if( wing.edge.common.y == hole[p].edge.common.x )
						{
							splits.Add( new SimpleTuple<int, int>(p, n) );
							break;
						}
					}
				}

				// create new lists from each segment
				// holes paths are nested, with holes
				// possibly split between multiple nested
				// holes
				//
				//	[2, 0]                                     [5, 3]
				// 	[0, 9]                                     [3, 11]
				// 	[9, 10]                                    [11, 10]
				// 		[10, 7]                                    [10, 2]
				// 			[7, 6]             or with split   	    [2, 0]
				// 			[6, 1]             nesting ->   	    [0, 9]
				// 			[1, 4]                             	    [9, 10]
				// 			[4, 7]	<- (y == x)                [10, 7]
				// 		[7, 8]                                 	    [7, 6]
				// 		[8, 5]                                 	    [6, 1]
				// 		[5, 3]                                 	    [1, 4]
				// 		[3, 11]                                	    [4, 7]
				// 		[11, 10]	<- (y == x)                [7, 8]
				// [10, 2] 			<- (y == x)                [8, 5]
				//
				// paths may also contain multiple segments non-tiered

				int splitCount = splits.Count;

				splits.Sort( (x, y) => x.item1.CompareTo(y.item1) );

				int[] shift = new int[splitCount];

				// Debug.Log(hole.ToString("\n") + "\n" + splits.ToString("\n"));

				for(int n = splitCount - 1; n > -1; n--)
				{
					int x = splits[n].item1, y = splits[n].item2 - shift[n];
					int range = (y - x) + 1;

					List<WingedEdge> section = hole.GetRange(x, range);

					hole.RemoveRange(x, range);

					for(int m = n - 1; m > -1; m--)
						if(splits[m].item2 > splits[n].item2)
							shift[m] += range;

					// verify that this path has at least one index that was asked for
					if(splitCount < 2 || section.Any(w => common.Contains(w.edge.common.x)) || section.Any(w => common.Contains(w.edge.common.y)))
						holes.Add( section );
				}
			}

			return holes;
		}

		static WingedEdge FindNextEdgeInHole(WingedEdge wing, int common)
		{
			WingedEdge next = wing.GetAdjacentEdgeWithCommonIndex(common);
			int counter = 0;
			while(next != null && next != wing && counter++ < k_MaxHoleIterations)
			{
				if(next.opposite == null)
					return next;

				next = next.opposite.GetAdjacentEdgeWithCommonIndex(common);
			}

			return null;
		}
	}
}
