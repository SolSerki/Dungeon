using Unity.Cinemachine;
using UnityEngine;

[RequireComponent(typeof(CinemachineImpulseSource))]
public class ScreenShakeOnDamage : MonoBehaviour
{
    [SerializeField] private float _force = 0.3f;
    private HealthSystem _healthSystem;
    private CinemachineImpulseSource _impulseSource;

    private void Awake()
    {
        _impulseSource = GetComponent<CinemachineImpulseSource>();
        _healthSystem = GetComponent<HealthSystem>();
    }

private void OnEnable() => _healthSystem.OnDamaged += Shake;
private void OnDisable() => _healthSystem.OnDamaged -= Shake;

    private void Shake(HealthSystem.DamageInfo damageInfo)
    {
        _impulseSource.GenerateImpulseWithVelocity(-damageInfo.Direction * _force);
    }
}