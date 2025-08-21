#if XNODE
#if UNITY_EDITOR
using System;

namespace Arawn.YarnGraph
{
	[ElementTitle("Run Command")]
	[Serializable]
	public class YarnCommandElement : DialogueElement
	{
		public string commandName;

		public override string ToYarnString()
		{
			// Trim whitespace.
			string cmd = commandName.Trim();
			// If the command is wrapped in quotes, remove them.
			if (cmd.StartsWith("\"") && cmd.EndsWith("\"") && cmd.Length > 1)
			{
				cmd = cmd.Substring(1, cmd.Length - 2);
			}
			// Wrap the cleaned command string in Yarn command markers.
			return $"<<{cmd}>>";
		}
	}
}
#endif
#endif