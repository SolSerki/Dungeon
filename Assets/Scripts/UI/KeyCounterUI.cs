using TMPro;
using UnityEngine;

public class KeyCounterUI : MonoBehaviour
{
    [SerializeField] private Inventory _inventory;
    [SerializeField] private TMP_Text _countText;

    private void OnEnable() => _inventory.OnKeysChanged += Refresh;
    private void OnDisable() => _inventory.OnKeysChanged -= Refresh;    

    private void Start() => Refresh(_inventory.Keys);

    private void Refresh(int keys) => _countText.text = $"x{keys}";
}
