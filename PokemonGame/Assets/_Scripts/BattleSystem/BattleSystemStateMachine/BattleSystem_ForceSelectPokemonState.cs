using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class BattleSystem_ForceSelectPokemonState : State<BattleSystem>
{
    private BattleSystem _battleSystem;

    public override void EnterState( BattleSystem owner )
    {
        _battleSystem = owner;

    }

    public override void ExitState()
    {

    }
}
