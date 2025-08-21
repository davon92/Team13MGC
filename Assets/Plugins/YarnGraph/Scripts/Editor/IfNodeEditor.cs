#if XNODE
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using XNodeEditor;
using System.Linq;
using System.Collections.Generic;

namespace Arawn.YarnGraph.Editor
{
	[CustomNodeEditor(typeof(IfNode))]
	public class IfNodeEditor : NodeEditor
	{
		private bool isResizing;
		private Vector2 resizeStartMousePos;
		private Vector2 resizeStartSize;

		private string[] conditionTypes = { "Compound", "Visited", "Visited Count", "Single Comparison" };
		private static Dictionary<SingleComparison, bool> customLeftStates = new Dictionary<SingleComparison, bool>();
		private static Dictionary<SingleComparison, bool> customRightStates = new Dictionary<SingleComparison, bool>();

		public override void OnBodyGUI()
		{
			serializedObject.Update();
			IfNode node = target as IfNode;
			if (node == null) return;

			EditorGUILayout.PropertyField(serializedObject.FindProperty("nodeTitle"), new GUIContent("Node Title"));
			NodeEditorGUILayout.PortField(node.GetInputPort("input"));
			NodeEditorGUILayout.PortField(node.GetOutputPort("trueOutput"));
			NodeEditorGUILayout.PortField(node.GetOutputPort("falseOutput"));

			EditorGUILayout.LabelField("Condition Type:");
			int currentTypeIndex = GetConditionTypeIndex(node.condition);
			int selectedTypeIndex = EditorGUILayout.Popup(currentTypeIndex, conditionTypes);
			if (selectedTypeIndex != currentTypeIndex)
				node.condition = CreateConditionInstance(conditionTypes[selectedTypeIndex]);

			DrawConditionFields(node.condition);

			DrawResizeHandle(node);

			serializedObject.ApplyModifiedProperties();
		}

		public override int GetWidth() => (int)((IfNode)target).nodeSize.x;

		private void DrawResizeHandle(IfNode node)
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
						node.nodeSize = new Vector2(Mathf.Max(100, resizeStartSize.x + delta.x), Mathf.Max(50, resizeStartSize.y + delta.y));
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

		#region Condition Helpers

		private int GetConditionTypeIndex(ICondition condition)
		{
			// If the condition is wrapped in a NotCondition, use its inner condition.
			if (condition is NotCondition notCond)
			{
				condition = notCond.innerCondition;
			}
			if (condition == null) return 0; // Default to Compound.
			if (condition is CompoundCondition) return 0;
			if (condition is VisitedCondition) return 1;
			if (condition is VisitedCountCondition) return 2;
			if (condition is SingleComparison) return 3;
			return 0;
		}

		private ICondition CreateConditionInstance(string typeName)
		{
			switch (typeName)
			{
				case "Compound": return new CompoundCondition();
				case "Visited": return new VisitedCondition();
				case "Visited Count": return new VisitedCountCondition();
				case "Single Comparison": return new SingleComparison();
				default: return new CompoundCondition();
			}
		}

		private int GetSubConditionTypeIndex(ICondition condition)
		{
			// Unwrap NotCondition if present.
			if (condition is NotCondition notCond)
			{
				condition = notCond.innerCondition;
			}
			if (condition == null) return 0; // Default to Visited.
			if (condition is VisitedCondition) return 0;
			if (condition is VisitedCountCondition) return 1;
			if (condition is SingleComparison) return 2;
			return 0;
		}

		private ICondition CreateSubConditionInstance(string typeName)
		{
			switch (typeName)
			{
				case "Visited": return new VisitedCondition();
				case "Visited Count": return new VisitedCountCondition();
				case "Single Comparison": return new SingleComparison();
				default: return new VisitedCondition();
			}
		}

		#endregion

		private void DrawConditionFields(ICondition condition)
		{
			if (condition == null) return;

			if (condition is CompoundCondition compound)
			{
				compound.logicOperator = (LogicOperator)EditorGUILayout.EnumPopup("Logic Operator", compound.logicOperator);
				EditorGUILayout.LabelField("Conditions:");
				if (compound.conditions == null) compound.conditions = new List<ICondition>();
				for (int i = 0; i < compound.conditions.Count; i++)
				{
					EditorGUILayout.BeginHorizontal();
					compound.conditions[i] = DrawSubCondition(compound.conditions[i]);
					if (GUILayout.Button("-", GUILayout.Width(20)))
					{
						compound.conditions.RemoveAt(i);
					}
					EditorGUILayout.EndHorizontal();
				}
				if (GUILayout.Button("+ Add Condition"))
				{
					compound.conditions.Add(new SingleComparison());
				}
			}
			else if (condition is VisitedCondition visited)
			{
				visited.nodeName = EditorGUILayout.TextField("Node Name", visited.nodeName);
			}
			else if (condition is VisitedCountCondition visitedCount)
			{
				visitedCount.nodeName = EditorGUILayout.TextField("Node Name", visitedCount.nodeName);
				visitedCount.op = (ComparisonOperator)EditorGUILayout.EnumPopup("Operator", visitedCount.op);
				visitedCount.countValue = EditorGUILayout.IntField("Count", visitedCount.countValue);
			}
			else if (condition is SingleComparison single)
			{
				// Retrieve declared variables from the graph.
				YarnDialogueGraph graph = (target as IfNode).graph as YarnDialogueGraph;
				if (graph != null && graph.declaredVariables != null && graph.declaredVariables.Count > 0)
				{
					// Build a list of variable names plus an "Other" option.
					List<string> options = graph.declaredVariables.Select(v => v.variableName).ToList();
					options.Add("Other");

					// --- Left Variable ---
					int currentLeftIndex = options.IndexOf(single.leftVariable);
					bool isCustomLeft = string.IsNullOrEmpty(single.leftVariable) || currentLeftIndex < 0;
					// Check stored state.
					if (customLeftStates.TryGetValue(single, out bool storedLeft))
						isCustomLeft = storedLeft;
					// If not custom, ensure current index is valid.
					if (!isCustomLeft && currentLeftIndex < 0)
						currentLeftIndex = options.Count - 1;

					int newLeftIndex = EditorGUILayout.Popup("Left Variable", isCustomLeft ? options.Count - 1 : currentLeftIndex, options.ToArray());
					if (newLeftIndex == options.Count - 1)
					{
						isCustomLeft = true;
						// Clear so the user can type.
						single.leftVariable = "";
					}
					else
					{
						isCustomLeft = false;
						single.leftVariable = options[newLeftIndex];
					}
					customLeftStates[single] = isCustomLeft;
					if (isCustomLeft)
					{
						single.leftVariable = EditorGUILayout.TextField("Custom Left Variable", single.leftVariable);
					}

					// --- Operator ---
					single.op = (ComparisonOperator)EditorGUILayout.EnumPopup("Operator", single.op);

					// --- Right Side ---
					single.isRightVariable = EditorGUILayout.Toggle("Is Right a Variable?", single.isRightVariable);
					if (single.isRightVariable)
					{
						if (graph != null && graph.declaredVariables != null && graph.declaredVariables.Count > 0)
						{
							List<string> rightOptions = graph.declaredVariables.Select(v => v.variableName).ToList();
							rightOptions.Add("Other");

							int currentRightIndex = rightOptions.IndexOf(single.rightVariableOrValue);
							bool isCustomRight = string.IsNullOrEmpty(single.rightVariableOrValue) || currentRightIndex < 0;
							if (customRightStates.TryGetValue(single, out bool storedRight))
								isCustomRight = storedRight;
							if (!isCustomRight && currentRightIndex < 0)
								currentRightIndex = rightOptions.Count - 1;

							int newRightIndex = EditorGUILayout.Popup("Right Variable", isCustomRight ? rightOptions.Count - 1 : currentRightIndex, rightOptions.ToArray());
							if (newRightIndex == rightOptions.Count - 1)
							{
								isCustomRight = true;
								single.rightVariableOrValue = "";
							}
							else
							{
								isCustomRight = false;
								single.rightVariableOrValue = rightOptions[newRightIndex];
							}
							customRightStates[single] = isCustomRight;
							if (isCustomRight)
							{
								single.rightVariableOrValue = EditorGUILayout.TextField("Custom Right Variable", single.rightVariableOrValue);
							}
						}
						else
						{
							single.rightVariableOrValue = EditorGUILayout.TextField("Right Variable", single.rightVariableOrValue);
						}
					}
					else
					{
						single.rightVariableOrValue = EditorGUILayout.TextField("Right Value", single.rightVariableOrValue);
					}
				}
				else
				{
					single.leftVariable = EditorGUILayout.TextField("Left Variable", single.leftVariable);
					single.op = (ComparisonOperator)EditorGUILayout.EnumPopup("Operator", single.op);
					single.isRightVariable = EditorGUILayout.Toggle("Is Right a Variable?", single.isRightVariable);
					if (single.isRightVariable)
					{
						single.rightVariableOrValue = EditorGUILayout.TextField("Right Variable", single.rightVariableOrValue);
					}
					else
					{
						single.rightVariableOrValue = EditorGUILayout.TextField("Right Value", single.rightVariableOrValue);
					}
				}
			}
		}

		private ICondition DrawSubCondition(ICondition subCondition)
		{
			string[] subConditionTypes = { "Visited", "Visited Count", "Single Comparison" };
			int currentTypeIndex = GetSubConditionTypeIndex(subCondition);
			int selectedTypeIndex = EditorGUILayout.Popup(currentTypeIndex, subConditionTypes);

			if (selectedTypeIndex != currentTypeIndex || subCondition == null)
			{
				subCondition = CreateSubConditionInstance(subConditionTypes[selectedTypeIndex]);
			}

			DrawConditionFields(subCondition);
			return subCondition;
		}
	}
}
#endif
#endif