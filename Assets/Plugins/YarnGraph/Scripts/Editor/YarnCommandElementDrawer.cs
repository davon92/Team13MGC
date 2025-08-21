#if XNODE
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Arawn.YarnGraph
{
	[CustomPropertyDrawer(typeof(YarnCommandElement))]
	public class YarnCommandElementDrawer : PropertyDrawer
	{
		private static readonly Dictionary<string, bool> FoldoutStates = new Dictionary<string, bool>();
		private const float PADDING = 4f;
		private static readonly float LINE_HEIGHT = EditorGUIUtility.singleLineHeight;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			string key = property.propertyPath;
			FoldoutStates.TryGetValue(key, out bool foldout);

			// Retrieve the 'commandName' property.
			SerializedProperty cmdProp = property.FindPropertyRelative("commandName");
			string commandName = cmdProp != null ? cmdProp.stringValue : "";
			string headerText = string.IsNullOrEmpty(commandName) ? "Run Command" : $"Run Command: {commandName}";

			// Draw foldout header with dynamic label.
			Rect foldoutRect = new Rect(position.x, position.y, position.width, LINE_HEIGHT);
			bool newFoldout = EditorGUI.Foldout(foldoutRect, foldout, headerText, true);
			FoldoutStates[key] = newFoldout;

			float yPos = foldoutRect.y + foldoutRect.height + PADDING;

			if (newFoldout)
			{
				// Draw an editable popup for commandName.
				Rect cmdRect = new Rect(position.x, yPos, position.width, LINE_HEIGHT);
				yPos += LINE_HEIGHT + PADDING;

				// Retrieve the YarnDialogueGraph from the current node.
				YarnDialogueGraph graphObj = FindYarnDialogueGraph(property);

				if (graphObj != null && graphObj.YarnCommands != null && graphObj.YarnCommands.Count > 0)
				{
					List<string> commands = graphObj.YarnCommands;

					// Optionally add a "(None)" option.
					List<string> allOptions = new List<string> { "(None)" };
					allOptions.AddRange(commands);

					int currentIndex = allOptions.IndexOf(cmdProp.stringValue);
					if (currentIndex < 0) currentIndex = 0;

					int newIndex = EditorGUI.Popup(cmdRect, "Command Name", currentIndex, allOptions.ToArray());
					if (newIndex != currentIndex)
					{
						// If user picks "(None)" => empty string.
						cmdProp.stringValue = (newIndex == 0) ? string.Empty : allOptions[newIndex];
					}
				}
				else
				{
					// Fallback: if no YarnCommands are found.
					EditorGUI.LabelField(cmdRect, "Command Name", "No commands found in YarnDialogueGraph.");
				}
			}

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			string key = property.propertyPath;
			bool foldout = FoldoutStates.TryGetValue(key, out bool f) && f;
			float height = LINE_HEIGHT + PADDING; // Height for the foldout header.
			if (foldout)
			{
				height += LINE_HEIGHT + PADDING; // Height for the command field.
			}
			return height;
		}

		/// <summary>
		/// Attempts to locate the YarnDialogueGraph from the node that owns this property.
		/// </summary>
		private YarnDialogueGraph FindYarnDialogueGraph(SerializedProperty property)
		{
			// Typically property.serializedObject.targetObject is your YarnNode 
			// or something that references the graph.
			if (property.serializedObject.targetObject is YarnNode yarnNode && yarnNode.graph is YarnDialogueGraph g)
			{
				return g;
			}
			// Adapt this method as needed for your project's structure.
			return null;
		}
	}
}
#endif
#endif