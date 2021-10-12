#if UNITY_2021_2_OR_NEWER
#define OVERLAYS_AVAILABLE
#endif
using System;
using System.Collections.Generic;
using UnityEditor.EditorTools;
using UnityEngine;
using UnityEngine.ProBuilder;

namespace UnityEditor.ProBuilder
{

#if UNITY_2020_2_OR_NEWER
#if OVERLAYS_AVAILABLE
    abstract class PositionToolContext : EditorToolContext
#else
    class PositionToolContext : EditorToolContext
#endif
    {
        protected override Type GetEditorToolType(Tool tool)
        {
            switch(tool)
            {
                case Tool.Move:
                    return typeof(ProbuilderMoveTool);
                case Tool.Rotate:
                    return typeof(ProbuilderRotateTool);
                case Tool.Scale:
                    return typeof(ProbuilderScaleTool);
                default:
                    return null;
            }
        }
    }
    class TextureToolContext : EditorToolContext
    {
        TextureToolContext() { }

        protected override Type GetEditorToolType(Tool tool)
        {
            switch(tool)
            {
                case Tool.Move:
                    return typeof(TextureMoveTool);
                case Tool.Rotate:
                    return typeof(TextureRotateTool);
                case Tool.Scale:
                    return typeof(TextureScaleTool);
                default:
                    return null;
            }
        }
    }
#endif

#if OVERLAYS_AVAILABLE
    [EditorToolContext("Vertex", typeof(ProBuilderMesh)), Icon(k_IconPath)]
    class VertexToolContext : PositionToolContext
    {
        const string k_IconPath = "Packages/com.unity.probuilder/Content/Icons/Modes/Mode_Vertex.png";

        public override void OnActivated()
        {
            ProBuilderEditor.selectMode = SelectMode.Vertex;
        }
    }

    [EditorToolContext("Edge", typeof(ProBuilderMesh)), Icon(k_IconPath)]
    class EdgeToolContext : PositionToolContext
    {
        const string k_IconPath = "Packages/com.unity.probuilder/Content/Icons/Modes/Mode_Edge.png";

        public override void OnActivated()
        {
            ProBuilderEditor.selectMode = SelectMode.Edge;
        }
    }

    [EditorToolContext("Face", typeof(ProBuilderMesh)), Icon(k_IconPath)]
    class FaceToolContext : PositionToolContext
    {
        const string k_IconPath = "Packages/com.unity.probuilder/Content/Icons/Modes/Mode_Face.png";

        public override void OnActivated()
        {
            ProBuilderEditor.selectMode = SelectMode.Face;
        }
    }
#endif
}
