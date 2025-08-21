#if XNODE
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.YarnGraph
{
	[Serializable]
	public class DecisionOption : IHasNestedElements
	{
		/// <summary>
		/// List of dialogue elements that define the option’s content.
		/// </summary>
		[SerializeReference]
		public List<DialogueElement> dialogueElements = new List<DialogueElement>();

		public YarnNode target;

		[SerializeReference]
		public ICondition condition;

		// Field to temporarily store the jump target title.
		public string jumpTargetTitle;

		public DecisionOption()
		{
			condition = new CompoundCondition();
		}

		List<DialogueElement> IHasNestedElements.NestedElements => dialogueElements;
	}
}
#endif
#endif