#if XNODE
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using XNodeEditor;

namespace Arawn.YarnGraph
{
	[CustomNodeEditor(typeof(YarnNode))]
	public class YarnNodeEditor : NodeEditor
	{

		private bool isResizing;
		private Vector2 startMouse;
		private Vector2 startSize;

		public override void OnBodyGUI()
		{
			// Let XNode draw everything exactly as it normally would…
			base.OnBodyGUI();

			// …then draw our resize handle underneath
			DrawResizeHandle((YarnNode)target);
		}

		public override int GetWidth() => (int)((YarnNode)target).nodeSize.x;

		private void DrawResizeHandle(YarnNode node)
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();
			Rect handle = GUILayoutUtility.GetRect(18, 18, GUILayout.ExpandWidth(false));
			GUI.Label(handle, "⇲");
			EditorGUIUtility.AddCursorRect(handle, MouseCursor.ResizeUpLeft);
			EditorGUILayout.EndHorizontal();

			Event e = Event.current;
			if (e.type == EventType.MouseDown && handle.Contains(e.mousePosition))
			{
				isResizing = true;
				startMouse = e.mousePosition;
				startSize = node.nodeSize;
				e.Use();
			}
			else if (e.type == EventType.MouseDrag && isResizing)
			{
				Vector2 delta = e.mousePosition - startMouse;
				node.nodeSize = new Vector2(Mathf.Max(100, startSize.x + delta.x), Mathf.Max(50, startSize.y + delta.y));
				EditorUtility.SetDirty(node);
				e.Use();
			}
			else if (e.type == EventType.MouseUp && isResizing)
			{
				isResizing = false;
				e.Use();
			}
		}
	}
}

#endif
#endif