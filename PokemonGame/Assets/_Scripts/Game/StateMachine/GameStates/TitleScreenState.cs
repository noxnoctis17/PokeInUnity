using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using Unity.VisualScripting;
using UnityEngine;

public class TitleScreenState : State<GameStateController>
{
    private GameStateController gameStateController;
    public static TitleScreenState Instance;

    private void OnEnable()
    {
        Instance = this;
    }

    public override void EnterState( GameStateController owner )
    {
        gameStateController = owner;

        //--Set Controls
        PlayerReferences.Instance.PlayerController.DisableCharacterControls();
        PlayerReferences.Instance.PlayerController.DisableBattleControls();

        //--Disable Player
        PlayerReferences.Instance.gameObject.SetActive( false );

        //--Set Enum for quick ref
        gameStateController.ChangeGameStateEnum( GameStateEnum.TitleScreenState );
        Debug.Log( "Entered Title Screen" );
    }

    public override void ReturnToState()
    {
        gameStateController.ChangeGameStateEnum( GameStateEnum.TitleScreenState );
        Debug.Log( "Returned to Title Screen" );
    }

    public override void PauseState()
    {
        Debug.Log( "Paused Title Screen State" );
    }

    public override void ExitState()
    {
        Debug.Log( "Exited Title Screen State" );
    }
}
