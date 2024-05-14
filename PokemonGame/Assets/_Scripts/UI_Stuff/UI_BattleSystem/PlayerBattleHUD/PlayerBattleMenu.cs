using UnityEngine;
using UnityEngine.UI;
using NoxNoctisDev.StateMachine;
using System;

public class PlayerBattleMenu : MonoBehaviour
{
    //================================================================================
    [SerializeField] private Button _fightButton, _pkmnButton, _bagButton, _runButton;
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private State<PlayerBattleMenu> _baseState;
    [SerializeField] private State<PlayerBattleMenu> _pauseMenuState;
    [SerializeField] private State<PlayerBattleMenu> _moveLearnSelectionState;
    public BattleSystem BattleSystem => _battleSystem;
    public State<PlayerBattleMenu> BaseState => _baseState;
    public State<PlayerBattleMenu> PausedState => _pauseMenuState;
    public State<PlayerBattleMenu> MoveLearnSelectionState => _moveLearnSelectionState;
    public Button FightButton => _fightButton;
    public Button PkmnButton => _pkmnButton;
    public Button BagButton => _bagButton;
    public Button RunButton => _runButton;
    public BattleUIActions BUIActions { get; private set; }
    public StateStackMachine<PlayerBattleMenu> StateMachine { get; private set; }
    public Button[] Buttons { get; private set; }
    public PlayerInput PlayerInput { get; private set; }
    public Action<State<PlayerBattleMenu>> OnPushNewState;
    public Action<State<PlayerBattleMenu>> OnChangeState;
    public Action OnPauseState;
    public Action OnUnpauseState;

    //================================================================================

    private void OnEnable(){
        Debug.Log( "Enable PlayerBattleMenu ");
        //--State Machine
        StateMachine = new StateStackMachine<PlayerBattleMenu>( this );

        //--Events
        OnPushNewState  += PushState;
        OnChangeState   += ChangeState;
        OnPauseState    += PauseMenu;
        OnUnpauseState  += UnpauseMenu;
        GameStateController.Instance.OnDialogueStateEntered += PauseMenu;
        GameStateController.Instance.OnDialogueStateExited  += UnpauseMenu;
        
        //--Reference Assignments
        PlayerInput = PlayerReferences.Instance.PlayerInput;
        BUIActions = GetComponent<BattleUIActions>();

        //--Set Button Array
        Buttons = new Button[] { _runButton, _bagButton, _pkmnButton, _fightButton  };

        //--Set Menu to Default Positions
        ResetMenuPositions();

        //--Push base menu state
        PushState( _baseState );
    }

    private void OnDisable(){
        Debug.Log( "Disable PlayerBattleMenu ");
        //--Events
        OnPushNewState  -= PushState;
        OnPauseState    -= PauseMenu;
        OnUnpauseState  -= UnpauseMenu;
        GameStateController.Instance.OnDialogueStateEntered -= PauseMenu;
        GameStateController.Instance.OnDialogueStateExited  -= UnpauseMenu;

        //--State Machine
        StateMachine.ClearStack();
        StateMachine = null;

        //--Buttons
        Buttons = null;

        //--Controls
        PlayerReferences.Instance.PlayerController.DisableBattleControls();
        PlayerReferences.Instance.PlayerController.EnableCharacterControls();
    }

    public void PopState(){
        StateMachine.Pop();
    }

    public void ChangeState( State<PlayerBattleMenu> newState ){
        StateMachine.ChangeState( newState );
    }

    private void PushState( State<PlayerBattleMenu> newState ){
        StateMachine.Push( newState );
    }

    private void PauseMenu(){
        StateMachine.Push( _pauseMenuState );
    }

    private void UnpauseMenu(){
        StateMachine.Pop();
    }

    public void ResetMenuPositions(){
        int step = 45;

        for( int i = 0; i < Buttons.Length; i++ ){
            var resetRotation = Quaternion.Euler( 0f, 0f, 0f );
            Buttons[i].GetComponent<RectTransform>().rotation = resetRotation;

            var defaultRotation = Buttons[i].GetComponent<RectTransform>().rotation * Quaternion.Euler( 0f, 0f, step );
            Buttons[i].GetComponent<RectTransform>().rotation = defaultRotation;

            Buttons[i].GetComponent<RectTransform>().SetAsLastSibling();

            step -= 15;
        }
    }

    public void EnableMenuButtons(){
        foreach( Button button in Buttons ){
            button.interactable = true;
        }
    }

    public void DisableMenuButtons(){
        foreach( Button button in Buttons ){
            button.interactable = false;
        }
    }

#if UNITY_EDITOR
    private void OnGUI(){
        var style = new GUIStyle();
        style.font = Resources.Load<Font>( "Fonts/Gotham Bold Outlined" );
        style.fontSize = 30;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;
        style.richText = true;

        GUILayout.BeginArea( new Rect( 0, 0, 600, 500 ) );
        GUILayout.Label( "STATE STACK", style );
        foreach( var state in StateMachine.StateStack ){
            GUILayout.Label( state.GetType().ToString(), style );
        }
        GUILayout.EndArea();
    }
#endif

}
