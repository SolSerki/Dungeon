using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Menu screen navigable with keyboard/gamepad: when shown it selects the first button,
// restores it if the selection is lost (click on the background) and, if it has a cursor, places it
// to the left of the selected button.
public class MenuScreen : MonoBehaviour
{
    [SerializeField] private Selectable _firstSelected;
    [SerializeField] private RectTransform _cursor; // optional
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
