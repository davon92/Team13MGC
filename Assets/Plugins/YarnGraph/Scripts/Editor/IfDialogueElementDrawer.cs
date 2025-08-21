#if XNODE
#if UNITY_EDITOR
using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Arawn.YarnGraph.Editor
{
	[CustomPropertyDrawer(typeof(IfDialogueElement))]
	public class IfDialogueElementDrawer : PropertyDrawer
	{
		// Cached ReorderableLists for the two branches.
		private ReorderableList ifTrueList;
		private ReorderableList ifFalseList;

		// Condition type names.
		// [ADDED for NotCondition] – Added "Not" at the end so existing indices remain valid
		private readonly string[] conditionTypes = { "Compound", "Visited", "Visited Count", "Single Comparison", "Not" };

		// [ADDED for NotCondition] – Include "Not" in the sub-condition popup as well
		private readonly string[] subConditionTypes = { "Visited", "Visited Count", "Single Comparison", "Not" };

		private const float PADDING = 4f;
		private const float SPACING = 2f;
		private static readonly float LINE_HEIGHT = EditorGUIUtility.singleLineHeight;
		private const int MAX_HEADER_LENGTH = 50;

		// Computes a dynamic height for a CompoundCondition.
		private float ComputeCompoundConditionHeight(CompoundCondition compound, SerializedObject serializedObject)
		{
			YarnDialogueGraph graph = GetGraph(serializedObject);
			return GetConditionHeight(compound, graph, isSubCondition: false) + 6f; // extra padding
		}

		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			var root = new VisualElement();
			root.style.flexDirection = FlexDirection.Column;

			// Build a header summary based on the condition.
			SerializedProperty conditionProp = property.FindPropertyRelative("condition");
			string summary = GetConditionSummary(conditionProp);
			if (summary.Length > MAX_HEADER_LENGTH)
				summary = summary.Substring(0, MAX_HEADER_LENGTH) + "...";
			string headerText = string.IsNullOrEmpty(summary) ? "If / Else" : "If " + summary;

			// Foldout header
			var foldout = new Foldout { text = headerText };
			foldout.RegisterValueChangedCallback(evt => property.isExpanded = evt.newValue);
			foldout.value = property.isExpanded;
			root.Add(foldout);

			// Container for content (condition and branches).
			var content = new VisualElement();
			content.style.marginLeft = 10; // Indent
			foldout.Add(content);

			// Add condition UI.
			AddConditionUI(content, property);

			// If True Branch
			var ifTrueProp = property.FindPropertyRelative("IfTrue");
			var ifTrueList = new DialogueElementListTool(ifTrueProp) { name = "IfTrueList" };
			ifTrueList.Insert(0, new Label("If True Branch") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
			content.Add(ifTrueList);

			// If False Branch
			var ifFalseProp = property.FindPropertyRelative("IfFalse");
			var ifFalseList = new DialogueElementListTool(ifFalseProp) { name = "IfFalseList" };
			ifFalseList.Insert(0, new Label("If False Branch") { style = { unityFontStyleAndWeight = FontStyle.Bold } });
			content.Add(ifFalseList);

			// Toggle display based on foldout state
			content.style.display = property.isExpanded ? DisplayStyle.Flex : DisplayStyle.None;
			foldout.RegisterValueChangedCallback(evt =>
				content.style.display = evt.newValue ? DisplayStyle.Flex : DisplayStyle.None);

			return root;
		}

		private void AddConditionUI(VisualElement container, SerializedProperty property)
		{
			var conditionProp = property.FindPropertyRelative("condition");
			// [ADDED for NotCondition] – Now we have 5 items in conditionTypes.
			var popup = new PopupField<string>("Condition Type",
				new List<string> { "Compound", "Visited", "Visited Count", "Single Comparison", "Not" },
				GetConditionTypeIndex(conditionProp.managedReferenceValue as ICondition));
			popup.RegisterValueChangedCallback(evt =>
			{
				conditionProp.managedReferenceValue = CreateConditionInstance(evt.newValue);
				conditionProp.serializedObject.ApplyModifiedProperties();
				container.MarkDirtyRepaint();
			});
			container.Add(popup);

			var conditionField = new PropertyField(conditionProp) { style = { marginTop = 4 } };
			conditionField.Bind(property.serializedObject);
			container.Add(conditionField);
		}

		/// <summary>
		/// Returns a summary string for the condition.
		/// Uses managedReferenceValue to cast to the concrete condition type.
		/// </summary>
		private string GetConditionSummary(SerializedProperty conditionProp)
		{
			if (conditionProp == null || conditionProp.managedReferenceValue == null)
				return "";
			ICondition condition = conditionProp.managedReferenceValue as ICondition;

			// The next lines “unwrap” NotCondition for the summary.
			// We keep them as is, so summary won't show "!"
			if (condition is NotCondition notCond)
				condition = notCond.innerCondition;

			if (condition is VisitedCondition visited)
			{
				return string.IsNullOrEmpty(visited.nodeName) ? "Visited" : "Visited " + visited.nodeName;
			}
			else if (condition is VisitedCountCondition visitedCount)
			{
				return $"{visitedCount.nodeName} Visited Count {visitedCount.op} {visitedCount.countValue}";
			}
			else if (condition is SingleComparison single)
			{
				return $"{single.leftVariable} {single.op} {single.rightVariableOrValue}";
			}
			else if (condition is CompoundCondition compound)
			{
				if (compound.conditions != null && compound.conditions.Count > 0)
				{
					var nestedSummaries = compound.conditions.Select(c =>
					{
						if (c is VisitedCondition vc)
							return string.IsNullOrEmpty(vc.nodeName) ? "Visited" : "Visited " + vc.nodeName;
						else if (c is VisitedCountCondition vcc)
							return $"{vcc.nodeName} Visited Count {vcc.op} {vcc.countValue}";
						else if (c is SingleComparison sc)
							return $"{sc.leftVariable} {sc.op} {sc.rightVariableOrValue}";
						else
							return c.ToString();
					}).ToArray();

					string opStr = compound.logicOperator == LogicOperator.AND ? " && " : " || ";
					return string.Join(opStr, nestedSummaries);
				}
				return "Compound";
			}
			return "";
		}

		/// <summary>
		/// Returns how much height the given ICondition should occupy (including subconditions).
		/// </summary>
		private float GetConditionHeight(ICondition condition, YarnDialogueGraph graphObj, bool isSubCondition)
		{
			float lineHeight = LINE_HEIGHT;
			float spacing = SPACING;

			// [ADDED for NotCondition] – If it’s NotCondition, we add some lines + measure the inner
			if (condition is NotCondition notCond)
			{
				// We'll display at least one line for the "Not" popup,
				// plus however many lines the inner condition needs (as a sub-condition).
				float baseHeight = (lineHeight + spacing); // "Type" popup for Not
														   // Then measure the inner condition as a sub-condition:
				baseHeight += GetConditionHeight(notCond.innerCondition, graphObj, true) + spacing;
				return baseHeight;
			}

			if (condition is CompoundCondition compound)
			{
				// If there's more than one subcondition, we add a line for the "LogicOperator"
				float compoundHeight = 0f;
				if (compound.conditions != null && compound.conditions.Count > 1)
					compoundHeight += lineHeight + spacing; // for Logic Operator

				// Sub-conditions
				if (compound.conditions != null && compound.conditions.Count > 0)
				{
					// Each sub-condition has (GetConditionHeight + spacing)
					compoundHeight += compound.conditions.Sum(c => GetConditionHeight(c, graphObj, true) + spacing);
				}
				else
				{
					// A single line for an empty compound
					compoundHeight += lineHeight + spacing;
				}

				// One line for the "+ Add Condition" button
				compoundHeight += lineHeight + spacing;
				return compoundHeight;
			}
			else
			{
				// Non-compound
				float baseHeight = 0f;

				if (condition is SingleComparison sc)
				{
					// Always at least 4 lines: left, operator, toggle, right
					baseHeight = (lineHeight + spacing) * 4;

					// Extra "Other" lines if needed
					var declaredVars = (graphObj != null)
						? graphObj.declaredVariables.Select(v => v.variableName).ToList()
						: new List<string>();
					if (!declaredVars.Contains("Other"))
						declaredVars.Add("Other");

					bool leftIsOther = !string.IsNullOrEmpty(sc.leftVariable)
										&& !declaredVars.Contains(sc.leftVariable);
					if (sc.leftVariable == "Other")
						leftIsOther = true;

					bool rightIsOther = false;
					if (sc.isRightVariable)
					{
						bool notDeclared = !string.IsNullOrEmpty(sc.rightVariableOrValue)
										   && !declaredVars.Contains(sc.rightVariableOrValue);
						if (sc.rightVariableOrValue == "Other")
							notDeclared = true;

						rightIsOther = notDeclared;
					}

					if (leftIsOther)
						baseHeight += lineHeight + spacing;
					if (rightIsOther)
						baseHeight += lineHeight + spacing;
				}
				else if (condition is VisitedCondition)
				{
					baseHeight = lineHeight + spacing; // "Node Name"
				}
				else if (condition is VisitedCountCondition)
				{
					baseHeight = (lineHeight + spacing) * 3; // NodeName, Operator, Count
				}

				// If this is a sub-condition inside a CompoundCondition, add an extra line for the "Type" popup:
				if (isSubCondition)
					baseHeight += (lineHeight + spacing);

				return baseHeight;
			}
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			// 1) Foldout header
			float height = LINE_HEIGHT + 2f;
			if (!property.isExpanded)
				return height;

			EditorGUI.indentLevel++;

			// 2) Condition Type popup
			height += LINE_HEIGHT + 2f;

			// 3) Condition details
			SerializedProperty conditionProp = property.FindPropertyRelative("condition");
			ICondition currentCondition = conditionProp.managedReferenceValue as ICondition;
			YarnDialogueGraph graph = GetGraph(property.serializedObject);

			if (currentCondition is CompoundCondition compound)
			{
				height += ComputeCompoundConditionHeight(compound, property.serializedObject) + 4f;
			}
			else
			{
				// [ADDED for NotCondition] – If it’s NotCondition, GetConditionHeight handles it anyway
				height += GetConditionHeight(currentCondition, graph, isSubCondition: false) + 4f;
			}

			// 4) IfTrue branch
			SerializedProperty ifTrueProp = property.FindPropertyRelative("IfTrue");
			InitializeIfTrueList(property, ifTrueProp);
			height += ifTrueList.GetHeight() + 4f;

			// 5) IfFalse branch
			SerializedProperty ifFalseProp = property.FindPropertyRelative("IfFalse");
			InitializeIfFalseList(property, ifFalseProp);
			height += ifFalseList.GetHeight() + 4f;

			EditorGUI.indentLevel--;
			return height;
		}

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			// Update foldout header text based on current condition
			SerializedProperty conditionProp = property.FindPropertyRelative("condition");
			string summary = GetConditionSummary(conditionProp);
			if (summary.Length > MAX_HEADER_LENGTH)
				summary = summary.Substring(0, MAX_HEADER_LENGTH) + "...";
			string header = string.IsNullOrEmpty(summary) ? "If / Else" : "If " + summary;

			// Foldout header
			property.isExpanded = EditorGUI.Foldout(
				new Rect(position.x, position.y, position.width, LINE_HEIGHT),
				property.isExpanded, header, true);

			if (!property.isExpanded)
				return;

			EditorGUI.indentLevel++;
			float y = position.y + LINE_HEIGHT + 2f;
			float x = position.x;
			float width = position.width;

			// --- Condition Type Popup ---
			SerializedProperty conditionProp2 = property.FindPropertyRelative("condition");
			ICondition currentCondition = conditionProp2.managedReferenceValue as ICondition;
			int currentTypeIndex = GetConditionTypeIndex(currentCondition);
			// [ADDED for NotCondition] – we now have 5 conditionTypes
			int newTypeIndex = EditorGUI.Popup(new Rect(x, y, width, LINE_HEIGHT),
											   "Condition Type",
											   currentTypeIndex,
											   conditionTypes);
			if (newTypeIndex != currentTypeIndex)
			{
				conditionProp2.managedReferenceValue = CreateConditionInstance(conditionTypes[newTypeIndex]);
				currentCondition = conditionProp2.managedReferenceValue as ICondition;
			}
			y += LINE_HEIGHT + 2f;

			// --- Condition Details ---
			YarnDialogueGraph graphObj = GetGraph(property.serializedObject);
			if (currentCondition is CompoundCondition compound)
			{
				float compoundHeight = ComputeCompoundConditionHeight(compound, property.serializedObject);
				Rect compoundRect = new Rect(x, y, width, compoundHeight);
				DrawCompoundCondition(compoundRect, conditionProp2, graphObj);
				y += compoundHeight + 4f;
			}
			else
			{
				float condHeight = GetConditionHeight(currentCondition, graphObj, false);
				Rect condRect = new Rect(x, y, width, condHeight);
				DrawConditionFields(condRect, conditionProp2, graphObj);
				y += condHeight + 4f;
			}

			// --- If True Branch ---
			SerializedProperty ifTrueProp = property.FindPropertyRelative("IfTrue");
			InitializeIfTrueList(property, ifTrueProp);
			float ifTrueHeight = ifTrueList.GetHeight();
			ifTrueList.DoList(new Rect(x, y, width, ifTrueHeight));
			y += ifTrueHeight + 4f;

			// --- If False Branch ---
			SerializedProperty ifFalseProp = property.FindPropertyRelative("IfFalse");
			InitializeIfFalseList(property, ifFalseProp);
			float ifFalseHeight = ifFalseList.GetHeight();
			ifFalseList.DoList(new Rect(x, y, width, ifFalseHeight));
			y += ifFalseHeight + 4f;

			EditorGUI.indentLevel--;
		}

		#region Condition Drawing Methods

		private void DrawCompoundCondition(Rect rect, SerializedProperty conditionProp, YarnDialogueGraph graph)
		{
			float currentY = rect.y;
			float lineHeight = LINE_HEIGHT;
			float spacing = SPACING;

			SerializedProperty conditionsProp = conditionProp.FindPropertyRelative("conditions");
			SerializedProperty logicOperatorProp = conditionProp.FindPropertyRelative("logicOperator");

			if (conditionsProp != null && conditionsProp.isArray)
			{
				// If multiple sub-conditions, draw the LogicOperator
				if (conditionsProp.arraySize > 1)
				{
					Rect logicRect = new Rect(rect.x, currentY, rect.width, lineHeight);
					EditorGUI.PropertyField(logicRect, logicOperatorProp, new GUIContent("Logic Operator"));
					currentY += lineHeight + spacing;
				}

				// Draw each sub-condition
				for (int i = 0; i < conditionsProp.arraySize; i++)
				{
					SerializedProperty subConditionProp = conditionsProp.GetArrayElementAtIndex(i);
					ICondition subCondition = subConditionProp.managedReferenceValue as ICondition;

					// Type selection popup
					int currentSubTypeIndex = GetSubConditionTypeIndex(subCondition);
					Rect typeRect = new Rect(rect.x, currentY, rect.width - 30f, lineHeight);
					int newSubTypeIndex = EditorGUI.Popup(typeRect, "Type", currentSubTypeIndex, subConditionTypes);
					if (newSubTypeIndex != currentSubTypeIndex || subCondition == null)
					{
						subConditionProp.managedReferenceValue =
							CreateSubConditionInstance(subConditionTypes[newSubTypeIndex]);
						subCondition = subConditionProp.managedReferenceValue as ICondition;
					}
					currentY += lineHeight + spacing;

					// Draw the sub-condition’s fields
					float subHeight = GetConditionHeight(subCondition, graph, true) - (lineHeight + spacing);
					Rect subRect = new Rect(rect.x, currentY, rect.width - 30f, subHeight);
					DrawConditionFields(subRect, subConditionProp, graph);

					// Remove button at the far right
					Rect removeRect = new Rect(rect.x + rect.width - 25f, currentY, 20f, lineHeight);
					if (GUI.Button(removeRect, "-"))
					{
						conditionsProp.DeleteArrayElementAtIndex(i);
						i--;
						continue;
					}
					currentY += subHeight + spacing;
				}

				// "+ Add Condition" button
				Rect addRect = new Rect(rect.x, currentY, rect.width, lineHeight);
				if (GUI.Button(addRect, "+ Add Condition"))
				{
					conditionsProp.arraySize++;
					// Default new sub-condition is SingleComparison
					conditionsProp.GetArrayElementAtIndex(conditionsProp.arraySize - 1).managedReferenceValue = new SingleComparison();
				}
			}
		}

		private void DrawConditionFields(Rect rect, SerializedProperty conditionProp, YarnDialogueGraph graph)
		{
			ICondition condition = conditionProp.managedReferenceValue as ICondition;
			if (condition == null) return;

			float currentY = rect.y;
			float lineHeight = LINE_HEIGHT;
			float spacing = SPACING;

			// [ADDED for NotCondition] – If it’s a NotCondition, let’s draw a sub-condition inside
			if (condition is NotCondition notCond)
			{
				// The “Type” popup for Not was already drawn if we’re in a sub-condition context.
				// We just draw the inner condition’s fields here.

				SerializedProperty innerProp = conditionProp.FindPropertyRelative("innerCondition");
				ICondition inner = innerProp.managedReferenceValue as ICondition;

				// We'll show a subCondition popup for the inner condition:
				int currentSubTypeIndex = GetSubConditionTypeIndex(inner);
				Rect typeRect = new Rect(rect.x, currentY, rect.width - 30f, lineHeight);
				int newSubTypeIndex = EditorGUI.Popup(typeRect, "Inner Type", currentSubTypeIndex, subConditionTypes);
				if (newSubTypeIndex != currentSubTypeIndex || inner == null)
				{
					innerProp.managedReferenceValue = CreateSubConditionInstance(subConditionTypes[newSubTypeIndex]);
					innerProp.serializedObject.ApplyModifiedProperties();
					inner = innerProp.managedReferenceValue as ICondition;
				}
				currentY += lineHeight + spacing;

				// Now draw the fields for the chosen inner condition
				float subHeight = GetConditionHeight(inner, graph, true) - (lineHeight + spacing);
				Rect subRect = new Rect(rect.x, currentY, rect.width, subHeight);
				DrawConditionFields(subRect, innerProp, graph);
				return;
			}

			if (condition is VisitedCondition)
			{
				SerializedProperty nodeNameProp = conditionProp.FindPropertyRelative("nodeName");
				Rect nodeRect = new Rect(rect.x, currentY, rect.width, lineHeight);
				EditorGUI.PropertyField(nodeRect, nodeNameProp, new GUIContent("Node Name"));
			}
			else if (condition is VisitedCountCondition)
			{
				SerializedProperty nodeNameProp = conditionProp.FindPropertyRelative("nodeName");
				SerializedProperty opProp = conditionProp.FindPropertyRelative("op");
				SerializedProperty countProp = conditionProp.FindPropertyRelative("countValue");

				// NodeName
				Rect nodeRect = new Rect(rect.x, currentY, rect.width, lineHeight);
				EditorGUI.PropertyField(nodeRect, nodeNameProp, new GUIContent("Node Name"));
				currentY += lineHeight + spacing;

				// Operator
				Rect opRect = new Rect(rect.x, currentY, rect.width, lineHeight);
				EditorGUI.PropertyField(opRect, opProp, new GUIContent("Operator"));
				currentY += lineHeight + spacing;

				// Count
				Rect countRect = new Rect(rect.x, currentY, rect.width, lineHeight);
				EditorGUI.PropertyField(countRect, countProp, new GUIContent("Count"));
			}
			else if (condition is SingleComparison)
			{
				SerializedProperty leftProp = conditionProp.FindPropertyRelative("leftVariable");
				SerializedProperty opProp = conditionProp.FindPropertyRelative("op");
				SerializedProperty isRightVarProp = conditionProp.FindPropertyRelative("isRightVariable");
				SerializedProperty rightProp = conditionProp.FindPropertyRelative("rightVariableOrValue");

				// Gather declared variables
				List<string> vars = (graph != null)
					? graph.declaredVariables.Select(v => v.variableName).ToList()
					: new List<string>();
				if (!vars.Contains("Other")) vars.Add("Other");

				// --- Left variable popup ---
				int leftIdx = vars.IndexOf(leftProp.stringValue);
				if (leftIdx < 0) leftIdx = vars.IndexOf("Other");
				Rect leftRect = new Rect(rect.x, currentY, rect.width, lineHeight);
				int newLeft = EditorGUI.Popup(leftRect, "Left Variable", leftIdx, vars.ToArray());
				string leftChoice = vars[newLeft];
				if (leftChoice != "Other")
					leftProp.stringValue = leftChoice;
				else if (vars.Contains(leftProp.stringValue))
					leftProp.stringValue = "";
				currentY += lineHeight + spacing;

				// If left is "Other," show the custom text field
				if (leftChoice == "Other")
				{
					if (string.IsNullOrEmpty(leftProp.stringValue))
						leftProp.stringValue = "other-edit-me";

					Rect leftCustomRect = new Rect(rect.x, currentY, rect.width, lineHeight);
					leftProp.stringValue = EditorGUI.TextField(leftCustomRect, "Left Variable", leftProp.stringValue);
					currentY += lineHeight + spacing;
				}

				// --- Operator ---
				Rect opRect = new Rect(rect.x, currentY, rect.width, lineHeight);
				EditorGUI.PropertyField(opRect, opProp, new GUIContent("Operator"));
				currentY += lineHeight + spacing;

				// --- Toggle (is Right a variable?) ---
				Rect toggleRect = new Rect(rect.x, currentY, rect.width, lineHeight);
				EditorGUI.PropertyField(toggleRect, isRightVarProp, new GUIContent("Is Right a Variable?"));
				currentY += lineHeight + spacing;

				// --- Right side (variable or value)
				if (isRightVarProp.boolValue)
				{
					int otherIdx = vars.IndexOf("Other");
					int rightIdx = vars.IndexOf(rightProp.stringValue);
					if (rightIdx < 0) rightIdx = otherIdx;
					Rect rightPopupRect = new Rect(rect.x, currentY, rect.width, lineHeight);
					int newRight = EditorGUI.Popup(rightPopupRect, "Right Variable", rightIdx, vars.ToArray());
					string rightChoice = vars[newRight];

					if (rightChoice != "Other")
					{
						rightProp.stringValue = rightChoice;
						currentY += lineHeight + spacing;
					}
					else
					{
						if (string.IsNullOrEmpty(rightProp.stringValue))
							rightProp.stringValue = "other-edit-me";

						currentY += lineHeight + spacing;
						Rect rightCustomRect = new Rect(rect.x, currentY, rect.width, lineHeight);
						rightProp.stringValue = EditorGUI.TextField(rightCustomRect, "Right Variable", rightProp.stringValue);
						currentY += lineHeight + spacing;
					}
				}
				else
				{
					Rect rightValueRect = new Rect(rect.x, currentY, rect.width, lineHeight);
					rightProp.stringValue = EditorGUI.TextField(rightValueRect, "Right Value", rightProp.stringValue);
					currentY += lineHeight + spacing;
				}
			}
		}

		#endregion

		#region Condition Helpers

		// [ADDED for NotCondition] – We handle NotCondition before unwrapping it.
		private int GetConditionTypeIndex(ICondition condition)
		{
			if (condition is NotCondition)
				return 4;

			// Keep the rest "as is":
			if (condition is NotCondition notCond)
				condition = notCond.innerCondition;

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
				// [ADDED for NotCondition]
				case "Not": return new NotCondition(new SingleComparison());
				default: return new CompoundCondition();
			}
		}

		// [ADDED for NotCondition] – Similar approach for sub-conditions
		private int GetSubConditionTypeIndex(ICondition condition)
		{
			if (condition is NotCondition)
				return 3;

			if (condition is NotCondition notCond)
				condition = notCond.innerCondition;

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
				// [ADDED for NotCondition]
				case "Not": return new NotCondition(new SingleComparison());
				default: return new VisitedCondition();
			}
		}

		private YarnDialogueGraph GetGraph(SerializedObject serializedObject)
		{
			if (serializedObject == null || serializedObject.targetObject == null)
			{
				Debug.LogError("SerializedObject or targetObject is null in GetGraph!");
				return null;
			}
			if (serializedObject.targetObject is DialogueNode node)
				return node.graph as YarnDialogueGraph;
			else
			{
				Debug.LogError("Target object is not a DialogueNode!");
				return null;
			}
		}

		#endregion

		#region Reorderable List Helpers

		private void InitializeIfTrueList(SerializedProperty parent, SerializedProperty ifTrueProp)
		{
			if (ifTrueList == null || ifTrueList.serializedProperty != ifTrueProp)
			{
				ifTrueList = new ReorderableList(parent.serializedObject, ifTrueProp, true, true, true, true)
				{
					drawHeaderCallback = (Rect rect) =>
					{
						EditorGUI.LabelField(rect, "If True Branch");
					},
					onAddDropdownCallback = (Rect buttonRect, ReorderableList list) =>
					{
						ShowAddDialogueElementMenu(list, parent.serializedObject, ifTrueProp);
					},
					drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
					{
						ReorderableList.defaultBehaviours.DrawElementBackground(rect, index, isActive, isFocused, false);
						rect.y += 2;

						float buttonWidth = 24f;
						float orderFieldWidth = 30f;
						float spacing = SPACING;

						// Up button
						Rect upButtonRect = new Rect(rect.x, rect.y, buttonWidth, LINE_HEIGHT);
						if (GUI.Button(upButtonRect, "▲") && index > 0)
						{
							ifTrueProp.MoveArrayElement(index, index - 1);
							parent.serializedObject.ApplyModifiedProperties();
						}
						// Order field
						Rect orderFieldRect = new Rect(rect.x + buttonWidth + spacing, rect.y, orderFieldWidth, LINE_HEIGHT);
						int newOrder = EditorGUI.DelayedIntField(orderFieldRect, index);
						if (newOrder != index)
						{
							int clampedIndex = Mathf.Clamp(newOrder, 0, ifTrueProp.arraySize - 1);
							if (clampedIndex != index)
							{
								ifTrueProp.MoveArrayElement(index, clampedIndex);
								parent.serializedObject.ApplyModifiedProperties();
							}
						}
						// Down button
						Rect downButtonRect = new Rect(rect.x + buttonWidth + spacing + orderFieldWidth + spacing, rect.y, buttonWidth, LINE_HEIGHT);
						if (GUI.Button(downButtonRect, "▼") && index < ifTrueProp.arraySize - 1)
						{
							ifTrueProp.MoveArrayElement(index, index + 1);
							parent.serializedObject.ApplyModifiedProperties();
						}

						float leftOffset = buttonWidth * 2 + orderFieldWidth + spacing * 3;
						float rightButtonsWidth = buttonWidth * 2 + spacing;
						Rect fieldRect = new Rect(rect.x + leftOffset, rect.y, rect.width - leftOffset - rightButtonsWidth, rect.height);
						EditorGUI.PropertyField(fieldRect, ifTrueProp.GetArrayElementAtIndex(index), GUIContent.none, true);

						// Right-side buttons
						Rect dupRect = new Rect(rect.x + rect.width - rightButtonsWidth, rect.y, buttonWidth, LINE_HEIGHT);
						if (GUI.Button(dupRect, "+"))
						{
							DuplicateElement(ifTrueProp, index);
						}
						Rect removeRect = new Rect(rect.x + rect.width - buttonWidth, rect.y, buttonWidth, LINE_HEIGHT);
						if (GUI.Button(removeRect, "-"))
						{
							ifTrueProp.DeleteArrayElementAtIndex(index);
							parent.serializedObject.ApplyModifiedProperties();
							return;
						}
					},
					elementHeightCallback = (int index) =>
					{
						SerializedProperty elementProp = ifTrueProp.GetArrayElementAtIndex(index);
						return EditorGUI.GetPropertyHeight(elementProp, GUIContent.none, true) + 4f;
					}
				};
			}
		}

		private void InitializeIfFalseList(SerializedProperty parent, SerializedProperty ifFalseProp)
		{
			if (ifFalseList == null || ifFalseList.serializedProperty != ifFalseProp)
			{
				ifFalseList = new ReorderableList(parent.serializedObject, ifFalseProp, true, true, true, true)
				{
					drawHeaderCallback = (Rect rect) =>
					{
						EditorGUI.LabelField(rect, "If False Branch");
					},
					onAddDropdownCallback = (Rect buttonRect, ReorderableList list) =>
					{
						ShowAddDialogueElementMenu(list, parent.serializedObject, ifFalseProp);
					},
					drawElementCallback = (Rect rect, int index, bool isActive, bool isFocused) =>
					{
						ReorderableList.defaultBehaviours.DrawElementBackground(rect, index, isActive, isFocused, false);
						rect.y += 2;

						float buttonWidth = 24f;
						float orderFieldWidth = 30f;
						float spacing = SPACING;

						// Up button
						Rect upButtonRect = new Rect(rect.x, rect.y, buttonWidth, LINE_HEIGHT);
						if (GUI.Button(upButtonRect, "▲") && index > 0)
						{
							ifFalseProp.MoveArrayElement(index, index - 1);
							parent.serializedObject.ApplyModifiedProperties();
						}
						// Order field
						Rect orderFieldRect = new Rect(rect.x + buttonWidth + spacing, rect.y, orderFieldWidth, LINE_HEIGHT);
						int newOrder = EditorGUI.DelayedIntField(orderFieldRect, index);
						if (newOrder != index)
						{
							int clampedIndex = Mathf.Clamp(newOrder, 0, ifFalseProp.arraySize - 1);
							if (clampedIndex != index)
							{
								ifFalseProp.MoveArrayElement(index, clampedIndex);
								parent.serializedObject.ApplyModifiedProperties();
							}
						}
						// Down button
						Rect downButtonRect = new Rect(rect.x + buttonWidth + spacing + orderFieldWidth + spacing, rect.y, buttonWidth, LINE_HEIGHT);
						if (GUI.Button(downButtonRect, "▼") && index < ifFalseProp.arraySize - 1)
						{
							ifFalseProp.MoveArrayElement(index, index + 1);
							parent.serializedObject.ApplyModifiedProperties();
						}

						float leftOffset = buttonWidth * 2 + orderFieldWidth + spacing * 3;
						float rightButtonsWidth = buttonWidth * 2 + spacing;
						Rect fieldRect = new Rect(rect.x + leftOffset, rect.y, rect.width - leftOffset - rightButtonsWidth, rect.height);
						EditorGUI.PropertyField(fieldRect, ifFalseProp.GetArrayElementAtIndex(index), GUIContent.none, true);

						// Right-side buttons
						Rect dupRect = new Rect(rect.x + rect.width - rightButtonsWidth, rect.y, buttonWidth, LINE_HEIGHT);
						if (GUI.Button(dupRect, "+"))
						{
							DuplicateElement(ifFalseProp, index);
						}

						Rect removeRect = new Rect(rect.x + rect.width - buttonWidth, rect.y, buttonWidth, LINE_HEIGHT);
						if (GUI.Button(removeRect, "-"))
						{
							ifFalseProp.DeleteArrayElementAtIndex(index);
							parent.serializedObject.ApplyModifiedProperties();
							return;
						}
					},
					elementHeightCallback = (int index) =>
					{
						SerializedProperty elementProp = ifFalseProp.GetArrayElementAtIndex(index);
						return EditorGUI.GetPropertyHeight(elementProp, GUIContent.none, true) + 4f;
					}
				};
			}
		}

		private void DuplicateElement(SerializedProperty listProp, int index)
		{
			SerializedProperty elementProp = listProp.GetArrayElementAtIndex(index);
			if (elementProp == null || elementProp.managedReferenceValue == null)
				return;
			string json = JsonUtility.ToJson(elementProp.managedReferenceValue);
			Type type = elementProp.managedReferenceValue.GetType();
			object clone = JsonUtility.FromJson(json, type);
			listProp.arraySize++;
			SerializedProperty newElement = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
			newElement.managedReferenceValue = clone;
			listProp.serializedObject.ApplyModifiedProperties();
		}

		private void ShowAddDialogueElementMenu(ReorderableList list, SerializedObject serializedObject, SerializedProperty listProp)
		{
			GenericMenu menu = new GenericMenu();
			Type baseType = typeof(DialogueElement);
			IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(assembly =>
				{
					try { return assembly.GetTypes(); }
					catch { return new Type[0]; }
				})
				.Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract)
				.OrderBy(t =>
				{
					var attr = t.GetCustomAttributes(typeof(ElementTitleAttribute), false)
								.FirstOrDefault() as ElementTitleAttribute;
					return attr != null ? attr.Title : t.Name;
				});
			foreach (Type type in types)
			{
				var attr = type.GetCustomAttributes(typeof(ElementTitleAttribute), false)
							   .FirstOrDefault() as ElementTitleAttribute;
				string displayName = attr != null ? attr.Title : type.Name;
				menu.AddItem(new GUIContent(displayName), false, () =>
				{
					listProp.arraySize++;
					SerializedProperty newElement = listProp.GetArrayElementAtIndex(listProp.arraySize - 1);
					newElement.managedReferenceValue = Activator.CreateInstance(type);
					serializedObject.ApplyModifiedProperties();
				});
			}
			menu.ShowAsContext();
		}

		#endregion
	}
}
#endif
#endif
