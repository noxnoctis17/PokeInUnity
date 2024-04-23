using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PKMN_Button : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    [SerializeField] private PKMNMenu _pkmnMenu;
    [SerializeField] GameObject _selectedOutline;
    public PokemonClass Pokemon;
    public Button ThisButton { get; private set; }
    private bool _isFaintedSelect;

    private void OnEnable(){
        BattleSystem.OnPlayerPokemonFainted += SetFaintSelectTrue;
        BattleSystem.OnPlayerChoseNextPokemon += SetFaintSelectFalse;

        ThisButton = GetComponent<Button>();
    }

    private void DisEnable(){
        BattleSystem.OnPlayerPokemonFainted -= SetFaintSelectTrue;
        BattleSystem.OnPlayerChoseNextPokemon -= SetFaintSelectFalse;
    }

    public void OnSelect( BaseEventData eventData ){
        _selectedOutline.SetActive( true );
    }

    public void OnDeselect( BaseEventData eventData ){
        _selectedOutline.SetActive( false );
    }

    public void OnSubmit( BaseEventData eventData ){
        Debug.Log ("fainted select in button is: " + _isFaintedSelect );
        //--we're popping the state first because code is sequential
        //--all battle system stuff would get run to completion before we get to the end, where we'd finally pop the state
        //--we need to pop the state immediately. i have already made this change in MoveButton. I will likely need to do this
        //--for the pokeball (soon to be items) button.
        
        _pkmnMenu.BattleMenu.BattleMenuStateMachine.Pop();

        if( Pokemon.CurrentHP <= 0 ){
            Debug.Log( "You can't select a fainted Pokemon!" ); //message pop up eventually
            return;
        }

        if( Pokemon == _pkmnMenu.BattleSystem.PlayerUnit.Pokemon ){
            Debug.Log( "This Pokemon is already out!" ); //message pop up eventually
            return;
        }

        if( _isFaintedSelect ){ //--If switch is caused by a faint, we don't add the command to the command queue
            BattleSystem.OnPlayerChoseNextPokemon?.Invoke();
            _pkmnMenu.BattleSystem.SetFaintedSwitchMon( Pokemon );
        }
        else{
            _pkmnMenu.BattleSystem.SetSwitchPokemonCommand( Pokemon );
            BattleUIActions.OnCommandUsed?.Invoke();
            BattleUIActions.OnSubMenuClosed?.Invoke();
        }
        
        _pkmnMenu.SetMemoryButton( ThisButton );
    }

    public void OnCancel( BaseEventData baseEventData ){
        if( _isFaintedSelect ){
            _pkmnMenu.BattleMenu.BattleSystem.DialogueBox.TypeDialogue( "You need to select a Pokemon!" );
        }
        else{
            _pkmnMenu.ClearMemoryButton();
            BattleUIActions.OnSubMenuClosed?.Invoke();
            StartCoroutine( WaitForCloseAnims() );
        }
    }

    private IEnumerator WaitForCloseAnims(){
        yield return new WaitForSeconds( 0.1f );
        // _pkmnMenu.gameObject.SetActive( false );
        _pkmnMenu.BattleMenu.BattleMenuStateMachine.Pop();
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
