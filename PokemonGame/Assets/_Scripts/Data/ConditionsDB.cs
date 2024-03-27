using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ConditionsDB
{
    public static void Init()
    {
        foreach(var kvp in Conditions)
        {
            var conditionID = kvp.Key;
            var condition = kvp.Value;

            condition.ID = conditionID;
        }
    }

    public static Dictionary<ConditionID, ConditionClass> Conditions { get; set; } = new Dictionary<ConditionID, ConditionClass>()
    {
        {   //--POISON
            ConditionID.PSN, new ConditionClass()
            {
                ConditionName = "Poison",
                OnAfterTurn = ( PokemonClass pokemon ) =>
                { 
                    pokemon.UpdateHP( pokemon.MaxHP / 8 );
                    BattleSystem.Instance.AfterTurnDialogue( $"{pokemon.PokeSO.pName} is hurt by its poisoning!" );
                }}},
        {   //--TOXIC
            ConditionID.TOX, new ConditionClass()
            {
                ConditionName = "Toxic",
                OnAfterTurn = ( PokemonClass pokemon ) =>
                { 
                    pokemon.UpdateHP( pokemon.MaxHP / 8 );
                    BattleSystem.Instance.AfterTurnDialogue( $"{pokemon.PokeSO.pName} is hurt by its horrible poisoning!" );
                }}},

        {   //--BURN
            ConditionID.BRN, new ConditionClass()
            {
                ConditionName = "Burn",
                OnAfterTurn = ( PokemonClass pokemon ) =>
                {
                    Debug.Log( pokemon.CurrentHP );
                    pokemon.UpdateHP( pokemon.MaxHP / 16 );
                    BattleSystem.Instance.AfterTurnDialogue( $"{pokemon.PokeSO.pName} is hurt by its burn!" );
                    Debug.Log( pokemon.CurrentHP );
                }}},

        {   //-PARAYLSIS
            ConditionID.PAR, new ConditionClass()
            {
                ConditionName = "Paralysis",
                OnBeforeTurn = ( PokemonClass pokemon ) =>
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
                }}},

        {   //--SLEEP
            ConditionID.SLP, new ConditionClass()
            {
                ConditionName = "Sleep",
                OnRoundStart = ( PokemonClass pokemon ) =>
                {
                    //--Sleep is for 1-3 turns? i'm gunna make it a guaranteed 2 turns only
                    pokemon.SevereStatusTime = 2;
                },

                OnBeforeTurn = ( PokemonClass pokemon ) =>
                {
                    if( pokemon.SevereStatusTime == 0 )
                    {
                        pokemon.CureSevereStatus();
                        BattleSystem.Instance.AfterTurnDialogue( $"{pokemon.PokeSO.pName} woke up!" );
                        return true;
                    }

                    BattleSystem.Instance.AfterTurnDialogue( $"{pokemon.PokeSO.pName} is fast asleep!" );
                    pokemon.SevereStatusTime--;
                    return false;
                }}},

        {   //--FROSTBITE
            ConditionID.FBT, new ConditionClass()
            {
                ConditionName = "Frostbite",
                OnAfterTurn = ( PokemonClass pokemon ) =>
                {
                    pokemon.UpdateHP( pokemon.MaxHP / 16 );
                    BattleSystem.Instance.AfterTurnDialogue( $"{pokemon.PokeSO.pName} is hurt by its frostbite!" );
                }}},

        {   //--FAINT
            ConditionID.FNT, new ConditionClass()
            {
                ConditionName = "Faint",
                OnAfterTurn = ( PokemonClass pokemon ) =>
                {
                    pokemon.CurrentHP = 0;
                }
            }},

        {   //--CONFUSION
            ConditionID.CONFUSION, new ConditionClass()
            {
                ConditionName = "Confusion",
                OnRoundStart = ( PokemonClass pokemon ) =>
                {
                    //--Confuse for 2-5 turns
                    pokemon.VolatileStatusTime = Random.Range( 2, 6 );
                },

                OnBeforeTurn = ( PokemonClass pokemon ) =>
                {
                    if( pokemon.VolatileStatusTime == 0 )
                    {
                        pokemon.CureVolatileStatus();
                        Debug.Log( $"{pokemon.PokeSO.pName} snapped out of confusion!" );
                        return true;
                    }

                    pokemon.VolatileStatusTime--;

                    //--33% Chance to Hurt Itself
                    if( Random.Range( 1,4 ) == 1 )
                    {
                        Debug.Log( $"{pokemon.PokeSO.pName} is confused!" );
                        pokemon.UpdateHP( pokemon.MaxHP / 16 );
                        Debug.Log( $"{pokemon.PokeSO.pName} hurt itself in confusion!" );
                        return false;
                    }

                    //--Perform Move
                    return true;
                }}},

    };

    public static float GetStatusBonus( ConditionClass condition ){
        if( condition == null )
            return 1f;
        else if( condition.ID == ConditionID.SLP )
            return 2.5f;
        else if( condition.ID == ConditionID.PAR )
            return 2f;
        else if( condition.ID == ConditionID.FBT || condition.ID == ConditionID.BRN || condition.ID == ConditionID.PSN )
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
    BRN, //--Burn. 1/16th max hp at the end of every round, lowers attack by 25% as part of the SpecialStatChange attribute
    PAR, //--Paralysis. 75% speed as part of the SpecialStatChange attribute
    SLP, //--Sleep. Guaranteed 2 turns of inactivity
    FBT, //--Frostbite. 1/16th max hp at the end of every round, lowers attack by 25% as part of the SpecialStatChange attribute

    //--Faint
    FNT, //-You're fuckin dead bro

    //--Volatile Statuses. Give them their own icon. maybe with a counter on it to show amount of turns left afflicted?
    CONFUSION, //--Lasts for a preset 2-5 turns. 33% chance to inflict self damage for a set 1/16th max hp
}