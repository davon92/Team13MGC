#if XNODE
#if UNITY_EDITOR
using System;

namespace Arawn.YarnGraph
{
	[Serializable]
	public class JumpElement : DialogueElement
	{
		public enum JumpMode { Manual = 0, UsePort = 1 }

		// How to interpret the JumpElement?
		public JumpMode jumpMode = JumpMode.Manual;

		// Manually typed node name (only used in Manual mode).
		public string NodeName;

		// A unique name for the dynamic port (only used in UsePort mode).
		public string portName;

		public override string ToYarnString()
		{
			// 1) Strip any spaces from NodeName by calling YarnExporter’s CleanNodeTitle
			string cleanedName = YarnExporter.CleanNodeTitle(NodeName ?? "");

			if (jumpMode == JumpMode.Manual)
			{
				// Manual mode => we jump to whatever cleanedName is
				return $"<<jump {cleanedName}>>";
			}
			else
			{
				// UsePort mode => also remove spaces from NodeName
				// (If connected, NodeName might have been set from the connected port’s node title.)
				return $"<<jump {cleanedName}>>";
			}
		}
	}
}
#endif
#endif