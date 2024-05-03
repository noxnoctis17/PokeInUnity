using UnityEngine;
using System.Collections;
using NoxNoctisDev.StateMachine;
using System;
using Pathfinding;

public class WildPokemon : MonoBehaviour
{
    //----------------------[ POKEMON SO ]--------------------------
    [SerializeField] private Pokemon _pokemon;
    [SerializeField] private PokemonSO _pokeSO;
    public Pokemon Pokemon => _pokemon;
    public PokemonSO PokeSO => _pokeSO;
    private WildPokemonSpawner _wildPokemonSpawner;

    //------------------[ SPRITES & ANIMATIONS ]--------------------

    [Header("Sprite and Animations")]
    [SerializeField] private PokemonAnimator _pokeAnimator;
    public PokemonAnimator PokeAnimator => _pokeAnimator;

    //------------------[ COLLIDER & WANDERING ]--------------------

    public static Vector3 WildPokemonLocation;
    public BoxCollider BoxCollider { get; private set; }
    public AIPath AgentMon { get; private set; }

    //------------------------[ ACTIONS ]---------------------------
    private WildPokemonEvents _wildPokemonEvents;
    public WildPokemonEvents WildPokemonEvents => _wildPokemonEvents;
    public Action<State<WildPokemon>> OnPlayerTooFar;

    //-------------------------[ STATE MACHINE ]---------------------------
    public StateMachine<WildPokemon> WildPokemonStateMachine { get; private set; }
    [SerializeField] private State<WildPokemon> _aggressiveState;
    [SerializeField] private State<WildPokemon> _curiousState;
    [SerializeField] private State<WildPokemon> _idleState;
    [SerializeField] private State<WildPokemon> _scaredState;
    [SerializeField] private State<WildPokemon> _wanderState;
    [SerializeField] private State<WildPokemon> _battleState;
    [SerializeField] private State<WildPokemon> _pausedState;
    public State<WildPokemon> AggressiveState => _aggressiveState;
    public State<WildPokemon> CuriousState => _curiousState;
    public State<WildPokemon> IdleState => _idleState;
    public State<WildPokemon> ScaredState => _scaredState;
    public State<WildPokemon> WanderState => _wanderState;
    public State<WildPokemon> BattleState => _battleState;
    public State<WildPokemon> PausedState => _pausedState;
    private bool _initialized;

    private void OnDisable(){
        if( _initialized )
            Despawn();
    }

    public void Init( WildPokemonSpawner spawner, PokemonSO pokeSO, int level ){
        // Debug.Log( this + " has initialized" );
        // Debug.Log( "spawner is: " + _wildPokemonSpawner );
        _initialized = false;

        //--References
        _wildPokemonSpawner = spawner;
        _pokeSO = pokeSO;
        _pokemon = new Pokemon( _pokeSO, level );
        
        //--Set State Machine & Initial State
        WildPokemonStateMachine = new StateMachine<WildPokemon>( this, _wanderState );
        // GameStateController.Instance.WildmonStateDisplayTest.Push( WildPokemonStateMachine );

        //--Set A* Wander AI
        AgentMon = GetComponent<AIPath>();
        AgentMon.enabled = true;

        //--Set Visual Components
        _pokeAnimator = GetComponentInChildren<PokemonAnimator>( true );
        _pokeAnimator.enabled = true;
        GetComponentInChildren<PokemonShadow>( true ).enabled = true;
        _pokeAnimator.Initialize( _pokeSO );

        //--Set WildPokemonEvents
        _wildPokemonEvents = GetComponent<WildPokemonEvents>();

        //--Actions
        OnPlayerTooFar += ChangeState;
        BattleSystem.OnBattleStarted += DisableCanStartBattle;
        BattleSystem.OnBattleEnded += EnableCanStartBattle;
        _wildPokemonSpawner.OnDespawnCall += Despawn;

        //--Manage Colliders
        BoxCollider = GetComponent<BoxCollider>();
        BoxCollider.enabled = false;
        StartCoroutine( CollisionDelay() );
        StartCoroutine( DespawnTimer() );

        //--Finally Initialize State Machine
        WildPokemonStateMachine.Initialize();

        //--Check to see if a battle is happening and this mon spawned during one somehow, missing the
        //--Event call and not turning its collider off, causing it to start a new battle with the player
        if( GameStateController.Instance.CurrentStateEnum == GameStateController.GameStateEnum.BattleState ){
            DisableCanStartBattle();
        }

        _initialized = true;
    }

    private void Update(){
        if( _initialized )
            WildPokemonStateMachine.Update();
    }

    public void Despawn(){
        // Debug.Log( this + " has despawned" );
        //--Stop despawn timer if it's still running
        StopAllCoroutines();

        //--Deinitialized
        _initialized = false;

        //--Clear the state machine
        WildPokemonStateMachine.ClearActions();
        WildPokemonStateMachine = null;

        //--Clear Visual Components
        _pokeAnimator.Clear();
        _pokeAnimator.enabled = false;
        GetComponentInChildren<PokemonShadow>().enabled = false;
        _pokeAnimator = null;

        //--Set AI's path to null
        AgentMon.SetPath( null );
        AgentMon = null;

        //--Clear Pokemon Data
        _pokemon = null;
        _pokeSO = null;

        //--Unsubscribe from Events
        OnPlayerTooFar -= ChangeState;
        BattleSystem.OnBattleStarted -= DisableCanStartBattle;
        BattleSystem.OnBattleEnded -= EnableCanStartBattle;
        _wildPokemonSpawner.OnDespawnCall -= Despawn;

        //--Despawned event raise
        WildPokemonEvents.OnPokeDespawned?.Invoke(); //--All this does is lower the spawnedAmnt in the spawner

        //--Release back to the object pool, which does SetActive( false ) on the gameObject
        _wildPokemonSpawner.SpawnPool.Release( gameObject );
        // _wildPokemonSpawner = null;
    }

    private void ChangeState( State<WildPokemon> newState ){
        WildPokemonStateMachine.OnQueueNextState?.Invoke( newState );
    }

    public void SetBirthSpawner( WildPokemonSpawner wildPokemonSpawner ){
        Debug.Log( "SetBirthSpawner" );
        _wildPokemonSpawner = wildPokemonSpawner;
    }

    //--If the wild pokemon can start a battle on collision or not-----
    private void EnableCanStartBattle(){
        // Debug.Log( "Battle Has Ended, enabled colliders" );
        StartCoroutine( CollisionDelay() );
    }

    private void DisableCanStartBattle(){
        // Debug.Log( "Battle Has Started, disabled colliders" );
        BoxCollider.enabled = false;
    }
    //-----------------------------------------------------------------

    public IEnumerator CollisionDelay(){
        //--Delay before collider is enabled so mons can't spawn directly on top of the player and trigger a battle immediately
        yield return new WaitForSeconds( 2f );
        BoxCollider.enabled = true;
        yield return null;
    }

    private void OnTriggerEnter( Collider col ){
        if( col.CompareTag( "Player" ) && !BattleSystem.BattleIsActive ){
            StopAllCoroutines();
            StartCoroutine( StartBattle() ) ;
        }
    }

    private IEnumerator StartBattle(){
        BoxCollider.enabled = false;
        WildPokemonStateMachine.OnQueueNextState?.Invoke( BattleState );
        yield return new WaitUntil( () => !AgentMon.enabled );
        Pokemon.Init();
        Pokemon.SetAsEnemyUnit();
        WildPokemonEvents.OnPlayerEncounter?.Invoke( this );
        WildPokemonLocation = transform.position;
    }
    
    // public void Despawn(){
    //     StopAllCoroutines();
    //     WildPokemonStateMachine.ClearActions();
        
    //     //--Despawn event call
    //     WildPokemonEvents.OnPokeDespawned?.Invoke( this );

    //     if( _wildPokemonSpawner.SpawnerPokemonList != null && _wildPokemonSpawner.SpawnerPokemonList.Contains( gameObject ) && gameObject != null ){
    //         _wildPokemonSpawner.SpawnerPokemonList.Remove( gameObject );
    //     }

    //     //--Died
    //     // Debug.Log( "Pokemon has been died" );
    //     if( gameObject != null )
    //         Destroy( gameObject );
    // }

    public IEnumerator DespawnTimer(){
        yield return new WaitForSeconds( 240 );
        Despawn();
    }

//     #if UNITY_EDITOR
//     private void OnGUI(){
//         var style = new GUIStyle();
//         style.fontSize = 30;
//         style.fontStyle = FontStyle.Bold;
//         style.normal.textColor = Color.black;        

//         GUILayout.BeginArea( new Rect( 1400, 0, 600, 500 ) );
//         GUILayout.Label( "STATE STACK", style );
//         foreach( var state in GameStateController.Instance.WildmonStateDisplayTest ){
//             GUILayout.Label( state.CurrentState.GetType().ToString(), style );
//         }
//         GUILayout.EndArea();
//     }
// #endif

}
