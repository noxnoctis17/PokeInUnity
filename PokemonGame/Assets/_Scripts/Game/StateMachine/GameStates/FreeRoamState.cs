using UnityEngine;
using NoxNoctisDev.StateMachine;

public class FreeRoamState : State<GameStateController>
{
    public static FreeRoamState Instance { get; private set; }
    private GameStateController gameStateController;

    private void Awake(){
        Instance = this;
    }
    
    public override void Enter( GameStateController owner ){
        gameStateController = owner;

        //--Set Controls
        PlayerReferences.Instance.EnableCharacterControls();
        // PlayerReferences.Instance.DisableBattleControls();
        PlayerReferences.Instance.DisableUI();
        Debug.Log( "exploration baaybeee" );
    }

    public override void Return(){
        //--Re-enable Controls, should they have been disabled by the previous state in the stack
        PlayerReferences.Instance.EnableCharacterControls();
    }

    public override void Exit(){
        
        //--Disable Character Controls
        PlayerReferences.Instance.DisableCharacterControls();
        Debug.Log( "no more exporation babe :c" );
    }
}
