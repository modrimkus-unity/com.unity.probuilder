using UnityEditor.Toolbars;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.UIElements;

namespace UnityEditor.ProBuilder.Actions
{
    [MenuActionShortcut(typeof(SceneView), KeyCode.P)]
    sealed class ToggleHandleOrientation : MenuAction
    {
        Texture2D[] m_Icons;

        HandleOrientation handleOrientation
        {
            get { return VertexManipulationTool.handleOrientation; }
            set
            {
                VertexManipulationTool.handleOrientation = value;
                ProBuilderEditor.Refresh(false);
            }
        }

        public override ToolbarGroup group
        {
            get { return ToolbarGroup.Settings; }
        }

        public override Texture2D icon
        {
            get { return m_Icons[(int)handleOrientation]; }
        }

        public override int toolbarPriority
        {
            get { return 0; }
        }

        public override TooltipContent tooltip
        {
            get { return k_Tooltips[(int)handleOrientation]; }
        }

        static readonly TooltipContent[] k_Tooltips = new TooltipContent[]
        {
            new TooltipContent("Global", "The transform handle is oriented in a fixed direction.", 'P'),
            new TooltipContent("Local", "The transform handle is aligned with the active object rotation.", 'P'),
            new TooltipContent("Normal", "The transform handle is aligned with the active element selection.", 'P')
        };

        public override string menuTitle
        {
            get { return "Orientation: " + k_Tooltips[(int)handleOrientation].title; }
        }

        public override SelectMode validSelectModes
        {
            get { return SelectMode.Vertex | SelectMode.Edge | SelectMode.Face; }
        }

        public override bool hidden
        {
            get { return false; }
        }

        public ToggleHandleOrientation()
        {
            m_Icons = new Texture2D[]
            {
                IconUtility.GetIcon("Toolbar/HandleAlign_World", IconSkin.Pro),
                IconUtility.GetIcon("Toolbar/HandleAlign_Local", IconSkin.Pro),
                IconUtility.GetIcon("Toolbar/HandleAlign_Plane", IconSkin.Pro),
            };
        }

        protected override ActionResult PerformActionImplementation()
        {
            handleOrientation = InternalUtility.NextEnumValue(handleOrientation);
            return new ActionResult(ActionResult.Status.Success, "Set Handle Orientation\n" + k_Tooltips[(int)handleOrientation].title);
        }

        public override bool enabled
        {
            get { return ProBuilderEditor.instance != null; }
        }
    }

    [EditorToolbarElement("ProBuilder Tool Settings/Pivot Rotation")]
    class PivotRotationDropdown : EditorToolbarDropdown
    {
        readonly GUIContent m_Local;
        readonly GUIContent m_Global;
        readonly GUIContent m_Normal;
        readonly ToggleHandleOrientation m_MenuAction;

        public PivotRotationDropdown()
        {
            m_MenuAction = EditorToolbarLoader.GetInstance<ToggleHandleOrientation>();
            name = "Pivot Rotation";

            m_Global = EditorGUIUtility.TrTextContent("Global",
                "Toggle Tool Handle Rotation\n\nTool handles are in global rotation.",
                "ToolHandleGlobal");
            m_Local = EditorGUIUtility.TrTextContent("Local",
                "Toggle Tool Handle Rotation\n\nTool handles are in the active object's rotation.",
                "ToolHandleLocal");
            m_Normal = EditorGUIUtility.TrTextContent("Normal",
                "The transform handle is aligned with the active element selection",
                "ToolHandleLocal");

            RegisterCallback<AttachToPanelEvent>(AttachedToPanel);
            RegisterCallback<DetachFromPanelEvent>(DetachedFromPanel);

            clicked += OpenContextMenu;

            OnDropdownOptionChange();
        }

        void OpenContextMenu()
        {
            var menu = new GenericMenu();
            menu.AddItem(m_Global, VertexManipulationTool.handleOrientation == HandleOrientation.World,
                () => SetHandleOrientationIfNeeded(HandleOrientation.World));

            menu.AddItem(m_Local, VertexManipulationTool.handleOrientation == HandleOrientation.ActiveObject,
                () => SetHandleOrientationIfNeeded(HandleOrientation.ActiveObject));

            menu.AddItem(m_Normal, VertexManipulationTool.handleOrientation == HandleOrientation.ActiveElement,
                () => SetHandleOrientationIfNeeded(HandleOrientation.ActiveElement));

            menu.DropDown(worldBound);
        }

        void SetHandleOrientationIfNeeded(HandleOrientation handleOrientation)
        {
            if (VertexManipulationTool.handleOrientation != handleOrientation)
            {
                VertexManipulationTool.handleOrientation = handleOrientation;
                OnDropdownOptionChange();
            }
        }

        void OnDropdownOptionChange()
        {
            var content = Tools.pivotRotation == PivotRotation.Global ? m_Global : m_Local;
            text = content.text;
            tooltip = content.tooltip;
            icon = content.image as Texture2D;

            //Ensuring constant size of the text area
            var textElement = this.Q<TextElement>(UnityEditor.Toolbars.EditorToolbar.elementLabelClassName);
            if (textElement != null)
                textElement.style.width = 40;
        }

        void AttachedToPanel(AttachToPanelEvent evt)
        {
            MenuAction.afterActionPerformed += OnMenuActionPerformed;
        }

        void DetachedFromPanel(DetachFromPanelEvent evt)
        {
            MenuAction.afterActionPerformed -= OnMenuActionPerformed;
        }

        void OnMenuActionPerformed(MenuAction menuAction)
        {
            if (menuAction == m_MenuAction)
                OnDropdownOptionChange();
        }
    }
}
