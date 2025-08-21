#if XNODE
#if UNITY_EDITOR
using System;

namespace Arawn.YarnGraph
{
	[Serializable]
	public class DialogueCharacterReference
	{
		// If true, use the variable reference; if false, use the constant string.
		public bool useVariable;

		// The constant character name.
		public string constantName;

		// The variable name to reference (e.g. one of the declared variables).
		public string variableName;
	}
}
#endif
#endif