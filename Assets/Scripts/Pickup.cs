using UnityEngine;

public abstract class Pickup : MonoBehaviour
{
    private bool _collected;

    private void OnTriggerEnter2D(Collider2D other)
    {
        // El slime tiene más de un collider: sin esta guarda, cada uno lo recogería en el mismo frame
        if (_collected) return;

        var character = other.GetComponentInParent<Character>();
        if (character == null) return;

        if (Collect(character))
        {
            _collected = true;
            Destroy(gameObject);
        }
    }

    // Devuelve false si el item no se puede recoger ahora (se queda en el suelo)
    protected abstract bool Collect(Character character);
}
