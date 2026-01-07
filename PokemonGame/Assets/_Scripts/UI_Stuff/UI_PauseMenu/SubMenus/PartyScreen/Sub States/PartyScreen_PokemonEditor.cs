using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class PartyScreen_PokemonEditor : State<UI_PauseMenuStateMachine>
{
    public UI_PauseMenuStateMachine StateMachine { get; private set; }
    [SerializeField] private PartyScreen_Pause _partyScreen;
    [SerializeField] private GameObject _editorButtonContainer;
    private IPokemonEditor_Button[] _editorButtons;
    public PartyScreen_Pause PartyScreen => _partyScreen;
    public Pokemon Pokemon { get; private set; }

    public override void EnterState( UI_PauseMenuStateMachine owner )
    {
        StateMachine = owner;
        Pokemon = _partyScreen.PokemonOptionMenu.ContextPokemon;
        SetButtons();

        gameObject.SetActive( true );
        _editorButtons[0].ThisButton.Select();
    }

    public override void ReturnToState()
    {
    
    }

    public override void PauseState()
    {
    
    }

    public override void ExitState()
    {
        gameObject.SetActive( false );
    }

    private void SetButtons()
    {
        _editorButtons = _editorButtonContainer.GetComponentsInChildren<IPokemonEditor_Button>();

        for( int i = 0; i < _editorButtons.Length; i++ )
        {
            _editorButtons[i].Setup( this, Pokemon );
        }
    }

    public void EnableAllButtons()
    {
        for( int i = 0; i< _editorButtons.Length; i++ )
        {
            _editorButtons[i].ThisButton.interactable = true;
        }
    }

    public void LockAllButtonsExcept( IPokemonEditor_Button button )
    {
        for( int i = 0; i< _editorButtons.Length; i++ )
        {
            if( _editorButtons[i] == button )
                continue;
            else
                _editorButtons[i].ThisButton.interactable = false;
        }
    }
}
