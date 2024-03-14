using UnityEngine;
using UnityEngine.EventSystems;
using System;

public class GameStateController : MonoBehaviour
{
//=========================[INSTANCE & STATE MACHINE]====================================
    public static GameStateController Instance { get; private set; }
    public StateStackMachine<GameStateController> GameStateMachine { get; private set; }

//=============================[PRIVATE VARIABLES]=======================================
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private GameObject _battleSystemContainer;
    [SerializeField] private EventSystem _eventSystem;

//=============================[PROPERTIES & PUBLIC GETTERS]=============================
    public BattleSystem BattleSystem => _battleSystem;
    public GameObject BattleSystemContainer => _battleSystemContainer;
    public EventSystem EventSystem => _eventSystem;

//======================================[ACTIONS]========================================
    public Action OnGameStateChanged;

//=======================================================================================

    private void OnEnable(){
        Instance = this;
        // OnGameStateChanged += ChangeGameState;
    }

    private void OnDisable(){
        // OnGameStateChanged -= ChangeGameState;
    }

    private void Start(){
        GameStateMachine = new StateStackMachine<GameStateController>( this );
        GameStateMachine.Push( FreeRoamState.Instance );
    }

    private void ChangeGameState(){
        GameStateMachine.Update();
    }

    private void OnGUI(){
        var style = new GUIStyle();
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        GUILayout.BeginArea( new Rect( 710, 0, 500, 500 ) );
        GUILayout.Label( "STATE STACK", style );
        foreach( var state in GameStateMachine.StateStack ){
            GUILayout.Label( state.GetType().ToString(), style );
        }
        GUILayout.EndArea();
    }

}
