using UnityEngine;
using UnityEngine.UI;
using NoxNoctisDev.StateMachine;
using System;
using System.Collections;

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
    public StateStackMachine<PlayerBattleMenu> BattleMenuStateMachine { get; private set; }
    public Button[] Buttons { get; private set; }
    public PlayerInput PlayerInput { get; private set; }
    public Action<State<PlayerBattleMenu>> OnChangeState;

    //================================================================================

    private void OnEnable(){
        Debug.Log( "Enable PlayerBattleMenu ");
        //--State Machine
        BattleMenuStateMachine = new StateStackMachine<PlayerBattleMenu>( this );

        //--Events
        OnChangeState += PushState;
        GameStateController.Instance.OnDialogueStateEntered += PauseMenu;
        GameStateController.Instance.OnDialogueStateExited += UnPauseMenu;
        
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
        OnChangeState -= PushState;
        GameStateController.Instance.OnDialogueStateEntered -= PauseMenu;
        GameStateController.Instance.OnDialogueStateExited -= UnPauseMenu;

        //--State Machine
        BattleMenuStateMachine.ClearStack();
        BattleMenuStateMachine = null;

        //--Buttons
        Buttons = null;

        //--Controls
        PlayerReferences.Instance.PlayerController.DisableBattleControls();
        PlayerReferences.Instance.PlayerController.EnableCharacterControls();
    }

    private void PushState( State<PlayerBattleMenu> newState ){
        BattleMenuStateMachine.Push( newState );
    }

    private void PauseMenu(){
        BattleMenuStateMachine.Push( _pauseMenuState );
    }

    private void UnPauseMenu(){
        BattleMenuStateMachine.Pop();
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

// #if UNITY_EDITOR
//     private void OnGUI(){
//         var style = new GUIStyle();
//         style.fontSize = 30;
//         style.fontStyle = FontStyle.Bold;
//         style.normal.textColor = Color.black;

//         GUILayout.BeginArea( new Rect( 0, 0, 600, 500 ) );
//         GUILayout.Label( "STATE STACK", style );
//         foreach( var state in BattleMenuStateMachine.StateStack ){
//             GUILayout.Label( state.GetType().ToString(), style );
//         }
//         GUILayout.EndArea();
//     }
// #endif

}
