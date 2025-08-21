#if XNODE
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;

namespace Arawn.YarnGraph
{
	[CustomPropertyDrawer(typeof(DialogueCharacterReference))]
	public class DialogueCharacterReferenceDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			// Get the properties
			SerializedProperty useVariableProp = property.FindPropertyRelative("useVariable");
			SerializedProperty constantNameProp = property.FindPropertyRelative("constantName");
			SerializedProperty variableNameProp = property.FindPropertyRelative("variableName");

			// Define layout: first a toggle, then a field below it.
			Rect toggleRect = new Rect(position.x, position.y, position.width, EditorGUIUtility.singleLineHeight);
			useVariableProp.boolValue = EditorGUI.ToggleLeft(toggleRect, "Use Declared Variable", useVariableProp.boolValue);

			Rect fieldRect = new Rect(position.x, position.y + EditorGUIUtility.singleLineHeight + 2, position.width, EditorGUIUtility.singleLineHeight);

			if (useVariableProp.boolValue)
			{
				// Attempt to retrieve the YarnDialogueGraph from the node.
				YarnDialogueGraph graph = null;
				if (property.serializedObject.targetObject is YarnNode node && node.graph is YarnDialogueGraph yarnGraph)
				{
					graph = yarnGraph;
				}

				// Build the list of variable options.
				string[] variableOptions;
				if (graph != null && graph.declaredVariables != null && graph.declaredVariables.Count > 0)
				{
					variableOptions = new string[graph.declaredVariables.Count + 1];
					variableOptions[0] = "None"; // Option to deselect.
					for (int i = 0; i < graph.declaredVariables.Count; i++)
					{
						// Here we use the ScriptableObject's name property.
						variableOptions[i + 1] = graph.declaredVariables[i].variableName;
					}
				}
				else
				{
					variableOptions = new string[] { "None" };
				}

				// Find the current index in the options.
				int currentIndex = Array.IndexOf(variableOptions, variableNameProp.stringValue);
				if (currentIndex < 0) currentIndex = 0;
				int newIndex = EditorGUI.Popup(fieldRect, "Character Variable", currentIndex, variableOptions);
				variableNameProp.stringValue = variableOptions[newIndex];
			}
			else
			{
				// Show a text field for a constant name.
				constantNameProp.stringValue = EditorGUI.TextField(fieldRect, "Character Name", constantNameProp.stringValue);
			}

			property.serializedObject.ApplyModifiedProperties();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			// Two lines plus a little spacing.
			return EditorGUIUtility.singleLineHeight * 2 + 2;
		}
	}
}

#endif
#endif