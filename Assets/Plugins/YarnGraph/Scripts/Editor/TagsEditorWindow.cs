#if XNODE
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Arawn.YarnGraph.Editor
{
	public class TagsEditorWindow : EditorWindow
	{
		private YarnDialogueGraph graph;
		private Vector2 scrollPos;
		private string newTag = "";

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

			GUILayout.Label("Edit Yarn Inline Tags", EditorStyles.boldLabel);

			scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
			for (int i = 0; i < graph.YarnInlineTags.Count; i++)
			{
				EditorGUILayout.BeginHorizontal();
				GUI.SetNextControlName("Tag_" + i);
				graph.YarnInlineTags[i] = EditorGUILayout.TextField("Tag", graph.YarnInlineTags[i]);
				if (GUILayout.Button("Remove", GUILayout.Width(60)))
				{
					graph.YarnInlineTags.RemoveAt(i);
					i--;
					EditorUtility.SetDirty(graph);
				}
				EditorGUILayout.EndHorizontal();
			}
			GUILayout.EndScrollView();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Add New Tag", EditorStyles.boldLabel);
			GUI.SetNextControlName("NewTag");
			newTag = EditorGUILayout.TextField("Tag", newTag);
			if (GUILayout.Button("Add Tag"))
			{
				if (!string.IsNullOrEmpty(newTag))
				{
					graph.YarnInlineTags.Add(newTag);
					newTag = "";
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