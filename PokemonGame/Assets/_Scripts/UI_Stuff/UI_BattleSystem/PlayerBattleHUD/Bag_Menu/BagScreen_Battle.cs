using System.Collections;
using UnityEngine;
using NoxNoctisDev.StateMachine;
using UnityEngine.UI;
using System;

public class BagScreen_Battle : State<PlayerBattleMenu>, IBagScreen, IPartyScreen
{
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private PlayerBattleMenu _battleMenu;
    [SerializeField] private BagDisplay _bagDisplay;
    [SerializeField] private PartyDisplay _partyDisplay;
    [SerializeField] private UseItemFromBag_Battle _useItemFromBagState;
    public PlayerBattleMenu BattleMenu => _battleMenu;
    private Button _initialButton;
    public Button LastButton { get; private set; }
    public BagDisplay BagDisplay => _bagDisplay;
    public PartyDisplay PartyDisplay => _partyDisplay;
    public Action<Pokemon, Item> OnItemCommand;

    public override void EnterState( PlayerBattleMenu owner ){
        Debug.Log( $"{this} EnterState()" );
        _battleMenu = owner;

        //--Events
        OnItemCommand += SetItemCommand;
        _bagDisplay.OnPocketChanged += SetNewInitialButton;
        GameStateController.Instance.OnDialogueStateEntered += _bagDisplay.EnterDialogueWrapper;
        GameStateController.Instance.OnDialogueStateExited += _bagDisplay.ExitDialogueWrapper;

        //--Request Itemlist
        UpdateItemList();

        //--Open Menu
        gameObject.SetActive( true );

        //--Enable Item Buttons if they were disabled previously, Disable Party Buttons if they were enabled, just in case
        _bagDisplay.SetItemButtons_Interactable( true );
        _partyDisplay.SetPartyButtons_Interactable( false );

        //--Select Initial Button
        _initialButton = _bagDisplay.InitialButton;
        LastButton = null;
        StartCoroutine( SetInitialButton() );
    }

    public override void ReturnToState(){
        Debug.Log( $"{this} ReturnToState()" );
        //--Enable Item Buttons
        _bagDisplay.SetItemButtons_Interactable( true );

        //--Events
        GameStateController.Instance.OnDialogueStateEntered += _bagDisplay.EnterDialogueWrapper;
        GameStateController.Instance.OnDialogueStateExited += _bagDisplay.ExitDialogueWrapper;

        //--Select Appropriate Button
        StartCoroutine( SetInitialButton() );
    }

    public override void PauseState(){
        Debug.Log( $"{this} PauseState()" );
        //--Events
        GameStateController.Instance.OnDialogueStateEntered -= _bagDisplay.EnterDialogueWrapper;
        GameStateController.Instance.OnDialogueStateExited -= _bagDisplay.ExitDialogueWrapper;

        //--Disable Item Buttons
        _bagDisplay.SetItemButtons_Interactable( false );
    }

    public override void ExitState(){
        //--Events
        OnItemCommand -= SetItemCommand;
        _bagDisplay.OnPocketChanged -= SetNewInitialButton;
        GameStateController.Instance.OnDialogueStateEntered -= _bagDisplay.EnterDialogueWrapper;
        GameStateController.Instance.OnDialogueStateExited -= _bagDisplay.ExitDialogueWrapper;

        //--Request Itemlist
        UpdateItemList();

        BattleUIActions.OnSubMenuClosed?.Invoke();
        gameObject.SetActive( false );
    }

    private void UpdateItemList(){
        _bagDisplay.OnItemListRequest?.Invoke();
    }

    public void UseItem( Item item ){
        _bagDisplay.SetSelectedItem( item );
        _battleMenu.StateMachine.Push( _useItemFromBagState );
    }

    public void UsePokeball( Item item ){
        _bagDisplay.SetSelectedItem( item );
        SetItemCommand( null, item );
    }

    private void SetNewInitialButton(){
        PlayerReferences.Instance.PlayerController.EventSystem.SetSelectedGameObject( null );
        LastButton = _bagDisplay.InitialButton;
        SelectMemoryButton();
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

    private void SetItemCommand( Pokemon pokemon, Item item ){
        if( BattleMenu.StateMachine.CurrentState == _useItemFromBagState ){
            BattleMenu.PopState();

            if( BattleMenu.StateMachine.CurrentState == this )
                BattleMenu.PopState();
        }
        else if( BattleMenu.StateMachine.CurrentState == this )
            BattleMenu.PopState();

        BattleMenu.BattleSystem.SetUseItemCommand( pokemon, item );
    }
}