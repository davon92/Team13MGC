#if XNODE
#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Arawn.YarnGraph
{
	[Serializable]
	public class VisitedCondition : ICondition
	{
		public string nodeName;

		public bool Evaluate(IDictionary<string, object> variables)
		{
			// Minimal implementation for editor simulation; runtime handled by Yarn Spinner.
			return false;
		}

		public string ToYarnScript()
		{
			if (string.IsNullOrEmpty(nodeName))
				throw new ArgumentException("Node name cannot be empty in VisitedCondition.");
			return $"visited(\"{nodeName}\")";
		}

		// Override ToString() to export as Yarn script.
		public override string ToString()
		{
			return ToYarnScript();
		}
	}
}
#endif
#endif