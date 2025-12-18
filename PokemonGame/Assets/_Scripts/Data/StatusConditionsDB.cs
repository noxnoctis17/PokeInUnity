using System.Collections.Generic;
using UnityEngine;

public class StatusConditionsDB
{
    public static Dictionary<StatusConditionID, StatusCondition> Conditions { get; set; } 
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
        Conditions = new Dictionary<StatusConditionID, StatusCondition>()
        {
//========================================================================================================================================
//========================================================[ SEVERE STATUS ]===============================================================
//========================================================================================================================================
            {   //--POISON
                StatusConditionID.PSN, new StatusCondition()
                {
                    Name = "Poison",
                    StartMessage = "was poisoned!",
                    StatusIcon = StatusIconAtlas.StatusIcons[StatusConditionID.PSN].Icon,
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    { 
                        pokemon.DecreaseHP( Mathf.FloorToInt( pokemon.MaxHP / 8 ) );
                        pokemon.AddStatusEvent( StatusEventType.SevereStatusDamage, $"{pokemon.NickName} is hurt by poison!" );
                    }}
            },
                    
            {   //--TOXIC
                StatusConditionID.TOX, new StatusCondition()
                {
                    Name = "Toxic",
                    StartMessage = "was severely poisoned!",
                    StatusIcon = StatusIconAtlas.StatusIcons[StatusConditionID.TOX].Icon,
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    { 
                        pokemon.DecreaseHP( Mathf.FloorToInt( pokemon.MaxHP / 8 ) );
                        pokemon.AddStatusEvent( StatusEventType.SevereStatusDamage, $"{pokemon.NickName} is hurt by its horrible poisoning!" );
                    }}
            },

            {   //--BURN
                StatusConditionID.BRN, new StatusCondition()
                {
                    Name = "Burn",
                    StartMessage = "was burned!",
                    StatusIcon = StatusIconAtlas.StatusIcons[StatusConditionID.BRN].Icon,

                    //--Immediate necessary changes that don't return a bool
                    OnApplyStatus = ( Pokemon pokemon ) =>
                    {
                        if( pokemon.PokeSO.Abilities[pokemon.CurrentAbilityIndex] != AbilityID.Guts )
                        {
                            Debug.Log( $"{pokemon.NickName}'s Attack Stat is: {pokemon.Attack}" );
                            pokemon.ApplyDirectStatModifier( Stat.Attack, DirectModifierCause.BRN, 0.5f );
                            Debug.Log( $"{pokemon.NickName}'s Attack Stat is: {pokemon.Attack}" );
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
                StatusConditionID.FBT, new StatusCondition()
                {
                    Name = "Frostbite",
                    StartMessage = "has become frostbitten!",
                    StatusIcon = StatusIconAtlas.StatusIcons[StatusConditionID.FBT].Icon,

                    //--Immediate necessary changes that don't return a bool
                    OnApplyStatus = ( Pokemon pokemon ) =>
                    {
                        Debug.Log( $"{pokemon.NickName}'s Sp.Atk Stat is: {pokemon.SpAttack}" );
                        pokemon.ApplyDirectStatModifier( Stat.SpAttack, DirectModifierCause.FBT, 0.5f );
                        Debug.Log( $"{pokemon.NickName}'s Sp.Atk Stat is: {pokemon.SpAttack}" );
                    },

                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        pokemon.DecreaseHP( Mathf.FloorToInt( pokemon.MaxHP / 16 ) );
                        pokemon.AddStatusEvent( StatusEventType.Damage, $"{pokemon.NickName} is hurt by its frostbite!" );
                    }}
            },

            {   //-PARAYLSIS
                StatusConditionID.PAR, new StatusCondition()
                {
                    Name = "Paralysis",
                    StartMessage = "has been paralyzed!",
                    StatusIcon = StatusIconAtlas.StatusIcons[StatusConditionID.PAR].Icon,

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
                StatusConditionID.SLP, new StatusCondition()
                {
                    Name = "Sleep",
                    StartMessage = "has fallen asleep!",
                    StatusIcon = StatusIconAtlas.StatusIcons[StatusConditionID.SLP].Icon,
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
                StatusConditionID.FNT, new StatusCondition()
                {
                    Name = "Faint",
                    StatusIcon = StatusIconAtlas.StatusIcons[StatusConditionID.FNT].Icon,
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
                StatusConditionID.CONFUSION, new StatusCondition()
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
                            pokemon.AddStatusEvent( $"{pokemon.NickName} snapped out of confusion!" );
                            return true;
                        }

                        pokemon.VolatileStatusTime--;

                        //--33% Chance to Hurt Itself
                        if( Random.Range( 1,4 ) == 1 )
                        {
                            pokemon.AddStatusEvent( $"{pokemon.NickName} is confused!" );
                            pokemon.DecreaseHP( Mathf.FloorToInt( pokemon.MaxHP / 16 ) );
                            pokemon.AddStatusEvent( StatusEventType.Damage, $"{pokemon.NickName} hurt itself in confusion!" );
                            return false;
                        }

                        //--Perform Move
                        return true;
                    }}
            },

            //========================================================================================================================================
            //=======================================================[ TRANSIENT STATUS ]=============================================================
            //========================================================================================================================================

            {
              StatusConditionID.Flinch, new()
              {
                Name = "Flinch",
                  
                OnStart = ( Pokemon pokemon ) =>
                {
                    Debug.Log( "Flinch OnStart" );
                    var commandQueue = BattleSystem.Instance.CommandQueue; 
                    foreach( var command in commandQueue )
                    {
                        if( command.User.Pokemon == pokemon ) //--Doesn't account for non-move commands. only move commands should get flinched. let's make it work first. --12/02/25
                        {
                            pokemon.TransientStatusActive = true;
                            Debug.Log( $"{pokemon.NickName} is going to be Flinched!" );
                        }
                    }
                },

                OnBeforeTurn = ( Pokemon pokemon ) =>
                {
                    Debug.Log( $"{pokemon.NickName}'s TransientStatus is: {pokemon.TransientStatusActive}" );

                    if( pokemon.TransientStatusActive )
                    {
                        pokemon.AddStatusEvent( $"{pokemon.NickName} flinched!" );
                        pokemon.CureTransientStatus();
                        return false;
                    }
                    else
                    {
                        pokemon.CureTransientStatus();
                        return true;
                    }
                }
              }
            },

            {
              StatusConditionID.Protect, new()
              {
                Name = "Protect",
                  
                OnStart = ( Pokemon pokemon ) =>
                {
                    Debug.Log( "Protect OnStart" );
                    pokemon.TransientStatusActive = true;
                },
              }
            },

        };
    }

    //--Status bonus that gets added when trying to catch a pokemon. buffed sleep from 2 to 2.5, buffed para from 1.5 to 2
    public static float GetStatusBonus( StatusCondition condition ){
        if( condition == null )
            return 1f;
        else if( condition.ID == StatusConditionID.SLP )
            return 2.5f;
        else if( condition.ID == StatusConditionID.PAR )
            return 2f;
        else if( condition.ID == StatusConditionID.FBT || condition.ID == StatusConditionID.BRN || condition.ID == StatusConditionID.PSN || condition.ID == StatusConditionID.TOX )
            return 1.5f;

        return 1;
    }

}

public enum StatusConditionID
{
    //--None
    NONE, //-None

    //--Severe Statuses
    PSN, //--Poison. 1/8th max hp at the end of every round
    TOX, //--Toxic. Increasing damage at the end of every round a pokemon stays out. restarts on switch
    BRN, //--Burn. 1/16th max hp at the end of every round, cuts attack by 25% as part of the SpecialStatChange attribute
    FBT, //--Frostbite. 1/16th max hp at the end of every round, cuts special attack by 25% as part of the SpecialStatChange attribute
    PAR, //--Paralysis. cuts speed by 75% as part of the SpecialStatChange attribute, only first turn full para
    SLP, //--Sleep. Guaranteed 2 turns of inactivity

    //--Faint
    FNT, //-You're fuckin dead bro

    //--Volatile Statuses. Give them their own icon. maybe with a counter on it to show amount of turns left afflicted?
    CONFUSION, //--Lasts for a preset 2-5 turns. 33% chance to inflict self damage for a set 1/16th max hp

    //--Transient Statuses. Occur only during the round they happen
    Flinch,
    Protect,

}