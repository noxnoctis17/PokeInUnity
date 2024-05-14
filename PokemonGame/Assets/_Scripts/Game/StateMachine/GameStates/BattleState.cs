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

        //--Initialize Databases
        // ConditionsDB.Init(); //--idk what the fuck
        TypeColorsDB.Init(); //--Primary and Secondary colors for each Type. Currently used for the Fight card and mini huds. Fight Cards will eventually be assigned as a single image to individual pokemon, and loaded from there.

        //--Set Controls
        //--Controls get activated at a later time, when all animations have completed and the menu is finally interactable

        //--Activate BattleSystem Container
        gameStateController.BattleSystemContainer.SetActive( true );

        //--Set Gamestate Enum for quick ref
        gameStateController.ChangeGameStateEnum( GameStateController.GameStateEnum.BattleState );
        Debug.Log( "BattleState Enter()" );
    }

    public override void PauseState(){
        PlayerReferences.Instance.PlayerController.DisableBattleControls();
    }

    public override void ReturnToState(){
        PlayerReferences.Instance.PlayerController.EnableBattleControls();
    }

    public override void ExitState(){
        gameStateController.BattleSystemContainer.SetActive( false );
        PlayerReferences.Instance.PlayerController.DisableBattleControls();
        Debug.Log( "BattleState Exit()" );
    }
}
