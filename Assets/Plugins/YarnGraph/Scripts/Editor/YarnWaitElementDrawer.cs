#if XNODE
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Arawn.YarnGraph
{
	[CustomPropertyDrawer(typeof(YarnWaitElement))]
	public class YarnWaitElementDrawer : PropertyDrawer
	{
		// Minimal example
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.BeginProperty(position, label, property);
			// Show a single line "Wait X seconds"
			// or do a foldout for more advanced layout
			EditorGUI.PropertyField(position, property.FindPropertyRelative("duration"), new GUIContent("Wait (in Sec)"));
			EditorGUI.EndProperty();
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return EditorGUIUtility.singleLineHeight;
		}
	}
}
#endif
#endif