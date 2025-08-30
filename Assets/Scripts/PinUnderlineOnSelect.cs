using UnityEngine;
using UnityEngine.EventSystems;

public class PinUnderlineOnSelect : MonoBehaviour, ISelectHandler
{
    [SerializeField] private UISelectScalerGroup group;

    public void Init(UISelectScalerGroup g) => group = g;

    public void OnSelect(BaseEventData eventData)
    {
        if (!group) group = GetComponentInParent<UISelectScalerGroup>(true);
        group?.ForceSelect(transform, true);
    }
}