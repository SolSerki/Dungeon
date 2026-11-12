using UnityEngine;

// Copies the sprite (current frame), flip and color from the body renderer to this
// "glow" renderer. Since the sprite is the same, Unity passes that frame's
// _EmissionTex Secondary Texture to the eyes material.
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

    // LateUpdate runs after the Animator: copy the already updated frame
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
