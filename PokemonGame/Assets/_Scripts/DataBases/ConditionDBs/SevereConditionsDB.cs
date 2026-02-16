using System.Collections.Generic;
using UnityEngine;

public class SevereConditionsDB
{
    public static Dictionary<SevereConditionID, SevereCondition> Conditions { get; set; } 
    public static string StatusIconsPath;

    public static void Init()
    {
        LoadStatusIcons();
        SetDictionary();

        foreach( var kvp in Conditions )
        {
            var conditionID = kvp.Key;
            var condition = kvp.Value;

            condition.ID = conditionID;
        }
    }

    public static void Clear()
    {
        Conditions = null;
    }

    private static void LoadStatusIcons()
    {
        StatusIconsPath = "Assets/Resources/UI Graphics/SevereStatusIcons.png";
    }

    private static void SetDictionary()
    {
        Conditions = new Dictionary<SevereConditionID, SevereCondition>()
        {
//========================================================================================================================================
//========================================================[ SEVERE STATUS ]===============================================================
//========================================================================================================================================
            {   //--POISON
                SevereConditionID.PSN, new()
                {
                    Name = "Poison",
                    StartMessage = "was poisoned!",
                    StatusIcon = StatusIconAtlas.StatusIcons[SevereConditionID.PSN].Icon,
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    { 
                        pokemon.DecreaseHP( Mathf.FloorToInt( pokemon.MaxHP / 8 ) );
                        pokemon.AddStatusEvent( StatusEventType.SevereStatusDamage, $"{pokemon.NickName} is hurt by poison!" );
                    }}
            },
                    
            {   //--TOXIC
                SevereConditionID.TOX, new()
                {
                    Name = "Toxic",
                    StartMessage = "was badly poisoned!",
                    StatusIcon = StatusIconAtlas.StatusIcons[SevereConditionID.TOX].Icon,
                    OnApplyStatus = ( Pokemon pokemon ) =>
                    {
                        pokemon.SevereStatusTime = 1;
                    },

                    OnAfterTurn = ( Pokemon pokemon ) =>
                    { 
                        pokemon.DecreaseHP( Mathf.FloorToInt( pokemon.SevereStatusTime * ( pokemon.MaxHP / 16 ) ) );
                        pokemon.AddStatusEvent( StatusEventType.SevereStatusDamage, $"{pokemon.NickName} is hurt by its horrible poisoning!" );
                        pokemon.SevereStatusTime++;
                    },

                    OnEnter = ( Pokemon pokemon ) =>
                    {
                        pokemon.SevereStatusTime = 1;
                    },

                    OnExit = ( Pokemon pokemon ) =>
                    {
                        pokemon.SevereStatusTime = 0;
                    }
                }
            },

            {   //--BURN
                SevereConditionID.BRN, new()
                {
                    Name = "Burn",
                    StartMessage = "was burned!",
                    StatusIcon = StatusIconAtlas.StatusIcons[SevereConditionID.BRN].Icon,

                    //--Immediate necessary changes that don't return a bool
                    OnApplyStatus = ( Pokemon pokemon ) =>
                    {
                        if( pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] != AbilityID.Guts )
                        {
                            // Debug.Log( $"{pokemon.NickName}'s Attack Stat is: {pokemon.Attack}" );
                            // pokemon.ApplyDirectStatModifier( Stat.Attack, DirectModifierCause.BRN, 0.5f );
                            // Debug.Log( $"{pokemon.NickName}'s Attack Stat is: {pokemon.Attack}" );
                        }
                        else
                            Debug.Log( "Guts is preventing burn's attack drop!" );
                    },

                    //--Effects that run after a turn is completed.
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        pokemon.DecreaseHP( Mathf.FloorToInt( pokemon.MaxHP / 16 ) );
                        pokemon.AddStatusEvent( StatusEventType.SevereStatusDamage, $"{pokemon.NickName} is hurt by its burn!" );
                    }}
            },

            {   //--FROSTBITE
                SevereConditionID.FBT, new()
                {
                    Name = "Frostbite",
                    StartMessage = "has become frostbitten!",
                    StatusIcon = StatusIconAtlas.StatusIcons[SevereConditionID.FBT].Icon,

                    //--Immediate necessary changes that don't return a bool
                    OnApplyStatus = ( Pokemon pokemon ) =>
                    {
                        // Debug.Log( $"{pokemon.NickName}'s Sp.Atk Stat is: {pokemon.SpAttack}" );
                        // pokemon.ApplyDirectStatModifier( Stat.SpAttack, DirectModifierCause.FBT, 0.5f );
                        // Debug.Log( $"{pokemon.NickName}'s Sp.Atk Stat is: {pokemon.SpAttack}" );
                    },

                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        pokemon.DecreaseHP( Mathf.FloorToInt( pokemon.MaxHP / 16 ) );
                        pokemon.AddStatusEvent( StatusEventType.SevereStatusDamage, $"{pokemon.NickName} is hurt by its frostbite!" );
                    }}
            },

            {   //-PARAYLSIS
                SevereConditionID.PAR, new()
                {
                    Name = "Paralysis",
                    StartMessage = "has been paralyzed!",
                    StatusIcon = StatusIconAtlas.StatusIcons[SevereConditionID.PAR].Icon,

                    //--Immediate necessary changes that don't return a bool
                    OnApplyStatus = ( Pokemon pokemon ) =>
                    {
                        pokemon.SevereStatusTime = 1;
                        Debug.Log( $"{pokemon.NickName}'s Speed Stat is: {pokemon.Speed}" );
                        pokemon.ApplyDirectStatModifier( Stat.Speed, DirectModifierCause.PAR, 0.25f );
                        Debug.Log( $"{pokemon.NickName}'s Speed Stat is: {pokemon.Speed}" );
                    },

                    OnBeforeTurn = ( Pokemon pokemon ) =>
                    {
                        //--we're going to change paralysis to 1/4th speed the way it was originally
                        //--and instead, we're going to prevent only the turn it was paralyzed on from happening, removing the paralysis chance
                        //--but this idea was pulled from an idea Cybertron had voiced about players potentially wanting to see
                        //--for changes to paralysis in his buffs and nerfs video
                        //--sleep was already guaranteed 2 turns, which is something he also brought up, so i nailed that idea lol
                        //--freeze will be turned into special burn, aka frostbite from legends arceus
                        Debug.Log( $"{pokemon.PokeSO.Species}'s Paralysis Counter is: {pokemon.SevereStatusTime}" );
                        if( pokemon.SevereStatusTime == 0 )
                        {
                            pokemon.AddStatusEvent( $"{pokemon.NickName} can slowly move again!" );
                            return true;
                        }

                        pokemon.AddStatusEvent( StatusEventType.SevereStatusPassive, $"{pokemon.NickName} is Paralyzed! It can't move yet!" );
                        pokemon.SevereStatusTime--;
                        return false;
                    }}
            },

            {   //--SLEEP
                SevereConditionID.SLP, new()
                {
                    Name = "Sleep",
                    StartMessage = "has fallen asleep!",
                    StatusIcon = StatusIconAtlas.StatusIcons[SevereConditionID.SLP].Icon,
                    OnApplyStatus = ( Pokemon pokemon ) =>
                    {
                        //--Sleep is for 1-3 turns? i'm gunna make it a guaranteed 2 turns only
                        pokemon.SevereStatusTime = 2;
                        Debug.Log( $"{pokemon.PokeSO.Species}'s Sleep Counter is: {pokemon.SevereStatusTime}" );
                    },

                    OnBeforeTurn = ( Pokemon pokemon ) =>
                    {
                        Debug.Log( $"{pokemon.PokeSO.Species}'s Sleep Counter is: {pokemon.SevereStatusTime}" );
                        if( pokemon.SevereStatusTime == 0 )
                        {
                            pokemon.CureSevereStatus();
                            pokemon.AddStatusEvent( $"{pokemon.NickName} woke up!" );
                            return true;
                        }

                        pokemon.AddStatusEvent( $"{pokemon.NickName} is fast asleep!" );
                        pokemon.SevereStatusTime--;
                        return false;
                    }}
            },

            {   //--FAINT
                SevereConditionID.FNT, new()
                {
                    Name = "Faint",
                    StatusIcon = StatusIconAtlas.StatusIcons[SevereConditionID.FNT].Icon,
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        pokemon.CurrentHP = 0;
                    }
                }
            },
        };
    }

    //--Status bonus that gets added when trying to catch a pokemon. buffed sleep from 2 to 2.5, buffed para from 1.5 to 2
    public static float GetStatusBonus( SevereCondition condition )
    {
        if( condition == null )
            return 1f;
        else if( condition.ID == SevereConditionID.SLP )
            return 2.5f;
        else if( condition.ID == SevereConditionID.PAR )
            return 2f;
        else if( condition.ID == SevereConditionID.FBT || condition.ID == SevereConditionID.BRN || condition.ID == SevereConditionID.PSN || condition.ID == SevereConditionID.TOX )
            return 1.5f;

        return 1;
    }

}

public enum SevereConditionID
{
    //--None
    None, //-None

    //--Severe Statuses
    PSN, //--Poison. 1/8th max hp at the end of every round
    TOX, //--Toxic. Increasing damage at the end of every round a pokemon stays out. restarts on switch
    BRN, //--Burn. 1/16th max hp at the end of every round, cuts attack by 25% as part of the SpecialStatChange attribute
    FBT, //--Frostbite. 1/16th max hp at the end of every round, cuts special attack by 25% as part of the SpecialStatChange attribute
    PAR, //--Paralysis. cuts speed by 75% as part of the SpecialStatChange attribute, only first turn full para
    SLP, //--Sleep. Guaranteed 2 turns of inactivity

    //--Faint
    FNT, //-You're fuckin dead bro
}