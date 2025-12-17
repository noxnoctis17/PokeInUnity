using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_CourtEffect : IRoundEndPhaseHandler
{
    public void Apply( BattleSystem battleSystem, BattleUnit unit )
    {
        int prevHp = unit.Pokemon.CurrentHP;
        Debug.Log( "RoundEndPhase_CourtEffect" );
        var court = battleSystem.Field.GetUnitCourt( unit );
        foreach( var condition in court.Conditions )
        {
            if( condition.Value?.OnCourtEffect != null )
                Debug.Log( $"Condition is: {condition.Key}" );
                
            condition.Value?.OnCourtEffect?.Invoke( unit, battleSystem.Field, court.Location );
        }

        int damage = prevHp - unit.Pokemon.CurrentHP;
        if( damage != 0 )
            unit.Pokemon.BattleItemEffect?.OnTakeMoveDamage?.Invoke( unit, null, null, damage );
    }
}
