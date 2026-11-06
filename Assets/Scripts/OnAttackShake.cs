using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachineImpulseSource))]
public class ScreenShakeOnAttack: MonoBehaviour
{
    [SerializeField] private float _force = 0.3f;
    private AttackSystem _attackSystem;
    private CinemachineImpulseSource _impulseSource;

    private void Awake()
    {
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        _attackSystem = GetComponent<AttackSystem>();
    }

    private void OnEnable()
    {
        _attackSystem.OnAttack += Shake;
    }

    private void OnDisable() => _attackSystem.OnAttack -= Shake;

    private void Shake(Vector2 direction)
    {
        if (direction == Vector2.zero)
        {
            direction = _impulseSource.DefaultVelocity; // Default direction if none is provided
        }
        _impulseSource.GenerateImpulseWithVelocity(direction * _force);
    }
}