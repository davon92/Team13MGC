#if XNODE
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;

namespace Arawn.YarnGraph
{
	[CustomPropertyDrawer(typeof(JumpElement))]
	public class JumpElementDrawer : PropertyDrawer
	{
		// Track foldout state across all JumpElements in the inspector
		private static readonly Dictionary<string, bool> FoldoutStates = new Dictionary<string, bool>();

		private const float PADDING = 4f;
		private static float LINE_HEIGHT
		{
			get { return EditorGUIUtility.singleLineHeight; }
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			// Retrieve foldout state
			string key = property.propertyPath;
			if (!FoldoutStates.TryGetValue(key, out bool isFoldedOut))
			{
				isFoldedOut = false;
				FoldoutStates[key] = false;
			}

			// Grab references to your fields
			SerializedProperty jumpModeProp = property.FindPropertyRelative("jumpMode");
			SerializedProperty nodeNameProp = property.FindPropertyRelative("NodeName");
			SerializedProperty portNameProp = property.FindPropertyRelative("portName");

			// Foldout label
			string displayName = string.IsNullOrEmpty(nodeNameProp.stringValue)
				? "Jump Element"
				: $"Jump: {nodeNameProp.stringValue}";

			// Draw the foldout
			float lineH = LINE_HEIGHT;
			Rect foldoutRect = new Rect(position.x, position.y, position.width, lineH);
			isFoldedOut = EditorGUI.Foldout(foldoutRect, isFoldedOut, new GUIContent(displayName), true);
			FoldoutStates[key] = isFoldedOut;

			float yPos = foldoutRect.y + foldoutRect.height + PADDING;

			if (isFoldedOut)
			{
				// Draw JumpMode
				Rect modeRect = new Rect(position.x, yPos, position.width, lineH);
				EditorGUI.PropertyField(modeRect, jumpModeProp);
				yPos += lineH + PADDING;

				JumpElement.JumpMode mode = (JumpElement.JumpMode)jumpModeProp.enumValueIndex;

				if (mode == JumpElement.JumpMode.Manual)
				{
					// Manual mode => just a NodeName text field
					Rect nameRect = new Rect(position.x, yPos, position.width, lineH);
					EditorGUI.PropertyField(nameRect, nodeNameProp, new GUIContent("Node Name"));
				}
				else
				{
					// ========== USE PORT MODE ==========

					// 1) If portName is empty, generate a default now
					if (string.IsNullOrEmpty(portNameProp.stringValue))
					{
						portNameProp.stringValue = "JumpPort_" + System.Guid.NewGuid().ToString("N").Substring(0, 4);
						// Make sure we commit this change so the node sees it
						property.serializedObject.ApplyModifiedProperties();
					}

					// 2) Show a text field to rename it
					Rect portNameRect = new Rect(position.x, yPos, position.width, lineH);

					EditorGUI.BeginChangeCheck();
					EditorGUI.PropertyField(portNameRect, portNameProp, new GUIContent("Port Name"));
					if (EditorGUI.EndChangeCheck())
					{
						// If user changed the port name, mark the node as dirty
						property.serializedObject.ApplyModifiedProperties();

						DialogueNode node = property.serializedObject.targetObject as DialogueNode;
						if (node != null)
						{
							EditorUtility.SetDirty(node);
						}
					}
				}
			}

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			bool isFoldedOut = false;
			FoldoutStates.TryGetValue(property.propertyPath, out isFoldedOut);

			if (!isFoldedOut)
			{
				// Just the foldout line
				return LINE_HEIGHT + PADDING;
			}
			else
			{
				// 1 line for foldout + 1 line for jumpMode + 1 line for NodeName/PortName
				return (LINE_HEIGHT * 3) + (PADDING * 2);
			}
		}
	}
}
#endif
#endif