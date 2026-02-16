using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_WeatherDamage : IRoundEndPhaseHandler
{
    public void OnUnitTick( BattleSystem battleSystem, BattleUnit unit )
    {
        int prevHp = unit.Pokemon.CurrentHP;
        battleSystem.Field.Weather?.OnWeatherEffect?.Invoke( unit.Pokemon );
        int damage = prevHp - unit.Pokemon.CurrentHP;

        if( damage != 0 )
            unit.Pokemon.BattleItemEffect?.OnTakePassiveDamage?.Invoke( unit, damage ); //--If this starts to act up in the future, use OnTakePassiveDamage instead. --it acted up, moved from OnTakeDamage with two nulls to passive damage -- 02/02/26
    }
}
