using System.Collections;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class WildMon_IdleState : State<WildPokemon>
{
    private WildPokemon _wildPokemon;
    private PokemonAnimator _pokeAnimator;
    private SpriteAnimator _spriteAnimator;
    
    public override void EnterState( WildPokemon owner ){
        Debug.Log( _wildPokemon + "Enter State: " + this );
        _wildPokemon = owner;
        _wildPokemon.AgentMon.SetPath( null );
        _wildPokemon.PokeAnimator.OnAnimationStateChange?.Invoke( PokemonAnimator.AnimationState.Idle );
        _pokeAnimator = _wildPokemon.PokeAnimator;
        _spriteAnimator = _pokeAnimator.Animator;
        StartCoroutine( WanderIdle() );
    }

    public override void ExitState(){
        // Debug.Log( "Exit Idle State" );
        StopAllCoroutines();
    }

    private IEnumerator WanderIdle(){
        yield return new WaitForSeconds( Random.Range( 5f, 21f ) );
        _wildPokemon.WildPokemonStateMachine.OnQueueNextState?.Invoke( _wildPokemon.WanderState );
    }
}
