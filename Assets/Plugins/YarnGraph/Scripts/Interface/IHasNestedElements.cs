#if XNODE
#if UNITY_EDITOR
using System.Collections.Generic;

namespace Arawn.YarnGraph
{
	public interface IHasNestedElements
	{
		List<DialogueElement> NestedElements { get; }
	}
}
#endif
#endif