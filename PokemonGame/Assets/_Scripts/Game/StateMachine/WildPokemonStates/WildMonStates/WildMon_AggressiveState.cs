using NoxNoctisDev.StateMachine;
using UnityEngine;

public class WildMon_AggressiveState : State<WildPokemon>
{
	public static WildMon_AggressiveState Instance { get; private set; }
    private WildPokemon _wildPokemon;
    private WildPokemonWander _wander;
    private Vector3 _previousPosition;

    private void Awake(){
        Instance = this;
    }

    public override void Enter( WildPokemon owner ){
        Debug.Log( "Enter Aggressive State" );
        _wildPokemon = owner;
        _wander = _wildPokemon.WildPokemonWander;

        _wander.AgentMon.maxSpeed = 10f;
        _wander.AgentMon.maxAcceleration = 10f;
        _previousPosition = _wander.AgentMon.position;
        _wander.AgentMon.destination = PlayerReferences.Instance.PlayerTransform.position;
    }

    public override void Execute(){
        float stopAggressiveDistance = 15f;

        if( Vector3.Distance( transform.position, PlayerReferences.Instance.PlayerTransform.position ) > stopAggressiveDistance ){
            if( _wander.AgentMon.remainingDistance < 0.5f ){
                _wander.AgentMon.destination = _previousPosition;
            }

            if( Vector3.Distance( transform.position, _previousPosition ) < 0.5f ){
                _wander.AgentMon.maxSpeed = 3;
                _wander.AgentMon.maxAcceleration = 3f;
                _wander.PopState();
            }
        }
    }

    public override void Exit(){
        Debug.Log( "Exit Aggressive State" );
    }
}
