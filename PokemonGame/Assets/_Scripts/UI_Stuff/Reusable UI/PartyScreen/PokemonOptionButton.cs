using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class PokemonOptionButton : MonoBehaviour, ISubmitHandler, ISelectHandler, IDeselectHandler, ICancelHandler
{
    private PartyDisplay _partyDisplay;
    private PartyScreen_Pause _partyScreen;
    private PokemonOptionMenu _optionMenu;
    [SerializeField] private OptionMenuType _optionMenuType;
    public OptionMenuType OptionMenuType => _optionMenuType;
    public Button ThisButton { get; private set; }

    public void Setup( PartyDisplay partyDisplay, PartyScreen_Pause pokemonScreen, PokemonOptionMenu optionMenu )
    {
        _partyDisplay = partyDisplay;
        _partyScreen = pokemonScreen;
        _optionMenu = optionMenu;
        ThisButton = GetComponent<Button>();

        gameObject.SetActive( true );
    }

    public void OnSelect( BaseEventData eventData )
    {
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
    }

    public void OnDeselect( BaseEventData eventData )
    {
        
    }

    public void OnSubmit( BaseEventData eventData )
    {
        switch( _optionMenuType )
        {
            case OptionMenuType.SwitchPokemon:
                StartCoroutine( SwitchPokemon() );
            break;

            case OptionMenuType.GiveItem:
            break;

            case OptionMenuType.FollowerPokemon:
                _partyScreen.SetFollowerPokemon( _optionMenu.ContextPokemon );
                
                if( _partyScreen.StateMachine.CurrentState == _optionMenu )
                    _partyScreen.StateMachine.PopState();
            break;

            case OptionMenuType.EditPokemon:
                _partyScreen.EditPokemon( _optionMenu.ContextPokemon );
            break;

            case OptionMenuType.EvolvePokemon:
                if( _optionMenu.ContextPokemon.CanEvolveByLevelUp && _optionMenu.ContextPokemon.CheckForEvolution() != null )
                    StartCoroutine( TriggerEvolution() );
            break;
        }
    }

    public void OnCancel( BaseEventData eventData )
    {
        if( _partyScreen.IsSwitchingPokemon )
        {
            DialogueManager.Instance.PlaySystemMessage( $"You have to select a different Pokemon!", true );
            return;
        }
        else
            _partyScreen.StateMachine.PopState();
    }

    private IEnumerator TriggerEvolution()
    {
        Debug.Log( $"Evolving {_optionMenu.ContextPokemon.NickName}!" );
        _optionMenu.SetButtons_Interactable( false );

        var evolution = _optionMenu.ContextPokemon.CheckForEvolution();
        GameStateController.Instance.PushGameState( EvolutionManager.Instance );
        yield return EvolutionManager.Instance.Evolve( _optionMenu.ContextPokemon, evolution );

        _optionMenu.SetButtons_Interactable( true );

        if( _partyScreen.StateMachine.CurrentState == _optionMenu )
            _partyScreen.StateMachine.PopState();
    }

    private IEnumerator SwitchPokemon()
    {
        Vector3 defaultPos = _optionMenu.transform.position;
        _optionMenu.PartyScreen.SetSwitchingPokemonState( true );
        _optionMenu.SetButtons_Interactable( false );
        _optionMenu.transform.DOMoveX( 3000f, 0f );
        yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"Switch with which Pokemon?", true );
        yield return null;

        _partyDisplay.SetPartyButtons_Interactable( true );
        _partyScreen.InitialButton.Select();

        Debug.Log( $"We're now waiting for SwitchToPokemon to no longer be null!" );
        yield return new WaitUntil( () => _optionMenu.SwitchToPokemon != null );
        yield return null;

        _partyDisplay.SetPartyButtons_Interactable( false );
        _partyDisplay.PlayerTrainer.SwitchPokemonPosition( _optionMenu.ContextPokemon, _optionMenu.SwitchToPokemon );
        yield return null;

        _optionMenu.SetButtons_Interactable( true );

        if( _partyScreen.StateMachine.CurrentState == _optionMenu )
            _partyScreen.StateMachine.PopState();

        _optionMenu.PartyScreen.SetSwitchingPokemonState( false );
        _optionMenu.SetSwitchToPokemon( null );
        _optionMenu.transform.DOMoveX( defaultPos.x, 0f );
    }
}
