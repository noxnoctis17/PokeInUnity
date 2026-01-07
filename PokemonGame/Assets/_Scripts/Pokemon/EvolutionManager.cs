using System.Collections;
using UnityEngine;
using NoxNoctisDev.StateMachine;
using UnityEngine.UI;
using DG.Tweening;
using System;

public class EvolutionManager : State<GameStateController>
{
    public static EvolutionManager Instance { get; private set; }
    public GameStateController StateMachine { get; private set; }
    [SerializeField] private GameObject _evolutionUI;
    [SerializeField] private GameObject _pokemonObject;
    [SerializeField] private Image _pokemonSprite;
    [SerializeField] private SpriteRenderer _evolutionSpriteRenderer;
    [SerializeField] private PokemonAnimator _pokeAnimator;
    [SerializeField] private Sprite _transparentPixel;
    public bool Evolving { get; private set; }

    public event Action OnEvolutionStateEntered;
    public event Action OnEvolutionStateExited;

    private void Awake(){
        Instance = this;
    }

    public override void EnterState( GameStateController owner ){
        StateMachine = owner;
        OnEvolutionStateEntered?.Invoke();

        _pokemonSprite.sprite = _transparentPixel;
        _evolutionSpriteRenderer.sprite = _transparentPixel;

        _evolutionUI.SetActive( true );

        PlayerReferences.Instance.PlayerController.EnableUI();
    }

    public void Update(){
        if( Evolving )
        {
            _pokemonSprite.color = _evolutionSpriteRenderer.color;
            _pokemonSprite.sprite = _evolutionSpriteRenderer.sprite;
        }
    }

    public override void ReturnToState(){
        OnEvolutionStateEntered?.Invoke();
        PlayerReferences.Instance.PlayerController.EnableUI();
    }

    public override void PauseState(){
        OnEvolutionStateExited?.Invoke();
        PlayerReferences.Instance.PlayerController.DisableUI();
    }

    public override void ExitState(){
        OnEvolutionStateExited?.Invoke();
        PlayerReferences.Instance.PlayerController.DisableUI();
        _evolutionUI.SetActive( false );
    }

    public IEnumerator Evolve( Pokemon pokemon, Evolutions evolution ){
        //--Initialize Animator
        _pokeAnimator.Initialize( pokemon.PokeSO );
        
        yield return _pokeAnimator.PlayBeginEvolutionAnimation( pokemon.PokeSO, evolution.Evolution );
        Evolving = true;

        //--Evolving Dialogue
        yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{pokemon.NickName} is evolving!", true );
        string prevoName = pokemon.NickName;
        string evoSpecies = evolution.Evolution.Species;

        Debug.Log( $"[Evolution Manager] Playing evolution animation!" );
        //--Evolution Animation
        yield return _pokemonSprite.DOFade( 1f, 0.1f );
        yield return _pokeAnimator.PlayEvolutionAnimation();
        yield return new WaitForSeconds( 1f );
        
        //--Evolve
        pokemon.Evolve( evolution );

        //--Evolved Dialogue
        yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{prevoName} evolved into {evoSpecies}!" );
        yield return new WaitForEndOfFrame();

        //--Check if the Pokemon can evolve again by level up, and mark it appropriately
        //--We do this because we'll be manually triggering evolution from the party screen
        //--in the pause menu, like in Legends Arceus, rather than auto-triggering after battle/candy
        //--So it's possible the player hasn't evolved from stage 1 to stage 2 until after reaching the level requirement
        //--for stage 2 to be able to evolve into stage 3, either intentionally or just through massive exp gain/box exp gain neglect
        //--since pokemon in storage will gain exp, too
        var evolveAgain = pokemon.CheckForEvolution();
        if( evolveAgain != null ){
            pokemon.SetCanEvolveByLevelUp( true );
            Debug.Log( $"[Evolution Manager] {pokemon.NickName} can evolve again." );
        }
        else if( evolveAgain == null ){
            pokemon.SetCanEvolveByLevelUp( false );
            Debug.Log( $"[Evolution Manager] {pokemon.NickName} can NOT evolve again." );
        }

        //--Leave Evolving State
        Evolving = false;
        _pokemonSprite.DOFade( 0f, 0f );
        StateMachine.GameStateMachine.Pop();
    }

}
