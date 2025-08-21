#if XNODE
#if UNITY_EDITOR
using UnityEngine;
using XNodeEditor;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEditorInternal;
using XNode;

namespace Arawn.YarnGraph
{
	static class DialogueNodeEditorState
	{
		public static Dictionary<DialogueElement, bool> Foldouts = new Dictionary<DialogueElement, bool>();
	}

	[CustomNodeEditor(typeof(DialogueNode))]
	public class DialogueNodeEditor : NodeEditor
	{
		private bool isResizing;
		private Vector2 resizeStartMousePos;
		private Vector2 resizeStartSize;

		// Reference to our reorderable list
		private ReorderableList reorderableList;

		public override void OnBodyGUI()
		{
			serializedObject.Update();

			DialogueNode node = (DialogueNode)target;

			// 1) Hide default fields
			DrawPropertiesExcluding(serializedObject,
				"elements", "m_Script", "nodeSize",
				"position", "ports", "input", "output", "graph");

			// 2) Optional: show UseNodeTags, etc.
			SerializedProperty useTagsProp = serializedObject.FindProperty("UseNodeTags");
			EditorGUILayout.PropertyField(useTagsProp, new GUIContent("Use Node Tags"));
			if (useTagsProp.boolValue)
			{
				SerializedProperty tagsProp = serializedObject.FindProperty("nodeTags");
				EditorGUILayout.PropertyField(tagsProp, new GUIContent("Node Tags"), true);
			}

			// 3) Show built-in input / output ports (if you still want them)
			if (node.GetInputPort("input") != null)
				NodeEditorGUILayout.PortField(node.GetInputPort("input"));
			if (node.GetOutputPort("output") != null)
				NodeEditorGUILayout.PortField(node.GetOutputPort("output"));

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Dialogue Elements", EditorStyles.boldLabel);

			// 4) Draw reorderable list for 'elements'
			if (reorderableList == null)
			{
				var elementsProp = serializedObject.FindProperty("elements");
				CreateReorderableList(elementsProp, node);
			}
			reorderableList.DoLayoutList();

			EditorGUILayout.Space();
			if (GUILayout.Button("Add Dialogue Element"))
			{
				ShowAddDialogueElementMenu(node);
			}

			// 5) Draw resizing handle if you want
			DrawResizeHandle(node);

			// 6) Apply changes
			serializedObject.ApplyModifiedProperties();

			// 7) Update the dynamic ports
			node.UpdateJumpPorts();

			// 8) Actually display each dynamic port so it’s visible in the node inspector
			//    For example, just loop all output ports:
			foreach (NodePort port in node.DynamicPorts)
			{
				if (port.IsOutput)
				{
					NodeEditorGUILayout.PortField(new GUIContent(port.fieldName), port);
				}
			}
		}

		// Creates the reorderable list referencing 'elements'
		private void CreateReorderableList(SerializedProperty elementsProp, DialogueNode node)
		{
			reorderableList = new ReorderableList(
				elementsProp.serializedObject,
				elementsProp,
				draggable: true,      // allow drag to reorder
				displayHeader: false, // no built-in header
				displayAddButton: false,
				displayRemoveButton: false
			);

			// Draw each element
			reorderableList.drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
			{
				SerializedProperty elementProp = elementsProp.GetArrayElementAtIndex(index);
				if (elementProp == null) return;

				float lineH = EditorGUIUtility.singleLineHeight;

				// We'll place two small buttons on the top-right:
				//  - Duplicate
				//  - Remove
				float buttonWidth = 24f;  // slightly larger for text or symbol
				float spacing = 2f;
				float totalButtonsWidth = buttonWidth * 2 + spacing;

				// The top row is one line high
				Rect topRow = new Rect(rect.x, rect.y, rect.width, lineH);

				// The remove button rect
				Rect removeRect = new Rect(
					topRow.x + topRow.width - buttonWidth,
					topRow.y,
					buttonWidth,
					lineH
				);

				// The duplicate button rect (to the left of removeRect)
				Rect dupRect = new Rect(
					removeRect.x - buttonWidth - spacing,
					topRow.y,
					buttonWidth,
					lineH
				);

				// 1) Duplicate Button
				if (GUI.Button(dupRect, "+"))
				{
					DuplicateElement(node, index);
					return;
				}

				// 2) Remove Button
				if (GUI.Button(removeRect, "-"))
				{
					node.elements.RemoveAt(index);
					EditorUtility.SetDirty(node);
					return;
				}

				// 3) Field area
				float fieldWidth = rect.width - totalButtonsWidth - spacing;
				Rect fieldRect = new Rect(rect.x, rect.y, fieldWidth, rect.height);

				// 4) Draw the item with its custom property drawer
				EditorGUI.PropertyField(fieldRect, elementProp, GUIContent.none, true);
			};

			// Dynamic height for each element
			reorderableList.elementHeightCallback = (int index) =>
			{
				if (index < 0 || index >= elementsProp.arraySize)
					return EditorGUIUtility.singleLineHeight;

				SerializedProperty elementProp = elementsProp.GetArrayElementAtIndex(index);
				// Let the property's custom drawer define the correct height
				return EditorGUI.GetPropertyHeight(elementProp, GUIContent.none, true);
			};
		}

		// This duplicates the element at 'index' and inserts the copy right after
		private void DuplicateElement(DialogueNode node, int index)
		{
			if (index < 0 || index >= node.elements.Count) return;

			DialogueElement original = node.elements[index];
			if (original == null) return;

			// Use JSON to create a deep copy 
			Type type = original.GetType();
			string json = JsonUtility.ToJson(original);
			DialogueElement clone = (DialogueElement)JsonUtility.FromJson(json, type);

			node.elements.Insert(index + 1, clone);
			EditorUtility.SetDirty(node);
		}

		private void ShowAddDialogueElementMenu(DialogueNode node)
		{
			GenericMenu menu = new GenericMenu();
			Type baseType = typeof(DialogueElement);

			// 1) Gather candidate types
			IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(assembly => {
					try { return assembly.GetTypes(); }
					catch { return new Type[0]; }
				})
				.Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract);

			// 2) Sort them by their friendly name (ElementTitle or fallback)
			types = types.OrderBy(t => {
				// Attempt to find the ElementTitle attribute
				var titleAttr = t.GetCustomAttributes(typeof(ElementTitleAttribute), false)
								 .FirstOrDefault() as ElementTitleAttribute;
				return titleAttr != null ? titleAttr.Title : t.Name;
			});

			// 3) Add each type to the dropdown
			foreach (Type type in types)
			{
				var titleAttr = type.GetCustomAttributes(typeof(ElementTitleAttribute), false)
									.FirstOrDefault() as ElementTitleAttribute;
				string displayName = titleAttr != null ? titleAttr.Title : type.Name;

				menu.AddItem(new GUIContent(displayName), false, () =>
				{
					DialogueElement element = (DialogueElement)Activator.CreateInstance(type);
					node.elements.Add(element);
					EditorUtility.SetDirty(node);
				});
			}
			menu.ShowAsContext();
		}

		public override int GetWidth()
		{
			return (int)((DialogueNode)target).nodeSize.x;
		}

		private void DrawResizeHandle(DialogueNode node)
		{
			EditorGUILayout.BeginHorizontal();
			GUILayout.FlexibleSpace();

			Rect resizeRect = GUILayoutUtility.GetRect(18, 18, GUILayout.ExpandWidth(false));
			GUI.Label(resizeRect, "⇲");
			EditorGUIUtility.AddCursorRect(resizeRect, MouseCursor.ResizeUpLeft);

			EditorGUILayout.EndHorizontal();

			Event e = Event.current;
			switch (e.type)
			{
				case EventType.MouseDown:
					if (e.button == 0 && resizeRect.Contains(e.mousePosition))
					{
						isResizing = true;
						resizeStartMousePos = e.mousePosition;
						resizeStartSize = node.nodeSize;
						e.Use();
					}
					break;
				case EventType.MouseDrag:
					if (e.button == 0 && isResizing)
					{
						Vector2 diff = e.mousePosition - resizeStartMousePos;
						float newWidth = Mathf.Max(100, resizeStartSize.x + diff.x);
						float newHeight = Mathf.Max(50, resizeStartSize.y + diff.y);

						node.nodeSize = new Vector2(newWidth, newHeight);
						EditorUtility.SetDirty(node);
						e.Use();
					}
					break;
				case EventType.MouseUp:
					if (e.button == 0 && isResizing)
					{
						isResizing = false;
						e.Use();
					}
					break;
			}
		}

		private static void DrawPropertiesExcluding(SerializedObject obj, params string[] exclude)
		{
			SerializedProperty iterator = obj.GetIterator();
			bool enterChildren = true;
			while (iterator.NextVisible(enterChildren))
			{
				enterChildren = false;
				if (!exclude.Contains(iterator.name))
				{
					EditorGUILayout.PropertyField(iterator, true);
				}
			}
		}
	}
}

#endif
#endif