#if XNODE
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Linq;

namespace Arawn.YarnGraph
{
	[CustomPropertyDrawer(typeof(SingleComparison))]
	public class SingleComparisonDrawer : PropertyDrawer
	{
		private const float Spacing = 2f;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			int index = GetArrayIndex(property);
			string header = "Condition" + (index >= 0 ? " " + (index + 1) : "");
			Rect headerRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
			property.isExpanded = EditorGUI.Foldout(headerRect, property.isExpanded, header, true);
			if (!property.isExpanded)
			{
				EditorGUI.EndProperty();
				return;
			}

			float y = position.y + EditorGUIUtility.singleLineHeight + Spacing;
			float lineHeight = EditorGUIUtility.singleLineHeight;

			var leftVarProp = property.FindPropertyRelative("leftVariable");
			var opProp = property.FindPropertyRelative("op");
			var isRightVarProp = property.FindPropertyRelative("isRightVariable");
			var rightProp = property.FindPropertyRelative("rightVariableOrValue");

			List<string> declared = GetDeclaredVariables(property.serializedObject);
			if (!declared.Contains("Other")) declared.Add("Other");

			// LEFT POPUP
			int leftIndex = declared.IndexOf(leftVarProp.stringValue);
			if (leftIndex < 0) leftIndex = declared.IndexOf("Other");
			Rect leftPopupRect = new Rect(position.x, y, position.width, lineHeight);
			int newLeft = EditorGUI.Popup(leftPopupRect, "Left Variable", leftIndex, declared.ToArray());
			string choiceLeft = declared[newLeft];
			if (choiceLeft != "Other")
				leftVarProp.stringValue = choiceLeft;
			else if (declared.Contains(leftVarProp.stringValue))
				leftVarProp.stringValue = ""; // clear it so Popup shows "Other" next frame

			// CUSTOM LEFT
			if (choiceLeft == "Other")
			{
				y += lineHeight + Spacing;
				Rect leftCustomRect = new Rect(position.x, y, position.width, lineHeight);
				leftVarProp.stringValue = EditorGUI.TextField(leftCustomRect, "Left Variable", leftVarProp.stringValue);
			}
			y += lineHeight + Spacing;

			// OPERATOR
			Rect opRect = new Rect(position.x, y, position.width, lineHeight);
			EditorGUI.PropertyField(opRect, opProp, new GUIContent("Operator"));
			y += lineHeight + Spacing;

			// TOGGLE
			Rect toggleRect = new Rect(position.x, y, position.width, lineHeight);
			EditorGUI.PropertyField(toggleRect, isRightVarProp, new GUIContent("Is Right Variable"));
			y += lineHeight + Spacing;

			// RIGHT POPUP or VALUE
			if (isRightVarProp.boolValue)
			{
				List<string> rightOptions = GetDeclaredVariables(property.serializedObject);
				if (!rightOptions.Contains("Other")) rightOptions.Add("Other");

				int otherIndex = rightOptions.IndexOf("Other");
				int rightIndex = rightOptions.IndexOf(rightProp.stringValue);
				if (rightIndex < 0) rightIndex = otherIndex;

				Rect rightPopupRect = new Rect(position.x, y, position.width, lineHeight);
				int newRightIndex = EditorGUI.Popup(rightPopupRect, "Right Variable", rightIndex, rightOptions.ToArray());

				if (newRightIndex == otherIndex)
				{
					// Clear so popup stays on “Other” next frame
					if (rightOptions.Contains(rightProp.stringValue))
						rightProp.stringValue = "";

					y += lineHeight + Spacing;
					Rect rightCustomRect = new Rect(position.x, y, position.width, lineHeight);
					rightProp.stringValue = EditorGUI.TextField(rightCustomRect, "Right Variable", rightProp.stringValue);
				}
				else
				{
					rightProp.stringValue = rightOptions[newRightIndex];
					y += lineHeight + Spacing;
				}
			}
			else
			{
				Rect rightValueRect = new Rect(position.x, y, position.width, lineHeight);
				rightProp.stringValue = EditorGUI.TextField(rightValueRect, "Right Value", rightProp.stringValue);
			}



			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (!property.isExpanded)
				return EditorGUIUtility.singleLineHeight;

			float lineHeight = EditorGUIUtility.singleLineHeight;
			float spacing = Spacing;

			var leftProp = property.FindPropertyRelative("leftVariable");
			var isRightVarProp = property.FindPropertyRelative("isRightVariable");
			var rightProp = property.FindPropertyRelative("rightVariableOrValue");

			List<string> declared = GetDeclaredVariables(property.serializedObject);
			if (!declared.Contains("Other")) declared.Add("Other");

			int otherIndex = declared.IndexOf("Other");

			int leftIndex = declared.IndexOf(leftProp.stringValue);
			if (leftIndex < 0) leftIndex = otherIndex;
			bool leftIsOther = leftIndex == otherIndex;

			int rightIndex = declared.IndexOf(rightProp.stringValue);
			if (rightIndex < 0) rightIndex = otherIndex;
			bool rightIsOther = isRightVarProp.boolValue && rightIndex == otherIndex;

			// Base lines: header + left popup + operator + toggle + right popup/value
			int totalLines = 5 + (leftIsOther ? 1 : 0) + (rightIsOther ? 1 : 0);

			return (lineHeight * totalLines) + (spacing * (totalLines - 1));
		}


		private int GetArrayIndex(SerializedProperty property)
		{
			string path = property.propertyPath;
			int start = path.LastIndexOf("Array.data[");
			if (start < 0) return -1;
			start += "Array.data[".Length;
			int end = path.IndexOf(']', start);
			if (end < 0) return -1;
			if (int.TryParse(path.Substring(start, end - start), out int idx))
				return idx;
			return -1;
		}

		private List<string> GetDeclaredVariables(SerializedObject so)
		{
			if (so.targetObject is DialogueNode node && node.graph is YarnDialogueGraph graph)
				return graph.declaredVariables.Select(v => v.variableName).ToList();
			if (so.targetObject is YarnDialogueGraph ydGraph)
				return ydGraph.declaredVariables.Select(v => v.variableName).ToList();
			return new List<string>();
		}
	}
}
#endif
#endif