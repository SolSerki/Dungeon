using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// Mientras el slime corre, deja migas de luz en el piso cada cierta distancia.
// Las migas salen de un grupo fijo que se recicla: la cola mantiene el orden por edad,
// así que si se acaban se reutiliza la más vieja, que es la que ya se estaba apagando.
public class SprintLightTrail : MonoBehaviour
{
    [SerializeField] private Character _character;
    [SerializeField] private Light2D _slimeLight;      // las migas copian su color
    [SerializeField] private TrailCrumb _crumbPrefab;
    [SerializeField] private ParticleSystem _goo;      // opcional: la baba del piso
    [SerializeField] private float _spacing = 0.6f;    // distancia entre migas
    [SerializeField] private float _lifetime = 2f;
    [SerializeField] private int _poolSize = 20;       // ~ vida * velocidad de sprint / separación

    private readonly Queue<TrailCrumb> _pool = new();
    private Vector2 _lastDrop;
    private bool _wasSprinting;

    private void Awake()
    {
        if (_character == null) _character = GetComponent<Character>();
        if (_crumbPrefab == null)
        {
            Debug.LogWarning($"{name}: SprintLightTrail no tiene prefab de miga asignado.", this);
            enabled = false;
            return;
        }

        for (int i = 0; i < _poolSize; i++)
        {
            // Sin padre: las migas tienen que quedarse en el piso aunque el slime se mueva
            var crumb = Instantiate(_crumbPrefab);
            crumb.gameObject.SetActive(false);
            _pool.Enqueue(crumb);
        }
    }

    private void OnDestroy()
    {
        foreach (var crumb in _pool)
        {
            if (crumb != null) Destroy(crumb.gameObject);
        }
    }

    private void Update()
    {
        bool sprinting = _character != null && _character.IsSprinting;

        if (_goo != null)
        {
            var emission = _goo.emission;
            emission.enabled = sprinting;
        }

        if (!sprinting)
        {
            _wasSprinting = false;
            return;
        }

        Vector2 pos = transform.position;

        // Al arrancar a correr la primera miga cae recién después de recorrer _spacing
        if (!_wasSprinting)
        {
            _lastDrop = pos;
            _wasSprinting = true;
            return;
        }

        if ((pos - _lastDrop).sqrMagnitude < _spacing * _spacing) return;

        _lastDrop = pos;
        var next = _pool.Dequeue();   // la primera de la cola es la más vieja
        Color color = _slimeLight != null ? _slimeLight.color : Color.white;
        next.Spawn(pos, _lifetime, color);
        _pool.Enqueue(next);
    }
}
