using UnityEngine;

public enum ItemCategory { Medicine, PokeBall, TM, Battle, Training, Berry, KeyItem }

public class ItemSO : ScriptableObject
{
    [Header( "Basic Information" )]
    [SerializeField] private ItemCategory _itemCategory;
    [SerializeField] private string _itemName;
    [TextArea(3, 10)]
    [SerializeField] private string _itemDescription;
    [SerializeField] private Sprite _icon;
    [SerializeField] private bool _hasBattleEffect;
    [SerializeField] private BattleItemEffectID _battleEffectID;
    [SerializeField] private int _flingPower;
    [SerializeField] private bool _canNotSteal;
    
    public ItemCategory ItemCategory => _itemCategory;
    public string ItemName => _itemName;
    public string ItemDescription => _itemDescription;
    public Sprite Icon => _icon;
    public bool HasBattleEffect => _hasBattleEffect;
    public BattleItemEffectID BattleEffectID => _battleEffectID;
    public int FlingPower => _flingPower;
    public bool CanNotSteal => _canNotSteal;

    public virtual bool Use( Pokemon pokemon ){
        return false;
    }

    public virtual bool CheckIfUsable( Pokemon pokemon ){
        return false;
    }

    public virtual string UseText( Pokemon pokemon ){
        return "It won't have any effect!";
    }
}
