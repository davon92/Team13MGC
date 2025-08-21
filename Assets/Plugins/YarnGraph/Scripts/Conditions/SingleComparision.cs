#if XNODE
#if UNITY_EDITOR
using System;
using System.Collections.Generic;

namespace Arawn.YarnGraph
{
	/// <summary>
	/// A single "VarA op VarB" or "VarA op literal" comparison, similar to your old VariableComparison.
	/// </summary>
	[Serializable]
	public class SingleComparison : ICondition
	{
		public string leftVariable;
		public ComparisonOperator op;
		public string rightVariableOrValue;
		public bool isRightVariable;

		public bool Evaluate(IDictionary<string, object> variables)
		{
			object leftValue = variables != null && variables.ContainsKey(leftVariable) ? variables[leftVariable] : null;
			object rightValue = isRightVariable && variables != null && variables.ContainsKey(rightVariableOrValue) ? variables[rightVariableOrValue] : rightVariableOrValue;

			// Handle string comparisons
			if (leftValue is string && rightValue is string)
			{
				string leftStr = (string)leftValue;
				string rightStr = (string)rightValue;
				switch (op)
				{
					case ComparisonOperator.Equals:
						return leftStr == rightStr;
					case ComparisonOperator.NotEquals:
						return leftStr != rightStr;
					default:
						return false; // Other operators like >, < don't make sense for strings
				}
			}
			// Handle numeric comparisons
			else if (leftValue != null && rightValue != null &&
					 double.TryParse(leftValue.ToString(), out double leftNum) &&
					 double.TryParse(rightValue.ToString(), out double rightNum))
			{
				switch (op)
				{
					case ComparisonOperator.Equals:
						return leftNum == rightNum;
					case ComparisonOperator.NotEquals:
						return leftNum != rightNum;
					case ComparisonOperator.GreaterThan:
						return leftNum > rightNum;
					case ComparisonOperator.LessThan:
						return leftNum < rightNum;
					case ComparisonOperator.GreaterOrEqual:
						return leftNum >= rightNum;
					case ComparisonOperator.LessOrEqual:
						return leftNum <= rightNum;
				}
			}
			// Handle cases where leftValue is a number and rightValue is a string that can be parsed as a number
			else if (leftValue != null && rightValue is string)
			{
				if (double.TryParse(leftValue.ToString(), out double leftNum2) &&
					double.TryParse((string)rightValue, out double rightNum2))
				{
					switch (op)
					{
						case ComparisonOperator.Equals:
							return leftNum2 == rightNum2;
						case ComparisonOperator.NotEquals:
							return leftNum2 != rightNum2;
						case ComparisonOperator.GreaterThan:
							return leftNum2 > rightNum2;
						case ComparisonOperator.LessThan:
							return leftNum2 < rightNum2;
						case ComparisonOperator.GreaterOrEqual:
							return leftNum2 >= rightNum2;
						case ComparisonOperator.LessOrEqual:
							return leftNum2 <= rightNum2;
					}
				}
			}
			// Handle cases where leftValue is a string and rightValue is a number (less common, but possible)
			else if (leftValue is string && rightValue != null)
			{
				if (double.TryParse((string)leftValue, out double leftNum3) &&
					double.TryParse(rightValue.ToString(), out double rightNum3))
				{
					switch (op)
					{
						case ComparisonOperator.Equals:
							return leftNum3 == rightNum3;
						case ComparisonOperator.NotEquals:
							return leftNum3 != rightNum3;
						case ComparisonOperator.GreaterThan:
							return leftNum3 > rightNum3;
						case ComparisonOperator.LessThan:
							return leftNum3 < rightNum3;
						case ComparisonOperator.GreaterOrEqual:
							return leftNum3 >= rightNum3;
						case ComparisonOperator.LessOrEqual:
							return leftNum3 <= rightNum3;
					}
				}
			}
			// If none of the above cases match, return false
			return false;
		}
	}
}
#endif
#endif