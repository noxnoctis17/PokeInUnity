using UnityEngine;
using NoxNoctisDev.StateMachine;

public class WildMon_CuriousState : State<WildPokemon>
{
	public static WildMon_CuriousState Instance { get; private set; }
    private WildPokemon _wildPokemon;
    private WildPokemonWander _wander;

    private void Awake(){
        Instance = this;
    }
    
    public override void Enter( WildPokemon owner ){
        Debug.Log( "Enter Curious State" );
        _wildPokemon = owner;
        _wander = _wildPokemon.WildPokemonWander;

        _wander.AgentMon.SetPath( null );
        _wander.AgentMon.endReachedDistance = 5f;
        _wander.AgentMon.destination = PlayerReferences.Instance.PlayerTransform.position;
    }

    public override void Execute(){
        float stopCuriousDistance = 11f;
        if( Vector3.Distance( transform.position, PlayerReferences.Instance.PlayerTransform.position ) > stopCuriousDistance ){
            _wander.AgentMon.endReachedDistance = 0.5f;
            _wander.PopState();
        }
    }

    public override void Exit(){
        Debug.Log( "Exit Curious State" );
    }
}
