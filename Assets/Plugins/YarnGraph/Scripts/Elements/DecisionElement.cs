#if XNODE
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.YarnGraph
{
	[ElementTitle("Make a Decision")]
	[System.Serializable]
	public class DecisionElement : DialogueElement
	{
		/// <summary>
		/// Whether we strip all other options if one condition is true.
		/// </summary>
		public bool stripOtherOptionsIfTrue = false;

		/// <summary>
		/// List of options representing the choices in the decision.
		/// </summary>
		[SerializeReference]
		public List<DecisionOption> options = new List<DecisionOption>();

		/// <summary>
		/// Converts the decision to its Yarn Script representation.
		/// </summary>
		public override string ToYarnString()
		{
			string yarn = "";
			foreach (var option in options)
			{
				if (option.dialogueElements == null || option.dialogueElements.Count == 0)
					continue;

				// The first element is the option text, prefixed with "->"
				yarn += "-> " + option.dialogueElements[0].ToYarnString() + "\n";

				// Subsequent elements are indented under the option
				for (int i = 1; i < option.dialogueElements.Count; i++)
				{
					yarn += "    " + option.dialogueElements[i].ToYarnString() + "\n";
				}
			}
			return yarn;
		}
	}
}
#endif
#endif