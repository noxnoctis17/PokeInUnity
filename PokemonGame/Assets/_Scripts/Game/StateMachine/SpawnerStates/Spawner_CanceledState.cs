using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NoxNoctisDev.StateMachine;

public class Spawner_CanceledState : State<WildPokemonSpawner>
{
    private WildPokemonSpawner _spawner;

    public override void EnterState( WildPokemonSpawner owner ){
        _spawner = owner;
        _spawner.OnSpawnerCanceled?.Invoke();
        Debug.Log( "Enter Canceled State" );
    }

    public override void ExitState(){
        Debug.Log( "Leaving Canceled State" );
    }
}
