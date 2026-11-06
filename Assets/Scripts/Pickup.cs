using UnityEngine;

public abstract class Pickup : MonoBehaviour
{
    private void OnTriggerEnter2D(Collider2D other)
    {
        var character = other.GetComponentInParent<Character>();
        if (character == null) return;

        if (Collect(character))
        {
            Destroy(gameObject);
        }
    }

    // Devuelve false si el item no se puede recoger ahora (se queda en el suelo)
    protected abstract bool Collect(Character character);
}
