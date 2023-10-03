using UnityEngine;
using System.Collections;
using NoxNoctisDev.StateMachine;

public class WildPokemon : MonoBehaviour
{
    public StateMachine<WildPokemon> WildPokemonStateMachine { get; private set; }
    public WildPokemonWander WildPokemonWander { get; private set; }
    private WildPokemonSpawner _wildPokemonSpawner;

    //------------------=-[ POKEMON SO ]-------------------------
    
    [Header("Pokemon SO")]
    public PokemonClass Pokemon;

    //---------------[ SPRITES & ANIMATIONS ]--------------------

    [Header("Sprite and Animations")]
    [SerializeField] private SpriteRenderer _sprite;
    // [SerializeField] private Animator _animator; --not in use yet hehe

    //----------------[ COLLIDER & WANDERING ]--------------------

    public static Vector3 WildPokemonLocation;
    public BoxCollider BoxCollider { get; private set; }

    //------------------------[ACTIONS]---------------------------
    private WildPokemonEvents _wildPokemonEvents;
    public WildPokemonEvents WildPokemonEvents => _wildPokemonEvents;

    private void OnDisable(){
        StopAllCoroutines();
    }

    private void Awake(){
        //--Set State Machine & Initial State
        WildPokemonStateMachine = new StateMachine<WildPokemon>( this );
        // Debug.Log( WildPokemonStateMachine );

        //--Set A* Wander AI
        WildPokemonWander = GetComponent<WildPokemonWander>();

        //--Set Visual Components
        _sprite.sprite = Pokemon.PokeSO.FrontSprite;
        //_animator = wildPokemon.animations; -----not in use yet, haven't added animations lol

        //--Add Self to general list of currently spawned pokemon
        WildPokemonManager.Instance.SpawnedPokemonList.Add( this );

        //--Set WildPokemonEvents
        _wildPokemonEvents = GetComponent<WildPokemonEvents>();

        //--Manage Colliders
        BoxCollider = GetComponent<BoxCollider>();
        BoxCollider.enabled = false;
        StartCoroutine( CollisionDelay() );
        StartCoroutine( DespawnTimer() );
    }

    public void SetBirthSpawner( WildPokemonSpawner wildPokemonSpawner ){
        _wildPokemonSpawner = wildPokemonSpawner;
    }

    public IEnumerator CollisionDelay(){
        //--Delay before collider is enabled so mons can't spawn directly on top of the player and trigger a battle immediately
        yield return new WaitForSeconds( 0.25f );
        BoxCollider.enabled = true;
        yield return null;
    }

    private void OnTriggerEnter( Collider col ){
        if( col.CompareTag( "Player" ) ){
            StopCoroutine( DespawnTimer() );
            WildPokemonWander.SetPausedState();
            BoxCollider.enabled = false;
            Pokemon.Init();
            Pokemon.SetAsEnemyUnit();
            WildPokemonEvents.OnPlayerEncounter?.Invoke( this );
            WildPokemonLocation = transform.position;
        }
    }

    public void Despawn(){
        StopCoroutine( DespawnTimer() );

        //--Clear the State Machine
        StopCoroutine( WildPokemonWander.WanderIdle() );
        WildPokemonStateMachine.StateStack.Clear();
        WildPokemonStateMachine = null;
        
        //--Despawn event call
        WildPokemonEvents.OnPokeDespawned?.Invoke( this );

        //--Make sure the list isn't null and contains the wildmon before we remove it
        if( WildPokemonManager.Instance.SpawnedPokemonList != null && WildPokemonManager.Instance.SpawnedPokemonList.Contains( this ) ){
            WildPokemonManager.Instance.SpawnedPokemonList?.Remove( this );
        }

        if( _wildPokemonSpawner.SpawnerPokemonList != null && _wildPokemonSpawner.SpawnerPokemonList.Contains( gameObject ) && gameObject != null ){
            _wildPokemonSpawner.SpawnerPokemonList.Remove( gameObject );
        }

        //--Die
        // Debug.Log( "Pokemon has been destroyed" );
        Destroy( gameObject );
    }

    public IEnumerator DespawnTimer(){
        yield return new WaitForSeconds( 240 );
        Despawn();
    }

}
