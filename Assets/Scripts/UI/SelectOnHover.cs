using UnityEngine;
using UnityEngine.EventSystems;

// Pasar el mouse por encima selecciona el botón: así nunca quedan dos resaltados a la vez
// (uno por hover y otro por la selección del teclado) y el cursor del menú lo sigue.
public class SelectOnHover : MonoBehaviour, IPointerEnterHandler
{
    public void OnPointerEnter(PointerEventData eventData) => EventSystem.current.SetSelectedGameObject(gameObject);
}
