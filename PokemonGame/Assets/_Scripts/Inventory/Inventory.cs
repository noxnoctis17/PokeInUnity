using System;
using System.Collections.Generic;
using UnityEngine;

public class Inventory : MonoBehaviour
{
    [SerializeField] private List<Item> _itemList; //--?? instead of inventory?
    public List<Item> ItemList => _itemList;
    public event Action OnInventoryUpdated;
    public event Action<Item> OnItemRemoved;

    //--Use Item
    public ItemSO UseItem( Item item, Pokemon pokemon ){
        var itemUsed = item.ItemSO.Use( pokemon );

        if( itemUsed ){
            RemoveItem( item );
            return item.ItemSO;
        }

        return null;
    }

    //--Use Item with a callback
    public ItemSO UseItem( Item item, Pokemon pokemon, Action onItemUsed ){
        var itemUsed = item.ItemSO.Use( pokemon );

        if( itemUsed ){
            RemoveItem( item );
            onItemUsed?.Invoke();
            return item.ItemSO;
        }

        return null;
    }

    //--Check if Item is usable, probably only for Battle use
    public bool CheckIfItemUsable( Item item, Pokemon pokemon ){
        return item.ItemSO.CheckIfUsable( pokemon );
    }

    //--Decrease item count by 1, remove from inventory if count == 0
    public void RemoveItem( Item item ){
        item.DecreaseItemCount();

        if( item.ItemCount == 0 ){
            OnItemRemoved?.Invoke( item );
            _itemList.Remove( item );
        }

        OnInventoryUpdated?.Invoke();
    }

}

[Serializable]
public class Item
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