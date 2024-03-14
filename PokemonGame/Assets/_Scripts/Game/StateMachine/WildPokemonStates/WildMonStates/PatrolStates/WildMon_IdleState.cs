using System.Collections;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class WildMon_IdleState : State<WildPokemon>
{
    private WildPokemon _wildPokemon;
    private PokemonAnimator _pokeAnimator;
    private SpriteAnimator _spriteAnimator;
    
    public override void EnterState( WildPokemon owner ){
        // Debug.Log( "Enter Idle State" );
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
        yield return new WaitForSeconds( UnityEngine.Random.Range( 5f, 21f ) );
        _wildPokemon.WildPokemonStateMachine.OnQueueNextState?.Invoke( _wildPokemon.WanderState );
    }

    // private void SetIdleSprites(){
    //     //--Assigns Idle Sprites based on facing direction/parent transform forward
    //     switch( _pokeAnimator.FacingDir ){
    //         case _pokeAnimator.FacingDirection.Up:
    //             _currentAnimSheet = _idleUpSprites;
            
    //         break;

    //         case FacingDirection.Down:
    //             _currentAnimSheet = _idleDownSprites;

    //         break;

    //         default:
    //             _currentAnimSheet = _defaultAnimSheet;

    //         break;
    //     }
    // }
}
