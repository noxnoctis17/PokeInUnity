using System;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class WildMon_BattleState : State<WildPokemon>
{
    private WildPokemon _wildPokemon;

    public override void EnterState( WildPokemon owner ){
        Debug.Log( _wildPokemon + "Enter State: " + this );
        _wildPokemon = owner;
        _wildPokemon.AgentMon.SetPath( null ); 
        _wildPokemon.AgentMon.enabled = false;
        _wildPokemon.PokeAnimator.OnAnimationStateChange?.Invoke( PokemonAnimator.AnimationState.Idle );    //--Non-attack battle anims will always be idles                                                              
    }

}
