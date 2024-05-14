using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Pool;
using System.Collections.Generic;

public enum BagScreenContext { Battle, Pause, GiveShortcut, }

[Serializable]
public class BagCategory
{
    private List<Item> _categoryItems;
    public List<Item> CategoryItems => _categoryItems;

    public void Init(){
        _categoryItems = new();
    }

    public void SetCategoryItems( Item item ){
        _categoryItems.Add( item );
    }
}

public class BagDisplay : MonoBehaviour
{
    [SerializeField] private BagScreenContext _bagScreenContext;
    [SerializeField] private ItemButton_PauseScreen _itemButtonPrefab;
    [SerializeField] private GameObject _itemContainer;
    [SerializeField] private Button _noneButton;
    [SerializeField] private TextMeshProUGUI _itemName;
    [SerializeField] private TextMeshProUGUI _itemDescription;
    [SerializeField] private PartyDisplay _partyDisplay;
    [SerializeField] private BagCategory[] _bagCategories;
    private IBagScreen _parentMenu;
    private ObjectPool<ItemButton_PauseScreen> _itemPool;
    private RectTransform _itemContainerRect;
    private ItemButton_PauseScreen _selectedButton;
    public Button InitialButton { get; private set; }
    public Button LastButton { get; private set; }
    public Inventory PlayerInventory { get; private set; }
    public Item ItemSelected { get; private set; }
    public List<ItemButton_PauseScreen> ItemButtons { get; private set; }
    public ObjectPool<ItemButton_PauseScreen> ItemPool => _itemPool;
    public TextMeshProUGUI ItemName => _itemName;
    public TextMeshProUGUI ItemDescription => _itemDescription;
    public PartyDisplay PartyDisplay => _partyDisplay;
    public Action<ItemButton_PauseScreen> OnButtonSelected;
    public Action OnItemListRequest;

    private Action _enterDialogueWrapper;
    private Action _exitDialogueWrapper;
    
    //--Constants
    const int ITEMS_IN_VIEWPORT = 10;
    const int ITEMS_LEFT_BEFORE_SCROLL = 2;
    const int STARTING_UNIQUE_ITEMS_IN_BAG = 0;
    const int MAX_UNIQUE_ITEMS_IN_BAG = 999;

    private void OnEnable(){
        //--Events
        //--These specific events should only be subscribed to when a Bag Display is actually open/active
        //--So we only sub in OnEnable();
        OnButtonSelected += SetSelectedButton;
        OnItemListRequest += UpdateItemList;
        GameStateController.Instance.OnDialogueStateEntered += _enterDialogueWrapper;
        GameStateController.Instance.OnDialogueStateExited += _exitDialogueWrapper;
    }

    private void Awake(){
        _enterDialogueWrapper = () => SetItemButtons_Interactable( false );
        _exitDialogueWrapper = () => SetItemButtons_Interactable( true );

        //--Initialize a new ObjectPool of Buttons for each Item
        _itemPool = new( () => { return ItemPoolCreate(); },
        itemButton => { /*ItemPoolGet();*/ }, //--unused
        itemButton => { ItemPoolRelease( itemButton ); },
        itemButton => { ItemPoolDestroy( itemButton ); },
        //--Handle Dupes, Starting Amount in Pool, Max Amount in Pool------------------------
        false, STARTING_UNIQUE_ITEMS_IN_BAG, MAX_UNIQUE_ITEMS_IN_BAG );

        Init();
    }

    private void OnDisable(){
        //--Events
        OnButtonSelected -= SetSelectedButton;
        OnItemListRequest -= UpdateItemList;
        //--We're not unsubbing from Release and Update because we want all bag displays
        //--to properly update when an item is added or removed outside of a Bag Screen
        //--For example, if we pick up an item in the overworld, or a key item is consumed, the itemlist should be updated
        // PlayerInventory.OnItemRemoved -= ReleaseExpendedItemToPool;
        // PlayerInventory.OnInventoryUpdated -= UpdateItemList
        GameStateController.Instance.OnDialogueStateEntered -= _enterDialogueWrapper;
        GameStateController.Instance.OnDialogueStateExited -= _exitDialogueWrapper;
    }

    private ItemButton_PauseScreen ItemPoolCreate(){
        var itemButton = Instantiate( _itemButtonPrefab, _itemContainer.transform );
        return itemButton;
    }

    private void ItemPoolRelease( ItemButton_PauseScreen itemButton ){
        itemButton.gameObject.SetActive( false );
    }

    private void ItemPoolDestroy( ItemButton_PauseScreen itemButton ){
        Destroy( itemButton.gameObject );
    }

    public void Init(){
        //--Parent Context Menu
        _parentMenu = GetComponentInParent<IBagScreen>();

        //--Set Inventory
        PlayerInventory = PlayerReferences.Instance.PlayerInventory;

        //--Events
        //--These are observer events from the inventory. we want the item list and the item pool to update when
        //--inventory changes happen from outside of the bag screen, so we subscribe to them once here in Init()
        //--and no where else so we don't have duplicate subs
        PlayerInventory.OnItemRemoved += ReleaseExpendedItemToPool;
        PlayerInventory.OnInventoryUpdated += UpdateItemList;

        //--Open Menu
        gameObject.SetActive( true );

        //--Select Initial Button
        if( PlayerInventory.ItemList.Count == 0 ){
            //--Set No Items Button
            _noneButton.gameObject.GetComponent<ItemButton_PauseScreen>().Init( _parentMenu, null, _bagScreenContext );
            InitialButton = _noneButton;
            _noneButton.gameObject.SetActive( true );
        }
        else{
            //--Disable No Items Button incase it was previously enabled
            _noneButton.gameObject.SetActive( false );

            //--Update the Item List
            UpdateItemList();

            //--Grab the viewport's rect for scrolling
            _itemContainerRect = _itemContainer.GetComponent<RectTransform>();
        }
    }

    public void SetSelectedItem( Item item ){
        ItemSelected = item;
    }

    public void SetSelectedItemInfoField( string itemName, string itemDescription ){
        _itemName.text = itemName;
        _itemDescription.text = itemDescription;
    }

    private void ReleaseExpendedItemToPool( Item item ){
        foreach( var button in ItemButtons ){
            var itemButton = button.GetComponent<ItemButton_PauseScreen>();

            if( ReferenceEquals( itemButton.ItemSlot, ItemSelected ) ){
                ItemButtons.Remove( button );
                _itemPool.Release( itemButton );
                ItemSelected = null;
                break;
            }
        }
    }

    private void UpdateItemList(){
        //--Instantiate a new item button inside the item button container for each item in
        //--the player's invenvtory that isn't accounted for. Sometimes this is all items.
        if( _itemPool.CountActive < PlayerInventory.ItemList.Count ){
            int amountToGet = PlayerInventory.ItemList.Count;
            foreach( var itemSlot in PlayerInventory.ItemList ){
                if( amountToGet > _itemPool.CountActive ){
                    var itemButton = _itemPool.Get();
                    itemButton.Init( _parentMenu, itemSlot, _bagScreenContext );
                    itemButton.gameObject.SetActive( true );
                }
            }
            //--Setup New Item Buttons
            ItemButtons = null;
            ItemButtons = new();               //--Initialize Button List
            ItemButtons = GetItemButtons();    //--Populate the Button List with updated, active from the pool, Item Buttons
            InitialButton = ItemButtons[0].ThisButton;   //--Set Initial Button to the first Item Button in the List
        }
        else{
            //--Update Existing Item Info
            foreach( var item in ItemButtons ){
                var itemButton = item.GetComponent<ItemButton_PauseScreen>();
                itemButton.UpdateInfo();
            }
        }

        //--If the last selected item no longer exists, the LastButton was nulled or already null, or the ItemCount of the
        //--last selected item was reduced to 0 on use, and use called updateitemlist, we should set the last button
        //--to null so the selector doesn't try to select it
        //--this will need to be changed when i convert this stuff to use object pooling instead of destroy and instantiate
        if( ItemSelected == null || ItemSelected.ItemCount == 0  ){
            LastButton = null;
        }
        else{
            LastButton = ItemButtons.First( item => item.ItemSlot.ItemSO.ItemName == ItemSelected.ItemSO.ItemName ).ThisButton;
        }
    }

    private List<ItemButton_PauseScreen> GetItemButtons(){
        var itemButtonArray = _itemContainer.GetComponentsInChildren<ItemButton_PauseScreen>();
        return itemButtonArray.ToList();
    }

    private void SetSelectedButton( ItemButton_PauseScreen itemButton ){
        _selectedButton = itemButton;
        LastButton = itemButton.ThisButton;

        HandleScrolling();
    }

    private void HandleScrolling(){
        var currentPosition = _selectedButton.RectTransform.localPosition.y;
        var startScrollingPosition = ITEMS_IN_VIEWPORT - ITEMS_LEFT_BEFORE_SCROLL;
        float scrollPosition = currentPosition - startScrollingPosition * -_selectedButton.RectHeight;
        _itemContainerRect.localPosition = new Vector2( _itemContainerRect.localPosition.x, -scrollPosition );
    }

    // private IEnumerator SetInitialButton(){
    //     yield return new WaitForSeconds( 0.15f );

    //     if( LastButton != null )
    //         SelectMemoryButton();
    //     else{
    //         SetMemoryButton( InitialButton );
    //     }
    // }

    // public void SetMemoryButton( Button lastButton ){
    //     LastButton = lastButton;
    //     SelectMemoryButton();
    // }

    // private void SelectMemoryButton(){
    //     LastButton.Select();
    // }

    // public void ClearMemoryButton(){
    //     LastButton = null;
    //     InitialButton.Select();
    // }

    public void SetItemButtons_Interactable( bool isInteractable ){
        Debug.Log( $"SetItemButtons_Interactable: {isInteractable}" );
        foreach( ItemButton_PauseScreen button in ItemButtons ){
            button.ThisButton.interactable = isInteractable;
        }
    }

}
