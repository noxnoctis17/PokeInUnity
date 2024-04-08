using UnityEngine;
using NoxNoctisDev.StateMachine;

public class DialogueState : State<GameStateController>
{
    public static DialogueState Instance { get; private set; }
    private GameStateController gameStateController;

    private void Awake(){
        Instance = this;
    }

    public override void EnterState( GameStateController owner ){
        gameStateController = owner;

        //--Set Controls
        PlayerReferences.Instance.EnableUI();
        PlayerReferences.Instance.DisableCharacterControls();
        PlayerReferences.Instance.DisableBattleControls();
        gameStateController.ChangeGameStateEnum( GameStateController.GameStateEnum.DialogueState );
        gameStateController.OnDialogueStateEntered?.Invoke();
        Debug.Log( "Dialogue State Enter()" );
    }

    public override void ExitState(){
        gameStateController.OnDialogueStateExited?.Invoke();
        PlayerReferences.Instance.DisableUI();
        Debug.Log( "Dialogue State Exit()" );
    }

}
