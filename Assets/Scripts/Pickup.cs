using UnityEngine;

public abstract class Pickup : MonoBehaviour
{
    [Header("Sounds")]
    [SerializeField] private AudioClip[] _collectSounds;   // they play from the slime: the pickup is destroyed instantly
    [SerializeField, Range(0f, 1f)] private float _collectVolume = 0.7f;

    private bool _collected;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // The slime has more than one collider: without this guard, each would pick it up in the same frame
        if (_collected) return;

        var character = other.GetComponentInParent<Character>();
        if (character == null) return;

        if (Collect(character))
        {
            _collected = true;
            character.PlaySound(_collectSounds, _collectVolume);
            Destroy(gameObject);
        }
    }

    // Returns false if the item can't be picked up right now (it stays on the ground)
    protected abstract bool Collect(Character character);
}
