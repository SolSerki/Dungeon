using UnityEngine;
using UnityEngine.UI;

// Mana bar: the empty bar as background and the full one on top as a horizontal "Filled" Image.
// The fill advances one sprite pixel at a time so the cut never lands mid-pixel.
public class ManaBarUI : MonoBehaviour
{
    [SerializeField] private ManaSystem _mana;
    [SerializeField] private Image _fill;
    [SerializeField] private int _spriteWidth = 44;  // width of the bar sprite in pixels
    [SerializeField] private int _leftBorder = 5;    // left border pixels, the same in the full and the empty bar

    private void OnEnable()
    {
        if (_mana != null) _mana.OnManaChanged += Refresh;
    }

    private void OnDisable()
    {
        if (_mana != null) _mana.OnManaChanged -= Refresh;
    }

    private void Start()
    {
        if (_mana != null) Refresh(_mana.CurrentMana, _mana.MaxMana);
    }

    private void Refresh(float current, float max)
    {
        float t = max > 0f ? Mathf.Clamp01(current / max) : 0f;
        // With some mana the fill starts after the border, so a bit of blue always shows
        int pixels = t <= 0f ? 0 : _leftBorder + Mathf.CeilToInt(t * (_spriteWidth - _leftBorder));
        _fill.fillAmount = (float)pixels / _spriteWidth;
    }
}
