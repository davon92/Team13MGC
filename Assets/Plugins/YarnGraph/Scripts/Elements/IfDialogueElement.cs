#if XNODE
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.YarnGraph
{
	[ElementTitle("If... Else...")]
	[Serializable]
	public class IfDialogueElement : DialogueElement
	{
		// The condition that determines which branch is executed.
		// Using SerializeReference so that polymorphism is preserved.
		[SerializeReference]
		public ICondition condition = new CompoundCondition();

		// The list of dialogue elements for the "if true" branch.
		[SerializeReference]
		public List<DialogueElement> IfTrue = new List<DialogueElement>();

		// The list of dialogue elements for the "else" branch.
		[SerializeReference]
		public List<DialogueElement> IfFalse = new List<DialogueElement>();

		/// <summary>
		/// Converts the IfDialogueElement into a Yarn Script fragment.
		/// </summary>
		/// <returns>A Yarn Script string representing the if/else block.</returns>
		public override string ToYarnString()
		{
			// For now, we use condition.ToString() to represent the condition.
			// A more complete implementation might require a dedicated export method.
			string conditionStr = condition != null ? condition.ToString() : "";
			string yarn = $"<<if {conditionStr}>>\n";

			// Add the "if true" branch lines.
			foreach (var element in IfTrue)
			{
				// Tab-indented for readability.
				yarn += "\t" + element.ToYarnString() + "\n";
			}

			// If there is an "else" branch, add it.
			if (IfFalse != null && IfFalse.Count > 0)
			{
				yarn += "<<else>>\n";
				foreach (var element in IfFalse)
				{
					yarn += "\t" + element.ToYarnString() + "\n";
				}
			}

			yarn += "<<endif>>";
			return yarn;
		}

	}
}
#endif
#endif