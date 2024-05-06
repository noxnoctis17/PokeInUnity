using System.Collections;
using NoxNoctisDev.StateMachine;
using UnityEngine;
using UnityEngine.UI;

public class UI_PauseMenu : State<UI_PauseMenuStateMachine>
{
    public UI_PauseMenuStateMachine PauseMenuStateMachine { get; private set; }
    public UI_PauseMenuButton_Events ButtonEvents { get; private set; }
    [SerializeField] private Button _pokedexButton, _pokemonButton, _bagButton, _saveButton, _loadButton, _optionsButton;
    private Button[] _buttons;
    private Button _initialButton;
    public Button LastButton { get; private set; }

    public override void EnterState( UI_PauseMenuStateMachine owner ){
        PauseMenuStateMachine = owner;
        gameObject.SetActive( true );

        //--Components
        ButtonEvents = GetComponent<UI_PauseMenuButton_Events>();
        Debug.Log( ButtonEvents );

        //--Events
        ButtonEvents.OnButtonSubmitted += SetMemoryButton;

        //--Set Controls
        PlayerReferences.Instance.PlayerController.DisableCharacterControls();
        PlayerReferences.Instance.PlayerController.EnableUI();

        //--Throw Buttons in Array for mass handling
        _buttons = new Button[]{ _pokedexButton, _pokemonButton, _bagButton, _saveButton, _loadButton, _optionsButton };
        SetupButtons();

        //--Select Initial Button
        _initialButton = _pokemonButton;
        StartCoroutine( SetInitialButton() );
    }

    public override void ReturnToState(){
        //--Events
        ButtonEvents.OnButtonSubmitted += SetMemoryButton;
        
        //--Enable interactability of buttons in this menu
        foreach( Button button in _buttons ){
            button.interactable = true;
        }

        SelectMemoryButton();
    }

    public override void PauseState(){
        //--Events
        ButtonEvents.OnButtonSubmitted -= SetMemoryButton;

        //--Disable interactability of buttons in this menu
        foreach( Button button in _buttons ){
            button.interactable = false;
        }
    }

    public override void ExitState(){
        //--Set Controls
        PlayerReferences.Instance.PlayerController.DisableUI();
        PlayerReferences.Instance.PlayerController.EnableCharacterControls();

        //--Events
        ButtonEvents.OnButtonSubmitted -= SetMemoryButton;

        CloseMenu();
    }

    private void SetupButtons(){
        foreach( Button button in _buttons ){
            button.GetComponent<UI_PauseMenuButton>().Setup( this );
        }
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

    public void CloseMenu(){
        gameObject.SetActive( false );
    }

}
