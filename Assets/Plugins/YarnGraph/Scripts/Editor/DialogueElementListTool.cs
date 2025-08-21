#if XNODE
#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using System.Reflection;

namespace Arawn.YarnGraph
{
	public class DialogueElementListTool : VisualElement
	{
		private SerializedProperty m_Property;
		private ListView m_ListView;

		public DialogueElementListTool(SerializedProperty property)
		{
			m_Property = property;

			// Add button
			Button addButton = new Button(AddElement) { text = "Add Dialogue Element" };
			this.Add(addButton);

			// Initialize ListView
			m_ListView = new ListView
			{
				itemsSource = Enumerable.Range(0, m_Property.arraySize).ToList(),
				makeItem = MakeItem,
				bindItem = BindItem,
				reorderable = true,
				reorderMode = ListViewReorderMode.Animated,
				style = { flexGrow = 1 }
			};

			// Register DragPerformEvent to detect reordering completion
			m_ListView.RegisterCallback<DragPerformEvent>(evt => UpdateSerializedProperty());

			this.Add(m_ListView);

			RefreshList();
		}

		private VisualElement MakeItem()
		{
			// Container for each list item
			var container = new VisualElement
			{
				style = { flexDirection = FlexDirection.Column, marginBottom = 6 }
			};
			return container;
		}

		private void BindItem(VisualElement element, int index)
		{
			element.Clear();
			var elementProp = m_Property.GetArrayElementAtIndex(index);

			// Store the original index in userData
			element.userData = index;

			// Handle DialogueTextElement specifically
			if (elementProp.managedReferenceValue is DialogueTextElement)
			{
				var characterField = new PropertyField(elementProp.FindPropertyRelative("character"), "Character");
				characterField.Bind(m_Property.serializedObject);
				element.Add(characterField);

				var textProp = elementProp.FindPropertyRelative("text");
				var textField = new TextField("Text") { multiline = true, style = { minHeight = 60 } };
				textField.SetValueWithoutNotify(textProp.stringValue);
				textField.RegisterValueChangedCallback(evt =>
				{
					textProp.stringValue = evt.newValue;
					textProp.serializedObject.ApplyModifiedProperties();
				});
				element.Add(textField);
			}
			else
			{
				// Generic PropertyField for other types
				var field = new PropertyField(elementProp);
				field.Bind(m_Property.serializedObject);
				element.Add(field);
			}

			// Remove button
			var removeButton = new Button(() =>
			{
				m_Property.DeleteArrayElementAtIndex(index);
				m_Property.serializedObject.ApplyModifiedProperties();
				RefreshList();
			})
			{ text = "Remove" };
			element.Add(removeButton);
		}

		private void UpdateSerializedProperty()
		{
			// Get all item elements in their current visual order
			var items = m_ListView.Query(className: "unity-list-view__item").ToList();

			// Extract the original indices from userData
			var newOrderIndices = items.Select(item => (int)item.userData).ToList();

			// Create a new list with elements in the new order
			var newOrder = new List<object>();
			for (int i = 0; i < newOrderIndices.Count; i++)
			{
				int originalIndex = newOrderIndices[i];
				newOrder.Add(m_Property.GetArrayElementAtIndex(originalIndex).managedReferenceValue);
			}

			// Apply the new order to the serialized property
			for (int i = 0; i < newOrder.Count; i++)
			{
				m_Property.GetArrayElementAtIndex(i).managedReferenceValue = newOrder[i];
			}
			m_Property.serializedObject.ApplyModifiedProperties();

			// Update itemsSource to reflect the new order
			m_ListView.itemsSource = Enumerable.Range(0, m_Property.arraySize).ToList();
		}

		private void AddElement()
		{
			GenericMenu menu = new GenericMenu();
			Type baseType = typeof(DialogueElement);

			// Find all non-abstract types derived from DialogueElement
			IEnumerable<Type> types = AppDomain.CurrentDomain.GetAssemblies()
				.SelectMany(assembly =>
				{
					try
					{
						return assembly.GetTypes();
					}
					catch
					{
						return new Type[0];
					}
				})
				.Where(t => baseType.IsAssignableFrom(t) && !t.IsAbstract);

			foreach (Type type in types)
			{
				menu.AddItem(new GUIContent(type.Name), false, () =>
				{
					m_Property.arraySize++;
					SerializedProperty elementProp = m_Property.GetArrayElementAtIndex(m_Property.arraySize - 1);
					elementProp.managedReferenceValue = Activator.CreateInstance(type);
					m_Property.serializedObject.ApplyModifiedProperties();
					RefreshList();
				});
			}

			menu.ShowAsContext();
		}

		private void RefreshList()
		{
			m_ListView.itemsSource = Enumerable.Range(0, m_Property.arraySize).ToList();
			m_ListView.RefreshItems();
		}
	}

	public static class CursorUtils
	{
		public static UnityEngine.UIElements.Cursor CreateCursor(int defaultCursorId)
		{
			var cursor = new UnityEngine.UIElements.Cursor();
			var property = typeof(UnityEngine.UIElements.Cursor).GetProperty("defaultCursorId", BindingFlags.Instance | BindingFlags.NonPublic);
			if (property != null)
			{
				property.SetValue(cursor, defaultCursorId);
			}
			return cursor;
		}
	}
}

#endif
#endif