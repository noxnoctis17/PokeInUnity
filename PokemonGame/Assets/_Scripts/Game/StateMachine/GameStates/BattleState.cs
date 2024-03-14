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
        // PlayerReferences.Instance.EnableBattleControls(); //--Not in use yet, need to overhaul battle ui
        PlayerReferences.Instance.DisableCharacterControls();
        // PlayerReferences.Instance.DisableUI(); //--will do once overhaul is done

        //--Activate BattleSystem Container
        gameStateController.BattleSystemContainer.SetActive( true );

        gameStateController.EventSystem.enabled = false; //--Once battle ui is overhauled we ideally shouldn't need to do this
        Debug.Log( "BattleState Enter()" );
    }

    public override void ExitState(){
        gameStateController.EventSystem.enabled = true; //--same as above, shouldn't need this after battle ui overhaul
        gameStateController.BattleSystemContainer.SetActive( false );
        // PlayerReferences.Instance.DisableBattleControls(); //--set once overhaul is done
        Debug.Log( "BattleState Exit()" );
    }
}
