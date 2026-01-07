using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NoxNoctisDev.StateMachine;
using UnityEngine;
using UnityEngine.UI;

public enum OptionMenuType { SwitchPokemon, EvolvePokemon, FollowerPokemon, GiveItem, EditPokemon, }
public class PokemonOptionMenu : State<UI_PauseMenuStateMachine>
{
    public UI_PauseMenuStateMachine PauseMenuStateMachine { get; private set; }
    [SerializeField] private PokemonOptionButton[] _optionButtons;
    private PartyScreen_Pause _partyScreen_Pause;
    private PartyDisplay _partyDisplay;
    private Pokemon _contextPokemon;
    private Pokemon _switchToPokemon;
    private PokemonButton _pkmnButton;
    public PartyScreen_Pause PartyScreen => _partyScreen_Pause;
    public PartyDisplay Display => _partyDisplay;
    public Pokemon ContextPokemon => _contextPokemon;
    public Pokemon SwitchToPokemon => _switchToPokemon;

    public override void EnterState( UI_PauseMenuStateMachine owner ){
        Debug.Log( "EnterState: " + this );
        //--Set State Machine
        PauseMenuStateMachine = owner;
    }

    public override void ReturnToState()
    {
        gameObject.SetActive( true );
        _optionButtons[0].ThisButton.Select();
    }

    public override void PauseState()
    {
        gameObject.SetActive( false );
    }

    public override void ExitState()
    {
        gameObject.SetActive( false );
    }

    public void Init( PartyScreen_Pause partyScreen, PartyDisplay display, Pokemon pokemon, PokemonButton button )
    {
        _partyScreen_Pause = partyScreen;
        _partyDisplay = display;
        _contextPokemon = pokemon;
        _pkmnButton = button;

        //--Open Menu
        OpenPopUpMenu();

        //--Select first button
        _optionButtons[0].ThisButton.Select();
    }

    private void OpenPopUpMenu()
    {
        //--Enable appropriate buttons
        SetButtons();

        //--Move to appropriate pokemon first!
        Vector3[] corners = new Vector3[4];
        _pkmnButton.Container.GetWorldCorners( corners );

        var rect = gameObject.GetComponent<RectTransform>().parent as RectTransform;
        RectTransformUtility.ScreenPointToLocalPointInRectangle( rect, RectTransformUtility.WorldToScreenPoint( null, corners[3] ), null, out Vector2 localPoint );
        
        gameObject.GetComponent<RectTransform>().anchoredPosition = localPoint;
        gameObject.SetActive( true );
    }

    private void SetButtons()
    {
        for( int i = 0; i < _optionButtons.Length; i++ )
        {
            if( _optionButtons[i].OptionMenuType == OptionMenuType.EvolvePokemon && !_contextPokemon.CanEvolveByLevelUp )
                continue;
            else
                _optionButtons[i].Setup( _partyDisplay, _partyScreen_Pause, this );
        }
    }

    public void SetSwitchToPokemon( Pokemon pokemon )
    {
        _switchToPokemon = pokemon;
    }

    public void SetButtons_Interactable( bool value )
    {
        foreach( var button in _optionButtons )
        {
            if( button.isActiveAndEnabled )
                button.ThisButton.interactable = value;
        }
    }
}
