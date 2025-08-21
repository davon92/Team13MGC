#if XNODE
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Arawn.YarnGraph
{
	[CustomPropertyDrawer(typeof(CompoundCondition))]
	public class CompoundConditionDrawer : PropertyDrawer
	{
		// Constants for layout
		private const float Spacing = 2f;
		private const float ButtonWidth = 20f;
		private const float ButtonHeight = 20f;

		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);

			SerializedProperty logicOperatorProp = property.FindPropertyRelative("logicOperator");
			SerializedProperty conditionsProp = property.FindPropertyRelative("conditions");

			float lineHeight = EditorGUIUtility.singleLineHeight;
			float y = position.y;

			// Only draw the logic operator if there are 2 or more conditions.
			if (conditionsProp != null && conditionsProp.isArray && conditionsProp.arraySize > 1)
			{
				Rect logicRect = new Rect(position.x, y, position.width, lineHeight);
				EditorGUI.PropertyField(logicRect, logicOperatorProp);
				y += lineHeight + Spacing;
			}

			// Draw each nested condition
			if (conditionsProp != null && conditionsProp.isArray)
			{
				for (int i = 0; i < conditionsProp.arraySize; i++)
				{
					SerializedProperty condProp = conditionsProp.GetArrayElementAtIndex(i);
					float condHeight = EditorGUI.GetPropertyHeight(condProp, true);
					Rect condRect = new Rect(position.x, y, position.width - ButtonWidth - Spacing, condHeight);
					EditorGUI.PropertyField(condRect, condProp, new GUIContent("Condition " + (i + 1)), true);

					// Draw a small remove ("-") button at the right side
					Rect removeRect = new Rect(position.x + position.width - ButtonWidth, y, ButtonWidth, lineHeight);
					if (GUI.Button(removeRect, "-"))
					{
						conditionsProp.DeleteArrayElementAtIndex(i);
						break;
					}
					y += condHeight + Spacing;
				}
			}

			// Draw "Add Condition" button (without Compound option)
			Rect addRect = new Rect(position.x, y, position.width, lineHeight);
			if (GUI.Button(addRect, "Add Condition"))
			{
				GenericMenu menu = new GenericMenu();
				menu.AddItem(new GUIContent("Single Comparison"), false, () =>
				{
					conditionsProp.arraySize++;
					SerializedProperty newCond = conditionsProp.GetArrayElementAtIndex(conditionsProp.arraySize - 1);
					newCond.managedReferenceValue = new SingleComparison();
					property.serializedObject.ApplyModifiedProperties();
				});
				menu.AddItem(new GUIContent("Visited"), false, () =>
				{
					conditionsProp.arraySize++;
					SerializedProperty newCond = conditionsProp.GetArrayElementAtIndex(conditionsProp.arraySize - 1);
					newCond.managedReferenceValue = new VisitedCondition();
					property.serializedObject.ApplyModifiedProperties();
				});
				menu.AddItem(new GUIContent("Visited Count"), false, () =>
				{
					conditionsProp.arraySize++;
					SerializedProperty newCond = conditionsProp.GetArrayElementAtIndex(conditionsProp.arraySize - 1);
					newCond.managedReferenceValue = new VisitedCountCondition();
					property.serializedObject.ApplyModifiedProperties();
				});
				menu.DropDown(addRect);
			}

			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			float height = 0f;
			float lineHeight = EditorGUIUtility.singleLineHeight;
			float spacing = Spacing;

			// Height for logic operator (only if 2+ conditions)
			SerializedProperty conditionsProp = property.FindPropertyRelative("conditions");
			if (conditionsProp != null && conditionsProp.isArray && conditionsProp.arraySize > 1)
			{
				height += lineHeight + spacing;
			}

			if (conditionsProp != null && conditionsProp.isArray)
			{
				for (int i = 0; i < conditionsProp.arraySize; i++)
				{
					SerializedProperty condProp = conditionsProp.GetArrayElementAtIndex(i);
					height += EditorGUI.GetPropertyHeight(condProp, true) + spacing;
				}
			}
			// Add height for the Add Condition button.
			height += lineHeight + spacing;
			return height;
		}
	}
}
#endif
#endif