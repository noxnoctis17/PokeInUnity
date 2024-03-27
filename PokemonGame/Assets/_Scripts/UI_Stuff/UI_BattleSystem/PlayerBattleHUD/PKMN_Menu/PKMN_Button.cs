using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PKMN_Button : MonoBehaviour, ISelectHandler, IDeselectHandler, ICancelHandler, ISubmitHandler
{
    [SerializeField] private PKMNMenu _pkmnMenu;
    [SerializeField] GameObject _selectedOutline;
    public PokemonClass Pokemon;
    private bool _isFaintedSelect;

    private void OnEnable(){
        BattleSystem.OnPlayerPokemonFainted += SetFaintSelectTrue;
        BattleSystem.OnPlayerChoseNextPokemon += SetFaintSelectFalse;
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
            _pkmnMenu.BattleSystem.ClosePartyMenu( Pokemon );
        }
        else{
            _pkmnMenu.BattleSystem.SetSwitchPokemonCommand( Pokemon );
            BattleUIActions.OnCommandUsed?.Invoke();
            BattleUIActions.OnSubMenuClosed?.Invoke();
        }
        
        Debug.Log( "Submitted Button: " + GetComponent<Button>().gameObject.name );
        _pkmnMenu.PartyScreen.OnSubmittedButton?.Invoke( GetComponent<Button>() );
        _pkmnMenu.BattleMenu.BattleMenuStateMachine.Pop();
    }

    public void OnCancel( BaseEventData baseEventData ){
        BattleUIActions.OnSubMenuClosed?.Invoke();
        StartCoroutine( WaitForCloseAnims() );
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
