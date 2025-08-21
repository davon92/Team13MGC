#if XNODE
#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using XNode;
using Unity.EditorCoroutines.Editor;

namespace Arawn.YarnGraph
{
	public static class YarnImporter
	{
		// Helper method: Search project-wide for a CustomCommandDatabase asset.
		// If not found, create one automatically.
		private static CustomCommandDatabase GetOrCreateCustomCommandDatabase()
		{
			string[] guids = AssetDatabase.FindAssets("t:CustomCommandDatabase");
			if (guids != null && guids.Length > 0)
			{
				string path = AssetDatabase.GUIDToAssetPath(guids[0]);
				CustomCommandDatabase db = AssetDatabase.LoadAssetAtPath<CustomCommandDatabase>(path);
				Debug.Log("CustomCommandDatabase found at: " + path);
				return db;
			}
			else
			{
				CustomCommandDatabase newDB = ScriptableObject.CreateInstance<CustomCommandDatabase>();
				string assetPath = "Assets/CustomCommandDatabase.asset";
				AssetDatabase.CreateAsset(newDB, assetPath);
				AssetDatabase.SaveAssets();
				Debug.Log("CustomCommandDatabase not found. Created new asset at: " + assetPath);
				return newDB;
			}
		}

		// --- Helper: Returns true if a line is indented (starts with four spaces or a tab).
		private static bool IsIndentedOptionLine(string line)
		{
			return line.StartsWith("    ") || line.StartsWith("\t");
		}

		// --- Helper: Given a block of lines belonging to one decision option,
		// create a DecisionOption with nested DialogueElements.
		private static DecisionOption ParseDecisionOptionBlock(List<string> optionBlock)
		{
			DecisionOption opt = new DecisionOption();
			opt.condition = null; // Set condition to null
			string firstLine = optionBlock[0].Trim();
			if (firstLine.StartsWith("->"))
			{
				string optionText = firstLine.Substring(2).Trim();
				opt.dialogueElements.Add(ParseDialogueLine(optionText));
			}
			for (int i = 1; i < optionBlock.Count; i++)
			{
				string line = optionBlock[i].Trim();
				if (line.StartsWith("<<jump"))
				{
					string jumpTarget = line.Substring(7, line.Length - 9).Trim();
					JumpElement jumpElem = new JumpElement()
					{
						jumpMode = JumpElement.JumpMode.UsePort,
						portName = $"JumpTo_{jumpTarget}",
						NodeName = jumpTarget
					};
					opt.dialogueElements.Add(jumpElem);
					opt.jumpTargetTitle = jumpTarget;
				}
				else
				{
					opt.dialogueElements.Add(ParseDialogueLine(line));
				}
			}
			return opt;
		}

		// --- Decision parsing remains largely unchanged.
		private static DecisionElement ParseDecisionElement(string initialCondition, List<string> blockLines, YarnDialogueGraph graph, CustomCommandDatabase commandDB)
		{
			DecisionElement decisionElement = new DecisionElement();
			decisionElement.options = new List<DecisionOption>();

			// Check for internal conditions
			bool hasInternalConditions = blockLines.Any(l => l.Trim().StartsWith("<<if") || l.Trim().StartsWith("<<elseif") || l.Trim().StartsWith("<<else>>"));

			if (!hasInternalConditions)
			{
				// No internal conditions: parse all options directly
				List<DecisionOption> options = ParseAllOptionsFromBlock(blockLines);
				// Set conditions based on initialCondition
				ICondition optionCondition = string.IsNullOrEmpty(initialCondition) ? null : ParseCompoundCondition(initialCondition);
				foreach (var opt in options)
				{
					opt.condition = optionCondition;
					decisionElement.options.Add(opt);
				}
			}
			else
			{
				// Proceed with branch logic as before
				List<(string condition, List<string> lines)> branches = new List<(string, List<string>)>();
				branches.Add((initialCondition, new List<string>()));
				for (int j = 0; j < blockLines.Count; j++)
				{
					string line = blockLines[j].Trim();
					if (line.StartsWith("<<elseif"))
					{
						int start = line.IndexOf("<<elseif") + 8;
						int end = line.IndexOf(">>");
						string cond = line.Substring(start, end - start).Trim();
						branches.Add((cond, new List<string>()));
					}
					else if (line.StartsWith("<<else>>"))
					{
						branches.Add((null, new List<string>())); // else branch: no condition
					}
					else
					{
						branches[branches.Count - 1].lines.Add(line);
					}
				}

				var optionAppearances = new Dictionary<(string text, string jumpTarget), HashSet<int>>();
				for (int branchIndex = 0; branchIndex < branches.Count; branchIndex++)
				{
					var branch = branches[branchIndex];
					List<string> branchLines = branch.lines;
					int k = 0;
					while (k < branchLines.Count)
					{
						string optionLine = branchLines[k].Trim();
						if (optionLine.StartsWith("->"))
						{
							string optionText = optionLine.Substring(2).Trim();
							k++;
							string jumpTarget = null;
							while (k < branchLines.Count)
							{
								string nextLine = branchLines[k].Trim();
								if (nextLine.StartsWith("<<jump"))
								{
									jumpTarget = nextLine.Substring(7, nextLine.Length - 9).Trim();
									k++;
									break;
								}
								else if (nextLine.StartsWith("->") || nextLine.StartsWith("<<"))
								{
									break;
								}
								k++;
							}
							var key = (optionText, jumpTarget);
							if (!optionAppearances.ContainsKey(key))
								optionAppearances[key] = new HashSet<int>();
							optionAppearances[key].Add(branchIndex);
						}
						else
						{
							k++;
						}
					}
				}

				foreach (var kvp in optionAppearances)
				{
					var (text, jumpTarget) = kvp.Key;
					var appearanceBranches = kvp.Value;
					DecisionOption opt = new DecisionOption
					{
						jumpTargetTitle = jumpTarget,
						dialogueElements = new List<DialogueElement> { ParseDialogueLine(text) }
					};

					if (!string.IsNullOrEmpty(jumpTarget))
					{
						var jumpElem = new JumpElement
						{
							jumpMode = JumpElement.JumpMode.UsePort,
							portName = $"JumpTo_{jumpTarget}",
							NodeName = jumpTarget
						};
						opt.dialogueElements.Add(jumpElem);
					}

					List<ICondition> conditions = new List<ICondition>();
					bool hasElse = false;
					foreach (int branchIndex in appearanceBranches)
					{
						string branchCond = branches[branchIndex].condition;
						if (string.IsNullOrEmpty(branchCond))
						{
							hasElse = true;
							break;
						}
						else
						{
							ICondition cond = ParseCompoundCondition(branchCond);
							conditions.Add(cond);
						}
					}
					if (hasElse || conditions.Count == 0)
						opt.condition = null;
					else if (conditions.Count == 1)
						opt.condition = conditions[0];
					else
					{
						CompoundCondition compound = new CompoundCondition();
						compound.logicOperator = LogicOperator.OR;
						compound.conditions = conditions;
						opt.condition = compound;
					}

					decisionElement.options.Add(opt);
				}
			}

			return decisionElement;
		}

		private static List<DecisionOption> ParseAllOptionsFromBlock(List<string> lines)
		{
			List<DecisionOption> options = new List<DecisionOption>();
			int i = 0;
			while (i < lines.Count)
			{
				string line = lines[i].Trim();
				if (line.StartsWith("->"))
				{
					List<string> optionBlock = new List<string>();
					optionBlock.Add(line);
					i++;
					while (i < lines.Count)
					{
						string nextLine = lines[i];
						if (IsIndent(nextLine))
						{
							optionBlock.Add(nextLine);
							i++;
						}
						else
						{
							break;
						}
					}
					DecisionOption opt = ParseDecisionOptionBlock(optionBlock);
					options.Add(opt);
				}
				else
				{
					i++;
				}
			}
			return options;
		}

		private static bool IsIndent(string line)
		{
			return line.StartsWith("    ") || line.StartsWith("\t");
		}

		private static Dictionary<string, bool> ParseBranchTruthValues(string branchCondition, List<string> atomicConditions)
		{
			var truthValues = new Dictionary<string, bool>();
			if (branchCondition == null)
			{
				foreach (var cond in atomicConditions)
					truthValues[cond] = false;
			}
			else
			{
				var parts = branchCondition.Split(new string[] { " && " }, StringSplitOptions.None)
										   .Select(s => s.Trim());
				foreach (var part in parts)
				{
					if (part.StartsWith("!("))
					{
						string cond = part.Substring(2, part.Length - 3).Trim();
						truthValues[cond] = false;
					}
					else
					{
						truthValues[part] = true;
					}
				}
			}
			return truthValues;
		}

		// --- Modified ParseAtomicCondition: if the text is a bare variable reference (like "$hasSword"),
		// we assume it means "$hasSword == true".
		private static ICondition ParseAtomicCondition(string text)
		{
			text = text.Trim();
			if (text.StartsWith("!"))
			{
				string withoutBang = text.Substring(1).Trim();
				ICondition inner = ParseAtomicCondition(withoutBang);
				return inner != null ? new NotCondition(inner) : null;
			}
			// Handle bare variable reference (e.g. "$hasSword")
			if (text.StartsWith("$") && !text.Contains(" "))
			{
				var sc = new SingleComparison();
				sc.leftVariable = text.Substring(1).Trim();
				sc.op = ComparisonOperator.Equals;
				sc.isRightVariable = false;
				sc.rightVariableOrValue = "true";
				return sc;
			}
			var regexVisited = new Regex(
				@"^visited\(""([^""]+)""\)(\s*(==|!=)\s*(true|false))?$",
				RegexOptions.IgnoreCase
			);
			Match matchVisited = regexVisited.Match(text);
			if (matchVisited.Success)
			{
				string nodeName = matchVisited.Groups[1].Value;
				if (string.IsNullOrEmpty(matchVisited.Groups[2].Value))
				{
					var visited = new VisitedCondition();
					visited.nodeName = nodeName;
					return visited;
				}
				else
				{
					string op = matchVisited.Groups[3].Value;
					string boolLiteral = matchVisited.Groups[4].Value.ToLower();
					bool literalValue = (boolLiteral == "true");
					bool negate = op == "==" ? !literalValue : literalValue;
					var visited = new VisitedCondition();
					visited.nodeName = nodeName;
					return negate ? new NotCondition(visited) : visited;
				}
			}
			if (text.StartsWith("visited_count("))
			{
				int endIndex = text.IndexOf(')');
				if (endIndex != -1)
				{
					string funcPart = text.Substring(0, endIndex + 1).Trim();
					string remainder = text.Substring(endIndex + 1).Trim();
					var regexFunc = new Regex(@"^visited_count\(""([^""]+)""\)$");
					Match funcMatch = regexFunc.Match(funcPart);
					if (funcMatch.Success)
					{
						var visitedCount = new VisitedCountCondition();
						visitedCount.nodeName = funcMatch.Groups[1].Value;
						var regexOp = new Regex(@"^(==|!=|>=|<=|>|<)\s*(\d+)$");
						Match opMatch = regexOp.Match(remainder);
						if (opMatch.Success)
						{
							string opStr = opMatch.Groups[1].Value;
							if (int.TryParse(opMatch.Groups[2].Value, out int count))
							{
								visitedCount.countValue = count;
								switch (opStr)
								{
									case "==": visitedCount.op = ComparisonOperator.Equals; break;
									case "!=": visitedCount.op = ComparisonOperator.NotEquals; break;
									case ">": visitedCount.op = ComparisonOperator.GreaterThan; break;
									case "<": visitedCount.op = ComparisonOperator.LessThan; break;
									case ">=": visitedCount.op = ComparisonOperator.GreaterOrEqual; break;
									case "<=": visitedCount.op = ComparisonOperator.LessOrEqual; break;
									default: visitedCount.op = ComparisonOperator.Equals; break;
								}
								return visitedCount;
							}
							else
							{
								Debug.LogWarning("Failed to parse count value in visited_count condition: " + text);
								return null;
							}
						}
						else
						{
							Debug.LogWarning("Failed to parse operator in visited_count condition: " + text);
							return null;
						}
					}
					else
					{
						Debug.LogWarning("Failed to parse visited_count function in: " + text);
						return null;
					}
				}
			}
			string[] operators = { "==", "!=", ">=", "<=", ">", "<" };
			foreach (var op in operators)
			{
				int idx = text.IndexOf(op);
				if (idx != -1)
				{
					var sc = new SingleComparison();
					string left = text.Substring(0, idx).Trim();
					string right = text.Substring(idx + op.Length).Trim();
					if (left.StartsWith("$"))
						left = left.Substring(1).Trim();
					sc.leftVariable = left;
					switch (op)
					{
						case "==": sc.op = ComparisonOperator.Equals; break;
						case "!=": sc.op = ComparisonOperator.NotEquals; break;
						case ">": sc.op = ComparisonOperator.GreaterThan; break;
						case "<": sc.op = ComparisonOperator.LessThan; break;
						case ">=": sc.op = ComparisonOperator.GreaterOrEqual; break;
						case "<=": sc.op = ComparisonOperator.LessOrEqual; break;
						default: sc.op = ComparisonOperator.Equals; break;
					}
					sc.isRightVariable = right.StartsWith("$");
					if (sc.isRightVariable)
						right = right.Substring(1).Trim();
					sc.rightVariableOrValue = right;
					return sc;
				}
			}
			return null;
		}

		private static bool IsCustomCommandLine(string line, out string commandPrefix)
		{
			commandPrefix = null;
			if (!line.StartsWith("<<") || !line.Trim().EndsWith(">>"))
				return false;
			string inner = line.Trim().Substring(2, line.Trim().Length - 4).Trim();
			int spaceIndex = inner.IndexOf(' ');
			commandPrefix = spaceIndex > 0 ? inner.Substring(0, spaceIndex) : inner;
			string prefixLocal = commandPrefix;
			string[] known = new string[] { "declare", "set", "jump", "if", "wait", "run", "else", "elseif", "endif" };
			if (known.Any(k => prefixLocal.Equals(k, StringComparison.OrdinalIgnoreCase)))
				return false;
			return true;
		}

		// Main import method.
		public static YarnDialogueGraph ImportYarnFile(string filePath, CustomCommandDatabase commandDB = null)
		{
			if (!File.Exists(filePath))
			{
				Debug.LogError("Yarn file not found: " + filePath);
				return null;
			}
			if (commandDB == null)
			{
				commandDB = GetOrCreateCustomCommandDatabase();
			}
			string[] lines = File.ReadAllLines(filePath);

			// Scan for jump targets.
			HashSet<string> referencedTitles = new HashSet<string>();
			Regex jumpRegex = new Regex(@"^<<jump\s+(.*?)>>", RegexOptions.Compiled);
			foreach (var rawLine in lines)
			{
				string line = rawLine.Trim();
				if (line.StartsWith("///"))
					continue;
				if (jumpRegex.IsMatch(line))
				{
					Match m = jumpRegex.Match(line);
					if (m.Success)
					{
						string target = CleanNodeTitle(m.Groups[1].Value.Trim());
						if (!string.IsNullOrEmpty(target))
							referencedTitles.Add(target);
					}
				}
			}

			// Split file into nodes.
			Dictionary<string, DialogueNode> titleToNode = new Dictionary<string, DialogueNode>();
			List<(string title, List<string> content)> nodeContents = new List<(string, List<string>)>();
			List<string> currentContent = null;
			string currentTitle = null;
			foreach (string rawLine in lines)
			{
				string line = rawLine.TrimEnd();
				if (line.StartsWith("///"))
					continue;
				if (line.StartsWith("title: "))
				{
					if (currentTitle != null)
						nodeContents.Add((currentTitle, currentContent));
					currentTitle = line.Substring(7).Trim();
					currentContent = new List<string>();
				}
				else if (line == "---")
				{
					continue;
				}
				else if (line == "===")
				{
					if (currentTitle != null)
					{
						nodeContents.Add((currentTitle, currentContent));
						currentTitle = null;
						currentContent = null;
					}
				}
				else if (currentContent != null)
				{
					currentContent.Add(line);
				}
			}
			if (currentTitle != null)
				nodeContents.Add((currentTitle, currentContent));

			// Create DialogueNodes.
			YarnDialogueGraph graph = ScriptableObject.CreateInstance<YarnDialogueGraph>();
			if (graph.YarnInlineTags == null)
				graph.YarnInlineTags = new List<string>();
			foreach (var (title, _) in nodeContents)
			{
				DialogueNode node = graph.AddNode<DialogueNode>();
				node.nodeTitle = title;
				if (node.elements == null)
					node.elements = new List<DialogueElement>();
				node.nodeTags = new List<string>();
				titleToNode[title] = node;
			}

			// Determine start node.
			DialogueNode startNode = null;
			foreach (var pair in titleToNode)
			{
				string cleanTitle = CleanNodeTitle(pair.Key);
				if (!referencedTitles.Contains(cleanTitle))
				{
					startNode = pair.Value;
					break;
				}
			}
			if (startNode == null)
				startNode = titleToNode.Values.FirstOrDefault();

			// Process node content.
			foreach (var (title, content) in nodeContents)
			{
				DialogueNode node = titleToNode[title];
				ParseNodeContent(node, content, graph, titleToNode, node == startNode, commandDB);
			}

			// Connect JumpElement ports.
			foreach (var node in graph.nodes.OfType<DialogueNode>())
			{
				node.UpdateJumpPorts();
				var allJumps = node.GetAllJumpElements();
				foreach (var jump in allJumps)
				{
					if (jump.jumpMode == JumpElement.JumpMode.UsePort &&
						!string.IsNullOrEmpty(jump.portName) &&
						!string.IsNullOrEmpty(jump.NodeName))
					{
						NodePort jumpPort = node.GetPort(jump.portName);
						if (jumpPort != null && titleToNode.TryGetValue(jump.NodeName, out DialogueNode targetNode))
						{
							jumpPort.Connect(targetNode.GetInputPort("input"));
						}
						else
						{
							Debug.LogWarning($"Could not connect JumpElement port '{jump.portName}' in node '{node.nodeTitle}' to target '{jump.NodeName}'");
						}
					}
				}
			}

			ProcessVoiceOverClips(graph);

			// 1. Save the graph asset (with correctly named sub-assets) and refresh.
			SaveGraphAsset(graph);
			AssetDatabase.Refresh();

			// 2. Bump the dummy script to force Unity to recompile & re-import.
			string dummyScriptPath = "Assets/Plugins/YarnGraph/Scripts/Editor/Util/EnforceReload.cs";
			if (File.Exists(dummyScriptPath))
			{
				File.SetLastWriteTime(dummyScriptPath, DateTime.Now);
				AssetDatabase.ImportAsset(dummyScriptPath);
				Debug.Log("EnforceReload.cs touched to trigger recompilation.");
			}
			else
			{
				Debug.LogWarning("EnforceReload.cs not found at: " + dummyScriptPath);
			}

			return graph;
		}

		// --- Modified ParseNodeContent: if‑blocks are always imported as IfDialogueElement.
		private static void ParseNodeContent(
			DialogueNode node,
			List<string> content,
			YarnDialogueGraph graph,
			Dictionary<string, DialogueNode> titleToNode,
			bool processDeclarations,
			CustomCommandDatabase commandDB = null)
		{
			// Pre-scan to count top-level jump commands.
			List<string> originalContent = new List<string>(content);
			int topLevelJumpCount = originalContent
				.Where(line =>
				{
					string trimmed = line.Trim();
					return trimmed.StartsWith("<<jump") &&
						   !trimmed.StartsWith("<<if") &&
						   !trimmed.StartsWith("<<elseif") &&
						   !trimmed.StartsWith("<<else");
				})
				.Count();

			while (content.Count > 0 && string.IsNullOrWhiteSpace(content[0]))
				content.RemoveAt(0);

			if (content.Count > 0 && content[0].TrimStart().StartsWith("tags:", StringComparison.OrdinalIgnoreCase))
			{
				string tagLine = content[0].Trim();
				string tagsText = tagLine.Substring(5).Trim();
				var tags = tagsText.Split(new char[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
				node.UseNodeTags = true;
				node.nodeTags = new List<string>(tags);
				content.RemoveAt(0);
			}

			int i = 0;
			// Process declarations.
			if (processDeclarations)
			{
				while (i < content.Count && content[i].Trim().StartsWith("<<declare "))
				{
					string line = content[i].Trim();
					if (line.StartsWith("///"))
					{
						i++;
						continue;
					}
					int endIndex = line.IndexOf(">>");
					if (endIndex != -1)
					{
						string declaration = line.Substring(10, endIndex - 10).Trim();
						if (declaration.StartsWith("$"))
							declaration = declaration.Substring(1).Trim();
						string[] parts = declaration.Split('=');
						if (parts.Length == 2)
						{
							string varName = parts[0].Trim();
							string initialValue = parts[1].Trim();
							graph.declaredVariables.Add(new YarnVariable
							{
								variableName = varName,
								initialValue = initialValue
							});
						}
						else
						{
							Debug.LogWarning($"Invalid declaration format in node '{node.nodeTitle}': {line}");
						}
					}
					else
					{
						Debug.LogWarning($"Missing '>>' in declaration in node '{node.nodeTitle}': {line}");
					}
					i++;
				}
			}
			else
			{
				while (i < content.Count && content[i].Trim().StartsWith("<<declare "))
					i++;
			}

			// Process initial plain dialogue lines.
			while (i < content.Count && !IsCommandIndicator(content[i].Trim()))
			{
				string line = content[i].Trim();
				if (line.StartsWith("///") || string.IsNullOrWhiteSpace(line) ||
					line.StartsWith("colorID:") || line.StartsWith("position:"))
				{
					i++;
					continue;
				}
				if (IsCustomCommandLine(line, out string prefix))
				{
					if (!commandDB.customCommands.Contains(prefix))
					{
						commandDB.customCommands.Add(prefix);
						EditorUtility.SetDirty(commandDB);
						AssetDatabase.SaveAssets();
					}
					YarnCommandElement customCommand = new YarnCommandElement();
					string inner = line.Trim();
					if (inner.StartsWith("<<") && inner.EndsWith(">>"))
						inner = inner.Substring(2, inner.Length - 4).Trim();
					customCommand.commandName = inner;
					node.elements.Add(customCommand);
					if (!graph.YarnCommands.Contains(inner))
						graph.YarnCommands.Add(inner);
					i++;
					continue;
				}
				DialogueTextElement element = ParseDialogueLine(line);
				node.elements.Add(element);
				ExtractAndAddInlineTags(line, graph);
				i++;
			}

			// Process remaining lines.
			while (i < content.Count)
			{
				string line = content[i].Trim();
				if (line.StartsWith("///") || line.StartsWith("colorID:") || line.StartsWith("position:"))
				{
					i++;
					continue;
				}
				if (IsCustomCommandLine(line, out string cmdPrefix))
				{
					if (!commandDB.customCommands.Contains(cmdPrefix))
					{
						commandDB.customCommands.Add(cmdPrefix);
						EditorUtility.SetDirty(commandDB);
						AssetDatabase.SaveAssets();
					}
					YarnCommandElement customCommand = new YarnCommandElement();
					string inner = line.Trim();
					if (inner.StartsWith("<<") && inner.EndsWith(">>"))
						inner = inner.Substring(2, inner.Length - 4).Trim();
					customCommand.commandName = inner;
					node.elements.Add(customCommand);
					if (!graph.YarnCommands.Contains(inner))
						graph.YarnCommands.Add(inner);
					i++;
					continue;
				}
				if (line.StartsWith("<<set "))
				{
					CreateAndAddVariableSetElement(node, line);
					i++;
					if (i < content.Count && content[i].Trim().StartsWith("<<jump"))
					{
						string jumpLine = content[i].Trim();
						string jumpTarget = jumpLine.Substring(7, jumpLine.Length - 9).Trim();
						if (titleToNode.TryGetValue(jumpTarget, out DialogueNode targetNode))
							node.GetOutputPort("output").Connect(targetNode.GetInputPort("input"));
						else
							Debug.LogWarning($"Jump target '{jumpTarget}' not found for node '{node.nodeTitle}'");
						i++;
					}
					continue;
				}
				else if (line.StartsWith("<<declare"))
				{
					i++;
					continue;
				}
				else if (line.StartsWith("<<wait"))
				{
					YarnWaitElement waitElement = new YarnWaitElement();
					Match m = Regex.Match(line, @"<<wait\s+([\d\.]+)>>");
					if (m.Success && float.TryParse(m.Groups[1].Value,
						   System.Globalization.NumberStyles.Float,
						   System.Globalization.CultureInfo.InvariantCulture,
						   out float duration))
						waitElement.duration = duration;
					node.elements.Add(waitElement);
					i++;
					continue;
				}
				else if (line.StartsWith("<<run"))
				{
					YarnCommandElement commandElement = new YarnCommandElement();
					Match m = Regex.Match(line, @"<<run\s+""(.*?)""\s*>>");
					if (m.Success)
					{
						commandElement.commandName = "run \"" + m.Groups[1].Value + "\"";
						if (!graph.YarnCommands.Contains(commandElement.commandName))
							graph.YarnCommands.Add(commandElement.commandName);
					}
					node.elements.Add(commandElement);
					i++;
					continue;
				}

				// ---- IF-BLOCK HANDLING ----
				else if (line.StartsWith("<<if"))
				{
					// Get the if-condition.
					int ifEnd = line.IndexOf(">>");
					string conditionText = line.Substring(4, ifEnd - 4).Trim();
					i++;

					// Gather all lines until the matching <<endif>>.
					List<string> blockLines = new List<string>();
					while (i < content.Count && !content[i].Trim().StartsWith("<<endif>>"))
					{
						// Add all non-comment lines.
						if (!content[i].Trim().StartsWith("///"))
							blockLines.Add(content[i]);
						i++;
					}
					if (i < content.Count && content[i].Trim().StartsWith("<<endif>>"))
						i++;

					// Split the block into a true-part and false-part (if an <<else>> exists).
					int elseIndex = blockLines.FindIndex(l => l.Trim().StartsWith("<<else>>"));
					List<string> truePart, falsePart;
					if (elseIndex >= 0)
					{
						truePart = blockLines.Take(elseIndex).ToList();
						falsePart = blockLines.Skip(elseIndex + 1).ToList();
					}
					else
					{
						truePart = blockLines;
						falsePart = new List<string>();
					}

					// Check: if the true part contains at least one decision option at its top level,
					// then import it as an if‑block wrapping a nested decision.
					int decisionIndex = truePart.FindIndex(l => l.Trim().StartsWith("->"));

					if (decisionIndex >= 0)
					{
						// Create an IfDialogueElement for the outer if.
						var ifDialogue = new IfDialogueElement();
						ifDialogue.condition = ParseCompoundCondition(conditionText);

						// Process the dialogue that comes before the decision block.
						List<DialogueElement> trueElements = new List<DialogueElement>();
						List<string> preDecision = truePart.Take(decisionIndex).ToList();
						trueElements.AddRange(ParseBranchLines(preDecision, graph, commandDB));

						// Process the rest of the true branch as a nested decision.
						List<string> decisionLines = truePart.Skip(decisionIndex).ToList();
						DecisionElement nestedDecision = ParseDecisionElement("", decisionLines, graph, commandDB);
						// *** In Solution 1 we do NOT merge the outer condition into the nested decision options ***
						trueElements.Add(nestedDecision);
						ifDialogue.IfTrue = trueElements;

						// Process the else block (if any) normally.
						ifDialogue.IfFalse = ParseBranchLines(falsePart, graph, commandDB);
						node.elements.Add(ifDialogue);
					}
					else
					{
						// No decision lines in the true branch: import as a plain IfDialogueElement.
						var ifDialogue = new IfDialogueElement();
						ifDialogue.condition = ParseCompoundCondition(conditionText);
						ifDialogue.IfTrue = ParseBranchLines(truePart, graph, commandDB);
						ifDialogue.IfFalse = ParseBranchLines(falsePart, graph, commandDB);
						node.elements.Add(ifDialogue);
					}
					continue;
				}

				// ---- NEW DECISION BLOCK HANDLING ----
				else if (line.StartsWith("->"))
				{
					List<DecisionOption> decisionOptions = new List<DecisionOption>();
					while (i < content.Count && content[i].Trim().StartsWith("->"))
					{
						List<string> optionBlock = new List<string>();
						optionBlock.Add(content[i]);
						i++;
						while (i < content.Count)
						{
							string nextLine = content[i];
							if (IsIndentedOptionLine(nextLine))
							{
								optionBlock.Add(nextLine);
								i++;
							}
							else if (!string.IsNullOrWhiteSpace(nextLine) && !IsCommandIndicator(nextLine.Trim()))
							{
								break;
							}
							else
							{
								break;
							}
						}
						DecisionOption opt = ParseDecisionOptionBlock(optionBlock);
						decisionOptions.Add(opt);
					}
					if (decisionOptions.Count >= 2)
					{
						DecisionElement decisionElement = new DecisionElement();
						decisionElement.options = decisionOptions;
						node.elements.Add(decisionElement);
					}
					else if (decisionOptions.Count == 1)
					{
						node.elements.AddRange(decisionOptions[0].dialogueElements);
					}
					continue;
				}
				else if (line.StartsWith("<<jump"))
				{
					string jumpTarget = line.Substring(7, line.Length - 9).Trim();
					if (titleToNode.TryGetValue(jumpTarget, out DialogueNode targetNode))
						node.GetOutputPort("output").Connect(targetNode.GetInputPort("input"));
					else
						Debug.LogWarning($"Jump target '{jumpTarget}' not found for node '{node.nodeTitle}'");
					if (topLevelJumpCount > 1)
					{
						var jumpElement = new JumpElement
						{
							jumpMode = JumpElement.JumpMode.Manual,
							NodeName = jumpTarget
						};
						node.elements.Add(jumpElement);
					}
					i++;
				}
				else if (!IsCommandIndicator(line))
				{
					if (!string.IsNullOrWhiteSpace(line))
					{
						DialogueTextElement textElement = ParseDialogueLine(line);
						node.elements.Add(textElement);
						ExtractAndAddInlineTags(line, graph);
					}
					i++;
				}
				else
				{
					i++;
				}
			}
		}



		private static bool IsCommandIndicator(string line)
		{
			return line.StartsWith("->") || line.StartsWith("<<");
		}

		private static DialogueTextElement ParseDialogueLine(string line)
		{
			if (line.StartsWith("\\") && !line.StartsWith("///"))
			{
				DialogueTextElement element = new DialogueTextElement();
				element.text = line;
				return element;
			}
			DialogueTextElement elementDefault = new DialogueTextElement();
			Regex voRegex = new Regex(@"^(.*?)(\s+#line:(\S+))$");
			Match voMatch = voRegex.Match(line);
			if (voMatch.Success)
			{
				line = voMatch.Groups[1].Value.Trim();
				elementDefault.voiceOverID = voMatch.Groups[3].Value.Trim();
				elementDefault.UseVoiceOver = true;
			}
			Regex varRegex = new Regex(@"^\{\s*\$(\w+)\s*\}:\s*(.*)$");
			Match varMatch = varRegex.Match(line);
			if (varMatch.Success)
			{
				elementDefault.character.useVariable = true;
				elementDefault.character.variableName = varMatch.Groups[1].Value;
				elementDefault.character.constantName = "";
				elementDefault.text = varMatch.Groups[2].Value;
				return elementDefault;
			}
			int colonIndex = line.IndexOf(':');
			if (colonIndex != -1)
			{
				string potentialName = line.Substring(0, colonIndex).Trim();
				string dialogue = line.Substring(colonIndex + 1).Trim();
				if (!string.IsNullOrEmpty(potentialName))
				{
					elementDefault.character.useVariable = false;
					elementDefault.character.constantName = potentialName;
				}
				elementDefault.text = dialogue;
				return elementDefault;
			}
			elementDefault.text = line;
			return elementDefault;
		}

		private static void CreateAndAddVariableSetElement(DialogueNode node, string line)
		{
			string inner = line.Substring(5).Trim();
			if (inner.StartsWith("$"))
				inner = inner.Substring(1).Trim();
			int closeIndex = inner.IndexOf(">>");
			if (closeIndex >= 0)
				inner = inner.Substring(0, closeIndex).Trim();
			string[] assignments = inner.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
			if (assignments.Length == 0)
			{
				Debug.LogWarning($"No assignments found in set command: {line}");
				return;
			}
			foreach (string assignment in assignments)
			{
				string[] parts = assignment.Split('=');
				if (parts.Length != 2)
					parts = assignment.Split(new string[] { " to " }, StringSplitOptions.None);
				if (parts.Length == 2)
				{
					string varName = parts[0].Trim();
					string varValue = parts[1].Trim();
					var assignmentElement = new YarnVariableAssignmentElement();
					assignmentElement.variableName = varName;
					assignmentElement.value = varValue;
					assignmentElement.isDeclaration = false;
					node.elements.Add(assignmentElement);
				}
				else
				{
					Debug.LogWarning($"Invalid variable assignment in set command: {assignment}");
				}
			}
		}

		private static List<DecisionOption> ParseOptionsFromBlock(string conditionText, List<string> lines)
		{
			var result = new List<DecisionOption>();
			int i = 0;
			while (i < lines.Count)
			{
				string line = lines[i].Trim();
				if (line.StartsWith("->"))
				{
					string optionText = line.Substring(2).Trim();
					DialogueTextElement optionTextElement = ParseDialogueLine(optionText);
					i++;
					var elements = new List<DialogueElement> { optionTextElement };
					string jumpTarget = null;
					while (i < lines.Count)
					{
						string nextLine = lines[i].Trim();
						if (nextLine.StartsWith("->") ||
							nextLine.StartsWith("<<if") ||
							nextLine.StartsWith("<<else") ||
							nextLine.StartsWith("<<endif"))
						{
							break;
						}
						if (nextLine.StartsWith("<<jump"))
						{
							jumpTarget = nextLine.Substring(7, nextLine.Length - 9).Trim();
							var jumpElement = new JumpElement
							{
								jumpMode = JumpElement.JumpMode.UsePort,
								portName = $"JumpTo_{jumpTarget}",
								NodeName = jumpTarget
							};
							elements.Add(jumpElement);
							i++;
						}
						else
						{
							DialogueTextElement textElem = ParseDialogueLine(nextLine);
							elements.Add(textElem);
							i++;
						}
					}
					DecisionOption opt = new DecisionOption
					{
						jumpTargetTitle = jumpTarget,
						condition = !string.IsNullOrEmpty(conditionText) ? ParseCompoundCondition(conditionText) : null,
						dialogueElements = elements
					};
					result.Add(opt);
				}
				else
				{
					i++;
				}
			}
			return result;
		}

		private static List<DecisionOption> ParseMultiBranchOptions(string initialCondition, List<string> blockLines)
		{
			var branches = new List<(string condition, List<string> lines)>();
			branches.Add((initialCondition, new List<string>()));
			foreach (var line in blockLines)
			{
				if (line.StartsWith("<<elseif"))
				{
					int start = line.IndexOf("<<elseif") + 8;
					int end = line.IndexOf(">>");
					string cond = line.Substring(start, end - start).Trim();
					branches.Add((cond, new List<string>()));
				}
				else if (line.StartsWith("<<else>>"))
				{
					branches.Add((null, new List<string>()));
				}
				else
				{
					if (branches.Count > 0)
						branches[branches.Count - 1].lines.Add(line);
				}
			}
			var allBranchOptions = new List<List<DecisionOption>>();
			foreach (var branch in branches)
			{
				var opts = ParseOptionsFromBlock(branch.condition, branch.lines);
				if (!string.IsNullOrEmpty(branch.condition) && opts.Count > 1)
				{
					for (int i = 1; i < opts.Count; i++)
						opts[i].condition = null;
				}
				allBranchOptions.Add(opts);
			}
			Dictionary<(string, string), ICondition> merged = new Dictionary<(string, string), ICondition>();
			List<(string, string)> order = new List<(string, string)>();
			foreach (var branchOptions in allBranchOptions)
			{
				foreach (var opt in branchOptions)
				{
					string optionKey = GetOptionText(opt);
					var key = (optionKey, opt.jumpTargetTitle);
					if (!merged.ContainsKey(key))
					{
						merged[key] = opt.condition;
						order.Add(key);
					}
					else
					{
						if (opt.condition == null || merged[key] == null)
							merged[key] = null;
					}
				}
			}
			List<DecisionOption> result = new List<DecisionOption>();
			foreach (var key in order)
			{
				result.Add(new DecisionOption
				{
					dialogueElements = new List<DialogueElement>() { new DialogueTextElement { text = key.Item1 } },
					jumpTargetTitle = key.Item2,
					condition = merged[key] ?? new CompoundCondition()
				});
			}
			return result;
		}

		private static string GetOptionText(DecisionOption option)
		{
			if (option.dialogueElements != null && option.dialogueElements.Count > 0 && option.dialogueElements[0] is DialogueTextElement textElement)
				return textElement.text;
			return "";
		}

		private static CompoundCondition ParseCompoundCondition(string conditionText)
		{
			var cond = new CompoundCondition();
			if (string.IsNullOrWhiteSpace(conditionText))
				return cond;
			if (conditionText.Contains("&&"))
			{
				cond.logicOperator = LogicOperator.AND;
				string[] parts = conditionText.Split(new string[] { "&&" }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var part in parts)
				{
					ICondition atomic = ParseAtomicCondition(part.Trim());
					if (atomic != null)
						cond.conditions.Add(atomic);
				}
			}
			else if (conditionText.Contains("||"))
			{
				cond.logicOperator = LogicOperator.OR;
				string[] parts = conditionText.Split(new string[] { "||" }, StringSplitOptions.RemoveEmptyEntries);
				foreach (var part in parts)
				{
					ICondition atomic = ParseAtomicCondition(part.Trim());
					if (atomic != null)
						cond.conditions.Add(atomic);
				}
			}
			else
			{
				cond.logicOperator = LogicOperator.AND;
				ICondition atomic = ParseAtomicCondition(conditionText.Trim());
				if (atomic != null)
					cond.conditions.Add(atomic);
			}
			return cond;
		}

		private static List<DialogueElement> ParseBranchLines(List<string> lines, YarnDialogueGraph graph, CustomCommandDatabase commandDB)
		{
			List<DialogueElement> elements = new List<DialogueElement>();
			foreach (string line in lines)
			{
				string trimmed = line.Trim();
				if (string.IsNullOrEmpty(trimmed))
					continue;
				if (trimmed.StartsWith("<<jump"))
				{
					string jumpTarget = trimmed.Substring(7, trimmed.Length - 9).Trim();
					JumpElement jumpElem = new JumpElement()
					{
						jumpMode = JumpElement.JumpMode.UsePort,
						portName = $"JumpTo_{jumpTarget}",
						NodeName = jumpTarget
					};
					elements.Add(jumpElem);
				}
				else if (IsCustomCommandLine(trimmed, out string prefix))
				{
					if (!commandDB.customCommands.Contains(prefix))
					{
						commandDB.customCommands.Add(prefix);
						EditorUtility.SetDirty(commandDB);
						AssetDatabase.SaveAssets();
					}
					YarnCommandElement customCommand = new YarnCommandElement();
					string inner = trimmed;
					if (inner.StartsWith("<<") && inner.EndsWith(">>"))
						inner = inner.Substring(2, inner.Length - 4).Trim();
					customCommand.commandName = inner;
					elements.Add(customCommand);
					if (!graph.YarnCommands.Contains(inner))
						graph.YarnCommands.Add(inner);
				}
				else if (trimmed.StartsWith("<<set "))
				{
					var assignmentElement = new YarnVariableAssignmentElement();
					string inner = trimmed.Substring(5).Trim();
					if (inner.StartsWith("$"))
						inner = inner.Substring(1).Trim();
					int closeIndex = inner.IndexOf(">>");
					if (closeIndex >= 0)
						inner = inner.Substring(0, closeIndex).Trim();
					string[] parts = inner.Split('=');
					if (parts.Length != 2)
						parts = inner.Split(new string[] { " to " }, StringSplitOptions.None);
					if (parts.Length == 2)
					{
						assignmentElement.variableName = parts[0].Trim();
						assignmentElement.value = parts[1].Trim();
					}
					else
					{
						Debug.LogWarning($"Invalid variable assignment in set command: {trimmed}");
					}
					elements.Add(assignmentElement);
				}
				else if (trimmed.StartsWith("<<wait"))
				{
					YarnWaitElement waitElement = new YarnWaitElement();
					Match m = Regex.Match(trimmed, @"<<wait\s+([\d\.]+)>>");
					if (m.Success && float.TryParse(m.Groups[1].Value, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float duration))
						waitElement.duration = duration;
					elements.Add(waitElement);
				}
				else if (!IsCommandIndicator(trimmed))
				{
					DialogueTextElement textElement = ParseDialogueLine(trimmed);
					ExtractAndAddInlineTags(trimmed, graph);
					elements.Add(textElement);
				}
			}
			return elements;
		}


		private static string CleanNodeTitle(string title)
		{
			return title.Replace(" ", "");
		}

		private static IEnumerator ProcessVoiceOverClipsCoroutine(YarnDialogueGraph graph)
		{
			foreach (var node in graph.nodes.OfType<DialogueNode>())
			{
				yield return ProcessVoiceOverClipsForElements(node.elements, graph);
				yield return null;
			}
			yield break;
		}

		private static IEnumerator ProcessVoiceOverClipsForElements(List<DialogueElement> elements, YarnDialogueGraph graph)
		{
			foreach (var element in elements)
			{
				if (element is DialogueTextElement textElement)
				{
					string[] lines = textElement.text.Split(new[] { '\n' }, StringSplitOptions.None);
					textElement.voiceOverClips.Clear();
					for (int i = 0; i < lines.Length; i++)
					{
						if (i % 10 == 0)
							yield return null;
						if (!string.IsNullOrEmpty(textElement.voiceOverID))
						{
							string clipName = textElement.voiceOverID;
							string[] guids = AssetDatabase.FindAssets($"{clipName} t:AudioClip");
							AudioClip foundClip = null;
							if (guids.Length > 0)
							{
								string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
								foundClip = AssetDatabase.LoadAssetAtPath<AudioClip>(assetPath);
							}
							textElement.voiceOverClips.Add(foundClip);
							if (foundClip != null)
								textElement.UseVoiceOver = true;
						}
						else
						{
							textElement.voiceOverClips.Add(null);
						}
					}
				}
				else if (element is IfDialogueElement ifElement)
				{
					yield return ProcessVoiceOverClipsForElements(ifElement.IfTrue, graph);
					yield return ProcessVoiceOverClipsForElements(ifElement.IfFalse, graph);
				}
			}
		}

		private static void ProcessVoiceOverClips(YarnDialogueGraph graph)
		{
			EditorCoroutineUtility.StartCoroutineOwnerless(ProcessVoiceOverClipsCoroutine(graph));
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

		private static void ExtractAndAddInlineTags(string line, YarnDialogueGraph graph)
		{
			if (graph.YarnInlineTags == null)
				graph.YarnInlineTags = new List<string>();
			var matches = Regex.Matches(line, @"#\S+");
			foreach (Match m in matches)
			{
				string tag = m.Value;
				if (tag.StartsWith("#line", StringComparison.OrdinalIgnoreCase))
					continue;
				if (!graph.YarnInlineTags.Contains(tag))
					graph.YarnInlineTags.Add(tag);
			}
		}
	}
}
#endif
#endif