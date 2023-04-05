using UnityEngine;
using System.Collections;
using NoxNoctisDev.StateMachine;

public class WildPokemon : MonoBehaviour
{
    public StateMachine<WildPokemon> WildPokemonStateMachine { get; private set; }
    public WildPokemonWander WildPokemonWander { get; private set; }

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
    private bool _collided;
    public bool Collided => _collided;

    //------------------------[ACTIONS]---------------------------
    private WildPokemonEvents _wildPokemonEvents;
    public WildPokemonEvents WildPokemonEvents => _wildPokemonEvents;

    private void Awake(){
        //--Set State Machine & Initial State
        WildPokemonStateMachine = new StateMachine<WildPokemon>( this );
        Debug.Log( WildPokemonStateMachine );

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
    }

    public IEnumerator CollisionDelay(){
        //--Delay before collider is enabled so mons can't spawn directly on top of the player and trigger a battle immediately
        yield return new WaitForSeconds( 0.25f );
        BoxCollider.enabled = true;
        yield return null;
    }

    private void OnTriggerEnter( Collider col ){
        if( col.CompareTag( "Player" ) ){
            BoxCollider.enabled = false;
            _collided = true;
            Pokemon.Init();
            WildPokemonEvents.OnPlayerEncounter?.Invoke( this );
            WildPokemonLocation = transform.position;
            WildPokemonWander.SetPausedState();
        }
    }

    public void Despawn(){
        //--Despawn event call
        WildPokemonEvents.OnPokeDespawned?.Invoke( this );

        //--Clear the State Machine
        WildPokemonStateMachine.StateStack.Clear();
        WildPokemonStateMachine = null;

        //--Make sure the list isn't null and contains the wildmon before we remove it
        if( WildPokemonManager.Instance.SpawnedPokemonList != null && WildPokemonManager.Instance.SpawnedPokemonList.Contains( this ) ){
            WildPokemonManager.Instance.SpawnedPokemonList?.Remove( this );
        }
        
        //--Die
        Debug.Log( this + " has been destroyed" );
        Destroy( gameObject );
    }

    private void OnGUI(){
        var style = new GUIStyle();
        style.fontSize = 24;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;

        GUILayout.BeginArea( new Rect( 300, 0, 500, 500 ) );
        GUILayout.Label( "WILD POKEMON STATE STACK", style ); //--REMEMBER YOU WERE DOING THIS LOL I LEFT THE VIDEO OPEN FOR YOU
        foreach( var state in WildPokemonStateMachine.StateStack ){
            GUILayout.Label( state.GetType().ToString(), style );
        }
        GUILayout.EndArea();
    }

}
