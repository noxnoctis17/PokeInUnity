using System.Collections;
using NoxNoctisDev.StateMachine;
using UnityEngine;
using UnityEngine.UI;

public class UseItemFromBag_Battle : State<PlayerBattleMenu>
{
    private PlayerBattleMenu _battleMenu;
    private Inventory _playerInventory;
    private BagDisplay _bagDisplay;
    private PokemonButton[] _pkmnButtons;
    private Button _initialButton;
    public Button LastButton { get; private set; }
    
    public override void EnterState( PlayerBattleMenu owner ){
        //--State Machine
        _battleMenu = owner;

        //--References
        _bagDisplay = gameObject.GetComponentInChildren<BagDisplay>();
        _playerInventory = _bagDisplay.PlayerInventory;

        //--Events
        _bagDisplay.PartyDisplay.OnSubmittedButton += SetMemoryButton;
        GameStateController.Instance.OnDialogueStateEntered += PauseState;
        GameStateController.Instance.OnDialogueStateExited += ReturnToState;

        //--Button Array
        _pkmnButtons = new PokemonButton[]{};
        _pkmnButtons = _bagDisplay.PartyDisplay.PKMNButtons;

        //--Select Initial Button;
        _initialButton = _pkmnButtons[0].ThisButton;
        _bagDisplay.PartyDisplay.SetPartyButtons_Interactable( true );
        StartCoroutine( SetInitialButton() );
    }

    public override void ReturnToState(){
        _bagDisplay.PartyDisplay.OnSubmittedButton += SetMemoryButton;
        _bagDisplay.PartyDisplay.SetPartyButtons_Interactable( true );

        StartCoroutine( SetInitialButton() );
    }

    public override void PauseState(){
        _bagDisplay.PartyDisplay.OnSubmittedButton -= SetMemoryButton;
        _bagDisplay.PartyDisplay.SetPartyButtons_Interactable( false );
    }

    public override void ExitState(){
        Debug.Log( $"{this} ExitState()" );
        //--Events
        _bagDisplay.PartyDisplay.OnSubmittedButton -= SetMemoryButton;
        GameStateController.Instance.OnDialogueStateEntered -= PauseState;
        GameStateController.Instance.OnDialogueStateExited -= ReturnToState;

        _bagDisplay.PartyDisplay.SetPartyButtons_Interactable( false );
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
