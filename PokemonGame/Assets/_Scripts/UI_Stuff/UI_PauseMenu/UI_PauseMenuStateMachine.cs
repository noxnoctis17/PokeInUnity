using System;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class UI_PauseMenuStateMachine : State<UI_PauseMenuStateMachine>
{
    [SerializeField] private State<UI_PauseMenuStateMachine> _pauseMenu;
    public StateStackMachine<UI_PauseMenuStateMachine> PauseMenuStateMachine { get; private set; }

    private void OnEnable(){
        
    }

    private void OnDisable(){
        
    }

    private void Start(){
        //--Push Paused State to the GameStateController
        // GameStateController.Instance.GameStateMachine.Push( GamePaused_State );

        //--Setup State Machine
        PauseMenuStateMachine = new StateStackMachine<UI_PauseMenuStateMachine>( this );
        PushNewState( this );
    }

    public void PushNewState( State<UI_PauseMenuStateMachine> newState ){
        PauseMenuStateMachine.Push( newState );
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

    public void CloseCurrentMenu(){
        PauseMenuStateMachine.Pop();
    }

    private void HandlePauseMenu(){
        Debug.Log( "HandlePauseMenu()" );
        if( PauseMenuStateMachine.CurrentState == this )
            PushNewState( _pauseMenu );
        else if( PauseMenuStateMachine.CurrentState == _pauseMenu )
            PauseMenuStateMachine.Pop();
        else
            Debug.Log( "State out of range or some shit" );
    }
}
