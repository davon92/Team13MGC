#if XNODE
#if UNITY_EDITOR
using UnityEditor;
using XNodeEditor;
using System.Linq;
using System;
using UnityEngine;
using System.Collections.Generic;

namespace Arawn.YarnGraph.Editor
{
	[CustomNodeEditor(typeof(VariableSetNode))]
	public class VariableSetNodeEditor : NodeEditor
	{
		private bool isResizing;
		private Vector2 resizeStartMousePos;
		private Vector2 resizeStartSize;

		public override void OnBodyGUI()
		{
			serializedObject.Update();
			VariableSetNode node = target as VariableSetNode;

			// --- Auto‑reference block omitted for brevity ---

			DrawPropertiesExcluding(serializedObject,
				"m_Script", "position", "ports", "graph", "input", "output", "variables", "nodeSize");
			DrawResizeHandle(node);
			serializedObject.ApplyModifiedProperties();

			if (node == null) return;
			if (node.GetInputPort("input") != null) NodeEditorGUILayout.PortField(node.GetInputPort("input"));
			if (node.GetOutputPort("output") != null) NodeEditorGUILayout.PortField(node.GetOutputPort("output"));

			SerializedProperty variablesProp = serializedObject.FindProperty("variables");
			YarnDialogueGraph graph = node.graph as YarnDialogueGraph;

			if (variablesProp != null)
			{
				for (int i = 0; i < variablesProp.arraySize; i++)
				{
					SerializedProperty variableProp = variablesProp.GetArrayElementAtIndex(i);
					SerializedProperty nameProp = variableProp.FindPropertyRelative("variableName");
					SerializedProperty valueProp = variableProp.FindPropertyRelative("initialValue");
					SerializedProperty operationProp = variableProp.FindPropertyRelative("operation");
					SerializedProperty sourceNodeProp = variableProp.FindPropertyRelative("sourceNodeName");

					EditorGUILayout.BeginVertical("box");

					// === UPDATED: Variable Name dropdown + "Other" ===
					if (graph != null && graph.declaredVariables != null && graph.declaredVariables.Count > 0)
					{
						List<string> options = graph.declaredVariables.Select(v => v.variableName).ToList();
						options.Add("Other");

						int currentIndex = options.IndexOf(nameProp.stringValue);
						bool isOther = currentIndex < 0 || currentIndex == options.Count - 1;

						int chosen = EditorGUILayout.Popup("Variable Name", isOther ? options.Count - 1 : currentIndex, options.ToArray());
						if (chosen == options.Count - 1)
						{
							nameProp.stringValue = EditorGUILayout.TextField("Custom Variable", isOther ? nameProp.stringValue : "");
						}
						else
						{
							nameProp.stringValue = options[chosen];
						}
					}
					else
					{
						EditorGUILayout.LabelField("No declared variables found in the graph.");
						nameProp.stringValue = EditorGUILayout.TextField("Variable Name", nameProp.stringValue);
					}

					// === Rest of UI ===
					operationProp.enumValueIndex = (int)(YarnVariable.VariableOperation)EditorGUILayout.EnumPopup(
						"Operation", (YarnVariable.VariableOperation)operationProp.enumValueIndex);

					if ((YarnVariable.VariableOperation)operationProp.enumValueIndex == YarnVariable.VariableOperation.Set)
						valueProp.stringValue = EditorGUILayout.TextField("Value", valueProp.stringValue);
					else if ((YarnVariable.VariableOperation)operationProp.enumValueIndex == YarnVariable.VariableOperation.Increment)
						sourceNodeProp.stringValue = EditorGUILayout.TextField("Source Node Name", sourceNodeProp.stringValue);

					if (GUILayout.Button("Remove Variable"))
						variablesProp.DeleteArrayElementAtIndex(i);

					EditorGUILayout.EndVertical();
				}

				if (GUILayout.Button("Add Variable"))
				{
					variablesProp.InsertArrayElementAtIndex(variablesProp.arraySize);
					var newElem = variablesProp.GetArrayElementAtIndex(variablesProp.arraySize - 1);
					newElem.FindPropertyRelative("variableName").stringValue = "";
					newElem.FindPropertyRelative("initialValue").stringValue = "";
					newElem.FindPropertyRelative("operation").enumValueIndex = (int)YarnVariable.VariableOperation.Set;
					newElem.FindPropertyRelative("sourceNodeName").stringValue = "";
				}
			}
		}

		public override int GetWidth() => (int)((VariableSetNode)target).nodeSize.x;

		private void DrawResizeHandle(VariableSetNode node)
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
						Vector2 delta = e.mousePosition - resizeStartMousePos;
						node.nodeSize = new Vector2(Mathf.Max(100, resizeStartSize.x + delta.x),
													Mathf.Max(50, resizeStartSize.y + delta.y));
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

		/// <summary>
		/// Local implementation of DrawPropertiesExcluding that draws all visible properties
		/// of the serialized object except those with the specified names.
		/// </summary>
		private static void DrawPropertiesExcluding(SerializedObject obj, params string[] propertyToExclude)
		{
			SerializedProperty iterator = obj.GetIterator();
			bool enterChildren = true;
			while (iterator.NextVisible(enterChildren))
			{
				enterChildren = false;
				if (!propertyToExclude.Contains(iterator.name))
				{
					EditorGUILayout.PropertyField(iterator, true);
				}
			}
		}
	}
}
#endif
#endif