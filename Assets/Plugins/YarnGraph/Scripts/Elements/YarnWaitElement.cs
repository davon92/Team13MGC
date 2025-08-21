#if XNODE
#if UNITY_EDITOR
using System;

namespace Arawn.YarnGraph
{
	[ElementTitle("Wait (in Sec)")]
	[Serializable]
	public class YarnWaitElement : DialogueElement
	{
		public float duration = 1.0f;

		public override string ToYarnString()
		{
			// Yarn script command: <<wait X>>
			return $"<<wait {duration}>>";
		}
	}
}
#endif
#endif