using System.Collections.Generic;
using UnityEngine;

public class WeatherConditionsDB
{
    public static Dictionary<WeatherConditionID, WeatherCondition> Conditions { get; private set; }

    public static void Init(){
        SetDictionary();

        foreach( var kvp in Conditions ){
            var conditionID = kvp.Key;
            var condition = kvp.Value;

            condition.ID = conditionID;
        }
    }

    public static void Clear(){
        Conditions = null;
    }

    private static void SetDictionary(){
        Conditions = new Dictionary<WeatherConditionID, WeatherCondition>()
        {
            //========================================================================================================================================
            //===========================================================[ WEATHER ]==================================================================
            //========================================================================================================================================
            {
              //--None Weather, left beef
              WeatherConditionID.NONE, new WeatherCondition()
              {
                  Name = "None Weather, left beef"
              }  
            },

            {   //--Harsh Sunlight
                WeatherConditionID.SUNNY, new WeatherCondition()
                {
                    Name = "Harsh Sunlight",
                    StartMessage = "The sunlight is harsh!",
                    StartByMoveMessage = "The sunlight turned harsh!",
                    EffectMessage = "The sunlight is strong.",
                    EndMessage = "The harsh sunlight faded.",

                    OnEnterWeather = ( Pokemon pokemon ) =>
                    {
                        //--Chlorophyll
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        if( pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] == AbilityID.Chlorophyll )
                        {
                            pokemon.Ability?.OnAbilityEnter?.Invoke( pokemon, null, BattleSystem.Instance.Field );
                        }

                        //--Solar Power
                        if( pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] == AbilityID.SolarPower )
                        {
                            Debug.Log( $"{pokemon.NickName}'s Solar Power has been activated!" );
                            pokemon.ApplyDirectStatModifier( Stat.SpAttack, DirectModifierCause.SolarPower, 1.5f );
                        }
                    },

                    OnExitWeather = ( Pokemon pokemon ) =>
                    {
                        //--Chlorophyll
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        if( pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] == AbilityID.Chlorophyll )
                        {
                            pokemon.Ability?.OnAbilityExit?.Invoke( pokemon, null, BattleSystem.Instance.Field );
                        }

                        //--Solar Power
                        if( pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] == AbilityID.SolarPower )
                        {
                            Debug.Log( $"{pokemon.NickName}'s Solar Power has gone out!" );
                            pokemon.RemoveDirectStatModifier( Stat.SpAttack, DirectModifierCause.SolarPower );
                        }
                    },

                    OnDamageModify = ( Pokemon source, Pokemon target, Move move ) =>
                    {
                        if( move.MoveSO.Type == PokemonType.Fire )
                            return 1.5f;
                        else if( move.MoveSO.Type == PokemonType.Water )
                            return 0.5f;

                        return 1f;
                    },
                }

            },

            {   //--RAIN
                WeatherConditionID.RAIN, new WeatherCondition()
                {
                    Name = "Heavy Rain",
                    StartMessage = "Rain is falling heavily!",
                    StartByMoveMessage = "It started raining!",
                    EffectMessage = "The rain continues to fall.",
                    EndMessage = "The rain stopped.",

                    OnEnterWeather = ( Pokemon pokemon ) =>
                    {
                        //--Swift Swim
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        if( pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] == AbilityID.SwiftSwim )
                        {
                            pokemon.Ability?.OnAbilityTriggered?.Invoke( pokemon );
                            Debug.Log( $"{pokemon.NickName}'s Swift Swim is active!" );
                            pokemon.ApplyDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD, 2f );
                            Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        }
                    },

                    OnExitWeather = ( Pokemon pokemon ) =>
                    {
                        //--Swift Swim
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        if( pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] == AbilityID.SwiftSwim )
                        {
                            Debug.Log( $"{pokemon.NickName}'s Swift Swim is no longer active!" );
                            pokemon.RemoveDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD );
                            Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        }
                    },

                    OnDamageModify = ( Pokemon source, Pokemon target, Move move ) =>
                    {
                        if( move.MoveSO.Type == PokemonType.Water )
                            return 1.5f;
                        else if( move.MoveSO.Type == PokemonType.Fire )
                            return 0.5f;

                        return 1f;
                    },
                }

            },

            {   //--SANDSTORM
                WeatherConditionID.SANDSTORM, new WeatherCondition()
                {
                    Name = "Sandstorm",
                    StartMessage = "A sandstorm is raging!",
                    StartByMoveMessage = "A sandstorm kicked up!",
                    EffectMessage = "The sandstorm rages!",
                    EndMessage = "The sandstorm subsided.",

                    //--This should only be called when a pokemon enters the field and a
                    //--Weather Condition is currently active. Really only for Sandstorm and Snow, but who knows
                    OnEnterWeather = ( Pokemon pokemon ) =>
                    {
                        //--Sandstorm SPDEF Boost.
                        Debug.Log( $"{pokemon.NickName}'s SPDEF Stat is: {pokemon.SpDefense}" );
                        if( pokemon.CheckTypes( PokemonType.Rock ) || pokemon.CheckTypes( PokemonType.Ground ) || pokemon.CheckTypes( PokemonType.Steel ) )
                        {
                            pokemon.ApplyDirectStatModifier( Stat.SpDefense, DirectModifierCause.WeatherSpDEF, 1.5f );
                            Debug.Log( $"{pokemon.NickName}'s SPDEF Stat is: {pokemon.SpDefense}" );
                        }

                        //--Sand Rush
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        if( pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] == AbilityID.SandRush )
                        {
                            pokemon.Ability?.OnAbilityTriggered?.Invoke( pokemon );
                            Debug.Log( $"{pokemon.NickName}'s Sand Rush is active!" );
                            pokemon.ApplyDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD, 2f );
                            Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        }      
                    },

                    OnWeatherEffect = ( Pokemon pokemon ) =>
                    {
                        //--If the Pokemon is Rock, Ground, or Steel type we simply return. Else, the Pokemon takes sandstorm damage. Pokemon with Abilities like Sandforce and Sand Rush also do not take sandstorm damage!
                        if( pokemon.CheckTypes( PokemonType.Rock ) || pokemon.CheckTypes( PokemonType.Ground ) || pokemon.CheckTypes( PokemonType.Steel ) || pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] == AbilityID.SandRush )
                            return;
                        else{
                            var damage = Mathf.RoundToInt( pokemon.MaxHP / 16f );
                            pokemon.DecreaseHP( damage );
                            pokemon.AddStatusEvent( StatusEventType.Damage, $"{pokemon.NickName} is buffeted by the sandstorm!" );
                        }
                    },

                    OnExitWeather = ( Pokemon pokemon ) =>
                    {
                        //--Sandstorm SPDEF Boost
                        Debug.Log( $"{pokemon.NickName}'s SPDEF Stat is: {pokemon.SpDefense}" );
                        if( pokemon.CheckTypes( PokemonType.Rock ) || pokemon.CheckTypes( PokemonType.Ground ) || pokemon.CheckTypes( PokemonType.Steel ) )
                        {
                            pokemon.RemoveDirectStatModifier( Stat.SpDefense, DirectModifierCause.WeatherSpDEF );
                            Debug.Log( $"{pokemon.NickName}'s SPDEF Stat is: {pokemon.SpDefense}" );
                        }
                            
                        
                        //--Sand Rush
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        if( pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] == AbilityID.SandRush )
                        {
                            Debug.Log( $"{pokemon.NickName}'s Sand Rush is no longer active!" );
                            pokemon.RemoveDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD );
                            Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        }
                    }
                }

            },

            {   //--SNOW
                WeatherConditionID.SNOW, new WeatherCondition()
                {
                    Name = "Snowscape",
                    StartMessage = "The falling snow chills the air!",
                    StartByMoveMessage = "It started snowing!",
                    EffectMessage = "The snow continues to fall.",
                    EndMessage = "The snowfall stopped.",

                    //--This should only be called when a pokemon enters the field and a
                    //--Weather Condition is currently active. Really only for Sandstorm and Snow, but who knows
                    OnEnterWeather = ( Pokemon pokemon ) =>
                    {
                        //--Ice type pokemon gain a 50% defense boost in snow
                        Debug.Log( $"{pokemon.NickName}'s DEF Stat is: {pokemon.Defense}" );
                        if( pokemon.CheckTypes( PokemonType.Ice ) )
                        {
                            pokemon.ApplyDirectStatModifier( Stat.Defense, DirectModifierCause.WeatherDEF, 1.5f );
                            Debug.Log( $"{pokemon.NickName}'s DEF Stat is: {pokemon.Defense}" );
                        }

                        //--Slush Rush
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        if( pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] == AbilityID.SlushRush )
                        {
                            pokemon.Ability?.OnAbilityTriggered?.Invoke( pokemon );
                            Debug.Log( $"{pokemon.NickName}'s Slush Rush is active!" );
                            pokemon.ApplyDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD, 2f );
                            Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        }
                    },

                    //--Called when a pokemon leaves the field during a weather condition
                    OnExitWeather = ( Pokemon pokemon ) =>
                    {
                        //--Ice type pokemon gain a 50% defense boost in snow
                        Debug.Log( $"{pokemon.NickName}'s DEF Stat is: {pokemon.Defense}" );
                        if( pokemon.CheckTypes( PokemonType.Ice ) )
                        {
                            pokemon.RemoveDirectStatModifier( Stat.Defense, DirectModifierCause.WeatherDEF );
                            Debug.Log( $"{pokemon.NickName}'s DEF Stat is: {pokemon.Defense}" );
                        }

                        //--Slush Rush
                        Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        if( pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] == AbilityID.SlushRush )
                        {
                            Debug.Log( $"{pokemon.NickName}'s Slush Rush is no longer active!" );
                            pokemon.RemoveDirectStatModifier( Stat.Speed, DirectModifierCause.WeatherSPD );
                            Debug.Log( $"{pokemon.NickName}'s SPD Stat is: {pokemon.Speed}" );
                        }
                    },

                    //--We're buffing the shit out of snow LOL
                    OnDamageModify = ( Pokemon source, Pokemon target, Move move ) =>
                    {
                        if( move.MoveSO.Type == PokemonType.Ice )
                            return 1.5f;
                        else if( move.MoveSO.Type == PokemonType.Fighting )
                            return 0.5f;

                        return 1f;
                    },
                }

            }

        };
    }
}

public enum WeatherConditionID
{
    //--None weather, left beef
    NONE,

    //--Weather
    SUNNY,
    RAIN,
    SANDSTORM,
    SNOW,
    SHADOWSKY,

}
