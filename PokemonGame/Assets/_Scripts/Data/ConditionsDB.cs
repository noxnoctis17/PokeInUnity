using System.Collections.Generic;
using UnityEngine;

public class ConditionsDB
{
    public static Dictionary<ConditionID, Condition> Conditions { get; set; } 
    public static string StatusIconsPath;

    public static void Init(){
        LoadStatusIcons();
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

    private static void LoadStatusIcons(){
        StatusIconsPath = "Assets/Resources/UI Graphics/SevereStatusIcons.png";
    }

    private static void SetDictionary(){
        Conditions = new Dictionary<ConditionID, Condition>()
        {
//========================================================================================================================================
//========================================================[ SEVERE STATUS ]===============================================================
//========================================================================================================================================
            {   //--POISON
                ConditionID.PSN, new Condition()
                {
                    Name = "Poison",
                    StartMessage = "was poisoned!",
                    StatusIcon = StatusIconAtlas.StatusIcons[ConditionID.PSN].icon,
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    { 
                        pokemon.DecreaseHP( pokemon.MaxHP / 8 );
                        pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.Name} is hurt by poison!" );
                    }}
            },
                    
            {   //--TOXIC
                ConditionID.TOX, new Condition()
                {
                    Name = "Toxic",
                    StartMessage = "was severely poisoned!",
                    StatusIcon = StatusIconAtlas.StatusIcons[ConditionID.TOX].icon,
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    { 
                        pokemon.DecreaseHP( pokemon.MaxHP / 8 );
                        pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.Name} is hurt by its horrible poisoning!" );
                    }}
            },

            {   //--BURN
                ConditionID.BRN, new Condition()
                {
                    Name = "Burn",
                    StartMessage = "was burned!",
                    StatusIcon = StatusIconAtlas.StatusIcons[ConditionID.BRN].icon,

                    //--Immediate necessary changes that don't return a bool
                    OnApplyStatus = ( Pokemon pokemon ) =>
                    {
                        Debug.Log( $"{pokemon.PokeSO.Name}'s Attack Stat is: {pokemon.Attack}" );
                        pokemon.ApplyDirectStatChange( Stat.Attack, 0.5f );
                        Debug.Log( $"{pokemon.PokeSO.Name}'s Attack Stat is: {pokemon.Attack}" );
                    },

                    //--Effects that run after a turn is completed.
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        // Debug.Log( pokemon.CurrentHP );
                        pokemon.DecreaseHP( pokemon.MaxHP / 16 );
                        pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.Name} is hurt by its burn!" );
                        // Debug.Log( pokemon.CurrentHP );
                    }}
            },

            {   //-PARAYLSIS
                ConditionID.PAR, new Condition()
                {
                    Name = "Paralysis",
                    StartMessage = "has been paralyzed!",
                    StatusIcon = StatusIconAtlas.StatusIcons[ConditionID.PAR].icon,

                    //--Immediate necessary changes that don't return a bool
                    OnApplyStatus = ( Pokemon pokemon ) =>
                    {
                        Debug.Log( $"{pokemon.PokeSO.Name}'s Speed Stat is: {pokemon.Speed}" );
                        pokemon.ApplyDirectStatChange( Stat.Speed, 0.25f );
                        Debug.Log( $"{pokemon.PokeSO.Name}'s Speed Stat is: {pokemon.Speed}" );
                    },

                    OnBeforeTurn = ( Pokemon pokemon ) =>
                    {
                        if( Random.Range( 1, 5 ) == 1 )
                        {
                            return false;
                            //--we're going to change paralysis to 1/4th speed the way it was originally
                            //--and instead, we're going to prevent only the turn it was paralyzed on from happening, removing the paralysis chance
                            //--but this idea was pulled from an idea Cybertron had voiced about players potentially wanting to see
                            //--for changes to paralysis in his buffs and nerfs video
                            //--sleep was already guaranteed 2 turns, which is something he also brought up, so i nailed that idea lol
                            //--freeze will be turned into special burn, aka frostbite from legends arceus
                        }

                        return true;
                    }}
            },

            {   //--SLEEP
                ConditionID.SLP, new Condition()
                {
                    Name = "Sleep",
                    StartMessage = "has fallen asleep!",
                    StatusIcon = StatusIconAtlas.StatusIcons[ConditionID.SLP].icon,
                    OnStart = ( Pokemon pokemon ) =>
                    {
                        //--Sleep is for 1-3 turns? i'm gunna make it a guaranteed 2 turns only
                        pokemon.SevereStatusTime = 2;
                    },

                    OnBeforeTurn = ( Pokemon pokemon ) =>
                    {
                        if( pokemon.SevereStatusTime == 0 )
                        {
                            pokemon.CureSevereStatus();
                            pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.Name} woke up!" );
                            return true;
                        }

                        pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.Name} is fast asleep!" );
                        pokemon.SevereStatusTime--;
                        return false;
                    }}
            },

            {   //--FROSTBITE
                ConditionID.FBT, new Condition()
                {
                    Name = "Frostbite",
                    StartMessage = "has become frostbitten!",
                    StatusIcon = StatusIconAtlas.StatusIcons[ConditionID.FBT].icon,

                    //--Immediate necessary changes that don't return a bool
                    OnApplyStatus = ( Pokemon pokemon ) =>
                    {
                        Debug.Log( $"{pokemon.PokeSO.Name}'s Sp.Atk Stat is: {pokemon.SpAttack}" );
                        pokemon.ApplyDirectStatChange( Stat.SpAttack, 0.5f );
                        Debug.Log( $"{pokemon.PokeSO.Name}'s Sp.Atk Stat is: {pokemon.SpAttack}" );
                    },

                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        pokemon.DecreaseHP( pokemon.MaxHP / 16 );
                        pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.Name} is hurt by its frostbite!" );
                    }}
            },

            {   //--FAINT
                ConditionID.FNT, new Condition()
                {
                    Name = "Faint",
                    StatusIcon = StatusIconAtlas.StatusIcons[ConditionID.FNT].icon,
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        pokemon.CurrentHP = 0;
                    }
                }
            },

//========================================================================================================================================
//=======================================================[ VOLATILE STATUS ]==============================================================
//========================================================================================================================================

            {   //--CONFUSION
                ConditionID.CONFUSION, new Condition()
                {
                    Name = "Confusion",
                    StartMessage = "became confused!",
                    // StatusIcon = StatusIconAtlas.StatusIcons[ConditionID.CNF],
                    OnStart = ( Pokemon pokemon ) =>
                    {
                        //--Confuse for 2-5 turns
                        pokemon.VolatileStatusTime = Random.Range( 2, 6 );
                    },

                    OnBeforeTurn = ( Pokemon pokemon ) =>
                    {
                        if( pokemon.VolatileStatusTime == 0 )
                        {
                            pokemon.CureVolatileStatus();
                            pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.Name} snapped out of confusion!" );
                            return true;
                        }

                        pokemon.VolatileStatusTime--;

                        //--33% Chance to Hurt Itself
                        if( Random.Range( 1,4 ) == 1 )
                        {
                            pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.Name} is confused!" );
                            pokemon.DecreaseHP( pokemon.MaxHP / 16 );
                            pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.Name} hurt itself in confusion!" );
                            return false;
                        }

                        //--Perform Move
                        return true;
                    }}
            },

//========================================================================================================================================
//===========================================================[ WEATHER ]==================================================================
//========================================================================================================================================

            {   //--Harsh Sunlight
                ConditionID.SUNNY, new Condition()
                {
                    Name = "Harsh Sunlight",
                    StartMessage = "The sunlight turned harsh!",
                    EffectMessage = "The sunlight is strong.",
                    EndMessage = "The harsh sunlight faded.",

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
                ConditionID.RAIN, new Condition()
                {
                    Name = "Heavy Rain",
                    StartMessage = "It started raining!",
                    EffectMessage = "The rain continues to fall.",
                    EndMessage = "The rain stopped.",

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
                ConditionID.SANDSTORM, new Condition()
                {
                    Name = "Sandstorm",
                    StartMessage = "A sandstorm kicked up!",
                    EffectMessage = "The sandstorm rages!",
                    EndMessage = "The sandstorm subsided.",

                    //--This should only be called when a pokemon enters the field and a
                    //--Weather Condition is currently active. Really only for Sandstorm and Snow, but who knows
                    OnEnterWeather = ( Pokemon pokemon ) =>
                    {
                        Debug.Log( $"{pokemon.PokeSO.Name}'s SPDEF Stat is: {pokemon.SpDefense}" );
                        if( pokemon.CheckTypes( PokemonType.Rock ) )
                            pokemon.ApplyDirectStatChange( Stat.SpDefense, 1.5f );

                        Debug.Log( $"{pokemon.PokeSO.Name}'s SPDEF Stat is: {pokemon.SpDefense}" );
                        
                    },

                    OnWeather = ( Pokemon pokemon ) =>
                    {
                        //--If the Pokemon is Rock, Ground, or Steel type we simply return. Else, the Pokemon takes sandstorm damage
                        if( pokemon.CheckTypes( PokemonType.Rock ) || pokemon.CheckTypes( PokemonType.Ground ) || pokemon.CheckTypes( PokemonType.Steel ) )
                            return;
                        else{
                            var damage = Mathf.RoundToInt( pokemon.MaxHP / 16f );
                            pokemon.DecreaseHP( damage );
                            pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.Name} is buffeted by the sandstorm!" );
                        }
                    },

                    OnExitWeather = ( Pokemon pokemon ) =>
                    {
                        Debug.Log( $"{pokemon.PokeSO.Name}'s SPDEF Stat is: {pokemon.SpDefense}" );
                        //--check snow, but because we add a 1.5f modifier, we have to remove the same value from the list of modifiers for this stat
                        if( pokemon.CheckTypes( PokemonType.Rock ) )
                            pokemon.RemoveDirectStatChange( Stat.SpDefense, 1.5f );
                            
                        Debug.Log( $"{pokemon.PokeSO.Name}'s SPDEF Stat is: {pokemon.SpDefense}" );
                    }
                }

            },

            {   //--SNOW
                ConditionID.SNOW, new Condition()
                {
                    Name = "Snowscape",
                    StartMessage = "It started snowing!",
                    EffectMessage = "The snow continues to fall.",
                    EndMessage = "The snowfall stopped.",

                    //--This should only be called when a pokemon enters the field and a
                    //--Weather Condition is currently active. Really only for Sandstorm and Snow, but who knows
                    OnEnterWeather = ( Pokemon pokemon ) =>
                    {
                        Debug.Log( $"{pokemon.PokeSO.Name}'s DEF Stat is: {pokemon.Defense}" );
                        //--Ice type pokemon gain a 50% defense boost in snow
                        if( pokemon.CheckTypes( PokemonType.Ice ) )
                            pokemon.ApplyDirectStatChange( Stat.Defense, 1.5f );

                        Debug.Log( $"{pokemon.PokeSO.Name}'s DEF Stat is: {pokemon.Defense}" );
                        
                    },

                    //--Called when a pokemon leaves the field during a weather condition
                    OnExitWeather = ( Pokemon pokemon ) =>
                    {
                        //--Ice type pokemon gain a 50% defense boost in snow
                        //--When we remove direct stat changes, we actually need to remove the value that was
                        //--added to the list of modifiers for that stat, because those modifiers are
                        //--multipled together to get the total modifier that gets multiplied to the stat (before stat stages)
                        Debug.Log( $"{pokemon.PokeSO.Name}'s DEF Stat is: {pokemon.Defense}" );
                        if( pokemon.CheckTypes( PokemonType.Ice ) )
                            pokemon.RemoveDirectStatChange( Stat.Defense, 1.5f );

                        Debug.Log( $"{pokemon.PokeSO.Name}'s DEF Stat is: {pokemon.Defense}" );
                    }
                }

            }

        };
    }

    //--Status bonus that gets added when trying to catch a pokemon. buffed sleep from 2 to 2.5, buffed para from 1.5 to 2
    public static float GetStatusBonus( Condition condition ){
        if( condition == null )
            return 1f;
        else if( condition.ID == ConditionID.SLP )
            return 2.5f;
        else if( condition.ID == ConditionID.PAR )
            return 2f;
        else if( condition.ID == ConditionID.FBT || condition.ID == ConditionID.BRN || condition.ID == ConditionID.PSN || condition.ID == ConditionID.TOX )
            return 1.5f;

        return 1;
    }

}

public enum ConditionID
{
    //--None
    NONE, //-None

    //--Severe Statuses
    PSN, //--Poison. 1/8th max hp at the end of every round
    TOX, //--Toxic. Increasing damage at the end of every round a pokemon stays out. restarts on switch
    BRN, //--Burn. 1/16th max hp at the end of every round, cuts attack by 25% as part of the SpecialStatChange attribute
    PAR, //--Paralysis. 75% speed as part of the SpecialStatChange attribute
    SLP, //--Sleep. Guaranteed 2 turns of inactivity
    FBT, //--Frostbite. 1/16th max hp at the end of every round, cuts special attack by 25% as part of the SpecialStatChange attribute

    //--Faint
    FNT, //-You're fuckin dead bro

    //--Volatile Statuses. Give them their own icon. maybe with a counter on it to show amount of turns left afflicted?
    CONFUSION, //--Lasts for a preset 2-5 turns. 33% chance to inflict self damage for a set 1/16th max hp

    //--Weather
    SUNNY,
    RAIN,
    SANDSTORM,
    SNOW,
    SHADOWSKY,
}