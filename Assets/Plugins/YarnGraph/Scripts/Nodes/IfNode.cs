#if XNODE
#if UNITY_EDITOR
using UnityEngine;
using XNode;

namespace Arawn.YarnGraph
{
	[NodeWidth(800)]
	public class IfNode : YarnNode
	{
		[SerializeReference]
		public ICondition condition = new CompoundCondition();

		[Output] public YarnNode trueOutput;
		[Output] public YarnNode falseOutput;

		public override object GetValue(NodePort port)
		{
			return null;
		}
	}
}
#endif
#endif