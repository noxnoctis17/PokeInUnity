using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RoundEndPhase_WeatherDuration : IRoundEndPhaseHandler
{
    public void OnPhaseTick( BattleSystem battleSystem )
    {
        if( battleSystem.Field.Weather != null )
        {
            if( battleSystem.Field.WeatherDuration > 0 )
            {
                if( battleSystem.Field.Weather?.EffectMessage != null )
                {
                    string message = battleSystem.Field.Weather?.EffectMessage;
                    battleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( message ) );
                }

                Debug.Log( $"Reducing {battleSystem.Field.Weather?.ID}'s Time Left from {battleSystem.Field.WeatherDuration} to {( battleSystem.Field.WeatherDuration - 1 )}" );
                battleSystem.Field.WeatherDuration--;
            }
            else if( battleSystem.Field.WeatherDuration == 0 )
            {
                if( battleSystem.Field.Weather?.EndMessage != null )
                {
                    string message = battleSystem.Field.Weather?.EndMessage;
                    battleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( message ) );
                }

                //--If the route has a default weather (or eventually an active weather as part of a global weather system or something)
                //--we set the battlefield's weather to the default weather, without a duration since it should just continue until another
                //--weather overrides it (or the route's weather ends if that's a thing)
                //--else we set the weather to None, which the weather controller handles as well, and clear the id and duration.
                if( WeatherController.Instance.CurrentListener != null && WeatherController.Instance.CurrentListener.DefaultAreaWeather != WeatherConditionID.None )
                {
                    battleSystem.Field.SetWeather( WeatherController.Instance.CurrentListener.DefaultAreaWeather );
                    battleSystem.Field.WeatherDuration = null;

                    if( battleSystem.Field.Weather?.StartMessage != null )
                    {
                        string message = battleSystem.Field.Weather?.StartMessage;
                        battleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( message ) );
                    }
                }
                else
                {
                    battleSystem.Field.SetWeather( WeatherConditionID.None );
                    battleSystem.Field.Weather = null;
                    battleSystem.Field.WeatherDuration = null;
                }
            }

        }
    }
}
