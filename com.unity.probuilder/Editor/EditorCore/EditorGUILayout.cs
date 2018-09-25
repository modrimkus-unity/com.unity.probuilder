﻿using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.ProBuilder;

namespace UnityEditor.ProBuilder.UI
{
	/// <summary>
	/// Additional GUI functions for Editor use.
	/// </summary>
	static class EditorGUILayout
	{
		static bool s_RowToggle = true;
		static readonly Color s_RowOddColor = new Color(.45f, .45f, .45f, .2f);
		static readonly Color s_RowEvenColor = new Color(.30f, .30f, .30f, .2f);

		public static void BeginRow(int index = -1)
		{
			if(index > -1)
				s_RowToggle = index % 2 == 0;

			EditorGUIUtility.PushBackgroundColor(s_RowToggle ? s_RowEvenColor : s_RowOddColor);
			GUILayout.BeginHorizontal(EditorStyles.rowStyle);
			s_RowToggle = !s_RowToggle;
			EditorGUIUtility.PopBackgroundColor();
		}

		public static void EndRow()
		{
			GUILayout.EndHorizontal();
		}

        /// <summary>
        /// An automatically laid out toolbar that returns the index of the selected button. Optionally allows no selection.
        /// </summary>
        /// <param name="index"></param>
        /// <param name="content"></param>
        /// <param name="style"></param>
        /// <param name="allowNoSelection"></param>
        /// <param name="addlParams"></param>
        /// <returns></returns>
        public static int Toolbar(int index, GUIContent[] content, GUIStyle style, bool allowNoSelection = false, params GUILayoutOption[] addlParams)
		{
			return Toolbar(index, content, style, style, style, allowNoSelection, addlParams);
		}

		public static int Toolbar(int index, GUIContent[] content, GUIStyle left, GUIStyle mid, GUIStyle right, bool allowNoSelection = false, params GUILayoutOption[] addlParams)
		{
			GUILayout.BeginHorizontal();

			for(int i = 0; i < content.Length; i++)
			{
				GUIStyle m_Style = i < 1 ? left : (i >= content.Length - 1 ? right : mid);

				if(index == i)
					m_Style = EditorGUIUtility.GetOnStyle(m_Style);

				if(GUILayout.Button(content[i], m_Style, addlParams))
				{
					if(index == i && allowNoSelection)
						index = -1;
					else
						index = i;
				}
			}

			GUILayout.EndHorizontal();

			return index;
		}

		/**
		 *	An automatically laid out toolbar that toggles flags. Content corresponds to the bits starting at 1 - ex:
		 *		- content[0] = 0x1
		 *		- content[1] = 0x2
		 *		- content[2] = 0x4
		 */
		public static int FlagToolbar(int index, GUIContent[] content, GUIStyle style, bool allowNoSelection = false, bool allowMultipleSelected = true, params GUILayoutOption[] addlParams)
		{
			return FlagToolbar(index, content, style, style, style, allowNoSelection, allowMultipleSelected, addlParams);
		}

		public static int FlagToolbar(int index, GUIContent[] content, bool allowNoSelection = false, bool allowMultipleSelected = true, params GUILayoutOption[] addlParams)
		{
			return FlagToolbar(index, content, UnityEditor.EditorStyles.miniButtonLeft, UnityEditor.EditorStyles.miniButtonMid, UnityEditor.EditorStyles.miniButtonRight, allowNoSelection, allowMultipleSelected, addlParams);
		}

		public static int FlagToolbar(int index, GUIContent[] content, GUIStyle left, GUIStyle mid, GUIStyle right, bool allowNoSelection = false, bool allowMultipleSelected = true, params GUILayoutOption[] addlParams)
		{
			GUILayout.BeginHorizontal();

			for(int i = 0; i < content.Length; i++)
			{
				GUIStyle m_Style = i < 1 ? left : (i >= content.Length - 1 ? right : mid);

				if( (index & (0x1 << i)) > 0 )
					m_Style = EditorGUIUtility.GetOnStyle(m_Style);

				if(GUILayout.Button(content[i], m_Style, addlParams))
				{
					if(!allowMultipleSelected)
						index = (index & (0x1 << i));

					index ^= (0x1 << i);

					if(!allowNoSelection && index == 0x0)
						index = 0x1 << i;
				}
			}

			GUILayout.EndHorizontal();

			return index;
		}

		class ResizeHandleState
		{
			public Vector2 origin;
			public Rect startingRect;
		}

		public static Rect DoResizeHandle(Rect rect)
		{
			var evt = Event.current;

			Rect resizeWindowRect = new Rect(
				rect.width - 16,
				rect.height - 16,
				16,
				16);

			int id = GUIUtility.GetControlID("ProBuilderWindowResize".GetHashCode(), FocusType.Passive, rect);
			HandleUtility.AddControl(id, Vector2.Distance(resizeWindowRect.center, evt.mousePosition));
			UnityEditor.EditorGUIUtility.AddCursorRect(resizeWindowRect, MouseCursor.ResizeUpLeft);

			if (evt.type == EventType.MouseDown)
			{
				if (!resizeWindowRect.Contains(evt.mousePosition))
					return rect;
				GUIUtility.hotControl = id;
				GUI.changed = true;
				var state = (ResizeHandleState) GUIUtility.GetStateObject(typeof(ResizeHandleState), id);
				state.origin = evt.mousePosition;
				state.startingRect = rect;
				evt.Use();
			}
			else if (GUIUtility.hotControl != id)
			{
				return rect;
			}
			if (evt.type == EventType.MouseUp)
			{
				GUIUtility.hotControl = 0;
				GUI.changed = true;
				evt.Use();
			}
			else if (evt.type == EventType.MouseDrag)
			{
				var state = (ResizeHandleState) GUIUtility.GetStateObject(typeof(ResizeHandleState), id);
				rect.width = state.startingRect.width + (evt.mousePosition.x - state.origin.x);
				rect.height = state.startingRect.height + (evt.mousePosition.y - state.origin.y);
				GUI.changed = true;
				evt.Use();
			}

			return rect;
		}

		public static float SearchableSlider(GUIContent content, float value, float min, float max, string searchContext)
		{
			if (!MatchSearchGroups(searchContext, content.text))
				return value;
			return UnityEditor.EditorGUILayout.Slider(content, value, min, max);
		}

		public static float SearchableSlider(string content, float value, float min, float max, string searchContext)
		{
			if (!MatchSearchGroups(searchContext, content))
				return value;
			return UnityEditor.EditorGUILayout.Slider(content, value, min, max);
		}

		internal static float SearchableFloatField(GUIContent title, float value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title.text))
				return value;
			return UnityEditor.EditorGUILayout.FloatField(title, value);
		}

		internal static float SearchableFloatField(string title, float value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title))
				return value;
			return UnityEditor.EditorGUILayout.FloatField(title, value);
		}

		internal static int SearchableIntField(GUIContent title, int value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title.text))
				return value;
			return UnityEditor.EditorGUILayout.IntField(title, value);
		}

		internal static int SearchableIntField(string title, int value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title))
				return value;
			return UnityEditor.EditorGUILayout.IntField(title, value);
		}

		internal static bool SearchableToggle(GUIContent title, bool value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title.text))
				return value;
			return UnityEditor.EditorGUILayout.Toggle(title, value);
		}

		internal static bool SearchableToggle(string title, bool value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title))
				return value;
			return UnityEditor.EditorGUILayout.Toggle(title, value);
		}

		internal static string SearchableTextField(GUIContent title, string value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title.text))
				return value;
			return UnityEditor.EditorGUILayout.TextField(title, value);
		}

		internal static string SearchableTextField(string title, string value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title))
				return value;
			return UnityEditor.EditorGUILayout.TextField(title, value);
		}

		internal static Color SearchableColorField(GUIContent title, Color value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title.text))
				return value;
			return UnityEditor.EditorGUILayout.ColorField(title, value);
		}

		internal static Color SearchableColorField(string title, Color value, string searchContext)
		{
			if (!MatchSearchGroups(searchContext, title))
				return value;
			return UnityEditor.EditorGUILayout.ColorField(title, value);
		}

		internal static bool MatchSearchGroups(string searchContext, string content)
		{
			if (searchContext == null)
				return true;
			var ctx = searchContext.Trim();
			if (string.IsNullOrEmpty(ctx))
				return true;
			var split = searchContext.Split(' ');
			return split.Any(x => !string.IsNullOrEmpty(x) && content.IndexOf(x, StringComparison.InvariantCultureIgnoreCase) > -1);
		}

		public static float SettingsSlider(GUIContent content, Pref<float> value, float min, float max, string searchContext)
		{
			if (!MatchSearchGroups(searchContext, content.text))
				return value;
			var res = UnityEditor.EditorGUILayout.Slider(content, value, min, max);
			DoResetContextMenuForLastRect(value);
			return res;
		}

		public static float SettingsSlider(string content, Pref<float> value, float min, float max, string searchContext)
		{
			if (!MatchSearchGroups(searchContext, content))
				return value;
			var res = UnityEditor.EditorGUILayout.Slider(content, value, min, max);
			DoResetContextMenuForLastRect(value);
			return res;
		}

		internal static float SettingsFloatField(GUIContent title, Pref<float> value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title.text))
				return value;
			var res = UnityEditor.EditorGUILayout.FloatField(title, value);
			DoResetContextMenuForLastRect(value);
			return res;
		}

		internal static float SettingsFloatField(string title, Pref<float> value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title))
				return value;
			var res = UnityEditor.EditorGUILayout.FloatField(title, value);
			DoResetContextMenuForLastRect(value);
			return res;
		}

		internal static int SettingsIntField(GUIContent title, Pref<int> value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title.text))
				return value;
			var res = UnityEditor.EditorGUILayout.IntField(title, value);
			DoResetContextMenuForLastRect(value);
			return res;
		}

		internal static int SettingsIntField(string title, Pref<int> value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title))
				return value;
			var res = UnityEditor.EditorGUILayout.IntField(title, value);
			DoResetContextMenuForLastRect(value);
			return res;
		}

		internal static bool SettingsToggle(GUIContent title, Pref<bool> value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title.text))
				return value;
			var res = UnityEditor.EditorGUILayout.Toggle(title, value);
			DoResetContextMenuForLastRect(value);
			return res;
		}

		internal static bool SettingsToggle(string title, Pref<bool> value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title))
				return value;
			var res = UnityEditor.EditorGUILayout.Toggle(title, value);
			DoResetContextMenuForLastRect(value);
			return res;
		}

		internal static string SettingsTextField(GUIContent title, Pref<string> value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title.text))
				return value;
			var res = UnityEditor.EditorGUILayout.TextField(title, value);
			DoResetContextMenuForLastRect(value);
			return res;
		}

		internal static string SettingsTextField(string title, Pref<string> value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title))
				return value;
			var res = UnityEditor.EditorGUILayout.TextField(title, value);
			DoResetContextMenuForLastRect(value);
			return res;
		}

		internal static Color SettingsColorField(GUIContent title, Pref<Color> value, string searchContext)
		{
			if(!MatchSearchGroups(searchContext, title.text))
				return value;
			var res = UnityEditor.EditorGUILayout.ColorField(title, value);
			DoResetContextMenuForLastRect(value);
			return res;
		}

		internal static Color SettingsColorField(string title, Pref<Color> value, string searchContext)
		{
			if (!MatchSearchGroups(searchContext, title))
				return value;
			var res = UnityEditor.EditorGUILayout.ColorField(title, value);
			DoResetContextMenuForLastRect(value);
			return res;
		}

		internal static void DoResetContextMenuForLastRect(IPref pref)
		{
			DoResetContextMenu(GUILayoutUtility.GetLastRect(), pref);
		}

		internal static void DoResetContextMenu(Rect rect, IPref pref)
		{
			var evt = Event.current;

			if (evt.type == EventType.ContextClick && rect.Contains(evt.mousePosition))
			{
				var menu = new GenericMenu();
				menu.AddItem(new GUIContent("Reset " + pref.key), false, () =>
				{
					pref.Reset(true);
					UnityEditor.ProBuilder.Settings.Save();
				});
				menu.ShowAsContext();
			}
		}
	}
}