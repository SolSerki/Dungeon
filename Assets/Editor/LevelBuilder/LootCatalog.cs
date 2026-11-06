using System.Linq;
using UnityEditor;
using UnityEngine;

// Los objetos que se pueden asignar como loot: todo prefab con un Pickup en Assets/Prefabs/Items.
// Para sumar uno nuevo alcanza con crear el prefab en esa carpeta.
[InitializeOnLoad]
public static class LootCatalog
{
    private const string ItemsFolder = "Assets/Prefabs/Items";

    private static GameObject[] _items;

    static LootCatalog() => EditorApplication.projectChanged += () => _items = null;

    public static GameObject[] Items
    {
        get
        {
            if (_items == null || _items.Any(i => i == null))
            {
                _items = AssetDatabase.FindAssets("t:Prefab", new[] { ItemsFolder })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(go => go.GetComponent<Pickup>() != null)
                    .OrderBy(go => go.name)
                    .ToArray();
            }
            return _items;
        }
    }

    public static bool IsKey(GameObject prefab) => prefab != null && prefab.GetComponent<KeyPickup>() != null;
}
