#if XNODE
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using static Arawn.YarnGraph.YarnVariableAssignmentElement;

namespace Arawn.YarnGraph
{
	[CustomPropertyDrawer(typeof(YarnVariableAssignmentElement))]
	public class YarnVariableAssignmentElementDrawer : PropertyDrawer
	{
		private static readonly Dictionary<string, bool> FoldoutStates = new Dictionary<string, bool>();
		private static readonly Dictionary<string, bool> CustomNameStates = new Dictionary<string, bool>();

		private const float PADDING = 4f;
		private static readonly float LINE_HEIGHT = EditorGUIUtility.singleLineHeight;
		private const int MAX_HEADER_LENGTH = 50;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			GUI.BeginGroup(position);
			Rect localArea = new Rect(0, 0, position.width, position.height);

			string key = property.propertyPath;
			FoldoutStates.TryGetValue(key, out bool foldout);

			// Retrieve the assignment element.
			YarnVariableAssignmentElement assignment = property.managedReferenceValue as YarnVariableAssignmentElement;
			string headerText = "Assign Variable";
			if (assignment != null)
			{
				if (assignment.isDeclaration)
				{
					headerText += ": " + assignment.variableName + " = " + assignment.value;
				}
				else
				{
					switch (assignment.assignmentType)
					{
						case AssignmentType.SetStatic:
							headerText += ": " + assignment.variableName + " = " + assignment.value;
							break;
						case AssignmentType.SetVisited:
						case AssignmentType.SetVisitedCount:
							headerText += ": " + assignment.variableName + " -> " + assignment.nodeName;
							break;
						default:
							headerText += ": " + assignment.variableName;
							break;
					}
				}
			}
			if (headerText.Length > MAX_HEADER_LENGTH)
				headerText = headerText.Substring(0, MAX_HEADER_LENGTH) + "...";

			Rect foldoutRect = new Rect(0, 0, localArea.width, LINE_HEIGHT);
			bool newFoldout = EditorGUI.Foldout(foldoutRect, foldout, headerText, true);
			FoldoutStates[key] = newFoldout;

			float yPos = foldoutRect.y + foldoutRect.height + PADDING;

			if (newFoldout)
			{
				// 1. Is Declaration checkbox.
				SerializedProperty isDeclarationProp = property.FindPropertyRelative("isDeclaration");
				Rect declRect = new Rect(0, yPos, localArea.width, LINE_HEIGHT);
				EditorGUI.PropertyField(declRect, isDeclarationProp, new GUIContent("Is Declaration?"));
				yPos += LINE_HEIGHT + PADDING;

				// For declarations, force assignmentType to SetStatic.
				SerializedProperty assignmentTypeProp = property.FindPropertyRelative("assignmentType");
				if (isDeclarationProp.boolValue)
				{
					assignmentTypeProp.enumValueIndex = (int)AssignmentType.SetStatic;
				}

				// 2. Variable Name field.
				SerializedProperty varNameProp = property.FindPropertyRelative("variableName");
				Rect varRect = new Rect(0, yPos, localArea.width, LINE_HEIGHT);

				// If this is a declaration, just show a free text field:
				if (isDeclarationProp.boolValue)
				{
					varNameProp.stringValue =
						EditorGUI.TextField(varRect, "Variable Name", varNameProp.stringValue);
				}
				else
				{
					// Non-declaration => show a dropdown of declared variables + "Other"
					YarnDialogueGraph graphObj = FindYarnDialogueGraph(property);
					if (graphObj != null && graphObj.declaredVariables != null && graphObj.declaredVariables.Count > 0)
					{
						// Build the list
						List<string> variableOptions = new List<string>();
						foreach (var yarnVar in graphObj.declaredVariables)
						{
							if (yarnVar != null)
								variableOptions.Add(yarnVar.variableName);
						}
						variableOptions.Add("Other");

						// Figure out which index to show as "current"
						int currentIndex = variableOptions.IndexOf(varNameProp.stringValue);
						if (currentIndex < 0)
						{
							// If the variable name isn't in the declared list, we assume "Other"
							currentIndex = variableOptions.Count - 1;
							CustomNameStates[key] = true;
						}

						int newIndex = EditorGUI.Popup(varRect, "Variable Name", currentIndex, variableOptions.ToArray());
						if (newIndex == variableOptions.Count - 1)
						{
							// User picked "Other" => set varName to "Other"
							CustomNameStates[key] = true;
							varNameProp.stringValue = "Other";
						}
						else
						{
							// They picked a declared variable
							CustomNameStates[key] = false;
							varNameProp.stringValue = variableOptions[newIndex];
						}
					}
					else
					{
						EditorGUI.LabelField(varRect, "Variable Name", "No declared variables found.");
					}
				}
				yPos += LINE_HEIGHT + PADDING;

				// If not declaration and custom entry is active, show a custom text field.
				if (!isDeclarationProp.boolValue
					&& CustomNameStates.TryGetValue(key, out bool customActive)
					&& customActive)
				{
					Rect customRect = new Rect(0, yPos, localArea.width, LINE_HEIGHT);
					varNameProp.stringValue =
						EditorGUI.TextField(customRect, "Custom Name", varNameProp.stringValue);
					yPos += LINE_HEIGHT + PADDING;
				}

				// 3. Assignment Type dropdown (only for assignments).
				if (!isDeclarationProp.boolValue)
				{
					Rect typeRect = new Rect(0, yPos, localArea.width, LINE_HEIGHT);
					AssignmentType currentType = (AssignmentType)assignmentTypeProp.enumValueIndex;
					AssignmentType newType = (AssignmentType)EditorGUI.EnumPopup(typeRect, "Assignment Type", currentType);
					assignmentTypeProp.enumValueIndex = (int)newType;
					yPos += LINE_HEIGHT + PADDING;
				}

				// 4. Value or Node Name field.
				AssignmentType assignmentType = (AssignmentType)assignmentTypeProp.enumValueIndex;
				if (isDeclarationProp.boolValue || assignmentType == AssignmentType.SetStatic)
				{
					SerializedProperty valueProp = property.FindPropertyRelative("value");
					Rect valueRect = new Rect(0, yPos, localArea.width, LINE_HEIGHT);
					EditorGUI.PropertyField(valueRect, valueProp, new GUIContent("Value"));
				}
				else
				{
					SerializedProperty nodeNameProp = property.FindPropertyRelative("nodeName");
					Rect nodeRect = new Rect(0, yPos, localArea.width, LINE_HEIGHT);
					EditorGUI.PropertyField(nodeRect, nodeNameProp, new GUIContent("Node Name"));
				}
				yPos += LINE_HEIGHT + PADDING;
			}

			GUI.EndGroup();
			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			string key = property.propertyPath;
			bool foldout = FoldoutStates.TryGetValue(key, out bool f) && f;
			bool isDeclaration = property.FindPropertyRelative("isDeclaration").boolValue;

			float height = LINE_HEIGHT + PADDING;
			if (foldout)
			{
				height += LINE_HEIGHT + PADDING; // isDeclaration
				height += LINE_HEIGHT + PADDING; // Variable Name field

				// If not declaration and we are in "Other" mode => custom text field
				if (!isDeclaration
					&& CustomNameStates.TryGetValue(key, out bool customActive)
					&& customActive)
				{
					height += LINE_HEIGHT + PADDING;
				}

				// Assignment Type
				if (!isDeclaration)
				{
					height += LINE_HEIGHT + PADDING;
				}

				// Value or Node Name
				height += LINE_HEIGHT + PADDING;
			}
			return height;
		}

		private YarnDialogueGraph FindYarnDialogueGraph(SerializedProperty property)
		{
			if (property.serializedObject.targetObject is YarnNode yarnNode && yarnNode.graph is YarnDialogueGraph g)
			{
				return g;
			}
			return null;
		}
	}
}
#endif
#endif