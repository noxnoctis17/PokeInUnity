using UnityEngine;
using System;
using System.Collections;

public class WildPokemon : MonoBehaviour
{
    //------------------=-[ POKEMON SO ]-------------------------
    
    [Header("Pokemon")]
    public PokemonClass wildPokemon;
    // [SerializeField] BattleSystem _battleSystem;

    //---------------[ SPRITES & ANIMATIONS ]--------------------

    [Header("Sprite and Animations")]
    [SerializeField] private SpriteRenderer _sprite;
    // [SerializeField] private Animator _animator; --not in use yet hehe

    //----------------[ COLLIDER & WANDERING ]--------------------

    public static event Action OnWildPokemonCollided;
    public static Vector3 WildPokemonLocation;
    public delegate PokemonClass PokeSODelegate();
    public static PokeSODelegate pokeSODelegate;
    private BoxCollider _boxCollider;
    private bool _collided;
    public bool Collided => _collided;

    //-----------------------------------------------------------

    private void Awake(){
        _sprite.sprite = wildPokemon.PokeSO.FrontSprite;
        //_animator = wildPokemon.animations; -----not in use yet, haven't added animations lol
        _boxCollider = GetComponent<BoxCollider>();
        _boxCollider.enabled = false;
        StartCoroutine(CollisionDelay());
    }

    private IEnumerator CollisionDelay()
    {
        yield return new WaitForSeconds( 0.25f );
        _boxCollider.enabled = true;
        yield return null;
    }

    private void OnTriggerEnter( Collider col ){
        if( col.CompareTag( "Player" ) ){
            _boxCollider.enabled = false;
            _collided = true;
            wildPokemon.Init();
            Debug.Log("wildPokemon");
            pokeSODelegate = () => this.wildPokemon;
            OnWildPokemonCollided?.Invoke();
            WildPokemonLocation = transform.position;
        }
    }

}
