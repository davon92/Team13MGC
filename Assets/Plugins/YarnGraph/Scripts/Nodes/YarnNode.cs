#if XNODE
#if UNITY_EDITOR
using UnityEngine;

namespace Arawn.YarnGraph
{
	[NodeTint("#444444")]
	public abstract class YarnNode : XNode.Node
	{
		[Input] public YarnNode input;
		//[SerializeReference] public List<DialogueElement> lines; // Allow polymorphic serialization
		public string nodeTitle;
		public Vector2 nodeSize = new Vector2(300, 150);
		// This method is called in the Unity Editor whenever the object is loaded or a value changes in the inspector.
		private void OnValidate()
		{
			// If nodeTitle has been set and differs from the asset's current name, update it.
			if (!string.IsNullOrEmpty(nodeTitle) && name != nodeTitle)
			{
				name = nodeTitle;
			}
		}
	}
}
#endif
#endif