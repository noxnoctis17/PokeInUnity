using System.Collections;
using NoxNoctisDev.StateMachine;
using UnityEngine;
using UnityEngine.UI;

public class BagScreen_Pause : State<UI_PauseMenuStateMachine>, IBagScreen, IPartyScreen
{
    public UI_PauseMenuStateMachine PauseMenuStateMachine { get; private set; }
    [SerializeField] private BagDisplay _bagDisplay;
    [SerializeField] private PartyDisplay _partyDisplay;
    [SerializeField] private UseItemFromBag_Pause _useItemFromBagState;
    [SerializeField] private LearnMove_Pause _learnMoveMenu;
    private Inventory _playerInventory;
    private Button _initialButton;
    public Button LastButton { get; private set; }
    public BagDisplay BagDisplay => _bagDisplay;
    public PartyDisplay PartyDisplay => _partyDisplay;
    public LearnMove_Pause LearnMoveMenu => _learnMoveMenu;

    public override void EnterState( UI_PauseMenuStateMachine owner ){
        Debug.Log( $"{this} EnterState()" );
        //--Set State Machine
        PauseMenuStateMachine = owner;
        _playerInventory = _bagDisplay.PlayerInventory;

        //--Events
        _bagDisplay.OnPocketChanged += SetNewPocketInitialButton;
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
        _bagDisplay.OnPocketChanged += SetNewPocketInitialButton;
        GameStateController.Instance.OnDialogueStateEntered += _bagDisplay.EnterDialogueWrapper;
        GameStateController.Instance.OnDialogueStateExited += _bagDisplay.ExitDialogueWrapper;

        //--Select Appropriate Button
        StartCoroutine( SetInitialButton() );
    }

    public override void PauseState(){
        Debug.Log( $"{this} PauseState()" );
        //--Events
        _bagDisplay.OnPocketChanged -= SetNewPocketInitialButton;
        GameStateController.Instance.OnDialogueStateEntered -= _bagDisplay.EnterDialogueWrapper;
        GameStateController.Instance.OnDialogueStateExited -= _bagDisplay.ExitDialogueWrapper;

        //--Disable Item Buttons
        _bagDisplay.SetItemButtons_Interactable( false );
    }

    public override void ExitState(){
        //--Events
        _bagDisplay.OnPocketChanged -= SetNewPocketInitialButton;
        GameStateController.Instance.OnDialogueStateEntered -= _bagDisplay.EnterDialogueWrapper;
        GameStateController.Instance.OnDialogueStateExited -= _bagDisplay.ExitDialogueWrapper;
        
        //--Close menu
        gameObject.SetActive( false );
    }

    private void UpdateItemList(){
        _bagDisplay.OnItemListRequest?.Invoke();
    }

    public void UseItem( Item item ){
        _bagDisplay.SetSelectedItem( item );
        PauseMenuStateMachine.StateMachine.Push( _useItemFromBagState );
    }

    public void UseTM( Item item ){
        //--Code to display whether a pokemon can learn
        //--or whether it already knows the selected TM
        //--on the party screen. and by code i probably mean an event
        //--that raises it for PokemonButton_UseItemFromPause

        UseItem( item );
    }

    private void SetNewPocketInitialButton(){
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

}
