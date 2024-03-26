using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;
using System;

public class WildPokemonSpawner : MonoBehaviour
{
    [SerializeField] private List<PokemonClass> _encounter;
    [SerializeField] private WildPokemon _wildPokemonPrefab;
    private PokemonClass _pokemon;
    public WildPokemon WildPokemonPrefab => _wildPokemonPrefab;

    //-------------------------------------------------------------------------------------------------------------//
    //-----------------------------------------[ ACTIONS ]---------------------------------------------------------//
    //-------------------------------------------------------------------------------------------------------------//

    public Action<State<WildPokemonSpawner>> OnStateChanged;
    public Action<PokemonClass> OnAddPokemonToSpawn;
    public Action OnSpawnerCanceled;

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
    private List<PokemonClass> _pokemonToSpawn;
    // private bool _isRespawn;
    private bool _isSpawningPokemon;
    public int NumberToSpawn => _numberToSpawn;
    public int SpawnedPokemonAmnt => _spawnedPokemonAmnt;
    public float SpawnDelay => _spawnDelay;
    public List<Transform> SpawnLocations => _spawnLocations;
    public List<GameObject> SpawnerPokemonList { get; private set; }
    public List<PokemonClass> PokemonToSpawn => _pokemonToSpawn;

    //-------------------------------------------------------------------------------------------------------------//
    //----------------------------------------[ STATE MACHINE ]----------------------------------------------------//
    //-------------------------------------------------------------------------------------------------------------//

    public StateMachine<WildPokemonSpawner> PokeSpawnerStateMachine { get; private set; }
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
        WildPokemonEvents.OnPokeDespawned += DespawnTracker;
        BattleSystem.OnBattleStarted += ChangeStateOnBattleStart;
        BattleSystem.OnBattleEnded += ChangeStateOnBattleEnd;
        OnAddPokemonToSpawn += AddPokemonToSpawnPool;
        OnSpawnerCanceled += ClearToSpawnList;
        OnStateChanged += ChangeState;

        //--Initialize a new List/Queue instance unique to each spawner instance
        SpawnerPokemonList = new();
        _pokemonToSpawn = new();

    }

    private void OnDisable() {
        WildPokemonEvents.OnPokeDespawned -= DespawnTracker;
        BattleSystem.OnBattleStarted -= ChangeStateOnBattleStart;
        BattleSystem.OnBattleEnded -= ChangeStateOnBattleEnd;
        OnAddPokemonToSpawn -= AddPokemonToSpawnPool;
        OnSpawnerCanceled -= ClearToSpawnList;
        OnStateChanged -= ChangeState;
        
        StopAllCoroutines();
        DespawnPokemon();

        if( _spawnedPokemonAmnt != 0 )
            _spawnedPokemonAmnt = 0;
    }

    private void Start(){
        PokeSpawnerStateMachine = new StateMachine<WildPokemonSpawner>( this, _canceledState );
        PokeSpawnerStateMachine.Initialize();
    }

    private void Update(){
        PokeSpawnerStateMachine.Update();
    }

    private void ChangeStateOnBattleStart(){
        if( PokeSpawnerStateMachine.CurrentState == _spawnState )
            ChangeState( _pausedState );
    }

    //--in order to properly implement states for the spawner, you need to actually pull the code that manages spawning out of this class
    //--and place it into the SpawnState class, that way the spawning code will ALWAYS only run in that state
    private void ChangeStateOnBattleEnd(){
        if( PokeSpawnerStateMachine.CurrentState == _pausedState )
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
        _lastState = PokeSpawnerStateMachine.CurrentState;
        PokeSpawnerStateMachine.OnQueueNextState?.Invoke( newState );
    }

    private void AddPokemonToSpawnPool( PokemonClass pokemon ){
        _pokemonToSpawn.Add( pokemon );
    }

    private void ClearToSpawnList(){
        if( _pokemonToSpawn.Count > 0 )
            _pokemonToSpawn?.Clear();

        DespawnPokemon();
    }
    
    private void DespawnPokemon(){
        while( SpawnerPokemonList?.Count > 0 ){
            var wildMon = SpawnerPokemonList[0];
            SpawnerPokemonList.RemoveAt( 0 );

            if( wildMon != null && !ReferenceEquals( wildMon, null ) ){
                var wildPokemon = wildMon.GetComponent<WildPokemon>();
                
                if( wildPokemon != null && !ReferenceEquals( wildPokemon, null ) ){
                    wildPokemon.Despawn();
                }
            }
        }
    }

    private void DespawnTracker( WildPokemon wildPokemon ){
        _spawnedPokemonAmnt--;
        // _isRespawn = true;

        if( PokeSpawnerStateMachine.CurrentState == FinishedState )
            OnStateChanged?.Invoke( SpawnState );
    }

    private float RespawnDelay(){
        float respawnDelay = UnityEngine.Random.Range( _respawnDelayMin, _respawnDelayMax );
        return respawnDelay;
    }

    public Transform SpawnLocation(){
        int rngLocation;
        for( int amountOfLocations = 0; amountOfLocations < _spawnLocations.Count; amountOfLocations++ ){
            rngLocation = UnityEngine.Random.Range( 0, amountOfLocations );
            _spawnPoint = _spawnLocations[ rngLocation ];
        }

        return _spawnPoint;
    }

    public PokemonClass RandomPokemon(){
        _totalWeight = 0;

        foreach( var num in _table )
        {
            _totalWeight += num;
        }

        _randomNumber = UnityEngine.Random.Range( 0, _totalWeight ) + 1;

        for( int i = 0; i < _table.Length; i++ ){
            if( _randomNumber <= _table[i] ){
                //--remember to now assign the prefab in the spawn state
                _pokemon = _encounter[i];
                break;
            }
            else{
                _randomNumber -= _table[i];
            }
        }

        return _pokemon;
    }

}
