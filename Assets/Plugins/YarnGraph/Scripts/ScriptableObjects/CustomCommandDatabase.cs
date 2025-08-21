#if XNODE
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace Arawn.YarnGraph
{
	[CreateAssetMenu(fileName = "CustomCommandDatabase", menuName = "")]
	public class CustomCommandDatabase : ScriptableObject
	{
		// List of custom command prefixes (without the surrounding << >>).
		public List<string> customCommands = new List<string>();
	}
}
#endif
#endif