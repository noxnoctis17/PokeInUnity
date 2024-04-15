using UnityEngine;
using NoxNoctisDev.StateMachine;
using UnityEditor;

public class FreeRoamState : State<GameStateController>
{
    public static FreeRoamState Instance { get; private set; }
    private GameStateController gameStateController;

    private void Awake(){
        Instance = this;
    }
    
    public override void EnterState( GameStateController owner ){
        gameStateController = owner;

        //--Set Controls
        PlayerReferences.Instance.PlayerController.EnableCharacterControls();
        PlayerReferences.Instance.PlayerController.DisableBattleControls();
        // PlayerReferences.Instance.PlayerController.DisableUI();
        gameStateController.ChangeGameStateEnum( GameStateController.GameStateEnum.FreeRoamState );
        Debug.Log( "exploration baaybeee" );
    }

    public override void PauseState(){
        //--Disable Character Controls
        PlayerReferences.Instance.PlayerController.DisableCharacterControls();
        Debug.Log( "paused exporation babe :c" );
    }

    public override void ReturnToState(){
        //--Re-enable Controls
        PlayerReferences.Instance.PlayerController.EnableCharacterControls();
        Debug.Log( "exploration AGAIN baaybeee" );
    }

    public override void ExitState(){
        
        //--Disable Character Controls
        PlayerReferences.Instance.PlayerController.DisableCharacterControls();
        Debug.Log( "no more exporation babe :c" );
    }
}
