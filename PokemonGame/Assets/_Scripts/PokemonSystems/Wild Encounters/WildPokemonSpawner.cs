using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;
using System;
using UnityEngine.Pool;

public class WildPokemonSpawner : MonoBehaviour
{
    // [SerializeField] private List<PokemonClass> _encounter;
    [SerializeField] private List<WildEncounterClass> _encounter;
    [SerializeField] private WildPokemon _wildPokemonPrefab;
    public WildPokemon WildPokemonPrefab => _wildPokemonPrefab;

    //-------------------------------------------------------------------------------------------------------------//
    //-----------------------------------------[ ACTIONS ]---------------------------------------------------------//
    //-------------------------------------------------------------------------------------------------------------//

    public Action<State<WildPokemonSpawner>> OnStateChanged;
    public Action<WildEncounterClass> OnAddPokemonToSpawn;
    // public Action<PokemonClass> OnAddPokemonToSpawn;
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
    [SerializeField] private float _spawnDelay; //--time between spawns
    [SerializeField] private float _respawnDelayMin; //--minimum time before a pokemon is generated after a despawn event happens
    [SerializeField] private float _respawnDelayMax; //--maximum time before a pokemon is generated after a despawn event happens
    [SerializeField] private List<Transform> _spawnLocations; //--list of empty game objects to use as the transform.position as spawn points
    private Transform _spawnPoint; //--assign randomly chosen spawnlocation to this for instantiate
    private ObjectPool<GameObject> _spawnPool;
    public int NumberToSpawn => _numberToSpawn;
    public int SpawnedPokemonAmnt => _spawnedPokemonAmnt;
    public float SpawnDelay => _spawnDelay;
    public List<Transform> SpawnLocations => _spawnLocations;
    // public List<PokemonClass> SpeciesToSpawnList { get; private set; }
    public List<WildEncounterClass> SpeciesToSpawnList { get; private set; }
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
        // BattleSystem.OnBattleStarted            += ChangeStateOnBattleStart;
        // BattleSystem.OnBattleEnded              += ChangeStateOnBattleEnd;
        OnAddPokemonToSpawn                     += AddPokemonToSpawnList;
        OnSpawnerCanceled                       += ClearToSpawnList;
        OnStateChanged                          += ChangeState;

        // //--Initialize a new List/Queue instance unique to each spawner instance
        // SpawnerPokemonList = new();
        // _pokemonToSpawn = new();

    }

    private void OnDisable() {
        WildPokemonEvents.OnPokeDespawned       -= DespawnTracker;
        // BattleSystem.OnBattleStarted            -= ChangeStateOnBattleStart;
        // BattleSystem.OnBattleEnded              -= ChangeStateOnBattleEnd;
        OnAddPokemonToSpawn                     -= AddPokemonToSpawnList;
        OnSpawnerCanceled                       -= ClearToSpawnList;
        OnStateChanged                          -= ChangeState;
        
        StopAllCoroutines();
        DespawnPokemon();

        _spawnedPokemonAmnt = 0;
    }

    private void Start(){
        //--Initialize a new List/Queue instance unique to each spawner instance
        SpeciesToSpawnList = new();
        //------------[ POOL SPAGHETTI ]--------------
        _spawnPool = new( () => {
            //--Create();
            Debug.Log( "SpawnPool.Create()" );
            var spawnObj = Instantiate( _wildPokemonPrefab.gameObject );
            spawnObj.SetActive( false );
            return spawnObj;
        },
            //--Get();
            null,
        spawn => {
            //--Release();
            Debug.Log( $"{spawn} has been released" );
            spawn.gameObject.SetActive( false );
        },
        spawn => {
            //--Destroy();
            if( spawn != null )
                Destroy( spawn.gameObject );

        },
            //--stuff*, starting amount, max amount
        false, _numberToSpawn, _numberToSpawn );
        //-------------------------------------------

        //--Initialize State Machine
        SpawnerStateMachine = new StateMachine<WildPokemonSpawner>( this, _pausedState );
        SpawnerStateMachine.Initialize();
    }

    //--stuff: have unity handle management if you think your code might return an object to the pool that has already been returned to the pool
    //--I'm a master coder and therefore do not have to worry about this, so it's set to false 04/14/24

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
            Debug.Log( collider );
        }
    }

    private void OnTriggerExit( Collider collider ){
        if( collider.transform == PlayerReferences.Instance.PlayerTransform ){
            ChangeState( _canceledState );
            Debug.Log( collider );
        }
    }

    private void ChangeState( State<WildPokemonSpawner> newState ){
        _lastState = SpawnerStateMachine.CurrentState;
        SpawnerStateMachine.OnQueueNextState?.Invoke( newState );
    }

    // private void AddPokemonToSpawnList( PokemonClass pokemon ){
    //     SpeciesToSpawnList.Add( pokemon );
    // }

    private void AddPokemonToSpawnList( WildEncounterClass pokemon ){
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

    // private void DespawnPokemon(){
    //     while( SpawnerPokemonList.Count > 0 ){
    //         var wildMon = SpawnerPokemonList[0];
    //         SpawnerPokemonList.RemoveAt( 0 );

    //         if( wildMon != null && !ReferenceEquals( wildMon, null ) ){
    //             var wildPokemon = wildMon.GetComponent<WildPokemon>();
                
    //             if( wildPokemon != null && !ReferenceEquals( wildPokemon, null ) ){
    //                 wildPokemon.Despawn();
    //             }
    //         }
    //     }
    // }

    private void DespawnTracker(){
        _spawnedPokemonAmnt--;

        // if( PokeSpawnerStateMachine.CurrentState == FinishedState )
        //     OnStateChanged?.Invoke( SpawnState );
    }

    public Vector3 SpawnLocation(){
        int rngLocation;
        for( int amountOfLocations = 0; amountOfLocations < _spawnLocations.Count; amountOfLocations++ ){
            rngLocation = UnityEngine.Random.Range( 0, amountOfLocations );
            _spawnPoint = _spawnLocations[ rngLocation ];
        }

        return _spawnPoint.position;
    }

    public WildEncounterClass RandomPokemon(){
        WildEncounterClass pokemon = null;
        Debug.Log( _randomNumber );
        int prevRand = _randomNumber;
        _totalWeight = 0;

        foreach( var num in _table )
        {
            _totalWeight += num;
        }

        _randomNumber = UnityEngine.Random.Range( 0, _totalWeight ) + 1;
        if( _randomNumber == prevRand )
            _randomNumber = UnityEngine.Random.Range( 0, _totalWeight ) + 1;

        Debug.Log( _randomNumber );

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

        Debug.Log( pokemon.PokeSO.pName + " was generated" );
        return pokemon;
    }
    // public PokemonClass RandomPokemon(){
    //     PokemonClass pokemon = null;
    //     Debug.Log( _randomNumber );
    //     int prevRand = _randomNumber;
    //     _totalWeight = 0;

    //     foreach( var num in _table )
    //     {
    //         _totalWeight += num;
    //     }

    //     _randomNumber = UnityEngine.Random.Range( 0, _totalWeight ) + 1;
    //     if( _randomNumber == prevRand )
    //         _randomNumber = UnityEngine.Random.Range( 0, _totalWeight ) + 1;

    //     Debug.Log( _randomNumber );

    //     for( int i = 0; i < _table.Length; i++ ){
    //         if( _randomNumber <= _table[i] ){
    //             //--remember to now assign the prefab in the spawn state
    //             pokemon = _encounter[i];
    //             break;
    //         }
    //         else{
    //             _randomNumber -= _table[i];
    //         }
    //     }

    //     Debug.Log( pokemon.PokeSO.pName + " was generated" );
    //     return pokemon;
    // }

}
