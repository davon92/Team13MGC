#if XNODE
#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using XNodeEditor;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.IO;
using System;

namespace Arawn.YarnGraph.Editor
{
	[CustomNodeGraphEditor(typeof(YarnDialogueGraph))]
	public class YarnDialogueGraphEditor : NodeGraphEditor
	{
		// Default height for nodes if not specified
		private const float defaultHeight = 100f;
		// Spacing buffers
		private const float horizontalBuffer = 50f;
		private const float verticalBuffer = 50f;

		public override void OnOpen()
		{
			base.OnOpen();
			window.titleContent = new GUIContent("Yarn Graph");
		}

		public override void OnGUI()
		{
			DrawToolbar();
			base.OnGUI();
		}

		private void DrawToolbar()
		{
			GUILayout.BeginHorizontal(EditorStyles.toolbar);

			// Left-aligned: Variables
			if (GUILayout.Button("Variables", EditorStyles.toolbarButton, GUILayout.Width(100)))
			{
				VariableEditorWindow varWindow = EditorWindow.GetWindow<VariableEditorWindow>("Variables");
				varWindow.SetGraph(target as YarnDialogueGraph);
			}

			// Left-aligned: Tags
			if (GUILayout.Button("Tags", EditorStyles.toolbarButton, GUILayout.Width(100)))
			{
				TagsEditorWindow tagsWindow = EditorWindow.GetWindow<TagsEditorWindow>("Tags");
				tagsWindow.SetGraph(target as YarnDialogueGraph);
			}

			// Left-aligned: Commands
			if (GUILayout.Button("Commands", EditorStyles.toolbarButton, GUILayout.Width(100)))
			{
				CommandsEditorWindow commandsWindow = EditorWindow.GetWindow<CommandsEditorWindow>("Commands");
				commandsWindow.SetGraph(target as YarnDialogueGraph);
			}

			GUILayout.FlexibleSpace();

			// Right-aligned: Export Yarn
			if (GUILayout.Button("Export Yarn", EditorStyles.toolbarButton))
			{
				ExportYarn();
			}

			// Right-aligned: Auto Arrange
			if (GUILayout.Button("Auto Arrange", EditorStyles.toolbarButton))
			{
				AutoArrangeNodes();
			}

			// Right-aligned: Refresh & Save
			if (GUILayout.Button("Refresh & Save", EditorStyles.toolbarButton))
			{
				YarnDialogueGraph graph = target as YarnDialogueGraph;
				if (graph != null)
				{
					SaveGraphAsset(graph);

					// Trigger a script recompilation by updating a dummy script file.
					string dummyScriptPath = "Assets/Plugins/YarnGraph/Scripts/Editor/Util/EnforceReload.cs";
					if (File.Exists(dummyScriptPath))
					{
						// Update the file's last write time to force Unity to re-import it
						File.SetLastWriteTime(dummyScriptPath, DateTime.Now);
						AssetDatabase.ImportAsset(dummyScriptPath);
						Debug.Log("EnforceReload.cs updated to trigger recompilation.");
					}
					else
					{
						Debug.LogWarning("EnforceReload.cs not found at: " + dummyScriptPath);
					}

					// Repaint the graph window immediately to reflect any changes.
					window.Repaint();
				}
			}

			GUILayout.EndHorizontal();
		}


		private void ExportYarn()
		{
			YarnDialogueGraph graph = target as YarnDialogueGraph;
			if (graph == null) return;

			// Prompt user for a destination .yarn file
			string path = EditorUtility.SaveFilePanel("Export Yarn Script", "", graph.name + ".yarn", "yarn");
			if (string.IsNullOrEmpty(path))
				return;

			// Do the actual export
			string yarnText = YarnExporter.Export(graph);

			// Write out to the user-specified path
			File.WriteAllText(path, yarnText);

			// Force Unity to refresh the asset database, so the new file is recognized
			AssetDatabase.Refresh();

			Debug.Log("Exported Yarn script to " + path);
		}
		private void AutoArrangeNodes()
		{
			var graph = target as YarnDialogueGraph;
			if (graph == null) return;

			// 1) Collect all nodes and find roots (no incoming connections)
			var allNodes = graph.nodes.Cast<XNode.Node>().ToList();
			var roots = allNodes
				.Where(n => n.Ports.Where(p => !p.IsOutput).All(p => p.ConnectionCount == 0))
				.OrderBy(n => n.name)
				.ToList();

			// 2) Build a map of each node’s ordered children (port order)
			var childrenMap = allNodes.ToDictionary(
				n => n,
				n => n.Ports
					.Where(p => p.IsOutput)
					.SelectMany(p => p.GetConnections().Select(c => c.node))
					.ToList()
			);

			// 3) Compute subtree heights for every node
			var subtreeHeight = new Dictionary<XNode.Node, float>();
			float ComputeSubtreeHeight(XNode.Node node)
			{
				if (subtreeHeight.TryGetValue(node, out var h)) return h;
				var kids = childrenMap[node];
				if (kids.Count == 0)
				{
					// leaf => just its own height
					h = GetNodeHeight(node);
				}
				else
				{
					// sum of each child’s subtree + buffers between them
					h = 0f;
					for (int i = 0; i < kids.Count; i++)
					{
						h += ComputeSubtreeHeight(kids[i]);
						if (i < kids.Count - 1) h += verticalBuffer;
					}
				}
				subtreeHeight[node] = h;
				return h;
			}
			foreach (var root in roots) ComputeSubtreeHeight(root);

			// 4) Horizontal spacing (depth → x)
			float maxWidth = allNodes
				.Where(n => n is DialogueNode || n is VariableSetNode)
				.Select(n => GetNodeWidth(n))
				.DefaultIfEmpty(300f)
				.Max();
			float hSpacing = maxWidth + horizontalBuffer;

			// 5) Recursively layout: leaves advance currentY by their full subtree height
			float currentY = 0f;
			void LayoutSubtree(XNode.Node node, int depth)
			{
				var kids = childrenMap[node];
				float x = depth * hSpacing;

				if (kids.Count == 0)
				{
					// Leaf: place at currentY, then bump by subtreeHeight + buffer
					SetPosition(node, x, currentY);
					currentY += subtreeHeight[node] + verticalBuffer;
				}
				else
				{
					// Internal: remember start, lay out all children,
					// then center this node above them
					float startY = currentY;
					foreach (var child in kids)
						LayoutSubtree(child, depth + 1);
					float endY = currentY - verticalBuffer;
					float midY = (startY + endY) * 0.5f;
					SetPosition(node, x, midY);
				}
			}

			// 6) Run layout for each root
			foreach (var root in roots)
				LayoutSubtree(root, 0);

			EditorUtility.SetDirty(graph);
		}

		// Helper to write a position back into the node’s SerializedObject
		private void SetPosition(XNode.Node node, float x, float y)
		{
			var so = new SerializedObject(node);
			so.FindProperty("position").vector2Value = new Vector2(x, y);
			so.ApplyModifiedProperties();
		}


		// Helper: Retrieve the node's width from the NodeWidth attribute.
		private float GetNodeWidth(XNode.Node node)
		{
			var type = node.GetType();
			var attr = type.GetCustomAttribute<XNode.Node.NodeWidthAttribute>();
			if (attr != null)
				return attr.width;
			return 200f; // Fallback default
		}

		// Helper: Get node height. You can customize per node type.
		private float GetNodeHeight(XNode.Node node)
		{
			if (node is DialogueNode || node is VariableSetNode)
				return 100f;
			else if (node is DecisionNode || node is IfNode)
				return 150f;
			return defaultHeight;
		}

		// Helper: Find the first parent node (from any input port).
		private XNode.Node FindParent(XNode.Node node)
		{
			foreach (var port in node.Ports.Where(p => !p.IsOutput))
			{
				foreach (var connection in port.GetConnections())
				{
					return connection.node;
				}
			}
			return null;
		}

		// Helper: Check if a node is a Decision node.
		private bool IsDecisionNode(XNode.Node node)
		{
			return node is DecisionNode;
		}

		public static void SaveGraphAsset(YarnDialogueGraph graph)
		{
			foreach (var node in graph.nodes)
			{
				if (AssetDatabase.GetAssetPath(node) != AssetDatabase.GetAssetPath(graph))
				{
					AssetDatabase.AddObjectToAsset(node, graph);
				}
			}
			EditorUtility.SetDirty(graph);
			AssetDatabase.SaveAssets();
			Debug.Log("Graph saved and nodes re-serialized as subassets.");
		}
	}

	public class VariableEditorWindow : EditorWindow
	{
		private YarnDialogueGraph graph;
		private Vector2 scrollPos;
		private string newVarName = "";
		private string newVarInitialValue = "";

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

			GUILayout.Label("Edit Yarn Variables", EditorStyles.boldLabel);

			scrollPos = GUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
			for (int i = 0; i < graph.declaredVariables.Count; i++)
			{
				EditorGUILayout.BeginHorizontal();
				string nameControl = "VarName_" + i;
				GUI.SetNextControlName(nameControl);
				graph.declaredVariables[i].variableName = EditorGUILayout.TextField("Name", graph.declaredVariables[i].variableName);
				string valueControl = "VarValue_" + i;
				GUI.SetNextControlName(valueControl);
				graph.declaredVariables[i].initialValue = EditorGUILayout.TextField("Initial Value", graph.declaredVariables[i].initialValue);
				if (GUILayout.Button("Remove", GUILayout.Width(60)))
				{
					graph.declaredVariables.RemoveAt(i);
					i--;
					EditorUtility.SetDirty(graph);
				}
				EditorGUILayout.EndHorizontal();
			}
			GUILayout.EndScrollView();

			EditorGUILayout.Space();
			EditorGUILayout.LabelField("Add New Variable", EditorStyles.boldLabel);
			GUI.SetNextControlName("NewVarName");
			newVarName = EditorGUILayout.TextField("Name", newVarName);
			GUI.SetNextControlName("NewVarValue");
			newVarInitialValue = EditorGUILayout.TextField("Initial Value", newVarInitialValue);
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
		}
	}
}
#endif
#endif