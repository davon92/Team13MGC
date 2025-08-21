#if XNODE
#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Arawn.YarnGraph
{
	[Serializable]
	public class VisitedCountCondition : ICondition
	{
		public string nodeName;
		public ComparisonOperator op;
		public int countValue;

		public bool Evaluate(IDictionary<string, object> variables)
		{
			// Minimal implementation for editor simulation; runtime handled by Yarn Spinner.
			return false;
		}

		public string ToYarnScript()
		{
			if (string.IsNullOrEmpty(nodeName))
				throw new ArgumentException("Node name cannot be empty in VisitedCountCondition.");

			string opStr = op switch
			{
				ComparisonOperator.Equals => "==",
				ComparisonOperator.NotEquals => "!=",
				ComparisonOperator.GreaterThan => ">",
				ComparisonOperator.LessThan => "<",
				ComparisonOperator.GreaterOrEqual => ">=",
				ComparisonOperator.LessOrEqual => "<=",
				_ => throw new ArgumentException("Unsupported comparison operator in VisitedCountCondition.")
			};
			return $"visited_count(\"{nodeName}\") {opStr} {countValue}";
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