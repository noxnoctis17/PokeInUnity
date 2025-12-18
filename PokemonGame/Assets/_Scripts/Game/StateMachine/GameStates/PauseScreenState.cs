using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class PauseScreenState : State<GameStateController>
{
    public static PauseScreenState Instance;
    private GameStateController _gameStateController;

    private void Awake(){
        Instance = this;
    }

    //--This state is currently a buffer between freeroam and the pause menu entering a dialogue state, designed to help manage player's controls correctly
    //--between states by not causing the dialogue state to pop and return to the freeroam state while the player is inside of the pause screen
    public override void EnterState( GameStateController owner ){
        Debug.Log( $"Entered State: {this}" );
        _gameStateController = owner;

        _gameStateController.ChangeGameStateEnum( GameStateController.GameStateEnum.PauseScreenState );
    }

    public override void ReturnToState(){
        Debug.Log( $"Return to State: {this}" );
        _gameStateController.ChangeGameStateEnum( GameStateController.GameStateEnum.PauseScreenState );
        PlayerReferences.Instance.PlayerController.EnableUI();
    }

    public override void ExitState(){
        Debug.Log( $"Exited State: {this}" );
    }
}
