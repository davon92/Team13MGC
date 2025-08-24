using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(Selectable))]
public class HoverSelect : MonoBehaviour, IPointerEnterHandler, IPointerMoveHandler
{
    public void OnPointerEnter(PointerEventData e) => Select();
    public void OnPointerMove (PointerEventData e) => Select();

    private void Select()
    {
        if (EventSystem.current &&
            EventSystem.current.currentSelectedGameObject != gameObject)
        {
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }
}