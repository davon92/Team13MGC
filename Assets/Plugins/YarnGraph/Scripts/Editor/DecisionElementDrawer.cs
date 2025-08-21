#if XNODE
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Arawn.YarnGraph
{
	[CustomPropertyDrawer(typeof(DecisionElement))]
	public class DecisionElementDrawer : PropertyDrawer
	{
		private const float ElementSpacing = 2f;
		private const float ButtonWidth = 20f;
		private const float ButtonHeight = 20f;

		private static Dictionary<Type, string> dialogueElementTypesWithTitles;

		private static void EnsureDialogueElementTypes()
		{
			if (dialogueElementTypesWithTitles == null)
			{
				dialogueElementTypesWithTitles = new Dictionary<Type, string>();
				var types = AppDomain.CurrentDomain.GetAssemblies()
					.SelectMany(assembly => assembly.GetTypes())
					.Where(type => typeof(DialogueElement).IsAssignableFrom(type)
								   && !type.IsAbstract
								   && !type.IsInterface);

				foreach (var type in types)
				{
					var attribute = type.GetCustomAttribute<ElementTitleAttribute>();
					string title = attribute != null ? attribute.Title : type.Name;
					dialogueElementTypesWithTitles[type] = title;
				}
			}
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			SerializedProperty optionsProp = property.FindPropertyRelative("options");
			if (optionsProp == null)
			{
				EditorGUI.LabelField(position, "Error: 'options' property not found.");
				EditorGUI.EndProperty();
				return;
			}

			float lineHeight = EditorGUIUtility.singleLineHeight;
			float verticalSpacing = ElementSpacing;

			// Always build the summary title from the options.
			string summary = BuildDecisionSummary(optionsProp);
			label = new GUIContent(summary);

			// Draw main property foldout using the summary label.
			Rect currentRect = new Rect(position.x, position.y, position.width, lineHeight);
			property.isExpanded = EditorGUI.Foldout(currentRect, property.isExpanded, label, true);
			if (!property.isExpanded)
			{
				EditorGUI.EndProperty();
				return;
			}

			EditorGUI.indentLevel++;
			currentRect.y += lineHeight + verticalSpacing;

			// "Options" header
			EditorGUI.LabelField(currentRect, "Options", EditorStyles.boldLabel);
			currentRect.y += lineHeight + verticalSpacing;

			for (int i = 0; i < optionsProp.arraySize; i++)
			{
				SerializedProperty optionProp = optionsProp.GetArrayElementAtIndex(i);
				if (optionProp.managedReferenceValue == null)
				{
					EditorGUI.LabelField(
						new Rect(currentRect.x, currentRect.y, currentRect.width, lineHeight),
						"Option is null"
					);
					currentRect.y += lineHeight + verticalSpacing;
					continue;
				}

				SerializedProperty dialogueElementsProp = optionProp.FindPropertyRelative("dialogueElements");
				if (dialogueElementsProp == null)
				{
					EditorGUI.LabelField(
						new Rect(currentRect.x, currentRect.y, currentRect.width, lineHeight),
						"Dialogue elements not initialized"
					);
					currentRect.y += lineHeight + verticalSpacing;
					continue;
				}

				float optionHeight = GetOptionHeight(optionProp);
				Rect optionRect = new Rect(currentRect.x, currentRect.y, currentRect.width, optionHeight);

				// Option header
				Rect headerRect = new Rect(
					optionRect.x,
					optionRect.y,
					optionRect.width - (ButtonWidth * 2 + 4),
					lineHeight
				);
				Rect removeButtonRect = new Rect(optionRect.xMax - ButtonWidth * 2 - 2, optionRect.y, ButtonWidth, lineHeight);
				Rect duplicateButtonRect = new Rect(optionRect.xMax - ButtonWidth, optionRect.y, ButtonWidth, lineHeight);

				string optionLabel = $"Option {i + 1}";
				if (dialogueElementsProp.arraySize > 0 && dialogueElementsProp.GetArrayElementAtIndex(0) != null)
				{
					SerializedProperty firstElement = dialogueElementsProp.GetArrayElementAtIndex(0);
					string firstText = GetFirstElementText(firstElement);
					if (!string.IsNullOrEmpty(firstText))
						optionLabel += $": \"{firstText}\"";
				}

				GUIStyle foldoutStyle = new GUIStyle(EditorStyles.foldout)
				{
					wordWrap = false,
					clipping = TextClipping.Clip
				};

				optionProp.isExpanded = EditorGUI.Foldout(
					headerRect,
					optionProp.isExpanded,
					new GUIContent(optionLabel),
					true,
					foldoutStyle
				);

				if (GUI.Button(removeButtonRect, "-"))
				{
					optionsProp.DeleteArrayElementAtIndex(i);
					property.serializedObject.ApplyModifiedProperties();
					break;
				}
				if (GUI.Button(duplicateButtonRect, "+"))
				{
					optionsProp.InsertArrayElementAtIndex(i);
					property.serializedObject.ApplyModifiedProperties();
					break;
				}

				if (optionProp.isExpanded)
				{
					EditorGUI.indentLevel++;
					Rect elementRect = new Rect(
						optionRect.x,
						optionRect.y + lineHeight + verticalSpacing,
						optionRect.width,
						optionHeight - (lineHeight + verticalSpacing)
					);

					// ---- Condition field (moved before dialogue) ----
					SerializedProperty conditionProp = optionProp.FindPropertyRelative("condition");
					// Capture the current conditionProp in a local variable to avoid closure issues.
					var localConditionProp = conditionProp;
					if (localConditionProp.managedReferenceValue == null)
					{
						if (GUI.Button(new Rect(elementRect.x, elementRect.y, elementRect.width, lineHeight), "Add Condition"))
						{
							GenericMenu menu = new GenericMenu();
							// Use direct instantiation for CompoundCondition to avoid type resolution issues.
							menu.AddItem(new GUIContent("Compound"), false, () =>
							{
								localConditionProp.managedReferenceValue = new CompoundCondition();
								localConditionProp.serializedObject.ApplyModifiedProperties();
							});
							menu.AddItem(new GUIContent("Comparison"), false, () =>
							{
								localConditionProp.managedReferenceValue = new SingleComparison();
								localConditionProp.serializedObject.ApplyModifiedProperties();
							});
							menu.AddItem(new GUIContent("Visited"), false, () =>
							{
								localConditionProp.managedReferenceValue = new VisitedCondition();
								localConditionProp.serializedObject.ApplyModifiedProperties();
							});
							menu.AddItem(new GUIContent("Visited Count"), false, () =>
							{
								localConditionProp.managedReferenceValue = new VisitedCountCondition();
								localConditionProp.serializedObject.ApplyModifiedProperties();
							});
							menu.AddItem(new GUIContent("Not"), false, () =>
							{
								localConditionProp.managedReferenceValue = new NotCondition(new VisitedCondition());
								localConditionProp.serializedObject.ApplyModifiedProperties();
							});
							menu.DropDown(new Rect(elementRect.x, elementRect.y, 0, 0));
						}
						elementRect.y += lineHeight + verticalSpacing;
					}
					else
					{
						float conditionHeight = EditorGUI.GetPropertyHeight(localConditionProp, true);
						EditorGUI.PropertyField(new Rect(elementRect.x, elementRect.y, elementRect.width, conditionHeight), localConditionProp, true);
						elementRect.y += conditionHeight + verticalSpacing;
						// ---- Remove Condition Button ----
						if (GUI.Button(new Rect(elementRect.x, elementRect.y, elementRect.width, lineHeight), "Remove Condition"))
						{
							localConditionProp.managedReferenceValue = null;
							localConditionProp.serializedObject.ApplyModifiedProperties();
						}
						elementRect.y += lineHeight + verticalSpacing;
					}

					// ---- Now draw the dialogue elements ----
					if (dialogueElementsProp.arraySize == 0)
					{
						EditorGUI.LabelField(elementRect, "Add an option text first!");
						elementRect.y += lineHeight + verticalSpacing;
					}
					else
					{
						SerializedProperty firstElement = dialogueElementsProp.GetArrayElementAtIndex(0);
						float firstElementHeight = EditorGUI.GetPropertyHeight(firstElement);
						EditorGUI.PropertyField(new Rect(elementRect.x, elementRect.y, elementRect.width, firstElementHeight), firstElement, GUIContent.none);
						elementRect.y += firstElementHeight + verticalSpacing;

						if (dialogueElementsProp.arraySize > 1)
						{
							for (int j = 1; j < dialogueElementsProp.arraySize; j++)
							{
								SerializedProperty elementProp = dialogueElementsProp.GetArrayElementAtIndex(j);
								if (elementProp == null) continue;
								float elementHeight = EditorGUI.GetPropertyHeight(elementProp);
								EditorGUI.PropertyField(new Rect(elementRect.x, elementRect.y, elementRect.width, elementHeight), elementProp, true);
								elementRect.y += elementHeight + verticalSpacing;
							}
						}
					}

					// ---- Add/Remove DialogueElement buttons ----
					SerializedProperty currentDialogueElementsProp = dialogueElementsProp;
					if (GUI.Button(new Rect(elementRect.x, elementRect.y, elementRect.width / 2 - 2, ButtonHeight), "Add Element"))
					{
						EnsureDialogueElementTypes();
						if (dialogueElementTypesWithTitles.Count == 0)
						{
							Debug.LogWarning("No DialogueElement subclasses found.");
						}
						else
						{
							GenericMenu menu = new GenericMenu();
							foreach (var kvp in dialogueElementTypesWithTitles)
							{
								Type type = kvp.Key;
								string title = kvp.Value;
								menu.AddItem(new GUIContent(title), false, () =>
								{
									currentDialogueElementsProp.InsertArrayElementAtIndex(currentDialogueElementsProp.arraySize);
									SerializedProperty newElement = currentDialogueElementsProp.GetArrayElementAtIndex(currentDialogueElementsProp.arraySize - 1);
									newElement.managedReferenceValue = Activator.CreateInstance(type);
									property.serializedObject.ApplyModifiedProperties();
								});
							}
							menu.DropDown(new Rect(elementRect.x, elementRect.y + ButtonHeight, 0, 0));
						}
					}
					if (GUI.Button(new Rect(elementRect.x + elementRect.width / 2 + 2, elementRect.y, elementRect.width / 2 - 2, ButtonHeight), "Remove Last"))
					{
						if (dialogueElementsProp.arraySize > 0)
							dialogueElementsProp.DeleteArrayElementAtIndex(dialogueElementsProp.arraySize - 1);
						property.serializedObject.ApplyModifiedProperties();
					}

					EditorGUI.indentLevel--;
				}

				currentRect.y += optionHeight + verticalSpacing;
			}

			// "Add Option" button
			if (GUI.Button(new Rect(currentRect.x, currentRect.y, currentRect.width, ButtonHeight), "Add Option"))
			{
				optionsProp.InsertArrayElementAtIndex(optionsProp.arraySize);
				SerializedProperty newOption = optionsProp.GetArrayElementAtIndex(optionsProp.arraySize - 1);
				if (newOption.managedReferenceValue == null)
				{
					newOption.managedReferenceValue = new DecisionOption();
				}

				// Initialize with a single DialogueTextElement
				SerializedProperty newDialogueElements = newOption.FindPropertyRelative("dialogueElements");
				if (newDialogueElements != null)
				{
					newDialogueElements.arraySize = 1;
					SerializedProperty firstElement = newDialogueElements.GetArrayElementAtIndex(0);
					if (firstElement != null && firstElement.managedReferenceValue == null)
					{
						firstElement.managedReferenceValue = new DialogueTextElement();
					}
				}

				// Explicitly set the condition to null for the new option so it can be assigned individually.
				SerializedProperty newConditionProp = newOption.FindPropertyRelative("condition");
				if (newConditionProp != null)
				{
					newConditionProp.managedReferenceValue = null;
				}

				property.serializedObject.ApplyModifiedProperties();
				property.serializedObject.Update();
			}

			EditorGUI.indentLevel--;
			EditorGUI.EndProperty();
		}

		// ---------------------------
		// Height Calculation
		// ---------------------------
		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			if (!property.isExpanded)
				return EditorGUIUtility.singleLineHeight;

			SerializedProperty optionsProp = property.FindPropertyRelative("options");
			if (optionsProp == null)
				return EditorGUIUtility.singleLineHeight * 2;

			float lineHeight = EditorGUIUtility.singleLineHeight;
			float verticalSpacing = ElementSpacing;
			// Foldout line + "Options" label
			float height = (lineHeight * 2) + (verticalSpacing * 2);

			// Add heights for each option
			for (int i = 0; i < optionsProp.arraySize; i++)
			{
				SerializedProperty optionProp = optionsProp.GetArrayElementAtIndex(i);
				height += GetOptionHeight(optionProp) + verticalSpacing;
			}

			// "Add Option" button
			height += ButtonHeight + verticalSpacing;
			return height;
		}

		private float GetOptionHeight(SerializedProperty optionProp)
		{
			if (optionProp == null || !optionProp.isExpanded)
				return EditorGUIUtility.singleLineHeight;

			float lineHeight = EditorGUIUtility.singleLineHeight;
			float verticalSpacing = ElementSpacing;
			float height = lineHeight; // Header

			// ---- Condition block ----
			height += lineHeight + verticalSpacing; // space for "Add Condition" button or condition field
			SerializedProperty conditionProp = optionProp.FindPropertyRelative("condition");
			if (conditionProp != null)
			{
				if (conditionProp.managedReferenceValue == null)
					height += lineHeight + verticalSpacing; // fixed height for Add Condition button
				else
					height += EditorGUI.GetPropertyHeight(conditionProp, true) + verticalSpacing + lineHeight + verticalSpacing;
			}
			else
			{
				height += lineHeight + verticalSpacing;
			}

			// ---- Dialogue Elements block ----
			SerializedProperty dialogueElementsProp = optionProp.FindPropertyRelative("dialogueElements");
			if (dialogueElementsProp.arraySize == 0)
			{
				height += lineHeight + verticalSpacing;
			}
			else
			{
				height += lineHeight + verticalSpacing;
				SerializedProperty firstElement = dialogueElementsProp.GetArrayElementAtIndex(0);
				if (firstElement != null && firstElement.managedReferenceValue != null)
					height += EditorGUI.GetPropertyHeight(firstElement) + verticalSpacing;
				else
					height += lineHeight + verticalSpacing;

				if (dialogueElementsProp.arraySize > 1)
				{
					height += lineHeight + verticalSpacing; // label for outcomes (omitted)
					for (int j = 1; j < dialogueElementsProp.arraySize; j++)
					{
						SerializedProperty elementProp = dialogueElementsProp.GetArrayElementAtIndex(j);
						if (elementProp != null && elementProp.managedReferenceValue != null)
							height += EditorGUI.GetPropertyHeight(elementProp) + verticalSpacing;
						else
							height += lineHeight + verticalSpacing;
					}
				}
			}

			// ---- Buttons row ----
			height += ButtonHeight;
			return height;
		}

		private string GetFirstElementText(SerializedProperty elementProp)
		{
			if (elementProp?.managedReferenceValue == null)
				return "";
			SerializedProperty textProp = elementProp.FindPropertyRelative("text");
			return textProp != null ? textProp.stringValue : "";
		}

		// ---- Helper: Build summary for fold-in and fold-out title ----
		private string BuildDecisionSummary(SerializedProperty optionsProp)
		{
			string summary = "Decision: ";
			for (int i = 0; i < optionsProp.arraySize; i++)
			{
				SerializedProperty option = optionsProp.GetArrayElementAtIndex(i);
				SerializedProperty dialogueElementsProp = option.FindPropertyRelative("dialogueElements");
				if (dialogueElementsProp != null && dialogueElementsProp.arraySize > 0)
				{
					SerializedProperty firstElement = dialogueElementsProp.GetArrayElementAtIndex(0);
					string optionText = GetFirstElementText(firstElement);
					if (string.IsNullOrEmpty(optionText))
						optionText = $"Option {i + 1}";
					if (i > 0)
						summary += " vs. ";
					summary += optionText;
				}
			}
			if (summary.Length > 60)
				summary = summary.Substring(0, 60) + "...";
			return summary;
		}
	}
}
#endif
#endif