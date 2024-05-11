using UnityEngine;

public class ItemSO : ScriptableObject
{
    [Header( "Basic Information" )]
    [SerializeField] private string _itemName;
    [TextArea(3, 10)]
    [SerializeField] private string _itemDescription;
    [SerializeField] private Sprite _icon;

    public string ItemName => _itemName;
    public string ItemDescription => _itemDescription;
    public Sprite Icon => _icon;

    public virtual bool Use( Pokemon pokemon ){
        return false;
    }
}
