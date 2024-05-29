using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using System.Linq;
using TMPro;
using UnityEngine;

public class Inventory : MonoBehaviour, ISavable
{
    [SerializeField] private GameObject _itemGetContainer;
    [SerializeField] private Image _itemIcon;
    [SerializeField] private TextMeshProUGUI _itemGetText;
    [SerializeField] private List<Item> _itemList; //--?? instead of inventory?
    public List<Item> ItemList => _itemList;
    public event Action OnInventoryUpdated;
    public event Action<Item> OnItemRemoved;
    public Action<ItemSO, int> OnItemGet;

    private void Start(){
        OnItemGet += StartItemGet;
    }

    private void OnDisable(){
        OnItemGet -= StartItemGet;
    }

    //--Add Item
    public void AddItem( ItemSO item, int count = 1 ){
        var itemSlot = _itemList.FirstOrDefault( slot => slot.ItemSO == item );

        if( itemSlot != null ){
            itemSlot.IncreaseItemCount( count );
        }
        else{
            var newItem = new Item( item, count );
            _itemList.Add( newItem );
        }

        OnInventoryUpdated?.Invoke();
    }

    private void StartItemGet( ItemSO item, int count = 1 ){
        StartCoroutine( ShowItemGet( item, count ) );
    }

    private IEnumerator ShowItemGet( ItemSO item, int count = 1 ){
        _itemGetText.text = $"Found X {count} {item.ItemName}!";
        _itemIcon.sprite = item.Icon;

        yield return new WaitForEndOfFrame();
        _itemGetContainer.SetActive( true );

        yield return new WaitForSeconds( 2.5f );
        _itemGetContainer.SetActive( false );
    }

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

    public void UsePokeball( Item item ){
        RemoveItem( item );
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

    public object CaptureState(){
        var saveData = new InventorySaveData()
        {
            Inventory = _itemList.Select( i => i.CreateSaveData() ).ToList(),
        };

        return saveData;
    }

    public void RestoreState( object state ){
        var saveData = (InventorySaveData)state;

        _itemList = saveData.Inventory.Select( item => new Item( item ) ).ToList();

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

    public Item( ItemSO item, int count ){
        _itemSO = item;
        _itemCount = count;
    }

    public Item( ItemSaveData saveData ){
        _itemSO = ItemsDB.GetItemByName( saveData.ItemName );
        _itemCount = saveData.Count;
    }

    public void IncreaseItemCount(){
        _itemCount++;

        if( _itemCount > 999 )
            _itemCount = 999;
    }

    public void IncreaseItemCount( int amount ){
        _itemCount += amount;
    }

    public void DecreaseItemCount(){
        // Debug.Log( _itemCount );
        _itemCount--;
        // Debug.Log( _itemCount );

        if( _itemCount < 0 )
            _itemCount = 0;
    }

    public void DecreaseItemCount( int amount ){
        _itemCount -= amount;
    }

    public ItemSaveData CreateSaveData(){
        var saveData = new ItemSaveData()
        {
            ItemName = _itemSO.ItemName,
            Count = _itemCount,
        };

        return saveData;
    }

}

[Serializable]
public class ItemSaveData
{
    public string ItemName;
    public int Count;
}

[Serializable]
public class InventorySaveData
{
    public List<ItemSaveData> Inventory;
}
