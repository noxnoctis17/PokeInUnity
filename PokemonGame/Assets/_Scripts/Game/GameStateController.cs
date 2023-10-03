using UnityEngine;
using UnityEngine.EventSystems;
using System;
using NoxNoctisDev.StateMachine;

public class GameStateController : MonoBehaviour
{
//=========================[INSTANCE & STATE MACHINE]====================================
    public static GameStateController Instance { get; private set; }
    public StateMachine<GameStateController> GameStateMachine { get; private set; }

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
        GameStateMachine = new StateMachine<GameStateController>( this );
        GameStateMachine.Push( FreeRoamState.Instance );
    }

    private void ChangeGameState(){
        GameStateMachine.Execute();
        //--so this is actually for any state that has logic that needs to be run in Update //--or code that is waiting to be called
        //--currently, none of my game states have code that needs to run in update. I've been
        //--using OnGameStateChanged to trigger this method, leftover from the enum state machine
        //--when i realized i didn't need to be running those things in Update()
        //--so i should migrate the code for those states into an override Enter(), which is called
        //--every time a new state is Pushed to the top of the state stack.
        //--ChangeState() calls Push() after it Pop() the current state, so it's a hard REPLACE of
        //--the current state. I need to try to remember this
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
