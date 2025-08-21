#if XNODE
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Arawn.YarnGraph.Editor
{
	public class CommandsEditorWindow : EditorWindow
	{
		private YarnDialogueGraph graph;
		private Vector2 scrollPos;
		private string newCommand = "";

		public void SetGraph(YarnDialogueGraph graph)
		{
			this.graph = graph;
		}

		private void OnGUI()
		{
			if (graph == null)
			{
				GUILayout.Label("No graph selected.");
				return;
			}

			GUILayout.Label("Edit Yarn Commands", EditorStyles.boldLabel);

			scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
			for (int i = 0; i < graph.YarnCommands.Count; i++)
			{
				EditorGUILayout.BeginHorizontal();
				GUI.SetNextControlName("Command_" + i);
				graph.YarnCommands[i] = EditorGUILayout.TextField("Command", graph.YarnCommands[i]);
				if (GUILayout.Button("Remove", GUILayout.Width(60)))
				{
					graph.YarnCommands.RemoveAt(i);
					i--;
					EditorUtility.SetDirty(graph);
				}
				EditorGUILayout.EndHorizontal();
			}
			GUILayout.EndScrollView();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Add New Command", EditorStyles.boldLabel);
			GUI.SetNextControlName("NewCommand");
			newCommand = EditorGUILayout.TextField("Command", newCommand);
			if (GUILayout.Button("Add Command"))
			{
				if (!string.IsNullOrEmpty(newCommand))
				{
					graph.YarnCommands.Add(newCommand);
					newCommand = "";
					EditorUtility.SetDirty(graph);
				}
			}

			if (GUILayout.Button("Close"))
			{
				Close();
			}
		}
	}
}
#endif
#endif