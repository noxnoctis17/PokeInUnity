using System.Collections.Generic;
using UnityEngine;
using NoxNoctisDev.StateMachine;

public class WildPokemonSpawnerManager : MonoBehaviour
{
    public static WildPokemonSpawnerManager Instance { get; private set; }
    public StateMachine<WildPokemonSpawnerManager> WildSpawnManagerStateMachine { get; private set; }
    [SerializeField] public List<WildPokemonSpawner> SpawnerList = new List<WildPokemonSpawner>();
    [SerializeField] private List<WildPokemonSpawner> _disabledSpawnerList = new List<WildPokemonSpawner>();
    

    private void OnEnable(){
        BattleSystem.OnBattleStarted += PushPausedState;
        BattleSystem.OnBattleEnded += PopCurrentState;
    }

    private void OnDisable(){
    }

    private void Awake(){
        Instance = this;

        WildSpawnManagerStateMachine = new StateMachine<WildPokemonSpawnerManager>( this );
        Debug.Log( this + " " + WildSpawnManagerStateMachine );
    }

    private void Start(){
        Debug.Log( this + " " + WildSpawnManagerStateMachine );
        WildSpawnManagerStateMachine.Push( Spawner_SpawnState.Instance );
    }

    private void PopCurrentState(){
        Debug.Log( this + " PopCurrentState()" );
        WildSpawnManagerStateMachine.Pop();
    }

    private void PushPausedState(){
        Debug.Log( this+ " PushPausedState()" );
        WildSpawnManagerStateMachine.Push( Spawner_PausedState.Instance );
    }

    private void EnableAllSpawners(){
        foreach( WildPokemonSpawner spawner in SpawnerList ){
            spawner.gameObject.SetActive( true );
        }
    }

    private void DisableAllSpawners(){
        foreach( WildPokemonSpawner spawner in SpawnerList ){
            spawner.gameObject.SetActive( false );
        }
    }

    private void EnableSpawnersInRange(){
        
    }

    private void DisableSpawnersOutOfRange(){

    }

    private void ClearSpawnerList(){
        foreach( WildPokemonSpawner spawner in SpawnerList ){
            SpawnerList.Remove( spawner );
        }
    }

    private void OnGUI(){
        var style = new GUIStyle();
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        GUILayout.BeginArea( new Rect( 900, 0, 500, 500 ) );
        GUILayout.Label( "WILD POKEMON STATE STACK", style );
        foreach( var state in WildSpawnManagerStateMachine.StateStack ){
            GUILayout.Label( state.GetType().ToString(), style );
        }
        GUILayout.EndArea();
    }


}