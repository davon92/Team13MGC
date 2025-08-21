#if XNODE
#if UNITY_EDITOR
using System;

namespace Arawn.YarnGraph
{
	[AttributeUsage(AttributeTargets.Class, Inherited = false)]
	public sealed class ElementTitleAttribute : Attribute
	{
		public string Title { get; }

		public ElementTitleAttribute(string title)
		{
			this.Title = title;
		}
	}
}
#endif
#endif