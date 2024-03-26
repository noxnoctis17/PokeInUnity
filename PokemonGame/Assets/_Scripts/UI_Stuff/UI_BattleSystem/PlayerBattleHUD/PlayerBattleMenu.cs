using UnityEngine;
using UnityEngine.UI;
using NoxNoctisDev.StateMachine;
using System;

public class PlayerBattleMenu : State<PlayerBattleMenu>
{
    //================================================================================
    [SerializeField] private Button _fightButton, _pkmnButton, _bagButton, _runButton;
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private State<PlayerBattleMenu> _baseState;
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
        Debug.Log( "enable playerbattlemenu ");
        //--State Machine
        BattleMenuStateMachine = new StateStackMachine<PlayerBattleMenu>( this );

        //--Events
        OnChangeState += ChangeState;
        
        //--Reference Assignments
        PlayerInput = PlayerReferences.Instance.PlayerInput;
        BUIActions = GetComponent<BattleUIActions>();

        //--Set Button Array
        Buttons = new Button[] { _runButton, _bagButton, _pkmnButton, _fightButton  };

        //--Push base menu state
        ChangeState( _baseState );
    }

    private void OnDisable(){
        Debug.Log( "disable playerbattlemenu ");
        //--Events
        OnChangeState -= ChangeState;

        //--State Machine
        BattleMenuStateMachine.ClearStack();
        BattleMenuStateMachine = null;

        //--Buttons
        Buttons = null;

        //--Controls
        PlayerReferences.Instance.DisableUI();
        PlayerReferences.Instance.EnableCharacterControls();
    }

    private void ChangeState( State<PlayerBattleMenu> newState ){
        BattleMenuStateMachine.Push( newState );
    }

#if UNITY_EDITOR
    private void OnGUI(){
        var style = new GUIStyle();
        style.fontSize = 48;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        GUILayout.BeginArea( new Rect( 0, 0, 500, 500 ) );
        GUILayout.Label( "STATE STACK", style );
        foreach( var state in BattleMenuStateMachine.StateStack ){
            GUILayout.Label( state.GetType().ToString(), style );
        }
        GUILayout.EndArea();
    }
#endif

}
