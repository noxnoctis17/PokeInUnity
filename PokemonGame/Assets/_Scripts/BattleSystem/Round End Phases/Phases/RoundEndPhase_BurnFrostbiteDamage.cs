using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_BurnFrostbiteDamage : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        if( unit.Pokemon.SevereStatus?.ID == StatusConditionID.BRN || unit.Pokemon.SevereStatus?.ID == StatusConditionID.FBT )
        {
            unit.Pokemon.SevereStatus?.OnAfterTurn?.Invoke( unit.Pokemon );
        }
    }
}
