using System;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    public int Keys { get; private set; }
    public event Action<int> OnKeysChanged;

    public void AddKey()
    {
        Keys++;
        OnKeysChanged?.Invoke(Keys);
    }

    public bool TryUseKey()
    {
        if (Keys == 0) return false;
        Keys--;
        OnKeysChanged?.Invoke(Keys);
        return true;
    }
}