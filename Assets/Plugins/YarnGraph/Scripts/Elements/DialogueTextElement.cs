#if XNODE
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.YarnGraph
{
	[ElementTitle("Write Dialogue")]
	[Serializable]
	public class DialogueTextElement : DialogueElement
	{
		public DialogueCharacterReference character = new DialogueCharacterReference();

		[TextArea(8, 8)]
		public string text;

		// NEW PROPERTY
		public bool UseVoiceOver;
		public List<AudioClip> voiceOverClips = new List<AudioClip>();
		public string voiceOverID;

		public override string ToYarnString()
		{
			// Always include curly braces when using a variable so that nested elements export correctly.
			string characterName = character.useVariable && !string.IsNullOrEmpty(character.variableName)
				? "{$" + character.variableName + "}"
				: character.constantName;

			// Split lines by newline.
			var lines = text.Split(new[] { '\n' }, StringSplitOptions.None);
			var yarnString = "";

			for (int i = 0; i < lines.Length; i++)
			{
				string line = lines[i];
				string lineId = "";

				// Check for a voice-over clip if enabled.
				if (UseVoiceOver && i < voiceOverClips.Count && voiceOverClips[i] != null)
				{
					string clipName = voiceOverClips[i].name;
					lineId = $" #line:{clipName}";
				}

				yarnString += $"{characterName}: {line}{lineId}\n";
			}

			return yarnString.TrimEnd(); // Remove trailing newline
		}
	}
}
#endif
#endif