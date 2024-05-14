using System;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class UI_PauseMenuStateMachine : State<UI_PauseMenuStateMachine>
{
    [SerializeField] private State<UI_PauseMenuStateMachine> _pauseMenu;
    public StateStackMachine<UI_PauseMenuStateMachine> StateMachine { get; private set; }

    private void Start(){
        //--Setup State Machine
        StateMachine = new StateStackMachine<UI_PauseMenuStateMachine>( this );
        PushState( this );
    }

    public void PushState( State<UI_PauseMenuStateMachine> newState ){
        StateMachine.Push( newState );
    }

    public void PopState(){
        StateMachine.Pop();
    }

    public void ChangeState( State<UI_PauseMenuStateMachine> newState ){
        StateMachine.ChangeState( newState );
    }

    public override void EnterState( UI_PauseMenuStateMachine owner ){
        PlayerController.OnPause += HandlePauseMenu;
    }

    public override void ReturnToState(){
        PlayerController.OnPause += HandlePauseMenu;
    }

    public override void PauseState(){
        PlayerController.OnPause -= HandlePauseMenu;
    }

    public override void ExitState(){
        PlayerController.OnPause -= HandlePauseMenu;
    }

    private void HandlePauseMenu(){
        Debug.Log( "HandlePauseMenu()" );
        if( StateMachine.CurrentState == this ){
            GameStateController.Instance.PushGameState( PauseScreenState.Instance );
            PushState( _pauseMenu );
        }
        else if( StateMachine.CurrentState == _pauseMenu ){
            Debug.Log( "popping pause states" );
            GameStateController.Instance.GameStateMachine.Pop();
            StateMachine.Pop();
        }
        else
            Debug.Log( "State out of range or some shit" );
    }

    private void OnGUI(){
        var style = new GUIStyle();
        style.fontSize = 30;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        GUILayout.BeginArea( new Rect( 1400, 0, 600, 500 ) );
        GUILayout.Label( "STATE STACK", style );
        foreach( var state in StateMachine.StateStack ){
            GUILayout.Label( state.GetType().ToString(), style );
        }
        GUILayout.EndArea();
    }
}
