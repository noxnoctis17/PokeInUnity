using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_WeatherDamage : IRoundEndPhaseHandler
{
    public void Apply( BattleSystem battleSystem, Pokemon pokemon )
    {
        battleSystem.Field.Weather?.OnWeatherEffect?.Invoke( pokemon );
    }
}
