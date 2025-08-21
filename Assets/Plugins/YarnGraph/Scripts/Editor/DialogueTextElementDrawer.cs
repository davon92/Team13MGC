#if XNODE
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Arawn.YarnGraph
{
	[CustomPropertyDrawer(typeof(DialogueTextElement))]
	public class DialogueTextElementDrawer : PropertyDrawer
	{
		// Cache for foldout states by instance and property path.
		private static readonly Dictionary<int, Dictionary<string, bool>> FoldoutStates = new Dictionary<int, Dictionary<string, bool>>();
		private const float PADDING = 4f;
		private static readonly float LINE_HEIGHT = EditorGUIUtility.singleLineHeight;
		private const float MIN_TEXT_AREA_HEIGHT = 60f;

		// Pre-compiled Regex to avoid creating it on every OnGUI call.
		private static readonly Regex tagRegex = new Regex(@"#\S+", RegexOptions.Compiled);

		// Cache to store split lines for a given text value.
		private static readonly Dictionary<string, string[]> splitTextCache = new Dictionary<string, string[]>();

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			// Get a unique key for the serialized object's instance.
			int instanceID = property.serializedObject.targetObject.GetInstanceID();
			if (!FoldoutStates.TryGetValue(instanceID, out var instanceFoldouts))
			{
				instanceFoldouts = new Dictionary<string, bool>();
				FoldoutStates[instanceID] = instanceFoldouts;
			}
			string key = property.propertyPath;
			if (!instanceFoldouts.TryGetValue(key, out bool foldout))
			{
				foldout = false;
				instanceFoldouts[key] = foldout;
			}

			// Retrieve Character property for summary.
			SerializedProperty charProp = property.FindPropertyRelative("character");
			string characterName = "";
			if (charProp != null)
			{
				SerializedProperty useVarProp = charProp.FindPropertyRelative("useVariable");
				if (useVarProp != null && useVarProp.boolValue)
				{
					SerializedProperty varNameProp = charProp.FindPropertyRelative("variableName");
					if (varNameProp != null)
						characterName = varNameProp.stringValue;
				}
				else
				{
					SerializedProperty constNameProp = charProp.FindPropertyRelative("constantName");
					if (constNameProp != null)
						characterName = constNameProp.stringValue;
				}
			}

			// Retrieve first line of the dialogue text.
			SerializedProperty textProp = property.FindPropertyRelative("text");
			string textValue = textProp.stringValue;
			string firstLine = "";
			if (!string.IsNullOrEmpty(textValue))
			{
				string[] split = textValue.Split('\n');
				if (split.Length > 0)
					firstLine = split[0];
			}

			// Build summary string.
			string summary = string.IsNullOrEmpty(characterName) ? firstLine : $"{characterName}: {firstLine}";
			// Limit to 50 characters.
			if (summary.Length > 50)
				summary = summary.Substring(0, 50) + "...";
			string headerText = $"Write Dialogue: {summary}";

			// Draw foldout header.
			Rect foldoutRect = new Rect(position.x, position.y, position.width, LINE_HEIGHT);
			foldout = EditorGUI.Foldout(foldoutRect, foldout, headerText, true);
			instanceFoldouts[key] = foldout;

			// When collapsed, add the extra icons on the header.
			if (!foldout)
			{
				// Get the DialogueNode and graph to check inline tag validity.
				DialogueNode node = property.serializedObject.targetObject as DialogueNode;
				YarnDialogueGraph graph = node != null ? node.graph as YarnDialogueGraph : null;

				// Check for inline tags (ignoring any "#line" tags).
				bool inlineTagFound = false;
				bool inlineTagAllValid = true;
				string[] lines = textValue.Split('\n');
				for (int i = 0; i < lines.Length; i++)
				{
					var matches = tagRegex.Matches(lines[i]);
					foreach (System.Text.RegularExpressions.Match m in matches)
					{
						if (m.Value.StartsWith("#line", System.StringComparison.OrdinalIgnoreCase))
							continue;
						inlineTagFound = true;
						if (graph == null || !graph.YarnInlineTags.Contains(m.Value))
						{
							inlineTagAllValid = false;
						}
					}
				}

				// Check for Voice Over: if Use Voice Over is true and at least one voiceOverClip is assigned.
				SerializedProperty useVOProp = property.FindPropertyRelative("UseVoiceOver");
				bool showSpeaker = false;
				if (useVOProp != null && useVOProp.boolValue)
				{
					SerializedProperty clipsProp = property.FindPropertyRelative("voiceOverClips");
					if (clipsProp != null && clipsProp.arraySize > 0)
					{
						for (int i = 0; i < clipsProp.arraySize; i++)
						{
							SerializedProperty clipProp = clipsProp.GetArrayElementAtIndex(i);
							if (clipProp.objectReferenceValue != null)
							{
								showSpeaker = true;
								break;
							}
						}
					}
				}

				// Draw icons on the header: speaker icon (if any) to the left, inline tag icon to its right.
				float iconSize = 16f;
				float iconPadding = 4f;
				int iconCount = 0;
				if (showSpeaker) iconCount++;
				if (inlineTagFound) iconCount++;
				if (iconCount > 0)
				{
					float totalWidth = (iconSize * iconCount) + (iconPadding * (iconCount - 1));
					float startX = foldoutRect.xMax - totalWidth;
					// Draw speaker icon if condition met.
					if (showSpeaker)
					{
						Rect speakerIconRect = new Rect(startX, foldoutRect.y, iconSize, iconSize);
						EditorGUI.LabelField(speakerIconRect, "🔊");
						startX += iconSize + iconPadding;
					}
					// Draw inline tag icon if any inline tag is used.
					if (inlineTagFound)
					{
						Color originalColor = GUI.color;
						GUI.color = inlineTagAllValid ? Color.green : new Color(1f, 0.4f, 0f);
						Rect inlineIconRect = new Rect(startX, foldoutRect.y, iconSize, iconSize);
						EditorGUI.LabelField(inlineIconRect, "●");
						GUI.color = originalColor;
					}
				}
			}

			float yPos = foldoutRect.y + foldoutRect.height + PADDING;

			if (foldout)
			{
				// Draw Character field.
				float charHeight = EditorGUI.GetPropertyHeight(charProp, true);
				Rect charRect = new Rect(position.x, yPos, position.width, charHeight);
				EditorGUI.PropertyField(charRect, charProp, new GUIContent("Character"), true);
				yPos += charHeight + PADDING;

				// Draw Use Voice Over toggle.
				SerializedProperty useVOProp = property.FindPropertyRelative("UseVoiceOver");
				Rect useVORect = new Rect(position.x, yPos, position.width, LINE_HEIGHT);
				EditorGUI.PropertyField(useVORect, useVOProp, new GUIContent("Use Voice Over"));
				yPos += LINE_HEIGHT + PADDING;

				// Draw the text area.
				// Retrieve cached split lines or split if not cached.
				if (!splitTextCache.TryGetValue(textValue, out var linesCache))
				{
					linesCache = textValue.Split('\n');
					splitTextCache[textValue] = linesCache;
				}
				int lineCount = Mathf.Max(linesCache.Length, 1);
				float textAreaHeight = Mathf.Max(lineCount * (LINE_HEIGHT + 2), MIN_TEXT_AREA_HEIGHT);

				Rect textAreaRect;
				if (useVOProp.boolValue)
					textAreaRect = new Rect(position.x, yPos, position.width * 0.75f, textAreaHeight);
				else
					textAreaRect = new Rect(position.x, yPos, position.width, textAreaHeight);

				// Draw the editable text area.
				textProp.stringValue = EditorGUI.TextArea(textAreaRect, textValue);

				// Update cache if text changes.
				if (textProp.stringValue != textValue)
				{
					linesCache = textProp.stringValue.Split('\n');
					splitTextCache[textProp.stringValue] = linesCache;
					lineCount = Mathf.Max(linesCache.Length, 1);
				}

				// For each line, check for inline tags and draw a symbol.
				// If any non-#line inline tag is not found in YarnDialogueGraph.YarnInlineTags, draw in orange; otherwise, green.
				DialogueNode node = property.serializedObject.targetObject as DialogueNode;
				YarnDialogueGraph graph = node != null ? node.graph as YarnDialogueGraph : null;
				for (int i = 0; i < lineCount; i++)
				{
					var matches = tagRegex.Matches(linesCache[i]);
					bool allValid = true;
					bool foundValidTag = false;
					foreach (System.Text.RegularExpressions.Match m in matches)
					{
						string tag = m.Value;
						// Skip "#line" tags.
						if (tag.StartsWith("#line", System.StringComparison.OrdinalIgnoreCase))
							continue;
						foundValidTag = true;
						// If any inline tag is not in the YarnInlineTags list, mark as invalid.
						if (graph == null || !graph.YarnInlineTags.Contains(tag))
						{
							allValid = false;
							break;
						}
					}
					if (matches.Count > 0 && foundValidTag)
					{
						Color symbolColor = allValid ? Color.green : new Color(1f, 0.4f, 0f);
						Rect symbolRect = new Rect(
							textAreaRect.xMax - 16 - PADDING,
							textAreaRect.y + i * (LINE_HEIGHT / 1.25f),
							16,
							LINE_HEIGHT
						);
						Color originalColor = GUI.color;
						GUI.color = symbolColor;
						EditorGUI.LabelField(symbolRect, "●");
						GUI.color = originalColor;
					}
				}

				if (useVOProp.boolValue)
				{
					SerializedProperty clipsProp = property.FindPropertyRelative("voiceOverClips");
					clipsProp.arraySize = lineCount;

					Rect clipsRect = new Rect(textAreaRect.xMax + PADDING, yPos, position.width * 0.25f - PADDING, textAreaHeight);
					GUI.Box(clipsRect, GUIContent.none);

					for (int i = 0; i < lineCount; i++)
					{
						Rect clipFieldRect = new Rect(clipsRect.x, clipsRect.y + i * (LINE_HEIGHT + 2), clipsRect.width, LINE_HEIGHT);
						SerializedProperty clipProp = clipsProp.GetArrayElementAtIndex(i);
						EditorGUI.PropertyField(clipFieldRect, clipProp, GUIContent.none);
					}
				}
			}

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			int instanceID = property.serializedObject.targetObject.GetInstanceID();
			bool foldout = false;
			if (FoldoutStates.TryGetValue(instanceID, out var instanceFoldouts))
			{
				foldout = instanceFoldouts.TryGetValue(property.propertyPath, out bool f) && f;
			}

			float height = LINE_HEIGHT + PADDING;
			if (foldout)
			{
				SerializedProperty charProp = property.FindPropertyRelative("character");
				height += EditorGUI.GetPropertyHeight(charProp, true) + PADDING;
				height += LINE_HEIGHT + PADDING;

				SerializedProperty textProp = property.FindPropertyRelative("text");
				int lineCount = Mathf.Max(textProp.stringValue.Split('\n').Length, 1);
				float textAreaHeight = Mathf.Max(lineCount * (LINE_HEIGHT + 2), MIN_TEXT_AREA_HEIGHT);
				height += textAreaHeight + PADDING;
			}
			return height;
		}
	}
}
#endif
#endif