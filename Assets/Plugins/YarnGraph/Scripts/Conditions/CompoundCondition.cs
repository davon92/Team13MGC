#if XNODE
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Arawn.YarnGraph
{
	[Serializable]
	public class CompoundCondition : ICondition
	{
		public LogicOperator logicOperator = LogicOperator.AND;

		// Now holds any ICondition (either a SingleComparison or nested CompoundCondition)
		[SerializeReference]
		public List<ICondition> conditions = new List<ICondition>();

		public bool Evaluate(IDictionary<string, object> variables)
		{
			if (conditions == null || conditions.Count == 0)
				return true; // or false, based on your design for an empty condition.

			if (logicOperator == LogicOperator.AND)
			{
				return conditions.All(c => c.Evaluate(variables));
			}
			else // LogicOperator.OR
			{
				return conditions.Any(c => c.Evaluate(variables));
			}
		}
	}
}
#endif
#endif