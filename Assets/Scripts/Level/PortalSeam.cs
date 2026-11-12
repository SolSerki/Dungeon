using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;

// One end of an invisible maze seam. They come in pairs (A._partner = B and
// B._partner = A), each in the middle of a straight corridor that looks the same in both copies.
// Crossing this end's line in the _forward direction puts the traveler at the same
// relative point of the other end. Since the destination copy is already rotated, it's just a
// translation: neither velocity nor input change.
[RequireComponent(typeof(BoxCollider2D))]
public class PortalSeam : MonoBehaviour
{
    [SerializeField] private PortalSeam _partner;
    [Tooltip("On-screen direction in which THIS end must be crossed to go to the other. The partner has the opposite one.")]
    [SerializeField] private Vector2 _forward = Vector2.left;

    // (seam that was crossed, who crossed it, offset)
    public static event System.Action<PortalSeam, Rigidbody2D, Vector2> Warped;

    // A point just past the line: enemies head there to cross after the slime
    public Vector2 ChasePoint => (Vector2)transform.position + _forward.normalized;
    public PortalSeam Partner => _partner;
    public Vector2 Forward => _forward;

    // Which side of the line each traveler was on (negative = hasn't crossed yet)
    private readonly Dictionary<Rigidbody2D, float> _side = new();

    private float SideOf(Rigidbody2D rb) => Vector2.Dot(rb.position - (Vector2)transform.position, _forward);

    private static bool IsTraveler(Rigidbody2D rb) =>
        rb != null && (rb.CompareTag("Player") || rb.TryGetComponent(out EnemyMovement _));

    private void OnTriggerEnter2D(Collider2D other)
    {
        // The slime has two colliders: its Rigidbody is the key so it's counted only once
        var rb = other.attachedRigidbody;
        if (IsTraveler(rb) && !_side.ContainsKey(rb)) _side[rb] = SideOf(rb);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.attachedRigidbody != null) _side.Remove(other.attachedRigidbody);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        var rb = other.attachedRigidbody;
        if (!IsTraveler(rb) || _partner == null) return;
        // The slime's second collider arrives in the same step, after it was already sent away
        if (Vector2.Distance(rb.position, transform.position) > 3f) return;

        float now = SideOf(rb);
        if (_side.TryGetValue(rb, out float before) && before < 0f && now >= 0f)
        {
            Vector2 delta = _partner.transform.position - transform.position;
            Warp(rb, delta);
            _side.Remove(rb);
            _partner._side[rb] = -now; // on the other side it ends up just before the return line: no bouncing
            Warped?.Invoke(this, rb, delta);
            return;
        }
        _side[rb] = now;
    }

    private static void Warp(Rigidbody2D rb, Vector2 delta)
    {
        rb.position += delta;
        rb.transform.position += (Vector3)delta; // so the transform doesn't wait for the next physics step
        if (rb.TryGetComponent(out NavMeshAgent agent) && agent.enabled) agent.Warp(rb.position);
        if (rb.CompareTag("Player")) CinemachineCore.OnTargetObjectWarped(rb.transform, delta);
    }

    private void OnValidate()
    {
        if (_partner == null) return;
        Vector2 d = _partner.transform.position - transform.position;
        if (d != new Vector2(Mathf.Round(d.x), Mathf.Round(d.y)))
            Debug.LogWarning($"{name}: the distance to the partner must be in whole cells (pixel perfect).", this);
        if (Vector2.Dot(_forward.normalized, _partner._forward.normalized) > -0.99f)
            Debug.LogWarning($"{name}: the partner's _forward must be the opposite of this one.", this);
    }

    private void OnDrawGizmos()
    {
        Vector3 p = transform.position;
        Vector3 f = _forward.normalized;
        Vector3 across = new Vector3(-f.y, f.x) * 1f;

        Gizmos.color = new Color(1f, 0.4f, 1f, 0.9f);
        Gizmos.DrawLine(p - across, p + across);   // the seam line
        Gizmos.DrawLine(p, p + f * 0.8f);          // crossing direction
        Gizmos.DrawLine(p + f * 0.8f, p + f * 0.55f + across * 0.2f);
        Gizmos.DrawLine(p + f * 0.8f, p + f * 0.55f - across * 0.2f);

        if (_partner != null)
        {
            Gizmos.color = new Color(1f, 0.4f, 1f, 0.25f);
            Gizmos.DrawLine(p, _partner.transform.position);
        }
    }
}
