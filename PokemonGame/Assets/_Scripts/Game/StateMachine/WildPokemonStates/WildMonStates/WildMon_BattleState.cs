using System;
using NoxNoctisDev.StateMachine;

public class WildMon_BattleState : State<WildPokemon>
{
    public Action OnAttack;
    private WildPokemon _wildPokemon;

    private void OnEnable(){
        OnAttack += RunAttackAnim;
    }

    private void OnDisable(){
        OnAttack -= RunAttackAnim;
    }

    public override void EnterState( WildPokemon owner ){
        _wildPokemon = owner;
        _wildPokemon.PokeAnimator.OnAnimationStateChange?.Invoke( PokemonAnimator.AnimationState.Idle );    //--Non-attack battle anims 
    }                                                                                                       //--will always be idles

    public override void ExitState(){

    }

    private void RunAttackAnim(){
        //--need to somehow get access to the battle system, or otherwise use a static
        //--action that passes in the specific pokemon
    }
}
