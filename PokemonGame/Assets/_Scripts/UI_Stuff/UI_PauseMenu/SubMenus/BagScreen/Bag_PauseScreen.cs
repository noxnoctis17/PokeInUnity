using System;
using System.Collections;
using System.Linq;
using NoxNoctisDev.StateMachine;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Pool;
using Unity.VisualScripting;
using System.Collections.Generic;

public class Bag_PauseScreen : State<UI_PauseMenuStateMachine>, IPartyScreen
{
    public UI_PauseMenuStateMachine PauseMenuStateMachine { get; private set; }
    [SerializeField] private ItemButton_PauseScreen _itemButtonPrefab;
    [SerializeField] private GameObject _itemContainer;
    [SerializeField] private Button _noneButton;
    [SerializeField] private TextMeshProUGUI _itemName;
    [SerializeField] private TextMeshProUGUI _itemDescription;
    [SerializeField] private PartyDisplay _partyDisplay;
    [SerializeField] private UseItemFromBagState _useItemFromBagState;
    private ObjectPool<ItemButton_PauseScreen> _itemPool;
    private RectTransform _itemContainerRect;
    private List<ItemButton_PauseScreen> _itemButtons;
    private Button _initialButton;
    private ItemButton_PauseScreen _selectedButton;
    public Button LastButton { get; private set; }
    public Inventory PlayerInventory { get; private set; }
    public ItemSlot ItemSelected { get; private set; }
    public ObjectPool<ItemButton_PauseScreen> ItemPool => _itemPool;
    public TextMeshProUGUI ItemName => _itemName;
    public TextMeshProUGUI ItemDescription => _itemDescription;
    public PartyDisplay PartyDisplay => _partyDisplay;
    public Action<ItemButton_PauseScreen> OnButtonSelected;
    
    //--Constants
    const int ITEMS_IN_VIEWPORT = 10;
    const int ITEMS_LEFT_BEFORE_SCROLL = 2;
    const int STARTING_UNIQUE_ITEMS_IN_BAG = 0;
    const int MAX_UNIQUE_ITEMS_IN_BAG = 999;

    private void Awake(){
        //--Initialize a new ObjectPool of Buttons for each Item
        _itemPool = new( () =>
        //--Create()-------------------------------------------------------------------
        {
            var itemButton = Instantiate( _itemButtonPrefab, _itemContainer.transform );
            return itemButton;
        },
        itemButton =>
        //--Get()----------------------------------------------------------------------
        {
            //--omg what are we gunna do on get lol
        },
        itemButton =>
        //--Release()------------------------------------------------------------------
        {
            itemButton.gameObject.SetActive( false );
        },
        itemButton =>
        //--Destroy()------------------------------------------------------------------
        {

        },
        //--STUFF*, Starting Amount in Pool, Max Amount in Pool------------------------
        false, STARTING_UNIQUE_ITEMS_IN_BAG, MAX_UNIQUE_ITEMS_IN_BAG
        );
        //*
        //--stuff: have unity handle management if you think your code might return an object to the pool that has already been returned to the pool
        //--I'm a master coder and therefore do not have to worry about this, so it's set to false 04/14/24
        //*
    }

    public override void EnterState( UI_PauseMenuStateMachine owner ){
        //--State Machine
        PauseMenuStateMachine = owner;

        //--Set Inventory
        PlayerInventory = PlayerReferences.Instance.PlayerInventory;

        //--Events
        OnButtonSelected += SetSelectedButton;
        PlayerInventory.OnItemRemoved += ReleaseExpendedItemToPool;
        PlayerInventory.OnInventoryUpdated += UpdateItemList;

        //--Open Menu
        gameObject.SetActive( true );

        //--Select Initial Button
        if( PlayerInventory.ItemSlots.Count == 0 ){
            //--Set No Items Button
            _noneButton.gameObject.GetComponent<ItemButton_PauseScreen>().Init( this, null );
            _initialButton = _noneButton;
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

        //--Enable Item Buttons if they were disabled previously, Disable Party Buttons if they were enabled, just in case
        SetItemButtons_Interactable( true );
        _partyDisplay.SetPartyButtons_Interactable( false );
        StartCoroutine( SetInitialButton() );
    }

    public override void ReturnToState(){
        //--Events
        PlayerInventory.OnInventoryUpdated += UpdateItemList;
        //--We don't unsubscribe from OnInventoryUpdated on Pause because if we started in the bag screen, that means we're using items somehow
        //--somewhere, no matter how many submenus deep we are into the menu state stack. that means we need to continue to listen for inventory
        //--changes, and then update the item list when that event is raised

        //--Enable Item Buttons
        SetItemButtons_Interactable( true );

        //--Select Appropriate Button
        StartCoroutine( SetInitialButton() );
    }

    public override void PauseState(){
        Debug.Log( "bag menu paused state" );
        //--Events
        OnButtonSelected -= SetSelectedButton;
        //--We don't unsubscribe from OnInventoryUpdated on Pause because if we started in the bag screen, that means we're using items somehow
        //--somewhere, no matter how many submenus deep we are into the menu state stack. that means we need to continue to listen for inventory
        //--changes, and then update the item list when that event is raised

        //--Disable Item Buttons
        SetItemButtons_Interactable( false );
    }

    public override void ExitState(){
        //--Events
        OnButtonSelected -= SetSelectedButton;
        PlayerInventory.OnItemRemoved -= ReleaseExpendedItemToPool;
        PlayerInventory.OnInventoryUpdated -= UpdateItemList;

        //--Close menu
        gameObject.SetActive( false );
    }

    private void ReleaseExpendedItemToPool( ItemSlot item ){
        foreach( var button in _itemButtons ){
            var itemButton = button.GetComponent<ItemButton_PauseScreen>();

            if( ReferenceEquals( itemButton.ItemSlot, ItemSelected ) ){
                _itemButtons.Remove( button );
                _itemPool.Release( itemButton );
                ItemSelected = null;
                break;
            }
        }
    }

    private void UpdateItemList(){
        //--Instantiate a new item button inside the item button container for each item in
        //--the player's invenvtory that isn't accounted for. Sometimes this is all items.
        if( _itemPool.CountActive < PlayerInventory.ItemSlots.Count ){
            int amountToGet = PlayerInventory.ItemSlots.Count;
            foreach( var itemSlot in PlayerInventory.ItemSlots ){
                if( amountToGet > _itemPool.CountActive ){
                    var itemButton = _itemPool.Get();
                    itemButton.Init( this, itemSlot );
                    itemButton.gameObject.SetActive( true );
                }
            }
            //--Setup New Item Buttons
            _itemButtons = null;
            _itemButtons = new();               //--Initialize Button List
            _itemButtons = GetItemButtons();    //--Populate the Button List with updated, active from the pool, Item Buttons
            _initialButton = _itemButtons[0].ThisButton;   //--Set Initial Button to the first Item Button in the List
        }
        else{
            //--Update Existing Item Info
            foreach( var item in _itemButtons ){
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
            LastButton = _itemButtons.First( item => item.ItemSlot.ItemSO.ItemName == ItemSelected.ItemSO.ItemName ).ThisButton;
        }
    }

    public void UseItem( ItemSlot item ){
        ItemSelected = item;
        PauseMenuStateMachine.PauseMenuStateMachine.Push( _useItemFromBagState );
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

    private IEnumerator SetInitialButton(){
        yield return new WaitForSeconds( 0.15f );

        if( LastButton != null )
            SelectMemoryButton();
        else{
            SetMemoryButton( _initialButton );
        }
    }

    public void SetMemoryButton( Button lastButton ){
        LastButton = lastButton;
        SelectMemoryButton();
    }

    private void SelectMemoryButton(){
        LastButton.Select();
    }

    public void ClearMemoryButton(){
        LastButton = null;
        _initialButton.Select();
    }

    private void SetItemButtons_Interactable( bool isInteractable ){
        foreach( ItemButton_PauseScreen button in _itemButtons ){
            button.ThisButton.interactable = isInteractable;
        }
    }

}
