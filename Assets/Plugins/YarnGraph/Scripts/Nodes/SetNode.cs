#if XNODE
#if UNITY_EDITOR
using XNode;
using System.Collections.Generic;

namespace Arawn.YarnGraph
{
	[NodeWidth(300)]
	public class VariableSetNode : YarnNode
	{
		// List of variables to set. Each element contains a variable name and a new value.
		public List<YarnVariable> variables = new List<YarnVariable>();

		[Output] public YarnNode output; // Single output to chain to the next node.

		public override object GetValue(NodePort port)
		{
			return null;
		}
	}
}
#endif
#endif