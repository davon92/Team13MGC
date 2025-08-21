#if XNODE
#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using XNode;

namespace Arawn.YarnGraph
{
	[NodeWidth(500)]
	public class DialogueNode : YarnNode
	{
		[SerializeReference]
		public List<DialogueElement> elements = new List<DialogueElement>();

		[Output] public YarnNode output; // Single default output port
		[HideInInspector] public bool UseNodeTags = false;
		[HideInInspector] public List<string> nodeTags = new List<string>();

		public override object GetValue(NodePort port) => null;

		/// <summary>
		/// Recursively gather all JumpElements contained in this node’s elements.
		/// </summary>
		public List<JumpElement> GetAllJumpElements()
		{
			var jumps = new List<JumpElement>();
			foreach (DialogueElement rootElem in elements)
			{
				DialogueElementUtils.GatherJumpElements(rootElem, jumps);
			}
			return jumps;
		}

		public void UpdateJumpPorts()
		{
			// 1) Gather all JumpElements in the node (including nested ones)
			var allJumps = GetAllJumpElements();

			// 2) Build a set of needed port names
			HashSet<string> neededPortNames = new HashSet<string>();
			foreach (JumpElement jump in allJumps)
			{
				if (jump.jumpMode == JumpElement.JumpMode.UsePort &&
					!string.IsNullOrEmpty(jump.portName))
				{
					neededPortNames.Add(jump.portName);
				}
			}

			// 3) Remove stale dynamic output ports
			var existingDynamicOutputs = DynamicPorts; // or .Where(p => p.IsOutput)
			List<NodePort> toRemove = new List<NodePort>();
			foreach (NodePort port in existingDynamicOutputs)
			{
				if (port.IsOutput && !neededPortNames.Contains(port.fieldName))
					toRemove.Add(port);
			}
			foreach (NodePort port in toRemove)
			{
				RemoveDynamicPort(port);
			}

			// 4) Create (or reuse) the needed ports
			foreach (JumpElement jump in allJumps)
			{
				if (jump.jumpMode == JumpElement.JumpMode.UsePort &&
					!string.IsNullOrEmpty(jump.portName))
				{
					if (!HasPort(jump.portName))
					{
						AddDynamicOutput(typeof(YarnNode),
										 connectionType: ConnectionType.Override,
										 fieldName: jump.portName);
					}
					else
					{
						// Optional: if it’s connected, we can sync the connected node’s name
						NodePort p = GetPort(jump.portName);
						if (p != null && p.IsConnected)
						{
							NodePort c = p.Connection;
							if (c != null && c.node is YarnNode connectedNode)
							{
								jump.NodeName = connectedNode.name;
							}
						}
					}
				}
			}
		}
	}
}
#endif
#endif