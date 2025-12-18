using UnityEngine;
using NoxNoctisDev.StateMachine;

public class Spawner_PausedState : State<WildPokemonSpawner>
{
	// public static Spawner_PausedState Instance;
    private WildPokemonSpawner _spawner;

    private void Awake(){
        // Instance = this;
    }

    public override void EnterState( WildPokemonSpawner owner ){
        _spawner = owner;
        // Debug.Log( "Enter Paused State" );
    }

    public override void ExitState(){
        // Debug.Log( "Leaving Paused State" );
    }
}
