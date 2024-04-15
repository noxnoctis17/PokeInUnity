using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class WildMon_ScaredState : State<WildPokemon>
{
    private WildPokemon _wildPokemon;
    
    public override void EnterState( WildPokemon owner ){
        Debug.Log( _wildPokemon + "Enter State: " + this );
        _wildPokemon = owner;
        _wildPokemon.PokeAnimator.OnAnimationStateChange?.Invoke( PokemonAnimator.AnimationState.Walking );
    }

    public override void UpdateState(){

    }

    public override void ExitState(){
        Debug.Log( "Exit Scared State" );
    }
}
