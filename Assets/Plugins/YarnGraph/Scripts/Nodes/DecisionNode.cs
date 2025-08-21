#if XNODE
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using XNode;

namespace Arawn.YarnGraph
{
	[NodeWidth(900)]
	public class DecisionNode : YarnNode
	{
		[Output(dynamicPortList = true)]
		public List<DecisionOption> options = new List<DecisionOption>();

		public bool stripOtherOptionsIfTrue = false;

		[HideInInspector]
		public bool isMerged = false;

		public override object GetValue(NodePort port)
		{
			return null;
		}

		public new void UpdatePorts()
		{
			if (options == null)
				options = new List<DecisionOption>();

			for (int i = Ports.Count() - 1; i >= 0; i--)
			{
				NodePort port = Ports.ElementAt(i);
				if (port.IsDynamic && port.IsOutput && port.fieldName.StartsWith("options "))
				{
					string[] tokens = port.fieldName.Split(' ');
					if (tokens.Length > 1 && int.TryParse(tokens[1], out int index))
					{
						if (index >= options.Count)
							RemoveDynamicPort(port);
					}
				}
			}
			for (int i = 0; i < options.Count; i++)
			{
				string portName = $"options {i}";
				if (GetPort(portName) == null)
				{
					AddDynamicOutput(typeof(YarnNode), fieldName: portName);
				}
			}
		}

		protected void OnValidate()
		{
			UpdatePorts();
			if (options != null)
			{
				for (int i = 0; i < options.Count; i++)
				{
					if (options[i].condition == null)
					{
						options[i].condition = new CompoundCondition(); // Default to CompoundCondition
					}
					// No need to check for shared conditions since we'll allow any ICondition type
				}
			}
		}
	}
}
#endif
#endif