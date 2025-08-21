#if XNODE
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using System.IO;

namespace Arawn.YarnGraph.Editor
{
	[CustomEditor(typeof(TextAsset))]
	public class YarnFileImporterEditor : UnityEditor.Editor
	{
		// Optionally, assign a custom command database via the inspector.
		// If you do not need it, you can remove this field and update the importer accordingly.
		[SerializeField]
		private CustomCommandDatabase customCommandDatabase;

		public override void OnInspectorGUI()
		{
			// Draw the default inspector.
			DrawDefaultInspector();

			// Ensure GUI is enabled.
			GUI.enabled = true;

			// Get the selected asset and its path.
			TextAsset textAsset = (TextAsset)target;
			string assetPath = AssetDatabase.GetAssetPath(textAsset);

			// Check if the asset is a Yarn file (by extension).
			if (Path.GetExtension(assetPath).ToLower() == ".yarn")
			{
				GUILayout.Space(10);
				if (GUILayout.Button("Import Yarn File"))
				{
					// Import the Yarn file into a YarnDialogueGraph.
					// Note: Ensure your YarnImporter has an ImportYarnFile method that accepts the asset path and an optional command database.
					YarnDialogueGraph graph = YarnImporter.ImportYarnFile(assetPath, customCommandDatabase);
					if (graph != null)
					{
						string savePath = EditorUtility.SaveFilePanelInProject(
							"Save Yarn Dialogue Graph",
							Path.GetFileNameWithoutExtension(assetPath) + "_Graph",
							"asset",
							"Enter a file name to save the imported Yarn Dialogue Graph."
						);

						if (!string.IsNullOrEmpty(savePath))
						{
							AssetDatabase.CreateAsset(graph, savePath);
							YarnImporter.SaveGraphAsset(graph);
							AssetDatabase.Refresh();

							EditorUtility.FocusProjectWindow();
							Selection.activeObject = graph;
							Debug.Log("Yarn file imported successfully into YarnDialogueGraph.");
						}
					}
					else
					{
						Debug.LogError("Failed to import Yarn file.");
					}
				}
			}
		}
	}
}
#endif
#endif
