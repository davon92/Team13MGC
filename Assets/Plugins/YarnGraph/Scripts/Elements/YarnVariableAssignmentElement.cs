#if XNODE
#if UNITY_EDITOR
using System;
using System.Globalization;

namespace Arawn.YarnGraph
{
	[ElementTitle("Assign Variable")]
	[Serializable]
	public class YarnVariableAssignmentElement : DialogueElement
	{
		public string variableName;
		public string value; // Used for static assignments
		public bool isDeclaration = false;
		public AssignmentType assignmentType = AssignmentType.SetStatic; // New: operation type
		public string nodeName; // New: for visited/visited_count operations

		public enum AssignmentType
		{
			SetStatic,      // Assign a static value (e.g., $var = 5)
			SetVisited,     // Assign result of visited() (e.g., $var = visited("NodeName"))
			SetVisitedCount // Assign result of visited_count() (e.g., $var = visited_count("NodeName"))
		}

		public override string ToYarnString()
		{
			string yarnCommand;
			switch (assignmentType)
			{
				case AssignmentType.SetStatic:
					object typedValue = AutoParseValue(value);
					string yarnValue = ConvertToYarnLiteral(typedValue);
					yarnCommand = isDeclaration ? $"<<declare ${variableName} = {yarnValue}>>" : $"<<set ${variableName} to {yarnValue}>>";
					break;

				case AssignmentType.SetVisited:
					if (string.IsNullOrEmpty(nodeName))
						throw new ArgumentException("Node name cannot be empty for visited assignment.");
					yarnCommand = isDeclaration
						? $"<<declare ${variableName} = visited(\"{nodeName}\")>>"
						: $"<<set ${variableName} to visited(\"{nodeName}\")>>";
					break;

				case AssignmentType.SetVisitedCount:
					if (string.IsNullOrEmpty(nodeName))
						throw new ArgumentException("Node name cannot be empty for visited_count assignment.");
					yarnCommand = isDeclaration
						? $"<<declare ${variableName} = visited_count(\"{nodeName}\")>>"
						: $"<<set ${variableName} to visited_count(\"{nodeName}\")>>";
					break;

				default:
					throw new ArgumentException("Unknown assignment type.");
			}
			return yarnCommand;
		}

		private object AutoParseValue(string userInput)
		{
			if (string.IsNullOrWhiteSpace(userInput))
				return "";

			string trimmed = userInput.Trim();

			if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
			{
				string inside = trimmed.Substring(1, trimmed.Length - 2);
				return inside;
			}

			if (trimmed.Contains(" "))
			{
				return trimmed;
			}

			if (trimmed.Equals("true", StringComparison.OrdinalIgnoreCase))
				return true;
			if (trimmed.Equals("false", StringComparison.OrdinalIgnoreCase))
				return false;

			if (float.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out float fValue))
			{
				return fValue;
			}

			return trimmed;
		}

		private string ConvertToYarnLiteral(object typedValue)
		{
			switch (typedValue)
			{
				case bool b:
					return b.ToString().ToLowerInvariant();
				case float fl:
					return fl.ToString(CultureInfo.InvariantCulture);
				default:
					string strVal = typedValue?.ToString() ?? "";
					strVal = strVal.Replace("\"", "\\\"");
					return $"\"{strVal}\"";
			}
		}
	}
}
#endif
#endif