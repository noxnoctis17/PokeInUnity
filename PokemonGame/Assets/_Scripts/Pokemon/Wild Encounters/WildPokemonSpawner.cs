using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;
using System;
using UnityEngine.Pool;

public class WildPokemonSpawner : MonoBehaviour
{
    [SerializeField] private List<WildEncounter> _encounter;
    [SerializeField] private WildPokemon _wildPokemonPrefab;
    public WildPokemon WildPokemonPrefab => _wildPokemonPrefab;

    //-------------------------------------------------------------------------------------------------------------//
    //-----------------------------------------[ ACTIONS ]---------------------------------------------------------//
    //-------------------------------------------------------------------------------------------------------------//

    public Action<State<WildPokemonSpawner>> OnStateChanged;
    public Action<WildEncounter> OnAddPokemonToSpawn;
    public Action OnSpawnerCanceled;
    public Action OnDespawnCall;

    //-------------------------------------------------------------------------------------------------------------//
    //--------------------------------------[ WEIGHTED RNG ]-------------------------------------------------------//
    //-------------------------------------------------------------------------------------------------------------//

    [Header("Weighted RNG")]
    [SerializeField] private int[] _table = {};
    [SerializeField] private int _totalWeight; //serialized for sight
    [SerializeField] private int _randomNumber; //serialized for sight

    //-------------------------------------------------------------------------------------------------------------//
    //-------------------------------------[ SPAWNER & DESPAWNER ]-------------------------------------------------//
    //-------------------------------------------------------------------------------------------------------------//
    
    [SerializeField] private float _wanderRange;
    [SerializeField] private int _spawnedPokemonAmnt;
    [SerializeField] private int _numberToSpawn;
    [SerializeField] private List<Transform> _spawnLocations; //--list of empty game objects to use as the transform.position as spawn points
    private Transform _prevSpawnPoint; //--assign randomly chosen spawnlocation to this for instantiate
    private ObjectPool<GameObject> _spawnPool;
    private Vector3 _spawnPoint;
    private int _currentSpawnIndex;
    public int NumberToSpawn => _numberToSpawn;
    public int SpawnedPokemonAmnt => _spawnedPokemonAmnt;
    public List<Transform> SpawnLocations => _spawnLocations;
    public List<WildEncounter> SpeciesToSpawnList { get; private set; }
    public ObjectPool<GameObject> SpawnPool => _spawnPool;

    //-------------------------------------------------------------------------------------------------------------//
    //----------------------------------------[ STATE MACHINE ]----------------------------------------------------//
    //-------------------------------------------------------------------------------------------------------------//

    public StateMachine<WildPokemonSpawner> SpawnerStateMachine { get; private set; }
    private State<WildPokemonSpawner> _lastState;
    [SerializeField] private State<WildPokemonSpawner> _spawnState;
    [SerializeField] private State<WildPokemonSpawner> _pausedState;
    [SerializeField] private State<WildPokemonSpawner> _canceledState;
    [SerializeField] private State<WildPokemonSpawner> _finishedState;
    public State<WildPokemonSpawner> SpawnState => _spawnState;
    public State<WildPokemonSpawner> PausedState => _pausedState;
    public State<WildPokemonSpawner> CanceledState => _canceledState;
    public State<WildPokemonSpawner> FinishedState => _finishedState;

    //------------------------------------------------------------------------------------------------------------//
    //------------------------------------------------------------------------------------------------------------//
    //------------------------------------------------------------------------------------------------------------//

    private void OnEnable(){
        //--Set Events
        WildPokemonEvents.OnPokeDespawned       += DespawnTracker;
        BattleSystem.OnBattleStarted            += ChangeStateOnBattleStart;
        BattleSystem.OnBattleEnded              += ChangeStateOnBattleEnd;
        OnAddPokemonToSpawn                     += AddPokemonToSpawnList;
        OnSpawnerCanceled                       += ClearToSpawnList;
        OnStateChanged                          += ChangeState;
    }

    private void OnDisable() {
        WildPokemonEvents.OnPokeDespawned       -= DespawnTracker;
        BattleSystem.OnBattleStarted            -= ChangeStateOnBattleStart;
        BattleSystem.OnBattleEnded              -= ChangeStateOnBattleEnd;
        OnAddPokemonToSpawn                     -= AddPokemonToSpawnList;
        OnSpawnerCanceled                       -= ClearToSpawnList;
        OnStateChanged                          -= ChangeState;
        
        StopAllCoroutines();
        DespawnPokemon();

        _spawnedPokemonAmnt = 0;
    }

    private void Start(){

        //--Initialize State Machine
        SpawnerStateMachine = new StateMachine<WildPokemonSpawner>( this, _pausedState );
        SpawnerStateMachine.Initialize();
        Debug.Log( $"Spawner State Machine is: {SpawnerStateMachine}" );

        // AstarPath.active.Scan();

        //--Initialize a new ObjectPool per Spawner Instance
        SpeciesToSpawnList = new();
        _spawnPool = new ( () => { return SpawnPoolCreate(); },
        spawn => { /*SpawnPoolGet( spawn );*/ },
        spawn => { SpawnPoolRelease( spawn ); },
        spawn => { /*Destroy( spawn );*/ },
        //--Handle Dupes, Starting Amount, Max Amount
        false, _numberToSpawn, _numberToSpawn );
    }

    private GameObject SpawnPoolCreate(){
        var spawnObj = Instantiate( _wildPokemonPrefab.gameObject );
        spawnObj.SetActive( false );
        return spawnObj;
    }

    private void SpawnPoolRelease( GameObject spawn ){
        spawn.SetActive( false );
    }

    //*
    //--stuff: have unity handle management if you think your code might return an object to the pool that has already been returned to the pool
    //--I'm a master coder and therefore do not have to worry about this, so it's set to false 04/14/24
    //*
    private void Update(){
        SpawnerStateMachine.Update();
    }

    private void ChangeStateOnBattleStart(){
        if( SpawnerStateMachine.CurrentState == _spawnState )
            ChangeState( _pausedState );
    }

    //--in order to properly implement states for the spawner, you need to actually pull the code that manages spawning out of this class
    //--and place it into the SpawnState class, that way the spawning code will ALWAYS only run in that state
    private void ChangeStateOnBattleEnd(){
        if( SpawnerStateMachine.CurrentState == _pausedState )
            ChangeState( _lastState );
    }

    private void OnTriggerEnter( Collider collider ){
        if( collider.transform == PlayerReferences.Instance.PlayerTransform ){
            ChangeState( _spawnState );
        }
    }

    private void OnTriggerExit( Collider collider ){
        if( collider.transform == PlayerReferences.Instance.PlayerTransform ){
            ChangeState( _canceledState );
        }
    }

    private void ChangeState( State<WildPokemonSpawner> newState ){
        _lastState = SpawnerStateMachine.CurrentState;
        SpawnerStateMachine.OnQueueNextState?.Invoke( newState );
    }

    private void AddPokemonToSpawnList( WildEncounter pokemon ){
        SpeciesToSpawnList.Add( pokemon );
    }

    public void AddToSpawnedAmount(){
        _spawnedPokemonAmnt++;;
    }

    private void ClearToSpawnList(){
        if( SpeciesToSpawnList.Count > 0 )
            SpeciesToSpawnList.Clear();

        DespawnPokemon();
    }
    
    private void DespawnPokemon(){
        OnDespawnCall?.Invoke();
    }

    private void DespawnTracker(){
        _spawnedPokemonAmnt--;
    }

    public Vector3 SpawnLocation(){
        // int rngLocation;
        if( _spawnPoint == null )
            _spawnPoint = Vector3.zero;

        if( _currentSpawnIndex < _spawnLocations.Count ){
            _spawnPoint = _spawnLocations[_currentSpawnIndex].position;
            _currentSpawnIndex++;
        }
        else if( _currentSpawnIndex == _spawnLocations.Count  ){
            _currentSpawnIndex = 0;
            _spawnPoint = _spawnLocations[_currentSpawnIndex].position;
        }

        // Debug.Log( $"Current Spawn Point is: {_spawnPoint}" );
        return _spawnPoint;
    }



    public WildEncounter RandomPokemon(){
        WildEncounter pokemon = null;
        int prevRand = _randomNumber;
        _totalWeight = 0;

        foreach( var num in _table )
        {
            _totalWeight += num;
        }

        _randomNumber = UnityEngine.Random.Range( 0, _totalWeight ) + 1;
        if( _randomNumber == prevRand )
            _randomNumber = UnityEngine.Random.Range( 0, _totalWeight ) + 1;

        for( int i = 0; i < _table.Length; i++ ){
            if( _randomNumber <= _table[i] ){
                //--remember to now assign the prefab in the spawn state
                pokemon = _encounter[i];
                break;
            }
            else{
                _randomNumber -= _table[i];
            }
        }

        // Debug.Log( pokemon.PokeSO.pName + " was generated" );
        return pokemon;
    }

    private void OnGUI(){
        if( !StateMachineDisplays.Show_WildPokemonStateStack )
            return; 

        var style = new GUIStyle();
        style.fontSize = 30;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        GUILayout.BeginArea( new Rect( 1400, 0, 600, 500 ) );
        GUILayout.Label( "SPAWNER STATE STACK", style );
        foreach( var state in SpawnerStateMachine.StateStack ){
            GUILayout.Label( state.GetType().ToString(), style );
        }
        GUILayout.EndArea();
    }

}
