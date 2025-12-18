using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_WeatherDamage : IRoundEndPhaseHandler
{
    public void Apply( BattleSystem battleSystem, BattleUnit unit )
    {
        int prevHp = unit.Pokemon.CurrentHP;
        battleSystem.Field.Weather?.OnWeatherEffect?.Invoke( unit.Pokemon );
        int damage = prevHp - unit.Pokemon.CurrentHP;

        if( damage != 0 )
            unit.Pokemon.BattleItemEffect?.OnTakeMoveDamage?.Invoke( unit, null, null, damage ); //--If this starts to act up in the future, use OnTakePassiveDamage instead.
    }
}
