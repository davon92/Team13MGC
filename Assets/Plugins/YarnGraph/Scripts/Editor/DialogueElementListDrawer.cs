#if XNODE
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEditor;
using UnityEngine.UIElements;

namespace Arawn.YarnGraph
{
	[CustomPropertyDrawer(typeof(List<DialogueElement>), true)]
	public class DialogueElementListDrawer : PropertyDrawer
	{
		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			DialogueElementListTool listTool = new DialogueElementListTool(property);
			return listTool;
		}
	}
}

#endif
#endif