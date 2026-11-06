using UnityEngine;

// Copia el sprite (frame actual), flip y color del renderer del cuerpo a este renderer
// de "brillo". Como el sprite es el mismo, Unity le pasa la Secondary Texture
// _EmissionTex de ese frame al material de los ojos.
[RequireComponent(typeof(SpriteRenderer))]
public class SpriteGlowFollower : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _source;

    private SpriteRenderer _spriteRenderer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_source == null && transform.parent != null)
            _source = transform.parent.GetComponent<SpriteRenderer>();
    }

    // LateUpdate corre después del Animator: copiamos el frame ya actualizado
    private void LateUpdate()
    {
        if (_source == null) return;

        _spriteRenderer.enabled = _source.enabled;
        _spriteRenderer.sprite = _source.sprite;
        _spriteRenderer.flipX = _source.flipX;
        _spriteRenderer.flipY = _source.flipY;
        _spriteRenderer.color = _source.color;
    }
}
