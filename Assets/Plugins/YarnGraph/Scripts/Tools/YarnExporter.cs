#if XNODE
#if UNITY_EDITOR
using System.Text;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using XNode;
using UnityEditor;
using UnityEngine;
using System;

namespace Arawn.YarnGraph
{
	public static class YarnExporter
	{
		// Global set to track which nodes (VariableSetNodes, DecisionNodes, and inlined DialogueNodes) have been inlined.
		private static HashSet<YarnNode> inlinedSetNodes;

		public static string Export(YarnDialogueGraph graph)
		{
			inlinedSetNodes = new HashSet<YarnNode>();
			StringBuilder sb = new StringBuilder();

			// Deduplicate variable declarations.
			var uniqueDeclarations = graph.declaredVariables != null
				? graph.declaredVariables
					.GroupBy(v => new { v.variableName, v.initialValue })
					.Select(g => g.First())
				: Enumerable.Empty<YarnVariable>();

			string variableDeclarations = string.Join("\n", uniqueDeclarations.Select(FormatVariableDeclaration));

			// (1) Attempt top-left titled node with no inputs
			YarnNode forcedStart = FindTopLeftTitledNoInputNode(graph);

			// (2) If none, fallback: first DialogueNode with no input
			DialogueNode fallbackStart = graph.nodes
				.OfType<DialogueNode>()
				.FirstOrDefault(dn => dn.GetInputPort("input") == null || dn.GetInputPort("input").ConnectionCount == 0);

			// The actual start node
			YarnNode actualStartNode = forcedStart ?? fallbackStart;

			// Gather all YarnNodes
			List<YarnNode> allNodes = graph.nodes.OfType<YarnNode>().ToList();

			// Remove the start node from the list if present
			if (actualStartNode != null)
			{
				allNodes.Remove(actualStartNode);
			}

			// Sort all the remaining nodes by their position (x then y)
			allNodes.Sort((a, b) =>
			{
				int compareX = a.position.x.CompareTo(b.position.x);
				if (compareX != 0) return compareX;
				return a.position.y.CompareTo(b.position.y);
			});

			// Now re-insert the start node at the front (if we had one)
			if (actualStartNode != null)
			{
				allNodes.Insert(0, actualStartNode);
			}

			// Finally, export in that new order
			foreach (var node in allNodes)
			{
				// Skip if we've inlined it
				if (inlinedSetNodes.Contains(node))
					continue;

				if (node is DialogueNode dialogueNode)
				{
					string nodeTitle = CleanNodeTitle(dialogueNode.nodeTitle);

					// Insert start meta comment for the DialogueNode.
					sb.AppendLine($"// DialogueNodeStart {nodeTitle}");
					sb.AppendLine($"title: {nodeTitle}");

					// Export tags
					if (dialogueNode.UseNodeTags && dialogueNode.nodeTags != null && dialogueNode.nodeTags.Count > 0)
					{
						sb.AppendLine("tags: " + string.Join(" ", dialogueNode.nodeTags));
					}

					sb.AppendLine("---");

					// Print variable declarations if this is the actual start node
					if (dialogueNode == actualStartNode)
					{
						sb.AppendLine(variableDeclarations);
					}

					// Export each DialogueElement
					foreach (var element in dialogueNode.elements)
					{
						if (element is DialogueTextElement textElement)
						{
							ExportDialogueTextElement(sb, textElement);
						}
						else if (element is YarnCommandElement commandElement)
						{
							sb.AppendLine(commandElement.ToYarnString());
						}
						else if (element is DecisionElement decElement)
						{
							// Inline DecisionElement meta data comments are added inside FormatDecisionElement.
							string decisionText = FormatDecisionElement(decElement, 0, nodeTitle);
							if (!string.IsNullOrEmpty(decisionText))
							{
								sb.AppendLine(decisionText);
							}
						}
						else if (element is IfDialogueElement ifElem)
						{
							ExportIfDialogueElement(sb, ifElem);
						}
						else
						{
							// fallback
							string elementText = element.ToYarnString().Trim();
							elementText = FixCharacterVariable(elementText);
							if (!string.IsNullOrEmpty(elementText))
							{
								sb.AppendLine(elementText);
							}
						}
					}

					// Output chain
					NodePort outputPort = dialogueNode.GetOutputPort("output");
					if (outputPort != null && outputPort.ConnectionCount > 0)
					{
						YarnNode nextNode = outputPort.GetConnection(0).node as YarnNode;
						GatherInlineChain(nextNode, out var inlinedContent, out var finalNode);

						foreach (var content in inlinedContent)
						{
							sb.AppendLine(content);
						}

						if (finalNode is DecisionNode dNode && string.IsNullOrEmpty(dNode.nodeTitle))
						{
							dNode.isMerged = true;
							inlinedSetNodes.Add(dNode);
							// Use the parent's nodeTitle for the inline decision
							string decisionContent = FormatDecisionNode(dNode, nodeTitle);
							if (!string.IsNullOrEmpty(decisionContent))
							{
								sb.AppendLine(decisionContent);
							}
						}
						else if (finalNode != null)
						{
							sb.AppendLine($"<<jump {CleanNodeTitle(finalNode.nodeTitle)}>>");
						}
					}

					sb.AppendLine("===");
					// Insert end meta comment for the DialogueNode.
					sb.AppendLine($"// DialogueNodeEnd {nodeTitle}");
					sb.AppendLine();
				}
				else if (node is VariableSetNode setNode && !string.IsNullOrEmpty(setNode.nodeTitle))
				{
					string nodeTitle = CleanNodeTitle(setNode.nodeTitle);

					// Insert start meta comment for the VariableSetNode.
					sb.AppendLine($"// VariableSetNodeStart {nodeTitle}");
					sb.AppendLine($"title: {nodeTitle}");
					sb.AppendLine("---");

					if (setNode.variables != null && setNode.variables.Count > 0)
					{
						foreach (var v in setNode.variables)
						{
							sb.AppendLine($"<<set ${v.variableName} = {v.initialValue}>>");
						}
					}

					NodePort outPort = setNode.GetOutputPort("output");
					if (outPort != null && outPort.ConnectionCount > 0)
					{
						YarnNode nextNode = outPort.GetConnection(0).node as YarnNode;
						if (nextNode != null)
							sb.AppendLine($"<<jump {CleanNodeTitle(nextNode.nodeTitle)}>>");
					}

					sb.AppendLine("===");
					// Insert end meta comment for the VariableSetNode.
					sb.AppendLine($"// VariableSetNodeEnd {nodeTitle}");
					sb.AppendLine();
				}
				// Full DecisionNodes (with a title) are now wrapped with meta comments including the node name and meta info.
				else if (node is DecisionNode decNode && !string.IsNullOrEmpty(decNode.nodeTitle))
				{
					string nodeTitle = CleanNodeTitle(decNode.nodeTitle);
					sb.AppendLine($"// DecisionNodeStart {nodeTitle}");
					// Add YarnDecisionInfo meta data for the DecisionNode.
					StringBuilder metaSB = new StringBuilder();
					metaSB.Append("// YarnDecisionInfo: OriginalOptions=");
					metaSB.Append(decNode.options.Count);
					metaSB.Append("; Options=[");
					List<string> optionInfo = new List<string>();
					foreach (var opt in decNode.options)
					{
						string label = GetOptionLabel(opt);
						string cond = (opt.condition != null && !string.IsNullOrEmpty(FormatCondition(opt.condition)))
							? FormatCondition(opt.condition)
							: "none";
						optionInfo.Add($"{label}: {cond}");
					}
					metaSB.Append(string.Join(", ", optionInfo));
					metaSB.Append("]");
					sb.AppendLine(metaSB.ToString());

					sb.AppendLine($"title: {nodeTitle}");
					sb.AppendLine("---");
					// Call FormatDecisionNode with includeMeta = false to avoid duplicate meta info.
					string decisionText = FormatDecisionNode(decNode, nodeTitle, false);
					if (!string.IsNullOrEmpty(decisionText))
					{
						sb.AppendLine(decisionText);
					}
					sb.AppendLine("===");
					sb.AppendLine($"// DecisionNodeEnd {nodeTitle}");
					sb.AppendLine();
				}
				// Export IfNodes wrapped with meta comments including the node name.
				else if (node is IfNode ifNode)
				{
					string nodeTitle = CleanNodeTitle(ifNode.nodeTitle);
					sb.AppendLine($"// IfNodeStart {nodeTitle}");
					sb.AppendLine($"title: {nodeTitle}");
					sb.AppendLine("---");
					sb.AppendLine($"<<if {FormatCondition(ifNode.condition)}>>");
					NodePort truePort = ifNode.GetOutputPort("trueOutput");
					if (truePort != null && truePort.ConnectionCount > 0)
					{
						YarnNode trueTarget = truePort.GetConnection(0).node as YarnNode;
						if (trueTarget != null)
							sb.AppendLine($"  <<jump {CleanNodeTitle(trueTarget.nodeTitle)}>>");
					}
					sb.AppendLine("<<else>>");
					NodePort falsePort = ifNode.GetOutputPort("falseOutput");
					if (falsePort != null && falsePort.ConnectionCount > 0)
					{
						YarnNode falseTarget = falsePort.GetConnection(0).node as YarnNode;
						if (falseTarget != null)
							sb.AppendLine($"  <<jump {CleanNodeTitle(falseTarget.nodeTitle)}>>");
					}
					sb.AppendLine("<<endif>>");
					sb.AppendLine("===");
					sb.AppendLine($"// IfNodeEnd {nodeTitle}");
					sb.AppendLine();
				}
				// Untitled DecisionNode, SetNode, DialogueNode => inlined
			}

			return sb.ToString().Trim();
		}

		#region Start Node

		/// <summary>
		/// Find a YarnNode with a non-empty title, zero inputs,
		/// and minimal (x, then y). Returns null if none found.
		/// </summary>
		private static YarnNode FindTopLeftTitledNoInputNode(YarnDialogueGraph graph)
		{
			var candidates = new List<YarnNode>();
			foreach (var node in graph.nodes.OfType<YarnNode>())
			{
				if (string.IsNullOrEmpty(node.nodeTitle))
					continue;

				var inPort = node.GetInputPort("input");
				if (inPort != null && inPort.ConnectionCount > 0)
					continue;

				candidates.Add(node);
			}

			if (candidates.Count == 0)
				return null;

			candidates.Sort((a, b) =>
			{
				int compareX = a.position.x.CompareTo(b.position.x);
				if (compareX != 0) return compareX;
				return a.position.y.CompareTo(b.position.y);
			});

			return candidates[0];
		}

		#endregion

		#region Helpers

		private static string FormatVariableDeclaration(YarnVariable v)
		{
			var temp = new YarnVariableAssignmentElement
			{
				variableName = v.variableName,
				value = v.initialValue,
				isDeclaration = true
			};
			return temp.ToYarnString();
		}

		private static void ExportDialogueTextElement(StringBuilder sb, DialogueTextElement textElement)
		{
			string charName = "";
			if (textElement.character.useVariable && !string.IsNullOrEmpty(textElement.character.variableName))
			{
				charName = "{$" + textElement.character.variableName + "}";
			}
			else if (!textElement.character.useVariable && !string.IsNullOrEmpty(textElement.character.constantName))
			{
				charName = textElement.character.constantName;
			}

			if (textElement.UseVoiceOver)
			{
				string[] lines = textElement.text.Split(new[] { '\n' }, StringSplitOptions.None);
				for (int i = 0; i < lines.Length; i++)
				{
					string lineText = lines[i];
					string lineId = "";
					if (i < textElement.voiceOverClips.Count && textElement.voiceOverClips[i] != null)
					{
						string clipName = textElement.voiceOverClips[i].name;
						lineId = $" #line:{clipName}";
					}
					if (string.IsNullOrEmpty(charName))
					{
						lineText = Regex.Replace(lineText, @"^\s*:\s*", "");
					}
					string finalLine = string.IsNullOrEmpty(charName)
						? lineText
						: $"{charName}: {lineText}";
					finalLine += lineId;
					finalLine = FixCharacterVariable(finalLine);
					sb.AppendLine(finalLine);
				}
			}
			else
			{
				string[] lines = textElement.text.Split(new[] { '\n' }, StringSplitOptions.None);
				foreach (string singleLine in lines)
				{
					string lineContent = singleLine;
					if (string.IsNullOrEmpty(charName))
					{
						lineContent = Regex.Replace(lineContent, @"^\s*:\s*", "");
					}
					string line = string.IsNullOrEmpty(charName)
						? lineContent
						: $"{charName}: {lineContent}";
					line = FixCharacterVariable(line);
					sb.AppendLine(line);
				}
			}
		}

		private static void ExportIfDialogueElement(StringBuilder sb, IfDialogueElement ifElem)
		{
			string conditionStr = FormatCondition(ifElem.condition);
			sb.AppendLine($"<<if {conditionStr}>>");

			// Process the "if true" branch with one level of indentation.
			foreach (var subElement in ifElem.IfTrue)
			{
				if (subElement is DecisionElement decisionElement)
				{
					// Call the updated FormatDecisionElement with indentLevel 1.
					string decisionText = FormatDecisionElement(decisionElement, 1);
					sb.Append(decisionText);
					sb.AppendLine();
				}
				else
				{
					string subText = subElement.ToYarnString().Trim();
					subText = FixCharacterVariable(subText);
					if (!string.IsNullOrEmpty(subText))
					{
						foreach (var line in subText.Split('\n'))
						{
							string lineContent = Regex.Replace(line, @"^\s*:\s*", "");
							sb.AppendLine("\t" + lineContent);
						}
					}
				}
			}

			// Process the "else" branch, if it exists.
			if (ifElem.IfFalse != null && ifElem.IfFalse.Count > 0)
			{
				sb.AppendLine("<<else>>");
				foreach (var subElement in ifElem.IfFalse)
				{
					if (subElement is DecisionElement decisionElement)
					{
						string decisionText = FormatDecisionElement(decisionElement, 1);
						sb.Append(decisionText);
						sb.AppendLine();
					}
					else
					{
						string subText = subElement.ToYarnString().Trim();
						subText = FixCharacterVariable(subText);
						if (!string.IsNullOrEmpty(subText))
						{
							foreach (var line in subText.Split('\n'))
							{
								string lineContent = Regex.Replace(line, @"^\s*:\s*", "");
								sb.AppendLine("\t" + lineContent);
							}
						}
					}
				}
			}
			sb.AppendLine("<<endif>>");
		}

		public static string CleanNodeTitle(string title)
		{
			return title.Replace(" ", "");
		}

		private static string FixCharacterVariable(string line)
		{
			return Regex.Replace(line, @"^\$(\w+):", @"{$$$1}:");
		}

		#endregion

		#region Inline Logic

		/// <summary>
		/// Gathers content from a chain of *untitled* YarnNodes (Dialogue, Decision, VarSet), 
		/// marking them as inlined, identifies the final non‐inlinable node. 
		/// </summary>
		private static void GatherInlineChain(YarnNode start, out List<string> inlinedContent, out YarnNode finalNode)
		{
			inlinedContent = new List<string>();
			finalNode = null;
			YarnNode current = start;

			while (current != null)
			{
				// Titled VarSet => do NOT inline
				if (current is VariableSetNode titledSet && !string.IsNullOrEmpty(titledSet.nodeTitle))
				{
					finalNode = current;
					break;
				}
				// Titled Decision => do NOT inline
				if (current is DecisionNode titledDecision && !string.IsNullOrEmpty(titledDecision.nodeTitle))
				{
					finalNode = current;
					break;
				}

				// Untitled Decision => inline
				if (current is DecisionNode decNode && string.IsNullOrEmpty(decNode.nodeTitle))
				{
					inlinedSetNodes.Add(decNode);
					// No text to gather
				}
				// Untitled VarSet => gather sets
				else if (current is VariableSetNode vsn && string.IsNullOrEmpty(vsn.nodeTitle))
				{
					if (vsn.variables != null)
					{
						foreach (var v in vsn.variables)
						{
							inlinedContent.Add($"<<set ${v.variableName} = {v.initialValue}>>");
						}
					}
					inlinedSetNodes.Add(vsn);
				}
				// Untitled Dialogue => gather text
				else if (current is DialogueNode dn && string.IsNullOrEmpty(dn.nodeTitle))
				{
					foreach (var element in dn.elements)
					{
						string text = element.ToYarnString().Trim();
						if (!string.IsNullOrEmpty(text))
						{
							inlinedContent.Add(text);
						}
					}
					inlinedSetNodes.Add(dn);
				}
				else
				{
					finalNode = current;
					break;
				}

				NodePort outPort = current.GetOutputPort("output");
				if (outPort == null || outPort.ConnectionCount == 0)
				{
					current = null;
				}
				else
				{
					current = outPort.GetConnection(0).node as YarnNode;
				}
			}
		}

		/// <summary>
		/// For inline VarSetNodes that have no title
		/// </summary>
		private static void GatherVariableSetChain(YarnNode start, out List<VariableSetNode> collectedSets, out YarnNode finalNode)
		{
			collectedSets = new List<VariableSetNode>();
			finalNode = null;
			YarnNode current = start;
			while (current is VariableSetNode vsn && string.IsNullOrEmpty(vsn.nodeTitle))
			{
				inlinedSetNodes.Add(vsn);
				collectedSets.Add(vsn);

				var outPort = vsn.GetOutputPort("output");
				if (outPort == null || outPort.ConnectionCount == 0)
				{
					current = null;
				}
				else
				{
					current = outPort.GetConnection(0).node as YarnNode;
				}
			}
			finalNode = current;
		}

		#endregion

		#region DecisionNode Export

		// Updated to use meta comments (including a YarnDecisionInfo line) for full DecisionNodes.
		// The new parameter includeMeta (default true) allows us to disable inner meta data when exporting full nodes.
		private static string FormatDecisionNode(DecisionNode dNode, string nodeTitle = "unknown", bool includeMeta = true)
		{
			List<DecisionOption> unconditional = new List<DecisionOption>();
			var conditionalGroups = new Dictionary<string, List<DecisionOption>>();
			var conditionalOrder = new List<string>();

			foreach (var opt in dNode.options)
			{
				if (opt == null) continue;
				string conditionStr = (opt.condition != null) ? FormatCondition(opt.condition) : "";
				if (!string.IsNullOrEmpty(conditionStr))
				{
					if (!conditionalGroups.ContainsKey(conditionStr))
					{
						conditionalGroups[conditionStr] = new List<DecisionOption>();
						conditionalOrder.Add(conditionStr);
					}
					conditionalGroups[conditionStr].Add(opt);
				}
				else
				{
					unconditional.Add(opt);
				}
			}

			StringBuilder sb = new StringBuilder();

			// If includeMeta is true (for inline decisions) then print the inner meta comments.
			if (includeMeta)
			{
				sb.AppendLine($"// DecisionNode in Node {nodeTitle} Start");
			}

			// For decisions with stripOtherOptionsIfTrue.
			if (dNode.stripOtherOptionsIfTrue)
			{
				foreach (var condition in conditionalOrder)
				{
					sb.AppendLine($"<<if {condition}>>");
					foreach (var opt in conditionalGroups[condition])
					{
						FormatOption(opt, sb, 0);
					}
					sb.AppendLine("<<endif>>");
				}
				foreach (var opt in unconditional)
				{
					FormatOption(opt, sb, 0);
				}
				if (includeMeta)
				{
					sb.AppendLine($"// DecisionNode in Node {nodeTitle} End");
				}
				return sb.ToString().Trim();
			}

			if (conditionalOrder.Count == 0)
			{
				foreach (var opt in unconditional)
				{
					FormatOption(opt, sb, 0);
				}
				if (includeMeta)
				{
					sb.AppendLine($"// DecisionNode in Node {nodeTitle} End");
				}
				return sb.ToString().Trim();
			}

			int n = conditionalOrder.Count;
			int numCombinations = 1 << n;

			Func<int, string> buildConditionString = combo =>
			{
				var parts = new List<string>();
				for (int j = 0; j < n; j++)
				{
					bool isTrue = (combo & (1 << j)) != 0;
					string cond = conditionalOrder[j];
					parts.Add(isTrue ? cond : "!(" + cond + ")");
				}
				return string.Join(" && ", parts);
			};

			Func<int, List<DecisionOption>> getOptionsToShow = combo =>
			{
				var result = new List<DecisionOption>(unconditional);
				for (int j = 0; j < n; j++)
				{
					bool isTrue = (combo & (1 << j)) != 0;
					string cond = conditionalOrder[j];
					if (isTrue && conditionalGroups.ContainsKey(cond))
					{
						result.AddRange(conditionalGroups[cond]);
					}
				}
				return result;
			};

			if (numCombinations > 0)
			{
				int firstCombo = numCombinations - 1;
				string ifCond = buildConditionString(firstCombo);
				sb.AppendLine("<<if " + ifCond + ">>");
				var firstOpts = getOptionsToShow(firstCombo);
				foreach (var opt in firstOpts)
				{
					FormatOption(opt, sb, 0);
				}

				for (int i = numCombinations - 2; i > 0; i--)
				{
					string elseifCond = buildConditionString(i);
					sb.AppendLine("<<elseif " + elseifCond + ">>");
					var iOpts = getOptionsToShow(i);
					foreach (var opt in iOpts)
					{
						FormatOption(opt, sb, 0);
					}
				}

				if (numCombinations > 1)
				{
					var elseOpts = getOptionsToShow(0);
					sb.AppendLine("<<else>>");
					foreach (var opt in elseOpts)
					{
						FormatOption(opt, sb, 0);
					}
				}
				sb.AppendLine("<<endif>>");
			}

			if (includeMeta)
			{
				sb.AppendLine($"// DecisionNode in Node {nodeTitle} End");
			}
			return sb.ToString().Trim();
		}

		private static void FormatOption(DecisionOption opt, StringBuilder sb, int indentLevel)
		{
			string indent = new string(' ', indentLevel);
			string label = GetOptionLabel(opt);
			if (string.IsNullOrEmpty(label))
				label = "Option";

			GatherVariableSetChain(opt.target, out var setNodes, out var finalNode);

			sb.AppendLine($"{indent}-> {label}");
			string subIndent = indent + "    ";
			foreach (var sn in setNodes)
			{
				if (sn.variables != null && sn.variables.Count > 0)
				{
					foreach (var v in sn.variables)
					{
						sb.AppendLine($"{subIndent}<<set ${v.variableName} = {v.initialValue}>>");
					}
				}
			}
			if (finalNode != null)
			{
				sb.AppendLine($"{subIndent}<<jump {CleanNodeTitle(finalNode.nodeTitle)}>>");
			}
		}

		private static string GetOptionLabel(DecisionOption opt)
		{
			if (opt.dialogueElements != null && opt.dialogueElements.Count > 0)
			{
				if (opt.dialogueElements[0] is DialogueTextElement textElement)
					return textElement.text.Trim();
				else
					return opt.dialogueElements[0].ToYarnString().Trim();
			}
			return "";
		}

		#endregion

		#region DecisionElement Export (inline)

		// Updated to include meta-information and markers for start/end.
		private static string FormatDecisionElement(DecisionElement decElement, int indentLevel = 0, string nodeTitle = "unknown")
		{
			var unconditional = new List<DecisionOption>();
			var conditionalGroups = new Dictionary<string, List<DecisionOption>>();
			var conditionalOrder = new List<string>();

			foreach (var opt in decElement.options)
			{
				if (opt == null) continue;
				string conditionStr = (opt.condition != null) ? FormatCondition(opt.condition) : "";
				if (!string.IsNullOrEmpty(conditionStr))
				{
					if (!conditionalGroups.ContainsKey(conditionStr))
					{
						conditionalGroups[conditionStr] = new List<DecisionOption>();
						conditionalOrder.Add(conditionStr);
					}
					conditionalGroups[conditionStr].Add(opt);
				}
				else
				{
					unconditional.Add(opt);
				}
			}

			StringBuilder sb = new StringBuilder();
			string baseIndent = new string('\t', indentLevel);

			// Add meta data comment for inline DecisionElements.
			StringBuilder commentSB = new StringBuilder();
			commentSB.Append(baseIndent);
			commentSB.Append("// YarnDecisionInfo: ");
			commentSB.Append("OriginalOptions=");
			commentSB.Append(decElement.options.Count);
			commentSB.Append("; Options=[");
			List<string> optionInfo = new List<string>();
			foreach (var opt in decElement.options)
			{
				string label = GetOptionLabel(opt);
				string cond = (opt.condition != null && !string.IsNullOrEmpty(FormatCondition(opt.condition)))
								? FormatCondition(opt.condition)
								: "none";
				optionInfo.Add($"{label}: {cond}");
			}
			commentSB.Append(string.Join(", ", optionInfo));
			commentSB.Append("]");
			sb.AppendLine(commentSB.ToString());

			// Mark the start of the DecisionElement.
			sb.AppendLine(baseIndent + $"// DecisionElement in Node {nodeTitle} Start");

			// If there are no conditional options, simply output all options.
			if (conditionalOrder.Count == 0)
			{
				foreach (var opt in unconditional)
				{
					FormatOptionElement(opt, sb, indentLevel + 1);
				}
				sb.AppendLine(baseIndent + $"// DecisionElement in Node {nodeTitle} End");
				return sb.ToString().TrimEnd();
			}

			int n = conditionalOrder.Count;
			int numCombinations = 1 << n;

			Func<int, string> buildConditionString = combo =>
			{
				var parts = new List<string>();
				for (int j = 0; j < n; j++)
				{
					bool isTrue = (combo & (1 << j)) != 0;
					string cond = conditionalOrder[j];
					parts.Add(isTrue ? cond : "!(" + cond + ")");
				}
				return string.Join(" && ", parts);
			};

			Func<int, List<DecisionOption>> getOptionsToShow = combo =>
			{
				var result = new List<DecisionOption>(unconditional);
				for (int j = 0; j < n; j++)
				{
					bool isTrue = (combo & (1 << j)) != 0;
					string cond = conditionalOrder[j];
					if (isTrue && conditionalGroups.ContainsKey(cond))
					{
						result.AddRange(conditionalGroups[cond]);
					}
				}
				return result;
			};

			if (numCombinations > 0)
			{
				int firstCombo = numCombinations - 1;
				string ifCond = buildConditionString(firstCombo);
				sb.AppendLine(baseIndent + "<<if " + ifCond + ">>");
				var firstOpts = getOptionsToShow(firstCombo);
				foreach (var opt in firstOpts)
				{
					FormatOptionElement(opt, sb, indentLevel + 1);
				}

				for (int i = numCombinations - 2; i > 0; i--)
				{
					string elseifCond = buildConditionString(i);
					sb.AppendLine(baseIndent + "<<elseif " + elseifCond + ">>");
					var iOpts = getOptionsToShow(i);
					foreach (var opt in iOpts)
					{
						FormatOptionElement(opt, sb, indentLevel + 1);
					}
				}

				if (numCombinations > 1)
				{
					var elseOpts = getOptionsToShow(0);
					sb.AppendLine(baseIndent + "<<else>>");
					foreach (var opt in elseOpts)
					{
						FormatOptionElement(opt, sb, indentLevel + 1);
					}
				}
				sb.AppendLine(baseIndent + "<<endif>>");
			}

			sb.AppendLine(baseIndent + $"// DecisionElement in Node {nodeTitle} End");

			return sb.ToString().TrimEnd();
		}

		private static void FormatOptionElement(DecisionOption opt, StringBuilder sb, int indentLevel)
		{
			string optionIndent = new string(' ', indentLevel * 4);
			string label = GetOptionLabel(opt);
			if (string.IsNullOrEmpty(label))
				label = "Option";

			sb.AppendLine($"{optionIndent}-> {label}");

			if (opt is IHasNestedElements nested && nested.NestedElements != null && nested.NestedElements.Count > 1)
			{
				string nestedIndent = new string(' ', (indentLevel + 1) * 4);
				for (int i = 1; i < nested.NestedElements.Count; i++)
				{
					string elemText = nested.NestedElements[i].ToYarnString().Trim();
					elemText = FixCharacterVariable(elemText);
					if (!string.IsNullOrEmpty(elemText))
					{
						sb.AppendLine($"{nestedIndent}{elemText}");
					}
				}
			}
		}
		#endregion

		#region Condition Formatting

		private static string FormatCondition(ICondition condition)
		{
			if (condition is SingleComparison sc)
			{
				string left = "$" + sc.leftVariable;
				string right = "";
				if (sc.isRightVariable)
				{
					// If it's a variable reference, prepend '$'
					right = "$" + sc.rightVariableOrValue;
				}
				else
				{
					// For literal values, if it's numeric or boolean, leave it as is.
					// Otherwise, wrap it in quotes if not already quoted.
					if (float.TryParse(sc.rightVariableOrValue, out _) || bool.TryParse(sc.rightVariableOrValue, out _))
					{
						right = sc.rightVariableOrValue;
					}
					else
					{
						if (!sc.rightVariableOrValue.StartsWith("\"") && !sc.rightVariableOrValue.EndsWith("\""))
						{
							right = "\"" + sc.rightVariableOrValue + "\"";
						}
						else
						{
							right = sc.rightVariableOrValue;
						}
					}
				}

				string opStr = sc.op switch
				{
					ComparisonOperator.Equals => "==",
					ComparisonOperator.NotEquals => "!=",
					ComparisonOperator.GreaterThan => ">",
					ComparisonOperator.LessThan => "<",
					ComparisonOperator.GreaterOrEqual => ">=",
					ComparisonOperator.LessOrEqual => "<=",
					_ => "=="
				};

				return $"{left} {opStr} {right}";
			}
			else if (condition is CompoundCondition cc)
			{
				return FormatCompoundCondition(cc);
			}
			else if (condition is VisitedCondition visited)
			{
				return visited.ToYarnScript();
			}
			else if (condition is VisitedCountCondition visitedCount)
			{
				return visitedCount.ToYarnScript();
			}
			else if (condition is NotCondition notCondition)
			{
				return notCondition.ToYarnScript();
			}
			return "";
		}

		private static string FormatCompoundCondition(ICondition condition)
		{
			if (condition is CompoundCondition cc && cc.conditions.Count > 0)
			{
				string joiner = cc.logicOperator == LogicOperator.AND ? "&&" : "||";
				var parts = cc.conditions.Select(FormatCondition);
				return string.Join($" {joiner} ", parts);
			}
			else if (condition is SingleComparison sc)
			{
				return FormatCondition(sc);
			}
			return "";
		}

		private static SingleComparison ParseSingleComparison(string text)
		{
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
					sc.op = op switch
					{
						"==" => ComparisonOperator.Equals,
						"!=" => ComparisonOperator.NotEquals,
						">" => ComparisonOperator.GreaterThan,
						"<" => ComparisonOperator.LessThan,
						">=" => ComparisonOperator.GreaterOrEqual,
						"<=" => ComparisonOperator.LessOrEqual,
						_ => ComparisonOperator.Equals
					};
					sc.isRightVariable = right.StartsWith("$");
					if (sc.isRightVariable)
						right = right.Substring(1).Trim();
					sc.rightVariableOrValue = right;
					return sc;
				}
			}
			return null;
		}

		#endregion

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
}
#endif
#endif