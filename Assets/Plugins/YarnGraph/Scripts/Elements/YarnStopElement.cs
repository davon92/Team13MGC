#if XNODE
#if UNITY_EDITOR
using System;

namespace Arawn.YarnGraph
{
	[ElementTitle("Stop Dialogue")]
	[Serializable]
	public class YarnStopElement : DialogueElement
	{
		public override string ToYarnString()
		{
			// Yarn script command: <<stop>>
			return "<<stop>>";
		}
	}
}
#endif
#endif