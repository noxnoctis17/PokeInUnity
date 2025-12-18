using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class BattleSystem_BusyState : State<BattleSystem>
{
    private BattleSystem _battleSystem;

    public override void EnterState( BattleSystem owner )
    {
        _battleSystem = owner;
        _battleSystem.SetStateEnum( BattleStateEnum.Busy );
        _battleSystem.PlayerBattleMenu.OnPauseState?.Invoke();
    }

    public override void ReturnToState()
    {
        _battleSystem.SetStateEnum( BattleStateEnum.Busy );
        _battleSystem.PlayerBattleMenu.OnPauseState?.Invoke();
    }
}
