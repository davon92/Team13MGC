#if XNODE
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.YarnGraph
{
	[Serializable]
	public class NotCondition : ICondition
	{
		[SerializeReference]
		public ICondition innerCondition;

		public NotCondition(ICondition condition)
		{
			innerCondition = condition;
		}

		public bool Evaluate(IDictionary<string, object> variables)
		{
			return !innerCondition.Evaluate(variables);
		}

		public string ToYarnScript()
		{
			// Instead of calling innerCondition.ToYarnScript(), call innerCondition.ToString()
			// because ICondition doesn't define ToYarnScript().
			return "!" + innerCondition.ToString();
		}

		public override string ToString()
		{
			return ToYarnScript();
		}
	}
}
#endif
#endif