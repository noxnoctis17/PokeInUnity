using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_PoisonDamage : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        if( unit.Pokemon.SevereStatus?.ID == StatusConditionID.PSN || unit.Pokemon.SevereStatus?.ID == StatusConditionID.TOX )
        {
            unit.Pokemon.SevereStatus?.OnAfterTurn?.Invoke( unit.Pokemon );
        }
    }
}
