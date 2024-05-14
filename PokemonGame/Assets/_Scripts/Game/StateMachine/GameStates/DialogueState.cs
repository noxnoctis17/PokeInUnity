using UnityEngine;
using NoxNoctisDev.StateMachine;

public class DialogueState : State<GameStateController>
{
    public static DialogueState Instance { get; private set; }
    private GameStateController _gameStateController;

    private void Awake(){
        Instance = this;
    }

    public override void EnterState( GameStateController owner ){
        _gameStateController = owner;

        //--Set Controls
        PlayerReferences.Instance.PlayerController.EnableUI();
        _gameStateController.ChangeGameStateEnum( GameStateController.GameStateEnum.DialogueState );
        _gameStateController.OnDialogueStateEntered?.Invoke();
        Debug.Log( "Dialogue State Enter()" );
    }

    public override void ExitState(){
        _gameStateController.OnDialogueStateExited?.Invoke();
        PlayerReferences.Instance.PlayerController.DisableUI();
        Debug.Log( "Dialogue State Exit()" );
    }

}
