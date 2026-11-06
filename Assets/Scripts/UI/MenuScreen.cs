using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Pantalla de menú navegable con teclado/gamepad: al mostrarse selecciona el primer botón,
// lo recupera si se pierde la selección (clic en el fondo) y, si tiene cursor, lo pone
// a la izquierda del botón seleccionado.
public class MenuScreen : MonoBehaviour
{
    [SerializeField] private Selectable _firstSelected;
    [SerializeField] private RectTransform _cursor; // opcional
    [SerializeField] private float _cursorGap = 2f;

    private void OnEnable() => SelectFirst();

    private void Update()
    {
        if (EventSystem.current == null) return;

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null || !selected.transform.IsChildOf(transform))
        {
            SelectFirst();
            return;
        }

        if (_cursor != null) PlaceCursor((RectTransform)selected.transform);
    }

    private void SelectFirst()
    {
        if (EventSystem.current != null && _firstSelected != null)
            EventSystem.current.SetSelectedGameObject(_firstSelected.gameObject);
    }

    private void PlaceCursor(RectTransform target)
    {
        Rect r = target.rect;
        _cursor.position = target.TransformPoint(new Vector3(r.xMin, r.center.y));
        _cursor.localPosition -= new Vector3(_cursor.rect.width * (1f - _cursor.pivot.x) + _cursorGap, 0f);
    }
}
