using UnityEngine;
using NoxNoctisDev.StateMachine;

public class BattleState : State<GameStateController>
{
	public static BattleState Instance { get; private set; }
    private GameStateController gameStateController;

    private void Awake(){
        Instance = this;
    }

    public override void EnterState( GameStateController owner ){
        gameStateController = owner;

        //--Set Controls
        PlayerReferences.Instance.DisableCharacterControls();
        PlayerReferences.Instance.DisableUI();

        //--Activate BattleSystem Container
        gameStateController.BattleSystemContainer.SetActive( true );

        //--Set Gamestate Enum for quick ref
        gameStateController.ChangeGameStateEnum( GameStateController.GameStateEnum.BattleState );
        Debug.Log( "BattleState Enter()" );
    }

    public override void PauseState(){
        PlayerReferences.Instance.DisableBattleControls();
    }

    public override void ReturnToState(){
        PlayerReferences.Instance.EnableBattleControls();
    }

    public override void ExitState(){
        gameStateController.BattleSystemContainer.SetActive( false );
        // PlayerReferences.Instance.EnableUI();
        PlayerReferences.Instance.DisableBattleControls(); //--set once overhaul is done
        Debug.Log( "BattleState Exit()" );
    }
}
