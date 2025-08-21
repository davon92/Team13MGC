#if XNODE
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

namespace Arawn.YarnGraph.Editor
{
	public static class ConditionEditor
	{
		// Existing GUILayout-based method
		public static void DrawConditionGUI(ICondition condition, YarnDialogueGraph graphObj)
		{
			if (condition is CompoundCondition compound)
			{
				if (compound.conditions.Count > 1)
				{
					compound.logicOperator = (LogicOperator)EditorGUILayout.EnumPopup("Logic Operator", compound.logicOperator);
				}
				EditorGUI.indentLevel++;
				for (int i = 0; i < compound.conditions.Count; i++)
				{
					EditorGUILayout.BeginHorizontal();
					DrawConditionGUI(compound.conditions[i], graphObj);
					if (GUILayout.Button("-", GUILayout.Width(20)))
					{
						compound.conditions.RemoveAt(i);
						i--;
					}
					EditorGUILayout.EndHorizontal();
				}
				if (GUILayout.Button("Add Condition"))
				{
					compound.conditions.Add(new SingleComparison());
				}
				EditorGUI.indentLevel--;
			}
			else if (condition is SingleComparison single)
			{
				// [Existing SingleComparison drawing logic remains unchanged]
				if (graphObj != null && graphObj.declaredVariables != null && graphObj.declaredVariables.Count > 0)
				{
					string[] variableNames = graphObj.declaredVariables.Select(v => v.variableName).ToArray();
					int leftIndex = Array.IndexOf(variableNames, single.leftVariable);
					if (leftIndex < 0) leftIndex = 0;
					leftIndex = EditorGUILayout.Popup("Left Variable", leftIndex, variableNames);
					single.leftVariable = variableNames[leftIndex];
				}
				else
				{
					single.leftVariable = EditorGUILayout.TextField("Left Variable", single.leftVariable);
				}
				single.op = (ComparisonOperator)EditorGUILayout.EnumPopup("Operator", single.op);
				single.isRightVariable = EditorGUILayout.Toggle("Right side is variable", single.isRightVariable);
				if (single.isRightVariable)
				{
					if (graphObj != null && graphObj.declaredVariables != null && graphObj.declaredVariables.Count > 0)
					{
						string[] variableNames = graphObj.declaredVariables.Select(v => v.variableName).ToArray();
						int rightIndex = Array.IndexOf(variableNames, single.rightVariableOrValue);
						if (rightIndex < 0) rightIndex = 0;
						rightIndex = EditorGUILayout.Popup("Right Variable", rightIndex, variableNames);
						single.rightVariableOrValue = variableNames[rightIndex];
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
		}

		// New Rect-based method
		public static void DrawConditionGUI(Rect rect, ICondition condition, YarnDialogueGraph graphObj, ref float yOffset)
		{
			float lineHeight = EditorGUIUtility.singleLineHeight;
			float spacing = 2f;

			if (condition is CompoundCondition compound)
			{
				float currentY = rect.y + yOffset;

				// Logic Operator (if more than one condition)
				if (compound.conditions.Count > 1)
				{
					Rect logicRect = new Rect(rect.x, currentY, rect.width, lineHeight);
					compound.logicOperator = (LogicOperator)EditorGUI.EnumPopup(logicRect, "Logic Operator", compound.logicOperator);
					currentY += lineHeight + spacing;
				}

				EditorGUI.indentLevel++;
				float indent = EditorGUI.indentLevel * 15f;

				// Draw each sub-condition
				for (int i = 0; i < compound.conditions.Count; i++)
				{
					Rect conditionRect = new Rect(rect.x + indent, currentY, rect.width - indent - 25f, lineHeight);
					Rect buttonRect = new Rect(conditionRect.xMax + spacing, currentY, 20f, lineHeight);

					DrawConditionGUI(conditionRect, compound.conditions[i], graphObj, ref yOffset); // Recursive call
					if (GUI.Button(buttonRect, "-"))
					{
						compound.conditions.RemoveAt(i);
						i--;
						continue;
					}
					currentY += GetConditionHeight(compound.conditions[i], graphObj) + spacing;
				}

				// Add Condition button
				Rect addButtonRect = new Rect(rect.x + indent, currentY, rect.width - indent, lineHeight);
				if (GUI.Button(addButtonRect, "Add Condition"))
				{
					compound.conditions.Add(new SingleComparison());
				}
				currentY += lineHeight + spacing;

				EditorGUI.indentLevel--;

				yOffset = currentY - rect.y; // Update offset
			}
			else if (condition is SingleComparison single)
			{
				float currentY = rect.y + yOffset;

				// Left Variable
				Rect leftRect = new Rect(rect.x, currentY, rect.width, lineHeight);
				if (graphObj != null && graphObj.declaredVariables != null && graphObj.declaredVariables.Count > 0)
				{
					string[] variableNames = graphObj.declaredVariables.Select(v => v.variableName).ToArray();
					int leftIndex = Array.IndexOf(variableNames, single.leftVariable);
					if (leftIndex < 0) leftIndex = 0;
					leftIndex = EditorGUI.Popup(leftRect, "Left Variable", leftIndex, variableNames);
					single.leftVariable = variableNames[leftIndex];
				}
				else
				{
					single.leftVariable = EditorGUI.TextField(leftRect, "Left Variable", single.leftVariable);
				}
				currentY += lineHeight + spacing;

				// Operator
				Rect opRect = new Rect(rect.x, currentY, rect.width, lineHeight);
				single.op = (ComparisonOperator)EditorGUI.EnumPopup(opRect, "Operator", single.op);
				currentY += lineHeight + spacing;

				// Right side toggle
				Rect toggleRect = new Rect(rect.x, currentY, rect.width, lineHeight);
				single.isRightVariable = EditorGUI.Toggle(toggleRect, "Right side is variable", single.isRightVariable);
				currentY += lineHeight + spacing;

				// Right Variable/Value
				Rect rightRect = new Rect(rect.x, currentY, rect.width, lineHeight);
				if (single.isRightVariable)
				{
					if (graphObj != null && graphObj.declaredVariables != null && graphObj.declaredVariables.Count > 0)
					{
						string[] variableNames = graphObj.declaredVariables.Select(v => v.variableName).ToArray();
						int rightIndex = Array.IndexOf(variableNames, single.rightVariableOrValue);
						if (rightIndex < 0) rightIndex = 0;
						rightIndex = EditorGUI.Popup(rightRect, "Right Variable", rightIndex, variableNames);
						single.rightVariableOrValue = variableNames[rightIndex];
					}
					else
					{
						single.rightVariableOrValue = EditorGUI.TextField(rightRect, "Right Variable", single.rightVariableOrValue);
					}
				}
				else
				{
					single.rightVariableOrValue = EditorGUI.TextField(rightRect, "Right Value", single.rightVariableOrValue);
				}
				currentY += lineHeight + spacing;

				yOffset = currentY - rect.y; // Update offset
			}
		}

		// Helper to calculate height of a condition
		public static float GetConditionHeight(ICondition condition, YarnDialogueGraph graphObj)
		{
			float lineHeight = EditorGUIUtility.singleLineHeight;
			float spacing = 2f;

			if (condition is CompoundCondition compound)
			{
				float height = 0f;
				if (compound.conditions.Count > 1)
					height += lineHeight + spacing; // Logic Operator
				height += compound.conditions.Count > 0
					? compound.conditions.Sum(c => GetConditionHeight(c, graphObj) + spacing)
					: lineHeight + spacing; // Conditions or placeholder
				height += lineHeight + spacing; // Add Condition button
				return height;
			}
			else if (condition is SingleComparison)
			{
				return (lineHeight + spacing) * 4; // 4 lines: Left, Op, Toggle, Right
			}
			return lineHeight; // Default
		}
	}
}
#endif
#endif