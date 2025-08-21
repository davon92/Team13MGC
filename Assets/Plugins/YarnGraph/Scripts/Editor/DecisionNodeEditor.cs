#if XNODE
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Collections.Generic;
using XNodeEditor;
using XNode;

namespace Arawn.YarnGraph.Editor
{
	[CustomNodeEditor(typeof(DecisionNode))]
	public class DecisionNodeEditor : NodeEditor
	{
		private bool isResizing;
		private Vector2 resizeStartMousePos;
		private Vector2 resizeStartSize;

		private string[] conditionTypes = { "Compound", "Visited", "Visited Count", "Single Comparison" };

		// Static dictionaries to track custom entry mode per SingleComparison instance.
		private static Dictionary<SingleComparison, bool> customLeftStates = new Dictionary<SingleComparison, bool>();
		private static Dictionary<SingleComparison, bool> customRightStates = new Dictionary<SingleComparison, bool>();

		// Dictionary to store advanced foldout state for each DecisionOption.
		private static Dictionary<DecisionOption, bool> optionAdvancedFoldouts = new Dictionary<DecisionOption, bool>();

		public override void OnHeaderGUI()
		{
			EditorGUILayout.LabelField("Decision Node", EditorStyles.boldLabel);
		}

		public override void OnBodyGUI()
		{
			serializedObject.Update();

			DecisionNode node = target as DecisionNode;
			if (node == null) return;

			// Auto-reference the input YarnNode from the input port.
			SerializedProperty inputProp = serializedObject.FindProperty("input");
			if (inputProp != null)
			{
				NodePort inputPort = node.GetInputPort("input");
				if (inputPort != null && inputPort.IsConnected)
				{
					NodePort connectedPort = inputPort.GetConnection(0);
					if (connectedPort != null)
					{
						YarnNode connectedNode = connectedPort.node as YarnNode;
						if (connectedNode != null && inputProp.objectReferenceValue != connectedNode)
						{
							inputProp.objectReferenceValue = connectedNode;
						}
					}
				}
				else if (inputProp.objectReferenceValue != null)
				{
					inputProp.objectReferenceValue = null;
				}
			}

			// Sync options with dynamic ports.
			SerializedProperty optionsProp = serializedObject.FindProperty("options");
			if (optionsProp != null && optionsProp.isArray)
			{
				for (int i = 0; i < optionsProp.arraySize; i++)
				{
					string portName = $"options {i}";
					NodePort port = node.GetPort(portName);
					if (port != null && port.IsConnected)
					{
						NodePort connectedPort = port.GetConnection(0);
						if (connectedPort != null)
						{
							YarnNode connectedNode = connectedPort.node as YarnNode;
							if (connectedNode != null)
							{
								SerializedProperty elementProp = optionsProp.GetArrayElementAtIndex(i);
								SerializedProperty targetProp = elementProp.FindPropertyRelative("target");
								if (targetProp != null && targetProp.objectReferenceValue != connectedNode)
								{
									targetProp.objectReferenceValue = connectedNode;
								}
							}
						}
					}
					else
					{
						SerializedProperty elementProp = optionsProp.GetArrayElementAtIndex(i);
						SerializedProperty targetProp = elementProp.FindPropertyRelative("target");
						if (targetProp != null && targetProp.objectReferenceValue != null)
						{
							targetProp.objectReferenceValue = null;
						}
					}
				}
			}

			DrawPropertiesExcluding(serializedObject,
				"m_Script", "position", "ports", "graph", "input", "options", "stripOtherOptionsIfTrue", "nodeSize");

			// --- Resize handle ---
			DrawResizeHandle(node);

			serializedObject.ApplyModifiedProperties();

			// Draw input port.
			if (node.GetInputPort("input") != null)
				NodeEditorGUILayout.PortField(node.GetInputPort("input"));

			// Update dynamic output ports.
			node.UpdatePorts();

			// Draw dynamic output ports with option label from dialogueElements.
			if (node.options != null)
			{
				for (int i = 0; i < node.options.Count; i++)
				{
					string portName = $"options {i}";
					NodePort port = node.GetPort(portName);
					if (port != null)
					{
						string label = $"Option {i}: {GetOptionLabel(node.options[i])}";
						NodeEditorGUILayout.PortField(new GUIContent(label), port);
					}
				}
			}

			// Custom UI for editing DecisionOptions.
			if (node.options != null)
			{
				EditorGUILayout.LabelField("Edit Decision Options:");
				YarnDialogueGraph graphObj = node.graph as YarnDialogueGraph;
				for (int i = 0; i < node.options.Count; i++)
				{
					EditorGUILayout.BeginVertical("box");

					EditorGUILayout.BeginHorizontal();

					// Up button.
					if (i > 0)
					{
						if (GUILayout.Button("↑", GUILayout.Width(25)))
						{
							var temp = node.options[i - 1];
							node.options[i - 1] = node.options[i];
							node.options[i] = temp;
						}
					}
					else
					{
						GUILayout.Space(25);
					}

					// Down button.
					if (i < node.options.Count - 1)
					{
						if (GUILayout.Button("↓", GUILayout.Width(25)))
						{
							var temp = node.options[i + 1];
							node.options[i + 1] = node.options[i];
							node.options[i] = temp;
						}
					}
					else
					{
						GUILayout.Space(25);
					}

					// Ensure the option has a DialogueTextElement.
					SetOrCreateOptionDialogueElement(node.options[i]);
					// Edit the option label.
					string currentLabel = GetOptionLabel(node.options[i]);
					string newLabel = EditorGUILayout.TextField($"Option {i}", currentLabel);
					SetOptionLabel(node.options[i], newLabel);

					// Remove button.
					if (GUILayout.Button("-", GUILayout.Width(20)))
					{
						node.options.RemoveAt(i);
						i--;
						EditorGUILayout.EndHorizontal();
						EditorGUILayout.EndVertical();
						continue;
					}
					EditorGUILayout.EndHorizontal();

					// Advanced Option Settings for the DialogueTextElement.
					DialogueTextElement dte = node.options[i].dialogueElements[0] as DialogueTextElement;
					if (dte != null)
					{
						if (!optionAdvancedFoldouts.ContainsKey(node.options[i]))
						{
							optionAdvancedFoldouts[node.options[i]] = false;
						}
						optionAdvancedFoldouts[node.options[i]] = EditorGUILayout.Foldout(optionAdvancedFoldouts[node.options[i]], "Advanced Option Settings");
						if (optionAdvancedFoldouts[node.options[i]])
						{
							// Field for Use Declared Variable.
							dte.character.useVariable = EditorGUILayout.Toggle("Use Declared Variable", dte.character.useVariable);
							if (dte.character.useVariable)
							{
								// If true, show a dropdown with declared variables.
								if (graphObj != null && graphObj.declaredVariables != null)
								{
									List<string> varNames = graphObj.declaredVariables.Select(v => v.variableName).ToList();
									varNames.Add("Custom");
									int currentIndex = varNames.IndexOf(dte.character.variableName);
									if (currentIndex < 0) currentIndex = varNames.Count - 1;
									int newIndex = EditorGUILayout.Popup("Variable", currentIndex, varNames.ToArray());
									if (newIndex == varNames.Count - 1)
									{
										dte.character.variableName = EditorGUILayout.TextField("Custom Variable", dte.character.variableName);
									}
									else
									{
										dte.character.variableName = varNames[newIndex];
									}
								}
								else
								{
									dte.character.variableName = EditorGUILayout.TextField("Variable", dte.character.variableName);
								}
							}
							else
							{
								// Otherwise, show Character Name.
								dte.character.constantName = EditorGUILayout.TextField("Character Name", dte.character.constantName);
							}

							// Toggle for Use Voice Over.
							dte.UseVoiceOver = EditorGUILayout.Toggle("Use Voice Over", dte.UseVoiceOver);
							// If Use Voice Over is true, show the AudioClip field.
							if (dte.UseVoiceOver)
							{
								if (dte.voiceOverClips == null || dte.voiceOverClips.Count == 0)
								{
									dte.voiceOverClips = new List<AudioClip>() { null };
								}
								dte.voiceOverClips[0] = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", dte.voiceOverClips[0], typeof(AudioClip), false);
							}
						}
					}

					// Condition UI.
					EditorGUILayout.LabelField("Condition:");
					if (node.options[i].condition == null)
					{
						node.options[i].condition = new CompoundCondition();
					}
					DrawConditionGUI(node.options[i], graphObj);

					EditorGUILayout.EndVertical();
				}
				if (GUILayout.Button("Add Option"))
				{
					node.options.Add(new DecisionOption()
					{
						dialogueElements = new List<DialogueElement>()
						{
							new DialogueTextElement(){ text = "Option" }
						}
					});
				}
			}
		}

		public override int GetWidth() => (int)((DecisionNode)target).nodeSize.x;

		private void DrawResizeHandle(DecisionNode node)
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

		private void DrawConditionGUI(DecisionOption option, YarnDialogueGraph graph)
		{
			int currentTypeIndex = GetConditionTypeIndex(option.condition);
			int selectedTypeIndex = EditorGUILayout.Popup("Condition Type", currentTypeIndex, conditionTypes);

			if (selectedTypeIndex != currentTypeIndex)
			{
				option.condition = CreateConditionInstance(conditionTypes[selectedTypeIndex]);
			}

			DrawConditionFields(option.condition);
		}

		#region Option Label Helpers

		private string GetOptionLabel(DecisionOption option)
		{
			if (option.dialogueElements != null && option.dialogueElements.Count > 0)
			{
				if (option.dialogueElements[0] is DialogueTextElement textElement)
					return textElement.text.Trim();
				else
					return option.dialogueElements[0].ToYarnString().Trim();
			}
			return "";
		}

		private void SetOptionLabel(DecisionOption option, string newText)
		{
			if (option.dialogueElements == null)
			{
				option.dialogueElements = new List<DialogueElement>();
			}
			if (option.dialogueElements.Count == 0)
			{
				option.dialogueElements.Add(new DialogueTextElement() { text = newText });
			}
			else if (option.dialogueElements[0] is DialogueTextElement textElement)
			{
				textElement.text = newText;
			}
			else
			{
				option.dialogueElements[0] = new DialogueTextElement() { text = newText };
			}
		}

		/// <summary>
		/// Ensures that the option has at least one DialogueTextElement.
		/// </summary>
		private void SetOrCreateOptionDialogueElement(DecisionOption option)
		{
			if (option.dialogueElements == null)
			{
				option.dialogueElements = new List<DialogueElement>();
			}
			if (option.dialogueElements.Count == 0 || !(option.dialogueElements[0] is DialogueTextElement))
			{
				option.dialogueElements.Insert(0, new DialogueTextElement() { text = "Option" });
			}
		}

		#endregion

		#region Condition Helpers

		private int GetConditionTypeIndex(ICondition condition)
		{
			// Unwrap a NotCondition, if present.
			if (condition is NotCondition notCond)
			{
				condition = notCond.innerCondition;
			}
			if (condition == null) return 0; // Default to Compound.
			if (condition is CompoundCondition) return 0;
			if (condition is VisitedCondition) return 1;
			if (condition is VisitedCountCondition) return 2;
			if (condition is SingleComparison) return 3;
			return 0; // Default to Compound.
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
			return 0; // Default to Visited.
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
				DecisionNode decisionNode = target as DecisionNode;
				YarnDialogueGraph graph = decisionNode.graph as YarnDialogueGraph;
				if (graph != null && graph.declaredVariables != null && graph.declaredVariables.Count > 0)
				{
					List<string> options = graph.declaredVariables.Select(v => v.variableName).ToList();
					options.Add("Other");

					// --- Left Variable ---
					int currentLeftIndex = options.IndexOf(single.leftVariable);
					bool isCustomLeft = string.IsNullOrEmpty(single.leftVariable) || currentLeftIndex < 0;
					if (customLeftStates.TryGetValue(single, out bool storedLeft))
						isCustomLeft = storedLeft;
					if (!isCustomLeft && currentLeftIndex < 0)
						currentLeftIndex = options.Count - 1;
					int newLeftIndex = EditorGUILayout.Popup("Left Variable", isCustomLeft ? options.Count - 1 : currentLeftIndex, options.ToArray());
					if (newLeftIndex == options.Count - 1)
					{
						isCustomLeft = true;
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