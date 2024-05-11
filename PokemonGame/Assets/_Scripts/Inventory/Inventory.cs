using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [SerializeField] private List<ItemSlot> _itemSlots; //--?? instead of inventory?
    public List<ItemSlot> ItemSlots => _itemSlots;
    public event Action OnInventoryUpdated;
    public event Action<ItemSlot> OnItemRemoved;

    public ItemSO UseItem( ItemSlot item, Pokemon pokemon, Action onItemUsed ){
        var itemUsed = item.ItemSO.Use( pokemon );

        if( itemUsed ){
            RemoveItem( item );
            onItemUsed?.Invoke();
            return item.ItemSO;
        }

        return null;
    }

    public void RemoveItem( ItemSlot item ){
        item.DecreaseItemCount();

        if( item.ItemCount == 0 ){
            OnItemRemoved?.Invoke( item );
            _itemSlots.Remove( item );
        }

        OnInventoryUpdated?.Invoke();
    }

}

[Serializable]
public class ItemSlot
{
    [SerializeField] private ItemSO _itemSO;
    [SerializeField] private int _itemCount;

    public ItemSO ItemSO => _itemSO;
    public int ItemCount => _itemCount;

    public void IncreaseItemCount(){
        _itemCount++;

        if( _itemCount > 999 )
            _itemCount = 999;
    }

    public void IncreaseItemCount( int amount ){
        _itemCount += amount;
    }

    public void DecreaseItemCount(){
        Debug.Log( _itemCount );
        _itemCount--;
        Debug.Log( _itemCount );

        if( _itemCount < 0 )
            _itemCount = 0;
    }

    public void DecreaseItemCount( int amount ){
        _itemCount -= amount;
    }

}