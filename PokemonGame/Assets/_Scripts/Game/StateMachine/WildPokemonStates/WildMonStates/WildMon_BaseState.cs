using NoxNoctisDev.StateMachine;
using UnityEngine;

public class WildMon_BaseState : State<WildPokemon>
{
	public static WildMon_BaseState Instance { get; private set; }
    private WildPokemon _wildPokemon;
    private PokemonSO _pokeSO;
    private WildPokemonWander _wander;

    private void Awake(){
        Instance = this;
    }
    
    public override void Enter( WildPokemon owner ){
        Debug.Log( "Enter Base State" );
        _wildPokemon = owner;
        _wander = _wildPokemon.WildPokemonWander;
        _pokeSO = _wildPokemon.Pokemon.PokeSO;

        _wander.SetWanderState();
    }
}
