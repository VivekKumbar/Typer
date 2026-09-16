using System.Collections.Generic;
using UnityEngine;

// A shop section shown in the side menu. Create via:
// right-click -> Create -> TypeKeep -> Shop -> Category.
[CreateAssetMenu(fileName = "ShopCategory", menuName = "TypeKeep/Shop/Category")]
public class ShopCategory : ScriptableObject
{
    public string categoryName = "Tower Skins";
    public Sprite categoryIcon;
    [Tooltip("Items shown in this section.")]
    public List<ShopItem> items = new List<ShopItem>();
}
