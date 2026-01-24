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
    [SerializeField] private State<PlayerBattleMenu> _pausedState;
    [SerializeField] private State<PlayerBattleMenu> _moveLearnSelectionState;
    [SerializeField] private State<PlayerBattleMenu> _targetSelectState;
    public BattleSystem BattleSystem => _battleSystem;
    public State<PlayerBattleMenu> BaseState => _baseState;
    public State<PlayerBattleMenu> PausedState => _pausedState;
    public State<PlayerBattleMenu> MoveLearnSelectionState => _moveLearnSelectionState;
    public State<PlayerBattleMenu> TargetSelectState => _targetSelectState;
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
    public BattleUnit Attacker { get; private set; }
    public Move ChosenMove { get; private set; }

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
        // GameStateController.Instance.OnDialogueStateEntered += PauseMenu;
        // GameStateController.Instance.OnDialogueStateExited  += UnpauseMenu;
        
        //--Reference Assignments
        PlayerInput = PlayerReferences.Instance.PlayerInput;
        BUIActions = GetComponent<BattleUIActions>();

        //--Set Button Array
        Buttons = new Button[] { _runButton, _bagButton, _pkmnButton, _fightButton  };

        //--Set Menu to Default Positions
        // ResetMenuPositions();

        //--Push base menu state
        PushState( _baseState );
    }

    private void OnDisable(){
        Debug.Log( "Disable PlayerBattleMenu ");
        //--Events
        OnPushNewState  -= PushState;
        OnPauseState    -= PauseMenu;
        OnUnpauseState  -= UnpauseMenu;
        // GameStateController.Instance.OnDialogueStateEntered -= PauseMenu;
        // GameStateController.Instance.OnDialogueStateExited  -= UnpauseMenu;

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
        if( StateMachine.CurrentState != _pausedState )
            StateMachine.Push( _pausedState );
    }

    private void UnpauseMenu(){
        if( StateMachine.CurrentState == _pausedState )
            StateMachine.Pop();
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

    public void HandleMoveTargetSelection( BattleUnit attacker, Move move )
    {
        Attacker = attacker;
        ChosenMove = move;
        PushState( _targetSelectState );
    }

    public void HandleSwitchTargetSelection( Pokemon pokemon )
    {
        
    }

#if UNITY_EDITOR
    private void OnGUI(){
        if( !StateMachineDisplays.Show_PlayerBattleMenuStateStack )
            return; 

        var style = new GUIStyle();
        style.font = Resources.Load<Font>( "Fonts/Gotham Bold Outlined" );
        style.fontSize = 30;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;
        style.richText = true;

        GUILayout.BeginArea( new Rect( 250, 0, 600, 500 ) );
        GUILayout.Label( "STATE STACK", style );
        foreach( var state in StateMachine.StateStack ){
            GUILayout.Label( state.GetType().ToString(), style );
        }
        GUILayout.EndArea();
    }
#endif

}
