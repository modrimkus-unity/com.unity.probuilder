using UnityEditor;
using UnityEditor.EditorTools;
using UnityEditor.Overlays;
using UnityEditor.ProBuilder;
using UnityEditor.ProBuilder.Actions;
using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.UIElements;
using MaterialEditor = UnityEditor.ProBuilder.MaterialEditor;

[Overlay(typeof(SceneView), k_Id, k_Name, true)]
sealed class ProBuilderToolsOverlay : ToolbarOverlay
{
    const string k_Id = "probuilder-tools";
    const string k_Name = "ProBuilder Tools";

    public ProBuilderToolsOverlay()
        : base(
            "ProBuilder/ShapeTool",
                "ProBuilder/MaterialEditor",
                "ProBuilder/SmoothingEditor",
                "ProBuilder/UVEditor",
                "ProBuilder/VertexColor"
            ) {}
}

[EditorToolbarElement("ProBuilder/ShapeTool", typeof(SceneView))]
sealed class ShapeToolElement : EditorToolbarToggle
{
    string k_IconPath = "Packages/com.unity.probuilder/Content/Icons/Tools/EditShape.png";

    MenuToolToggle m_Action;

    public ShapeToolElement()
    {
        m_Action = EditorToolbarLoader.GetInstance<NewShapeToggle>();

        name = m_Action.menuTitle;
        tooltip = m_Action.tooltip.summary;
        icon = m_Action.icon;

        value = false;

        RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
        RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);

        this.RegisterValueChangedCallback(OnToggleValueChanged);
    }

    void OnAttachedToPanel(AttachToPanelEvent evt)
    {
        ToolManager.activeToolChanged += OnActiveToolChanged;
    }

    void OnDetachFromPanel(DetachFromPanelEvent evt)
    {
        ToolManager.activeToolChanged -= OnActiveToolChanged;
    }

    void OnActiveToolChanged()
    {
        if(value && !ToolManager.IsActiveTool(m_Action.Tool))
           value = false;
    }


    void OnToggleValueChanged(ChangeEvent<bool> toggleValue)
    {
        if(toggleValue.newValue)
            m_Action.PerformAction();
        else if(ToolManager.IsActiveTool(m_Action.Tool))
            m_Action.EndActivation();
    }
}


// [EditorToolbarElement("ProBuilder/PolyShape", typeof(SceneView))]
// sealed class PolyShapeElement : EditorToolbarToggle
// {
//      string k_IconPath = "Packages/com.unity.probuilder/Content/Icons/Tools/PolyShape/CreatePolyShape.png";
//
//      MenuToolToggle m_Action;
//
//      public PolyShapeElement()
//          : base()
//      {
//          m_Action = EditorToolbarLoader.GetInstance<NewPolyShapeToggle>();
//
//          name = m_Action.menuTitle;
//          tooltip = m_Action.tooltip.summary;
//          icon = m_Action.icon;
//
//          this.value = false;
//
//          RegisterCallback<AttachToPanelEvent>(OnAttachedToPanel);
//          RegisterCallback<DetachFromPanelEvent>(OnDetachFromPanel);
//
//          this.RegisterValueChangedCallback(OnToggleValueChanged);
//      }
//
//      void OnAttachedToPanel(AttachToPanelEvent evt)
//      {
//          ToolManager.activeContextChanged += OnActiveToolChanging;
//      }
//
//      void OnDetachFromPanel(DetachFromPanelEvent evt)
//      {
//          ToolManager.activeContextChanged -= OnActiveToolChanging;
//      }
//
//      void OnActiveToolChanging()
//      {
//          // Debug.Log("ActiveToolChanging "+ToolManager.IsActiveTool(m_Action.Tool));
//          // if(this.value && !ToolManager.IsActiveTool(m_Action.Tool))
//          //    this.value = false;
//      }
//
//
//      void OnToggleValueChanged(ChangeEvent<bool> toggleValue)
//      {
//          if(toggleValue.newValue)
//          {
//              m_Action.PerformAction();
//          }
//          else
//          {
//              //if(ToolManager.IsActiveTool(m_Action.Tool))
//              if(ToolManager.activeToolType == m_Action.Tool.GetType())
//              {
//                  Debug.Log("Tool is still active");
//                  m_Action.EndActivation();
//              }
//          }
//      }
// }

[EditorToolbarElement("ProBuilder/SmoothingEditor", typeof(SceneView))]
sealed class SmoothingEditorElement : EditorToolbarButton
{
    static readonly string k_Name = L10n.Tr("Smoothing Groups");
    const string k_IconPath = "Toolbar/SelectBySmoothingGroup";
    static readonly string k_Tooltip = L10n.Tr("Opens the Material Editor window.\n\nThe Material Editor window applies materials to selected faces or objects.");

    public SmoothingEditorElement()
        : base(k_Name, IconUtility.GetIcon(k_IconPath, EditorGUIUtility.isProSkin ? IconSkin.Pro : IconSkin.Light), OnClicked)
    {
        tooltip = k_Tooltip;
    }

    static void OnClicked()
    {
        MaterialEditor.MenuOpenMaterialEditor();
    }
}

[EditorToolbarElement("ProBuilder/MaterialEditor", typeof(SceneView))]
sealed class MaterialEditorElement : EditorToolbarButton
{
    static readonly string k_Name = L10n.Tr("Material Editor");
    const string k_IconPath = "Toolbar/SelectByMaterial";
    static readonly string k_Tooltip = L10n.Tr("Opens the Material Editor window.\n\nThe Material Editor window applies materials to selected faces or objects.");

    public MaterialEditorElement()
        : base(k_Name, IconUtility.GetIcon(k_IconPath, EditorGUIUtility.isProSkin ? IconSkin.Pro : IconSkin.Light), OnClicked)
    {
        tooltip = k_Tooltip;
    }

    static void OnClicked()
    {
        MaterialEditor.MenuOpenMaterialEditor();
    }
}

[EditorToolbarElement("ProBuilder/UVEditor", typeof(SceneView))]
sealed class UVEditorElement : EditorToolbarButton
{
    static readonly string k_Name = L10n.Tr("UV Editor");
    const string k_IconPath = "Toolbar/SceneManipUVs";
    static readonly string k_Tooltip = L10n.Tr("Opens the UV Editor window.\n\nThe UV Editor allows you to change how textures are rendered on this mesh.");

    public UVEditorElement()
        : base(k_Name, IconUtility.GetIcon(k_IconPath, EditorGUIUtility.isProSkin ? IconSkin.Pro : IconSkin.Light), OnClicked)
    {
        tooltip = k_Tooltip;
    }

    static void OnClicked()
    {
        UVEditor.MenuOpenUVEditor();
    }
}

[EditorToolbarElement("ProBuilder/VertexColor", typeof(SceneView))]
sealed class VertexColorElement : EditorToolbarButton
{
    static readonly string k_Name = L10n.Tr("Vertex Colors");
    const string k_IconPath = "Toolbar/SelectByVertexColor";
    static readonly string k_Tooltip = L10n.Tr("Opens the Vertex Color Palette.\n\nApply using Face mode for hard-edged colors.\nApply using Edge or Vertex mode for soft, blended colors.");

    public VertexColorElement()
        : base(k_Name, IconUtility.GetIcon(k_IconPath, EditorGUIUtility.isProSkin ? IconSkin.Pro : IconSkin.Light), OnClicked)
    {
        tooltip = k_Tooltip;
    }

    static void OnClicked()
    {
        VertexColorPalette.MenuOpenWindow();
    }
}
