#if XNODE
#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Arawn.YarnGraph
{
	[CustomPropertyDrawer(typeof(YarnStopElement))]
	public class YarnStopElementDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			EditorGUI.LabelField(position, "Stop Dialogue");
		}

		public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
		{
			return EditorGUIUtility.singleLineHeight;
		}
	}
}

#endif
#endif