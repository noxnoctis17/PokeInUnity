using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class EffortValueEditor : MonoBehaviour, IPokemonEditor_Button
{
    [SerializeField] private Stat _stat;
    PartyScreen_PokemonEditor _editor;
    private Pokemon _pokemon;
    private PlayerInput _input;
    private bool _isEditing;
    public Button ThisButton { get; set; }

    public void Setup( PartyScreen_PokemonEditor editor, Pokemon pokemon )
    {
        _editor = editor;
        _pokemon = pokemon;
        ThisButton = GetComponent<Button>();
        _input = PlayerReferences.Instance.PlayerInput;
    }

    private void EnableEVControls()
    {
        _input.UI.Navigate.performed += EditEVs;
    }

    private void DisableEVControls()
    {
        _input.UI.Navigate.performed -= EditEVs;
    }

    public void OnSelect( BaseEventData eventData )
    {
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
    }

    public void OnSubmit( BaseEventData eventData )
    {
        //--We will freely navigate all "editor" buttons
        //--When we "submit" on a stat editor button
        //--we will disable all other editor buttons
        //--And then enable the editor control events.
        //--left and right will remove or add 4 effort points, respectively
        //--up and down will add and remove 12 effort points, respectively
        _editor.LockAllButtonsExcept( this );
        EnableEVControls();
        _isEditing = true;
    }

    public void OnDeselect( BaseEventData eventData )
    {

    }

    public void OnCancel( BaseEventData eventData )
    {
        if( _isEditing )
        {
            _isEditing = false;
            DisableEVControls();
            _editor.EnableAllButtons();
        }
        else
        {
            if( _editor.StateMachine.CurrentState == _editor )
                _editor.StateMachine.PopState();
        }
    }

    private void EditEVs( InputAction.CallbackContext context )
    {
        var direction = context.ReadValue<Vector2>();

        if( _pokemon.RemainingEffortPoints > 0 )
        {
            //--Increase by 1
            if( direction.x > 0 )
                _pokemon.AssignEVs( _stat, 1 );
            
            //--Decrease by 1
            if( direction.x < 0 && _pokemon.EffortValues[_stat] != 0 )
                _pokemon.AssignEVs( _stat, -1 );

            //--Incrase by 4
            if( direction.y > 0 && _pokemon.RemainingEffortPoints >= 4 )
                _pokemon.AssignEVs( _stat, 4 );
            
            //--Decrease by 4
            if( direction.y < 0 && _pokemon.RemainingEffortPoints >= 4 && _pokemon.EffortValues[_stat] != 0 )
                _pokemon.AssignEVs( _stat, -4 );
        }
        else
        {
            //--This makes sure you can remove effort points from a stat that has them when you have 0 remaining effort points in the pool.
            if( direction.x < 0 && _pokemon.EffortValues[_stat] > 0 )
                _pokemon.AssignEVs( _stat, -1 );

            else if( direction.y < 0 && _pokemon.EffortValues[_stat] >= 4 )
                _pokemon.AssignEVs( _stat, -4 );
        }

        _editor.PartyScreen.UpdateEVs( _pokemon );
        _pokemon.StatUpdated();
        _editor.PartyScreen.UpdateStats( _pokemon );
    }
}
