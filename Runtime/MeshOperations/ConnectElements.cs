using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.ProBuilder;

namespace UnityEngine.ProBuilder.MeshOperations
{
	/// <summary>
	/// Store face rebuild data with indices to mark which vertices are new.
	/// </summary>
	class ConnectFaceRebuildData
	{
		public FaceRebuildData faceRebuildData;
		public List<int> newVertexIndices;

		public ConnectFaceRebuildData(FaceRebuildData faceRebuildData, List<int> newVertexIndices)
		{
			this.faceRebuildData = faceRebuildData;
			this.newVertexIndices = newVertexIndices;
		}
	};

	/// <summary>
	/// Utility class for connecting edges, faces, and vertices.
	/// </summary>
	public static class ConnectElements
	{
		/// <summary>
		/// Subdivide faces.
		/// </summary>
		/// <param name="pb">pb_Object target.</param>
		/// <param name="faces">The faces to subdivide (more accurately, poke).</param>
		/// <param name="subdividedFaces">The resulting faces.</param>
		/// <returns>An action result indicating the status of the operation.</returns>
		public static ActionResult Connect(this ProBuilderMesh pb, IEnumerable<Face> faces, out Face[] subdividedFaces)
		{
			IEnumerable<Edge> edges = faces.SelectMany(x => x.edges);
			HashSet<Face> mask = new HashSet<Face>(faces);
			Edge[] empty;
			return Connect(pb, edges, out subdividedFaces, out empty, true, false, mask);
		}

		/// <summary>
		/// Insert new edges connecting a set of edges.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="edges"></param>
		/// <param name="faces">The faces created as a result of inserting new edges.</param>
		/// <returns>A result with the state of the action.</returns>
		public static ActionResult Connect(this ProBuilderMesh pb, IEnumerable<Edge> edges, out Face[] faces)
		{
			Edge[] empty;
			return Connect(pb, edges, out faces, out empty, true, false);
		}

		/// <summary>
		/// Insert new edges connecting a set of edges.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="edges"></param>
		/// <param name="connections">The edges created as a result of inserting new edges.</param>
		/// <returns>A result with the state of the action.</returns>
		public static ActionResult Connect(this ProBuilderMesh pb, IEnumerable<Edge> edges, out Edge[] connections)
		{
			Face[] empty;
			return Connect(pb, edges, out empty, out connections, false, true);
		}

		/// <summary>
		/// Connect vertices inserts an edge between a list of indices.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="indices">A list of indices (corresponding to the pb_Object.vertices array) to connect with new edges.</param>
		/// <param name="newVertices">A list of newly created vertex indices.</param>
		/// <returns>An action result indicating the status of the operation.</returns>
		public static ActionResult Connect(this ProBuilderMesh pb, IList<int> indices, out int[] newVertices)
		{
			int sharedIndexOffset = pb.sharedIndicesInternal.Length;
			Dictionary<int, int> lookup = pb.sharedIndicesInternal.ToDictionary();

			HashSet<int> distinct = new HashSet<int>(indices.Select(x=>lookup[x]));
			HashSet<int> affected = new HashSet<int>();

			foreach(int i in distinct)
				affected.UnionWith(pb.sharedIndicesInternal[i].array);

			Dictionary<Face, List<int>> splits = new Dictionary<Face, List<int>>();
			List<Vertex> vertices = new List<Vertex>(Vertex.GetVertices(pb));

			foreach(Face face in pb.facesInternal)
			{
				int[] faceIndices = face.distinctIndices;

				for(int i = 0; i < faceIndices.Length; i++)
				{
					if( affected.Contains(faceIndices[i]) )
						splits.AddOrAppend(face, faceIndices[i]);
				}
			}

			List<ConnectFaceRebuildData> appendFaces = new List<ConnectFaceRebuildData>();
			List<Face> successfulSplits = new List<Face>();
			HashSet<int> usedTextureGroups = new HashSet<int>(pb.facesInternal.Select(x => x.textureGroup));
			int newTextureGroupIndex = 1;

			foreach(KeyValuePair<Face, List<int>> split in splits)
			{
				Face face = split.Key;

				List<ConnectFaceRebuildData> res = split.Value.Count == 2 ?
					ConnectIndicesInFace(face, split.Value[0], split.Value[1], vertices, lookup) :
					ConnectIndicesInFace(face, split.Value, vertices, lookup, sharedIndexOffset++);

				if(res == null)
					continue;

				if(face.textureGroup < 0)
				{
					while(usedTextureGroups.Contains(newTextureGroupIndex))
						newTextureGroupIndex++;

					usedTextureGroups.Add(newTextureGroupIndex);
				}

				foreach(ConnectFaceRebuildData c in res)
				{
					c.faceRebuildData.face.textureGroup 	= face.textureGroup < 0 ? newTextureGroupIndex : face.textureGroup;
					c.faceRebuildData.face.uv 				= new AutoUnwrapSettings(face.uv);
					c.faceRebuildData.face.smoothingGroup 	= face.smoothingGroup;
					c.faceRebuildData.face.manualUV 		= face.manualUV;
					c.faceRebuildData.face.material 		= face.material;
				}

				successfulSplits.Add(face);
				appendFaces.AddRange(res);
			}

			FaceRebuildData.Apply( appendFaces.Select(x => x.faceRebuildData), pb, vertices, null, lookup, null );
			pb.SetSharedIndices(lookup);
			pb.SetSharedIndicesUV(new IntArray[0]);
			int removedVertexCount = pb.DeleteFaces(successfulSplits).Length;

			lookup = pb.sharedIndicesInternal.ToDictionary();

			HashSet<int> newVertexIndices = new HashSet<int>();

			for(int i = 0; i < appendFaces.Count; i++)
				for(int n = 0; n < appendFaces[i].newVertexIndices.Count; n++)
					newVertexIndices.Add( lookup[appendFaces[i].newVertexIndices[n] + (appendFaces[i].faceRebuildData.Offset() - removedVertexCount)] );

			newVertices = newVertexIndices.Select(x => pb.sharedIndicesInternal[x][0]).ToArray();

			pb.ToMesh();

			return new ActionResult(Status.Success, string.Format("Connected {0} Vertices", distinct.Count));
		}

		/// <summary>
		/// Inserts new edges connecting the passed edges, optionally restricting new edge insertion to faces in faceMask.
		/// </summary>
		/// <param name="pb"></param>
		/// <param name="edges"></param>
		/// <param name="addedFaces"></param>
		/// <param name="connections"></param>
		/// <param name="returnFaces"></param>
		/// <param name="returnEdges"></param>
		/// <param name="faceMask"></param>
		/// <returns></returns>
		static ActionResult Connect(
			this ProBuilderMesh pb,
			IEnumerable<Edge> edges,
			out Face[] addedFaces,
			out Edge[] connections,
			bool returnFaces = false,
			bool returnEdges = false,
			HashSet<Face> faceMask = null)
		{
			Dictionary<int, int> lookup = pb.sharedIndicesInternal.ToDictionary();
			Dictionary<int, int> lookupUV = pb.sharedIndicesUVInternal != null ? pb.sharedIndicesUVInternal.ToDictionary() : null;
			HashSet<EdgeLookup> distinctEdges = new HashSet<EdgeLookup>(EdgeLookup.GetEdgeLookup(edges, lookup));
			List<WingedEdge> wings = WingedEdge.GetWingedEdges(pb);

			// map each edge to a face so that we have a list of all touched faces with their to-be-subdivided edges
			Dictionary<Face, List<WingedEdge>> touched = new Dictionary<Face, List<WingedEdge>>();
			List<WingedEdge> faceEdges;

			foreach(WingedEdge wing in wings)
			{
				if( distinctEdges.Contains(wing.edge) )
				{
					if(touched.TryGetValue(wing.face, out faceEdges))
						faceEdges.Add(wing);
					else
						touched.Add(wing.face, new List<WingedEdge>() { wing });
				}
			}

			Dictionary<Face, List<WingedEdge>> affected = new Dictionary<Face, List<WingedEdge>>();

			// weed out edges that won't actually connect to other edges (if you don't play ya' can't stay)
			foreach(KeyValuePair<Face, List<WingedEdge>> kvp in touched)
			{
				if(kvp.Value.Count <= 1)
				{
					WingedEdge opp = kvp.Value[0].opposite;

					if(opp == null)
						continue;

					List<WingedEdge> opp_list;

					if(!touched.TryGetValue(opp.face, out opp_list))
						continue;

					if(opp_list.Count <= 1)
						continue;
				}

				affected.Add(kvp.Key, kvp.Value);
			}

			List<Vertex> vertices = new List<Vertex>( Vertex.GetVertices(pb) );
			List<ConnectFaceRebuildData> results = new List<ConnectFaceRebuildData>();
			// just the faces that where connected with > 1 edge
			List<Face> connectedFaces = new List<Face>();

			HashSet<int> usedTextureGroups = new HashSet<int>(pb.facesInternal.Select(x => x.textureGroup));
			int newTextureGroupIndex = 1;

			// do the splits
			foreach(KeyValuePair<Face, List<WingedEdge>> split in affected)
			{
				Face face = split.Key;
				List<WingedEdge> targetEdges = split.Value;
				int inserts = targetEdges.Count;
				Vector3 nrm = ProBuilderMath.Normal(vertices, face.indices);

				if(inserts == 1 || (faceMask != null && !faceMask.Contains(face)))
				{
					ConnectFaceRebuildData c = InsertVertices(face, targetEdges, vertices);

					Vector3 fn = ProBuilderMath.Normal(c.faceRebuildData.vertices, c.faceRebuildData.face.indices);

					if(Vector3.Dot(nrm, fn) < 0)
						c.faceRebuildData.face.ReverseIndices();

					results.Add( c );
				}
				else
				if(inserts > 1)
				{
					List<ConnectFaceRebuildData> res = inserts == 2 ?
						ConnectEdgesInFace(face, targetEdges[0], targetEdges[1], vertices) :
						ConnectEdgesInFace(face, targetEdges, vertices);

					if(face.textureGroup < 0)
					{
						while(usedTextureGroups.Contains(newTextureGroupIndex))
							newTextureGroupIndex++;

						usedTextureGroups.Add(newTextureGroupIndex);
					}

					foreach(ConnectFaceRebuildData c in res)
					{
						connectedFaces.Add(c.faceRebuildData.face);

						Vector3 fn = ProBuilderMath.Normal(c.faceRebuildData.vertices, c.faceRebuildData.face.indices);

						if(Vector3.Dot(nrm, fn) < 0)
							c.faceRebuildData.face.ReverseIndices();

						c.faceRebuildData.face.textureGroup 	= face.textureGroup < 0 ? newTextureGroupIndex : face.textureGroup;
						c.faceRebuildData.face.uv 				= new AutoUnwrapSettings(face.uv);
						c.faceRebuildData.face.smoothingGroup 	= face.smoothingGroup;
						c.faceRebuildData.face.manualUV 		= face.manualUV;
						c.faceRebuildData.face.material 		= face.material;
					}

					results.AddRange(res);
				}
			}

			FaceRebuildData.Apply(results.Select(x => x.faceRebuildData), pb, vertices, null, lookup, lookupUV);

			pb.SetSharedIndicesUV(new IntArray[0]);
			int removedVertexCount = pb.DeleteFaces(affected.Keys).Length;
			pb.SetSharedIndices(IntArrayUtility.ExtractSharedIndices(pb.positionsInternal));
			pb.ToMesh();

			// figure out where the new edges where inserted
			if(returnEdges)
			{
				// offset the newVertexIndices by whatever the FaceRebuildData did so we can search for the new edges by index
				HashSet<int> appendedIndices = new HashSet<int>();

				for(int n = 0; n < results.Count; n++)
					for(int i = 0; i < results[n].newVertexIndices.Count; i++)
						appendedIndices.Add( ( results[n].newVertexIndices[i] + results[n].faceRebuildData.Offset() ) - removedVertexCount );

				Dictionary<int, int> lup = pb.sharedIndicesInternal.ToDictionary();
				IEnumerable<Edge> newEdges = results.SelectMany(x => x.faceRebuildData.face.edges).Where(x => appendedIndices.Contains(x.x) && appendedIndices.Contains(x.y));
				IEnumerable<EdgeLookup> distNewEdges = EdgeLookup.GetEdgeLookup(newEdges, lup);

				connections = distNewEdges.Distinct().Select(x => x.local).ToArray();
			}
			else
			{
				connections = null;
			}

			if(returnFaces)
				addedFaces = connectedFaces.ToArray();
			else
				addedFaces = null;

			return new ActionResult(Status.Success, string.Format("Connected {0} Edges", results.Count / 2));
		}

		/// <summary>
		/// Accepts a face and set of edges to split on.
		/// </summary>
		/// <param name="face"></param>
		/// <param name="a"></param>
		/// <param name="b"></param>
		/// <param name="vertices"></param>
		/// <returns></returns>
		static List<ConnectFaceRebuildData> ConnectEdgesInFace(
			Face face,
			WingedEdge a,
			WingedEdge b,
			List<Vertex> vertices)
		{
			List<Edge> perimeter = WingedEdge.SortEdgesByAdjacency(face);

			List<Vertex>[] n_vertices = new List<Vertex>[2] {
				new List<Vertex>(),
				new List<Vertex>()
			};

			List<int>[] n_indices = new List<int>[2] {
				new List<int>(),
				new List<int>()
			};

			int index = 0;

			// creates two new polygon perimeter lines by stepping the current face perimeter and inserting new vertices where edges match
			for(int i = 0; i < perimeter.Count; i++)
			{
				n_vertices[index % 2].Add(vertices[perimeter[i].x]);

				if(perimeter[i].Equals(a.edge.local) || perimeter[i].Equals(b.edge.local))
				{
					Vertex mix = Vertex.Mix(vertices[perimeter[i].x], vertices[perimeter[i].y], .5f);

					n_indices[index % 2].Add(n_vertices[index % 2].Count);
					n_vertices[index % 2].Add(mix);
					index++;
					n_indices[index % 2].Add(n_vertices[index % 2].Count);
					n_vertices[index % 2].Add(mix);
				}
			}

			List<ConnectFaceRebuildData> faces = new List<ConnectFaceRebuildData>();

			for(int i = 0; i < n_vertices.Length; i++)
			{
				FaceRebuildData f = AppendPolygon.FaceWithVertices(n_vertices[i], false);
				faces.Add(new ConnectFaceRebuildData(f, n_indices[i]));
			}

			return faces;
		}

		/// <summary>
		/// Insert a new vertex at the center of a face and connect the center of all edges to it.
		/// </summary>
		/// <param name="face"></param>
		/// <param name="edges"></param>
		/// <param name="vertices"></param>
		/// <returns></returns>
		static List<ConnectFaceRebuildData> ConnectEdgesInFace(
			Face face,
			List<WingedEdge> edges,
			List<Vertex> vertices)
		{
			List<Edge> perimeter = WingedEdge.SortEdgesByAdjacency(face);

			int splitCount = edges.Count;

			Vertex centroid = Vertex.Average(vertices, face.distinctIndices);

			List<List<Vertex>> n_vertices = InternalUtility.Fill<List<Vertex>>(x => { return new List<Vertex>(); }, splitCount);
			List<List<int>> n_indices = InternalUtility.Fill<List<int>>(x => { return new List<int>(); }, splitCount);

			HashSet<Edge> edgesToSplit = new HashSet<Edge>(edges.Select(x => x.edge.local));

			int index = 0;

			// creates two new polygon perimeter lines by stepping the current face perimeter and inserting new vertices where edges match
			for(int i = 0; i < perimeter.Count; i++)
			{
				n_vertices[index % splitCount].Add(vertices[perimeter[i].x]);

				if( edgesToSplit.Contains(perimeter[i]) )
				{
					Vertex mix = Vertex.Mix(vertices[perimeter[i].x], vertices[perimeter[i].y], .5f);

					// split current poly line
					n_indices[index].Add(n_vertices[index].Count);
					n_vertices[index].Add(mix);

					// add the centroid vertex
					n_indices[index].Add(n_vertices[index].Count);
					n_vertices[index].Add(centroid);

					// advance the poly line index
					index = (index + 1) % splitCount;

					// then add the edge center vertex and move on
					n_vertices[index].Add(mix);
				}
			}

			List<ConnectFaceRebuildData> faces = new List<ConnectFaceRebuildData>();

			for(int i = 0; i < n_vertices.Count; i++)
			{
				FaceRebuildData f = AppendPolygon.FaceWithVertices(n_vertices[i], false);
				faces.Add(new ConnectFaceRebuildData(f, n_indices[i]));
			}

			return faces;
		}

		static ConnectFaceRebuildData InsertVertices(Face face, List<WingedEdge> edges, List<Vertex> vertices)
		{
			List<Edge> perimeter = WingedEdge.SortEdgesByAdjacency(face);
			List<Vertex> n_vertices = new List<Vertex>();
			List<int> newVertexIndices = new List<int>();
			HashSet<Edge> affected = new HashSet<Edge>( edges.Select(x=>x.edge.local) );

			for(int i = 0; i < perimeter.Count; i++)
			{
				n_vertices.Add(vertices[perimeter[i].x]);

				if(affected.Contains(perimeter[i]))
				{
					newVertexIndices.Add(n_vertices.Count);
					n_vertices.Add(Vertex.Mix(vertices[perimeter[i].x], vertices[perimeter[i].y], .5f));
				}
			}

			FaceRebuildData res = AppendPolygon.FaceWithVertices(n_vertices, false);

			res.face.textureGroup 	= face.textureGroup;
			res.face.uv 			= new AutoUnwrapSettings(face.uv);
			res.face.smoothingGroup = face.smoothingGroup;
			res.face.manualUV 		= face.manualUV;
			res.face.material 		= face.material;

			return new ConnectFaceRebuildData(res, newVertexIndices);
		}

		static List<ConnectFaceRebuildData> ConnectIndicesInFace(
			Face face,
			int a,
			int b,
			List<Vertex> vertices,
			Dictionary<int, int> lookup)
		{
			List<Edge> perimeter = WingedEdge.SortEdgesByAdjacency(face);

			List<Vertex>[] n_vertices = new List<Vertex>[] {
				new List<Vertex>(),
				new List<Vertex>()
			};

			List<int>[] n_sharedIndices = new List<int>[] {
				new List<int>(),
				new List<int>()
			};

			List<int>[] n_indices = new List<int>[] {
				new List<int>(),
				new List<int>()
			};

			int index = 0;

			for(int i = 0; i < perimeter.Count; i++)
			{
				// trying to connect two vertices that are already connected
				if(perimeter[i].Contains(a) && perimeter[i].Contains(b))
					return null;

				int cur = perimeter[i].x;

				n_vertices[index].Add(vertices[cur]);
				n_sharedIndices[index].Add(lookup[cur]);

				if(cur == a || cur == b)
				{
					index = (index + 1) % 2;

					n_indices[index].Add(n_vertices[index].Count);
					n_vertices[index].Add(vertices[cur]);
					n_sharedIndices[index].Add(lookup[cur]);
				}
			}

			List<ConnectFaceRebuildData> faces = new List<ConnectFaceRebuildData>();
			Vector3 nrm = ProBuilderMath.Normal(vertices, face.indices);

			for(int i = 0; i < n_vertices.Length; i++)
			{
				FaceRebuildData f = AppendPolygon.FaceWithVertices(n_vertices[i], false);
				f.sharedIndices = n_sharedIndices[i];

				Vector3 fn = ProBuilderMath.Normal(n_vertices[i], f.face.indices);

				if(Vector3.Dot(nrm, fn) < 0)
					f.face.ReverseIndices();

				faces.Add(new ConnectFaceRebuildData(f, n_indices[i]));
			}

			return faces;
		}

		static List<ConnectFaceRebuildData> ConnectIndicesInFace(
			Face face,
			List<int> indices,
			List<Vertex> vertices,
			Dictionary<int, int> lookup,
			int sharedIndexOffset)
		{
			if(indices.Count < 3)
				return null;

			List<Edge> perimeter = WingedEdge.SortEdgesByAdjacency(face);

			int splitCount = indices.Count;

			List<List<Vertex>> n_vertices = InternalUtility.Fill<List<Vertex>>(x => { return new List<Vertex>(); }, splitCount);
			List<List<int>> n_sharedIndices = InternalUtility.Fill<List<int>>(x => { return new List<int>(); }, splitCount);
			List<List<int>> n_indices = InternalUtility.Fill<List<int>>(x => { return new List<int>(); }, splitCount);

			Vertex center = Vertex.Average(vertices, indices);
			Vector3 nrm = ProBuilderMath.Normal(vertices, face.indices);

			int index = 0;

			for(int i = 0; i < perimeter.Count; i++)
			{
				int cur = perimeter[i].x;

				n_vertices[index].Add(vertices[cur]);
				n_sharedIndices[index].Add(lookup[cur]);

				if( indices.Contains(cur) )
				{
					n_indices[index].Add(n_vertices[index].Count);
					n_vertices[index].Add(center);
					n_sharedIndices[index].Add(sharedIndexOffset);

					index = (index + 1) % splitCount;

					n_indices[index].Add(n_vertices[index].Count);
					n_vertices[index].Add(vertices[cur]);
					n_sharedIndices[index].Add(lookup[cur]);
				}
			}

			List<ConnectFaceRebuildData> faces = new List<ConnectFaceRebuildData>();

			for(int i = 0; i < n_vertices.Count; i++)
			{
				if(n_vertices[i].Count < 3)
					continue;

				FaceRebuildData f = AppendPolygon.FaceWithVertices(n_vertices[i], false);
				f.sharedIndices = n_sharedIndices[i];

				Vector3 fn = ProBuilderMath.Normal(n_vertices[i], f.face.indices);

				if(Vector3.Dot(nrm, fn) < 0)
					f.face.ReverseIndices();

				faces.Add(new ConnectFaceRebuildData(f, n_indices[i]));
			}

			return faces;
		}
	}
}
