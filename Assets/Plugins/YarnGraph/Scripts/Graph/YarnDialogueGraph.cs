#if XNODE
#if UNITY_EDITOR
using UnityEngine;
using XNode;
using System.Collections.Generic;

namespace Arawn.YarnGraph
{
	[CreateAssetMenu(fileName = "NewYarnDialogueGraph", menuName = "Yarn Graph/Yarn Dialogue Graph")]
	public class YarnDialogueGraph : NodeGraph
	{
		public List<YarnVariable> declaredVariables = new List<YarnVariable>();
		public List<string> YarnCommands = new List<string>();
		public List<string> YarnInlineTags = new List<string>();

		public override Node AddNode(System.Type type)
		{
			Node node = base.AddNode(type);
			if (node is YarnNode yarnNode && string.IsNullOrEmpty(yarnNode.nodeTitle))
			{
				yarnNode.nodeTitle = $"{type.Name}_{System.Guid.NewGuid().ToString("N")}";
				node.name = yarnNode.nodeTitle;
			}
			return node;
		}
	}
}
#endif
#endif