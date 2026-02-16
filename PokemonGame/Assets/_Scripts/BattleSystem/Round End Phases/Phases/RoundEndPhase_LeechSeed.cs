using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_LeechSeed : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        int prevHp = unit.Pokemon.CurrentHP;
        var court = battleSystem.Field.GetUnitCourt( unit );

        if( court.Conditions.ContainsKey( CourtConditionID.LeechSeed ) )
        {
            var leechSeed = court.Conditions[CourtConditionID.LeechSeed];
            leechSeed.OnCourtEffect?.Invoke( unit, battleSystem.Field, court.Location );

            int damage = prevHp - unit.Pokemon.CurrentHP;
            if( damage != 0 )
                unit.Pokemon.BattleItemEffect?.OnTakePassiveDamage?.Invoke( unit, damage );
        }
    }
}
