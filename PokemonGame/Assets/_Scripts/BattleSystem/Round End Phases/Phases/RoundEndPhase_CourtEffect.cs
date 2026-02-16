using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_CourtEffect : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        int prevHp = unit.Pokemon.CurrentHP;
        var court = battleSystem.Field.GetUnitCourt( unit );
        
        foreach( var condition in court.Conditions )
        {                
            if( court.Conditions.ContainsKey( CourtConditionID.LeechSeed ) && condition.Value == court.Conditions[CourtConditionID.LeechSeed] )
                continue;
            else
                condition.Value?.OnCourtEffect?.Invoke( unit, battleSystem.Field, court.Location );
        }

        int damage = prevHp - unit.Pokemon.CurrentHP;
        if( damage != 0 )
            unit.Pokemon.BattleItemEffect?.OnTakePassiveDamage?.Invoke( unit, damage );
    }
}
