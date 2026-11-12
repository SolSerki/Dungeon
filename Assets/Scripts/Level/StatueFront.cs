using UnityEngine;

// Lets the slime walk behind a statue. "Front" is a copy of the statue that only shows
// inside its mask (the part of the statue that stands up). It lives on the
// "Statue Front" sorting layer: above the slime and its glow, and out of reach of the
// slime's own light. It only appears while the slime is behind the statue, so from
// there the statue hides the slime and stays dark against its light; from the front
// the statue is lit normally.
public class StatueFront : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _front;
    [Tooltip("Local Y of the line through the middle of the pedestal: above it, the slime is behind the statue.")]
    [SerializeField] private float _behindLine;
    [Tooltip("Seconds to fade the front in or out, so the light doesn't pop.")]
    [SerializeField] private float _fadeTime = 0.15f;

    private Transform _player;
    private float _alpha = -1f;

    private void Start()
    {
        var player = GameObject.FindWithTag("Player");
        if (player != null) _player = player.transform;
        SetAlpha(IsBehind() ? 1f : 0f);
    }

    private void LateUpdate()
    {
        float target = IsBehind() ? 1f : 0f;
        if (_alpha == target) return;
        SetAlpha(_fadeTime > 0f ? Mathf.MoveTowards(_alpha, target, Time.deltaTime / _fadeTime) : target);
    }

    private bool IsBehind() => _player != null && _player.position.y > transform.position.y + _behindLine;

    private void SetAlpha(float alpha)
    {
        _alpha = alpha;
        _front.enabled = alpha > 0f;
        Color c = _front.color;
        c.a = alpha;
        _front.color = c;
    }

    // The line in the Scene view (select the statue)
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Vector3 y = Vector3.up * _behindLine;
        Gizmos.DrawLine(transform.position + y + Vector3.left * 2f, transform.position + y + Vector3.right * 2f);
    }
}
