using System.Collections;
using Unity.Cinemachine;
using UnityEngine;

// The first time the slime enters this trigger: it stops, the camera turns to look at
// whoever speaks (_lookCamera, an inactive CinemachineCamera), the box shows its text
// (the one written in the box's Text field), and when it closes the camera goes back to
// the slime and it can move again.
[RequireComponent(typeof(Collider2D))]
public class DialogueTrigger : MonoBehaviour
{
    [SerializeField] private DialogueBox _box;
    [Tooltip("Inactive camera with more priority than the slime's. Optional.")]
    [SerializeField] private CinemachineCamera _lookCamera;
    [Tooltip("Seconds the camera travels before the text starts.")]
    [SerializeField] private float _lookTime = 1f;

    private bool _done;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // The Character has two colliders: _done also stops the second one
        if (_done) return;
        var slime = other.GetComponentInParent<Character>();
        if (slime == null) return; // ignores enemies

        _done = true;
        StartCoroutine(Play(slime));
    }

    private IEnumerator Play(Character slime)
    {
        SetPlayerControl(slime, false);

        if (_lookCamera != null)
        {
            _lookCamera.gameObject.SetActive(true); // the CinemachineBrain blends towards it
            yield return new WaitForSeconds(_lookTime);
        }

        yield return _box.Show();

        if (_lookCamera != null) _lookCamera.gameObject.SetActive(false); // and blends back
        SetPlayerControl(slime, true);
    }

    // Movement lives in Character; spitting and the light trail have their own input
    private static void SetPlayerControl(Character slime, bool enabled)
    {
        slime.SetControlsEnabled(enabled);
        if (slime.TryGetComponent(out SlimeThrower thrower)) thrower.enabled = enabled;
        if (slime.TryGetComponent(out LightTrail trail)) trail.enabled = enabled;
    }
}
