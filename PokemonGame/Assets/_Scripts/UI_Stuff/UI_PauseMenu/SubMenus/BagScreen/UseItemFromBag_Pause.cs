using System.Collections;
using NoxNoctisDev.StateMachine;
using UnityEngine;
using UnityEngine.UI;

public class UseItemFromBag_Pause : State<UI_PauseMenuStateMachine>
{
    private UI_PauseMenuStateMachine PauseMenuStateMachine;
    private Inventory _playerInventory;
    private BagDisplay _bagDisplay;
    private PokemonButton[] _pkmnButtons;
    private Button _initialButton;
    public Button LastButton { get; private set; }
    
    public override void EnterState( UI_PauseMenuStateMachine owner ){
        //--State Machine
        PauseMenuStateMachine = owner;

        //--References
        _bagDisplay = gameObject.GetComponentInChildren<BagDisplay>();
        _playerInventory = _bagDisplay.PlayerInventory;

        //--Events
        _bagDisplay.PartyDisplay.OnSubmittedButton              += SetMemoryButton;
        GameStateController.Instance.OnDialogueStateEntered     += PauseState;
        GameStateController.Instance.OnDialogueStateExited      += ReturnToState;
        EvolutionManager.Instance.OnEvolutionStateEntered       += PauseState;
        EvolutionManager.Instance.OnEvolutionStateExited        += ReturnToState;

        //--Button Array
        _pkmnButtons = new PokemonButton[]{};
        _pkmnButtons = _bagDisplay.PartyDisplay.PKMNButtons;

        var itemCategory = _bagDisplay.ItemSelected.ItemSO.ItemCategory;

        if( itemCategory == ItemCategory.Training ){
            if( _bagDisplay.ItemSelected.ItemSO is EvolutionItemsSO )
                _bagDisplay.PartyDisplay.OnEvolutionItemSelected?.Invoke( _bagDisplay.ItemSelected, true );
        }

        if( itemCategory == ItemCategory.TM )
            _bagDisplay.PartyDisplay.OnTMSelected?.Invoke( _bagDisplay.ItemSelected, true );

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
        _bagDisplay.PartyDisplay.OnSubmittedButton              -= SetMemoryButton;
        GameStateController.Instance.OnDialogueStateEntered     -= PauseState;
        GameStateController.Instance.OnDialogueStateExited      -= ReturnToState;
        EvolutionManager.Instance.OnEvolutionStateEntered       -= PauseState;
        EvolutionManager.Instance.OnEvolutionStateExited        -= ReturnToState;

        if( _bagDisplay.ItemSelected != null ){
            var itemCategory = _bagDisplay.ItemSelected.ItemSO.ItemCategory;

            if( itemCategory == ItemCategory.Training ){
                if( _bagDisplay.ItemSelected.ItemSO is EvolutionItemsSO )
                    _bagDisplay.PartyDisplay.OnEvolutionItemSelected?.Invoke( _bagDisplay.ItemSelected, false );
            }

            if( itemCategory == ItemCategory.TM )
                _bagDisplay.PartyDisplay.OnTMSelected?.Invoke( _bagDisplay.ItemSelected, false );
        }
        else if( _bagDisplay.ItemSelected == null ){
            _bagDisplay.PartyDisplay.OnEvolutionItemSelected?.Invoke( null, false );
            _bagDisplay.PartyDisplay.OnTMSelected?.Invoke( null, false );
        }

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
