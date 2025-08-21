#if XNODE
#if UNITY_EDITOR
using System.Collections.Generic;

namespace Arawn.YarnGraph
{
	public interface ICondition
	{
		/// <summary>
		/// Evaluate the condition against a dictionary of variable values.
		/// </summary>
		bool Evaluate(IDictionary<string, object> variables);
	}
}
#endif
#endif