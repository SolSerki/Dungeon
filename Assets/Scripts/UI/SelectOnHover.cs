using UnityEngine;
using UnityEngine.EventSystems;

// Hovering selects the button: that way two buttons are never highlighted at once
// (one by hover and another by keyboard selection) and the menu cursor follows it.
public class SelectOnHover : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData) => EventSystem.current.SetSelectedGameObject(gameObject);
}
