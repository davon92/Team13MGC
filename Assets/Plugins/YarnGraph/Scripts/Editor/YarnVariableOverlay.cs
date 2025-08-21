#if XNODE
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace Arawn.YarnGraph.Editor
{
	public class YarnVariableOverlay : EditorWindow
	{
		private YarnDialogueGraph graph;
		private Vector2 scrollPos;
		private string newVarName = "";
		private string newVarInitialValue = "";

		public static void ShowWindow(YarnDialogueGraph graph)
		{
			YarnVariableOverlay window = CreateInstance<YarnVariableOverlay>();
			window.graph = graph;
			window.titleContent = new GUIContent("Variables");
			// Adjust window position and size as needed
			window.position = new Rect(200, 200, 400, 300);
			window.ShowUtility(); // Floating utility window
		}

		private void OnGUI()
		{
			if (graph == null)
			{
				EditorGUILayout.LabelField("Graph not assigned.");
				return;
			}

			EditorGUILayout.LabelField("Edit Yarn Variables", EditorStyles.boldLabel);
			EditorGUILayout.Space();

			// Display the list of declared variables in a scroll view.
			scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.Height(200));
			for (int i = 0; i < graph.declaredVariables.Count; i++)
			{
				EditorGUILayout.BeginHorizontal();

				// Editable text field for variable name.
				graph.declaredVariables[i].variableName = EditorGUILayout.TextField("Name", graph.declaredVariables[i].variableName);
				// Editable text field for initial value.
				graph.declaredVariables[i].initialValue = EditorGUILayout.TextField("Initial Value", graph.declaredVariables[i].initialValue);

				// Optionally, a remove button to delete the variable.
				if (GUILayout.Button("Remove", GUILayout.Width(60)))
				{
					graph.declaredVariables.RemoveAt(i);
					i--;
				}
				EditorGUILayout.EndHorizontal();
			}
			EditorGUILayout.EndScrollView();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Add New Variable", EditorStyles.boldLabel);
			newVarName = EditorGUILayout.TextField("Name", newVarName);
			newVarInitialValue = EditorGUILayout.TextField("Initial Value", newVarInitialValue);

			EditorGUILayout.BeginHorizontal();
			if (GUILayout.Button("Add Variable"))
			{
				if (!string.IsNullOrEmpty(newVarName))
				{
					YarnVariable newVar = new YarnVariable
					{
						variableName = newVarName,
						initialValue = newVarInitialValue
					};
					graph.declaredVariables.Add(newVar);
					newVarName = "";
					newVarInitialValue = "";
					EditorUtility.SetDirty(graph);
				}
			}
			if (GUILayout.Button("Close"))
			{
				Close();
			}
			EditorGUILayout.EndHorizontal();
		}
	}
}
#endif
#endif