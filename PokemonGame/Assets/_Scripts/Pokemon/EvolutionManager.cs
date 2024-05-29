using System.Collections;
using UnityEngine;
using NoxNoctisDev.StateMachine;
using UnityEngine.UI;

public class EvolutionManager : State<GameStateController>
{
    public static EvolutionManager Instance { get; private set; }
    public GameStateController StateMachine { get; private set; }
    [SerializeField] private GameObject _evolutionUI;
    [SerializeField] private Image _pokemonSprite;
    [SerializeField] private SpriteRenderer _evolutionSpriteRenderer;
    [SerializeField] private PokemonAnimator _pokeAnimator;

    private void Awake(){
        Instance = this;
    }

    public override void EnterState( GameStateController owner ){
        StateMachine = owner;

        PlayerReferences.Instance.PlayerController.EnableUI();
        _evolutionUI.SetActive( true );
    }

    public override void UpdateState(){
        _pokemonSprite.sprite = _evolutionSpriteRenderer.sprite;
    }

    public override void ReturnToState(){
        PlayerReferences.Instance.PlayerController.EnableUI();
    }

    public override void PauseState(){
        PlayerReferences.Instance.PlayerController.DisableUI();
    }

    public override void ExitState(){
        PlayerReferences.Instance.PlayerController.DisableUI();
        _evolutionUI.SetActive( false );
    }

    public IEnumerator Evolve( Pokemon pokemon, Evolutions evolution ){
        //--Initialize Animator
        _pokeAnimator.Initialize( pokemon.PokeSO );

        //--Evolution Dialogue
        yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"What? {pokemon.PokeSO.Name} is evolving!" );
        string prevoName = pokemon.PokeSO.Name;

        //--Evolution Animations
        yield return _pokeAnimator.PlayBeginEvolutionAnimation();
        pokemon.Evolve( evolution );
        _pokeAnimator.Initialize( pokemon.PokeSO );
        yield return _pokeAnimator.PlayFinishedEvolutionAnimation();

        //--Evolved Dialogue
        yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{prevoName} evolved into {pokemon.PokeSO.Name}!" );
        yield return new WaitForEndOfFrame();

        //--Check if the Pokemon can evolve again by level up, and mark it appropriately
        //--We do this because we'll be manually triggering evolution from the party screen
        //--in the pause menu, like in Legends Arceus, rather than auto-triggering after battle/candy
        //--So it's possible the player hasn't evolved from stage 1 to stage 2 until after reaching the level requirement
        //--for stage 2 to be able to evolve into stage 3, either intentionally or just through massive exp gain/box exp gain neglect
        //--since pokemon in storage will gain exp, too
        var evolveAgain = pokemon.CheckForEvolution();
        if( evolveAgain != null )
            pokemon.SetCanEvolveByLevelUp( true );
        else
            pokemon.SetCanEvolveByLevelUp( false );

        //--Leave Evolving State
        StateMachine.GameStateMachine.Pop();
    }

}
