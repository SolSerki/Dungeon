using System.Linq;
using UnityEditor;
using UnityEngine;

// Items that can be assigned as loot: every ItemData asset in Assets/Data/Items.
// To add a new one, create it there (Create > Dungeon > Items).
[InitializeOnLoad]
public static class LootCatalog
{
    public const string ItemsFolder = "Assets/Data/Items";

    private static ItemData[] _items;

    static LootCatalog() => EditorApplication.projectChanged += () => _items = null;

    public static ItemData[] Items
    {
        get
        {
            if (_items == null || _items.Any(i => i == null))
            {
                _items = AssetDatabase.FindAssets($"t:{nameof(ItemData)}", new[] { ItemsFolder })
                    .Select(guid => AssetDatabase.LoadAssetAtPath<ItemData>(AssetDatabase.GUIDToAssetPath(guid)))
                    .Where(item => item != null)
                    .OrderBy(item => item.name)
                    .ToArray();
            }
            return _items;
        }
    }

    public static bool IsKey(ItemData item) => item is KeyItem;
}
