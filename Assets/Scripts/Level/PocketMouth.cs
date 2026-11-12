using UnityEngine;

// A pocket mouth: the open end of a corridor. They're joined in pairs: leaving through one
// is entering through the other. Put it near the end of the corridor (as a child of the Pocket); Build
// snaps it to the edge and detects which way it opens and how wide it is.
public class PocketMouth : MonoBehaviour
{
    [SerializeField] private PocketMouth _partner;
    [SerializeField, HideInInspector] private Vector2Int _direction; // pointing out of the corridor
    [SerializeField, HideInInspector] private int _width;

    public PocketMouth Partner
    {
        get => _partner;
        set => _partner = value;
    }

    public Vector2Int Direction => _direction;
    public int Width => _width;

    public void SetShape(Vector2Int direction, int width)
    {
        _direction = direction;
        _width = width;
    }

    private void OnDrawGizmos()
    {
        Vector3 p = transform.position;
        Gizmos.color = _partner != null ? new Color(1f, 0.4f, 1f, 0.95f) : new Color(1f, 0.3f, 0.3f, 0.95f);

        if (_direction == Vector2Int.zero)
        {
            Gizmos.DrawWireSphere(p, 0.4f); // not snapped yet: run Build
        }
        else
        {
            Vector3 f = (Vector2)_direction;
            Vector3 across = new Vector3(-f.y, f.x) * (_width * 0.5f);
            Gizmos.DrawLine(p - across, p + across);
            Gizmos.DrawLine(p, p + f);
            Gizmos.DrawLine(p + f, p + f * 0.7f + across.normalized * 0.25f);
            Gizmos.DrawLine(p + f, p + f * 0.7f - across.normalized * 0.25f);
        }

        if (_partner != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 1f, 0.3f);
            Gizmos.DrawLine(p, _partner.transform.position);
        }
#if UNITY_EDITOR
        UnityEditor.Handles.Label(p + Vector3.up * 0.6f, _partner != null ? $"{name} ↔ {_partner.name}" : $"{name} (unpaired)");
#endif
    }
}
