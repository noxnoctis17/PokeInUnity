using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PKMN_Button : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    [SerializeField] private GameObject _pkmnMenu;
    [SerializeField] private GameObject _selectedOutline;
    private PartyScreenContext _partyScreenContext;
    public Pokemon Pokemon;
    public Button ThisButton { get; private set; }
    private bool _isFaintedSelect;

    private void OnEnable(){
        ThisButton = GetComponent<Button>();
    }

    private void Disable(){
        switch( _partyScreenContext )
        {
            case PartyScreenContext.Battle:
                BattleSystem.OnPlayerPokemonFainted -= SetFaintSelectTrue;
                BattleSystem.OnPlayerChoseNextPokemon -= SetFaintSelectFalse;
            break;

            case PartyScreenContext.PauseMenu:
            break;

            case PartyScreenContext.ItemUse:
            break;
        }
    }

    public void Setup( PartyScreenContext context ){
        _partyScreenContext = context;

        switch( _partyScreenContext )
        {
            case PartyScreenContext.Battle:
                BattleSystem.OnPlayerPokemonFainted += SetFaintSelectTrue;
                BattleSystem.OnPlayerChoseNextPokemon += SetFaintSelectFalse;
            break;

            case PartyScreenContext.PauseMenu:
            break;

            case PartyScreenContext.ItemUse:
            break;
        }
    }

    public void OnSelect( BaseEventData eventData ){
        _selectedOutline.SetActive( true );
    }

    public void OnDeselect( BaseEventData eventData ){
        _selectedOutline.SetActive( false );
    }

    public void OnSubmit( BaseEventData eventData ){

        switch( _partyScreenContext )
        {
            case PartyScreenContext.Battle:
                BattleSubmit();
            break;

            case PartyScreenContext.PauseMenu:
                PauseSubmit();
            break;

            case PartyScreenContext.ItemUse:
                ItemSubmit();
            break;
        }

    }

    public void OnCancel( BaseEventData baseEventData ){

        switch( _partyScreenContext )
        {
            case PartyScreenContext.Battle:
                BattleCancel();
            break;

            case PartyScreenContext.PauseMenu:
                PauseCancel();
            break;

            case PartyScreenContext.ItemUse:
                ItemCancel();
            break;
        }
    }

    private IEnumerator WaitForCloseAnims(){
        yield return new WaitForSeconds( 0.1f );
        // _pkmnMenu.gameObject.SetActive( false );
        _pkmnMenu.GetComponent<PKMNMenu_Events>().OnPopPartyScreenState?.Invoke();
    }

    private void BattleSubmit(){
        Debug.Log ("fainted select in button is: " + _isFaintedSelect );
        //--we're popping the state first because code is sequential
        //--all battle system stuff would get run to completion before we get to the end, where we'd finally pop the state
        //--we need to pop the state immediately. i have already made this change in MoveButton. I will likely need to do this
        //--for the pokeball (soon to be items) button.

        var pkmnBattleMenu = _pkmnMenu.GetComponent<PKMNMenu_Battle>();
        
        _pkmnMenu.GetComponent<PKMNMenu_Events>().OnPopPartyScreenState?.Invoke();
        // pkmnBattleMenu.BattleMenu.BattleMenuStateMachine.Pop();

        if( Pokemon.CurrentHP <= 0 ){
            Debug.Log( "You can't select a fainted Pokemon!" ); //message pop up eventually
            return;
        }

        if( Pokemon == pkmnBattleMenu.BattleSystem.PlayerUnit.Pokemon ){
            Debug.Log( "This Pokemon is already out!" ); //message pop up eventually
            return;
        }

        if( _isFaintedSelect ){ //--If switch is caused by a faint, we don't add the command to the command queue
            BattleSystem.OnPlayerChoseNextPokemon?.Invoke();
            pkmnBattleMenu.BattleSystem.SetFaintedSwitchMon( Pokemon );
        }
        else{
            pkmnBattleMenu.BattleSystem.SetSwitchPokemonCommand( Pokemon );
            BattleUIActions.OnCommandUsed?.Invoke();
            BattleUIActions.OnSubMenuClosed?.Invoke();
        }
        
        pkmnBattleMenu.SetMemoryButton( ThisButton );
    }

    private void PauseSubmit(){
        Debug.Log( Pokemon.PokeSO.pName );
    }

    private void ItemSubmit(){

    }

    private void BattleCancel(){
        var pkmnBattleMenu = _pkmnMenu.GetComponent<PKMNMenu_Battle>();

        if( _isFaintedSelect ){
            StartCoroutine( pkmnBattleMenu.BattleMenu.BattleSystem.DialogueBox.TypeDialogue( "You need to select a Pokemon!" ) );
        }
        else{
            pkmnBattleMenu.ClearMemoryButton();
            BattleUIActions.OnSubMenuClosed?.Invoke();
            StartCoroutine( WaitForCloseAnims() );
        }
    }
    
    private void PauseCancel(){
        StartCoroutine( WaitForCloseAnims() );
    }

    private void ItemCancel(){

    }

    private void SetFaintSelectTrue(){
        Debug.Log( "SetFaintSelectTrue in button fired. should it have?" );
        _isFaintedSelect = true;
    }

    private void SetFaintSelectFalse(){
        Debug.Log( "SetFaintSelectFalse in button fired. should it have?" );
        _isFaintedSelect = false;
    }

}
