#if XNODE
#if UNITY_EDITOR
namespace Arawn.YarnGraph
{
	[System.Serializable]
	public abstract class DialogueElement {
		// Converts the element to a Yarn Script representation.
		public abstract string ToYarnString();
	}
}
#endif
#endif