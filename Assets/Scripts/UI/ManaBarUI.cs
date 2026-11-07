using UnityEngine;
using UnityEngine.UI;

// Barra de maná: la barra vacía de fondo y encima la llena como Image "Filled" horizontal.
// El relleno avanza de a un píxel del sprite para que el corte no quede a medio píxel.
public class ManaBarUI : MonoBehaviour
{
    [SerializeField] private ManaSystem _mana;
    [SerializeField] private Image _fill;
    [SerializeField] private int _spriteWidth = 44;  // ancho en píxeles del sprite de la barra
    [SerializeField] private int _leftBorder = 5;    // píxeles del borde izquierdo, iguales en la barra llena y la vacía

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
        // Con algo de maná el relleno arranca después del borde, así siempre se ve un poco de azul
        int pixels = t <= 0f ? 0 : _leftBorder + Mathf.CeilToInt(t * (_spriteWidth - _leftBorder));
        _fill.fillAmount = (float)pixels / _spriteWidth;
    }
}
