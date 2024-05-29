using System;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Pool;
using System.Collections.Generic;
using UnityEngine.InputSystem;

public enum BagScreenContext { Battle, Pause, GiveShortcut, }

[Serializable]
public class BagPocket
{
    [SerializeField] private ItemCategory _itemCategory;
    [SerializeField] private GameObject _itemPocket;
    [SerializeField] private RectTransform _currentPocketRect;
    public RectTransform CurrentPocketRect => _currentPocketRect;
    public ItemCategory ItemCategory => _itemCategory;
    public GameObject ItemPocket => _itemPocket;
    public Image PocketIcon { get; private set; }
    public List<ItemButton_PauseScreen> ItemButtons { get; private set; }
    public Action OnCurrentPocket;
    public Action OnPreviousPocket;

    public void Init(){
        OnCurrentPocket += SetIconActive;
        OnPreviousPocket += SetIconInactive;

        SetButtons();
    }

    private void SetButtons(){
        // Debug.Log( "BagPocket SetButtons()" );
        var itemButtonArray = _itemPocket.GetComponentsInChildren<ItemButton_PauseScreen>();
        ItemButtons = itemButtonArray.ToList();
    }

    public void SetIcon( Image icon ){
        PocketIcon = icon;
        PocketIcon.sprite = BagPocketIconAtlas.PocketIcons[_itemCategory];
        PocketIcon.color = Color.white;
    }

    private void SetIconActive(){
        if( PocketIcon != null )
            PocketIcon.color = Color.yellow;
    }

    private void SetIconInactive(){
        if( PocketIcon != null )
            PocketIcon.color = Color.white;
    }

}


public class BagDisplay : MonoBehaviour, IInitializeMeDaddy
{
    [SerializeField] private BagScreenContext _bagScreenContext;
    [SerializeField] private ItemButton_PauseScreen _itemButtonPrefab;
    [SerializeField] private Transform _itemPoolContainer;
    [Space( 20 )]
    [SerializeField] private GameObject _pocketIconContainer;
    [SerializeField] private BagPocket _medicinePocket;
    [SerializeField] private BagPocket _pokeballPocket;
    [SerializeField] private BagPocket _tmPocket;
    [Space( 20 )]
    [SerializeField] private TextMeshProUGUI _itemName;
    [SerializeField] private TextMeshProUGUI _itemDescription;
    [SerializeField] private PartyDisplay _partyDisplay;
    private BagPocket[] _availablePockets;
    private BagPocket _currentPocket;
    private IBagScreen _parentMenu;
    private bool _canChangePockets;
    private ObjectPool<ItemButton_PauseScreen> _itemPool;
    private RectTransform _currentPocketRect;
    private ItemButton_PauseScreen _selectedButton;
    public BagPocket CurrentPocket { get { return _currentPocket; } set { CurrentPocketSetter( value ); } }
    public Button InitialButton { get; private set; }
    public Button LastButton { get; private set; }
    public Inventory PlayerInventory { get; private set; }
    public Item ItemSelected { get; private set; }
    public ObjectPool<ItemButton_PauseScreen> ItemPool => _itemPool;
    public TextMeshProUGUI ItemName => _itemName;
    public TextMeshProUGUI ItemDescription => _itemDescription;
    public PartyDisplay PartyDisplay => _partyDisplay;
    public Action<ItemButton_PauseScreen> OnButtonSelected;
    public Action OnItemListRequest;
    public event Action OnPocketChanged;

    public Action EnterDialogueWrapper;
    public Action ExitDialogueWrapper;
    
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
        PlayerReferences.Instance.PlayerInput.UI.Navigate.performed += NavigatePockets;
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
        GameStateController.Instance.OnDialogueStateEntered -= EnterDialogueWrapper;
        GameStateController.Instance.OnDialogueStateExited -= ExitDialogueWrapper;
        PlayerReferences.Instance.PlayerInput.UI.Navigate.performed -= NavigatePockets;
    }

    private ItemButton_PauseScreen ItemPoolCreate(){
        var itemButton = Instantiate( _itemButtonPrefab, _itemPoolContainer );
        return itemButton;
    }

    private void ItemPoolRelease( ItemButton_PauseScreen itemButton ){
        itemButton.gameObject.SetActive( false );
        itemButton.gameObject.transform.SetParent( _itemPoolContainer );
    }

    private void ItemPoolDestroy( ItemButton_PauseScreen itemButton ){
        Destroy( itemButton.gameObject );
    }

    public void Init(){
        //--Dialogue Event Wrappers
        EnterDialogueWrapper = () => SetItemButtons_Interactable( false );
        ExitDialogueWrapper = () => SetItemButtons_Interactable( true );

        //--Initialize a new ObjectPool of Buttons for each Item
        _itemPool = new( () => { return ItemPoolCreate(); },
        itemButton => { /*ItemPoolGet( itemButton );*/ },
        itemButton => { ItemPoolRelease( itemButton ); },
        itemButton => { ItemPoolDestroy( itemButton ); },
        //--Handle Dupes, Starting Amount in Pool, Max Amount in Pool------------------------
        false, STARTING_UNIQUE_ITEMS_IN_BAG, MAX_UNIQUE_ITEMS_IN_BAG );

        //--Parent Context Menu
        _parentMenu = GetComponentInParent<IBagScreen>( true );

        //--Set Inventory
        PlayerInventory = PlayerReferences.Instance.PlayerInventory;

        //--Events
        //--These are observer events from the inventory. we want the item list and the item pool to update when
        //--inventory changes happen from outside of the bag screen, so we subscribe to them once here in Init()
        //--and no where else so we don't have duplicate subs
        PlayerInventory.OnItemRemoved       += ReleaseExpendedItemToPool;
        PlayerInventory.OnInventoryUpdated  += UpdateItemList;

        //--Add Available Pockets
        SetAvailablePockets();
        CurrentPocket = _availablePockets[0];

        //--Open Menu
        gameObject.SetActive( true );

        //--Intialize the Exit Buttons
        InitializeExitButtons();

        //--Select Initial Button
        //--Initial button is set when we update the item list
        //--Update the Item List
        UpdateItemList();

        //--Grab the viewport's rect for scrolling
        _currentPocketRect = CurrentPocket.CurrentPocketRect;

        //--Set Pocket Icons and set initial pocket
        SetPocketIcons();
        CurrentPocket.OnCurrentPocket?.Invoke();
    }

    private void SetAvailablePockets(){
        switch( _bagScreenContext )
        {
            case BagScreenContext.Battle:
                _availablePockets = new BagPocket[] { _medicinePocket, _pokeballPocket, };
            break;

            case BagScreenContext.Pause:
                _availablePockets = new BagPocket[] { _medicinePocket, _pokeballPocket, _tmPocket };
            break;

            case BagScreenContext.GiveShortcut:
                //--When you select a pokemon and choose "Give", this will be that context
                //--I don't think it will be any different from pause, though. pause will likely have
                //--a Give() that can just be directly called or something from the item button under this context
            break;
        }

        // Debug.Log( $"Available Pockets Count: {_availablePockets.Length}" );
    }

    private void SetPocketIcons(){
        var pocketIcons = _pocketIconContainer.GetComponentsInChildren<Image>();

        for( int i = 0; i < _availablePockets.Length; i++ ){
            _availablePockets[i].SetIcon( pocketIcons[i] );
        }
    }

    private void CurrentPocketSetter( BagPocket pocket ){
        _currentPocket = pocket;
        _currentPocket.OnCurrentPocket?.Invoke();

        if( pocket.ItemCategory == ItemCategory.Medicine || pocket.ItemCategory == ItemCategory.Berry )
            _partyDisplay.OnHPPocketEntered?.Invoke( true );
        else
            _partyDisplay.OnHPPocketEntered?.Invoke( false );
    }

    private void NavigatePockets( InputAction.CallbackContext context ){
        Vector2 direction = context.ReadValue<Vector2>();
        // Debug.Log( $"Navigate Pockets direction: {direction}" );

        if( _canChangePockets ){
            //--Player moved Right through the menu
            if( direction.x > 0 )
                NextPocketRight();

            //--Player moved Left through the menu
            if( direction.x < 0 )
                NextPocketLeft();
        }
    }

    private void NextPocketRight(){
        //--Hide the current pocket's scrollview contents
        CurrentPocket.ItemPocket.SetActive( false );
        CurrentPocket.OnPreviousPocket?.Invoke();
        // Debug.Log( $"Previous Pocket: {_currentPocket.ItemPocket.name}" );

        //--Navigate forwards through the array of available pockets
        for( int i = 0; i < _availablePockets.Length; i++ ){
            if( _availablePockets[i] == CurrentPocket ){
                // Debug.Log( $"Previous Pocket Index: {i}" );
                int lastIndex = _availablePockets.Length - 1;
                int nextIndex = i + 1;

                if( _availablePockets[i] == _availablePockets[lastIndex] )
                    CurrentPocket = _availablePockets[0];
                else
                    CurrentPocket = _availablePockets[nextIndex];
                
                // Debug.Log( $"Current Pocket Index: {i}" );
                break;
            }
        }

        //--Set Initial Button
        InitialButton = CurrentPocket.ItemButtons[0].ThisButton;

        //--Show the new current pocket's scrollview contents
        CurrentPocket.ItemPocket.SetActive( true );
        // Debug.Log( $"Current Pocket: {_currentPocket.ItemPocket.name}" );
        //--Notify the current BagScreen its pocket has been changed so it can select the new initial button
        OnPocketChanged?.Invoke();
    }

    private void NextPocketLeft(){
        //--Hide the current pocket's scrollview contents
        CurrentPocket.ItemPocket.SetActive( false );
        CurrentPocket.OnPreviousPocket?.Invoke();
        // Debug.Log( $"Previous Pocket: {_currentPocket.ItemPocket.name}" );

        //--Navigate backwards through the array of available pockets
        for( int i = 0; i < _availablePockets.Length; i++ ){
            if( _availablePockets[i] == CurrentPocket ){
                // Debug.Log( $"Previous Pocket Index: {i}" );
                int lastIndex = _availablePockets.Length - 1;
                int nextIndex = i - 1;

                if( _availablePockets[i] == _availablePockets[0] )
                    CurrentPocket = _availablePockets[lastIndex];
                else
                    CurrentPocket = _availablePockets[nextIndex];
                
                // Debug.Log( $"Current Pocket Index: {i}" );
                break;
            }
        }

        //--Set Initial Button
        InitialButton = CurrentPocket.ItemButtons[0].ThisButton;

        //--Show the new current pocket's scrollview contents
        CurrentPocket.ItemPocket.SetActive( true );
        // Debug.Log( $"Current Pocket: {_currentPocket.ItemPocket.name}" );
        //--Notify the current BagScreen its pocket has been changed so it can select the new initial button
        OnPocketChanged?.Invoke();
    }

    public void SetSelectedItem( Item item ){
        ItemSelected = item;
    }

    public void SetSelectedItemInfoField( string itemName, string itemDescription ){
        _itemName.text = itemName;
        _itemDescription.text = itemDescription;
    }

    private void ReleaseExpendedItemToPool( Item item ){
        foreach( var itemButton in CurrentPocket.ItemButtons ){
            if( itemButton.IsExitButton )
                continue;

            if( ReferenceEquals( itemButton.Item, ItemSelected ) ){
                CurrentPocket.ItemButtons.Remove( itemButton );
                _itemPool.Release( itemButton );
                ItemSelected = null;
                break;
            }
        }
    }

    private void PopulatePockets(){
        foreach( var pocket in _availablePockets ){
            pocket.Init();
        }
    }

    private void SortItemsToPockets( ItemButton_PauseScreen itemButton ){
        ItemCategory itemCategory = itemButton.Item.ItemSO.ItemCategory;
        int childCount;

        switch( itemCategory )
        {
            case ItemCategory.Medicine:
                itemButton.gameObject.transform.SetParent( _medicinePocket.ItemPocket.transform );
                childCount = _medicinePocket.ItemPocket.transform.childCount;
                Debug.Log( $"Medicine Pocket child count is: {childCount}" );
                itemButton.gameObject.transform.SetSiblingIndex( Mathf.Max( childCount - 2, 0 ) );
            break;

            case ItemCategory.PokeBall:
                itemButton.gameObject.transform.SetParent( _pokeballPocket.ItemPocket.transform );
                childCount = _pokeballPocket.ItemPocket.transform.childCount;
                Debug.Log( $"PokeBall Pocket child count is: {childCount}" );
                itemButton.gameObject.transform.SetSiblingIndex( Mathf.Max( childCount - 2, 0 ) );
            break;

            case ItemCategory.TM:
                if( _bagScreenContext == BagScreenContext.Pause ){ //--TODO figure out better sorting and context management...
                    itemButton.gameObject.transform.SetParent( _tmPocket.ItemPocket.transform );
                    childCount = _tmPocket.ItemPocket.transform.childCount;
                    Debug.Log( $"TM Pocket child count is: {childCount}" );
                    itemButton.gameObject.transform.SetSiblingIndex( Mathf.Max( childCount - 2, 0 ) );
                }
                else{
                    itemButton.gameObject.transform.SetParent( _itemPoolContainer.transform );
                    itemButton.gameObject.SetActive( false );
                }

            break;
        }
    }

    private void InitializeExitButtons(){
        //--Initialize Exit Buttons
        foreach( var pocket in _availablePockets ){
            pocket.Init();
            foreach( var itemButton in pocket.ItemButtons ){
                if( itemButton.IsExitButton ){
                    itemButton.Init( _parentMenu, null, _bagScreenContext );
                }
            }
        }
    }

    private void UpdateItemList(){
        // Debug.Log( $"Bag Display {_bagScreenContext}'s UpdateItemList()" );
        //--Instantiate a new item button inside the respective item button container for each item in
        //--the player's invenvtory that isn't accounted for. Sometimes this is all items.
        if( _itemPool.CountActive < PlayerInventory.ItemList.Count ){
            int amountToGet = PlayerInventory.ItemList.Count;

            foreach( var itemSlot in PlayerInventory.ItemList ){
                if( amountToGet > _itemPool.CountActive ){
                    var itemButton = _itemPool.Get();
                    itemButton.Init( _parentMenu, itemSlot, _bagScreenContext );
                    SortItemsToPockets( itemButton );
                    itemButton.gameObject.SetActive( true );
                }
            }

            //--Setup New Item Buttons
            PopulatePockets();                          //--Assign all new buttons to their respective pocket's item button list
        }   
        else{
            //--Update Existing Item Info in all pockets
            foreach( var pocket in _availablePockets ){
                foreach( var itemButton in pocket.ItemButtons ){
                    // Debug.Log( itemButton.name );
                    itemButton.UpdateInfo();
                }
            }
        }

        // Debug.Log( $"Bag Display {_bagScreenContext}'s Current Pocket's child count is: {CurrentPocket.ItemPocket.transform.childCount}" );
        //--Set Initial Button to the first Item Button in the List
        InitialButton = CurrentPocket.ItemButtons[0].ThisButton;

        //--If the last selected item no longer exists, the LastButton was nulled or already null, or the ItemCount of the
        //--last selected item was reduced to 0 on use, and use called updateitemlist, we should set the last button
        //--to null so the selector doesn't try to select it
        //--this will need to be changed when i convert this stuff to use object pooling instead of destroy and instantiate
        if( ItemSelected == null || ItemSelected.ItemCount == 0  ){
            LastButton = null;
        }
        else{
            LastButton = CurrentPocket.ItemButtons.First( item => item.Item.ItemSO.ItemName == ItemSelected.ItemSO.ItemName ).ThisButton;
        }
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
        _currentPocketRect.localPosition = new Vector2( _currentPocketRect.localPosition.x, -scrollPosition );
    }

    public void SetItemButtons_Interactable( bool isInteractable ){
        // Debug.Log( $"SetItemButtons_Interactable: {isInteractable}" );
        _canChangePockets = isInteractable;
        foreach( ItemButton_PauseScreen button in CurrentPocket.ItemButtons ){
                button.ThisButton.interactable = isInteractable;
        }
    }

}
