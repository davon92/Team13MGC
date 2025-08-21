#if XNODE
#if UNITY_EDITOR
using System;

namespace Arawn.YarnGraph
{
	[Serializable]
	public class YarnVariable
	{
		public string variableName;
		public string initialValue; // For set operations
		public VariableOperation operation = VariableOperation.Set; // Default to set
		public string sourceNodeName; // For increment operations using visited_count()

		public enum VariableOperation
		{
			Set,       // Directly set the value (e.g., $var = 5)
			Increment  // Increment based on visited_count (e.g., $var = visited_count("NodeName"))
		}
	}
}
#endif
#endif