#if XNODE
#if UNITY_EDITOR
using System.Collections.Generic;

namespace Arawn.YarnGraph
{
	public static class DialogueElementUtils
	{
		/// <summary>
		/// Recursively gather all JumpElements contained within a single DialogueElement.
		/// Handles DecisionElement options and IfDialogueElement branches, etc.
		/// </summary>
		public static void GatherJumpElements(DialogueElement element, List<JumpElement> results)
		{
			if (element == null)
				return;

			// 1) If this element is a JumpElement itself, add it
			if (element is JumpElement jump)
			{
				results.Add(jump);
			}

			// 2) If it’s a container type, recurse into its children

			// -- DECISION ELEMENT --
			if (element is DecisionElement decisionElem)
			{
				// Each DecisionOption has a List<DialogueElement> "dialogueElements"
				foreach (var opt in decisionElem.options)
				{
					if (opt.dialogueElements != null)
					{
						foreach (DialogueElement childElem in opt.dialogueElements)
						{
							GatherJumpElements(childElem, results);
						}
					}
				}
			}
			// -- IF DIALOGUE ELEMENT --
			else if (element is IfDialogueElement ifElem)
			{
				// The IfTrue list
				if (ifElem.IfTrue != null)
				{
					foreach (DialogueElement childElem in ifElem.IfTrue)
					{
						GatherJumpElements(childElem, results);
					}
				}
				// The IfFalse list
				if (ifElem.IfFalse != null)
				{
					foreach (DialogueElement childElem in ifElem.IfFalse)
					{
						GatherJumpElements(childElem, results);
					}
				}
			}
			// -- ANY OTHER NESTED CONTAINER? --
			// If you define more container-like DialogueElements, handle them here
			// e.g., if (element is MyCustomContainer container) { foreach(...) GatherJumpElements(...); }
		}
	}
}
#endif
#endif