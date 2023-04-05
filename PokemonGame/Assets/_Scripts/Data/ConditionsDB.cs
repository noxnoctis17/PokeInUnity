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
                }}},
        {   //--TOXIC
            ConditionID.TOX, new ConditionClass()
            {
                ConditionName = "Toxic",
                OnAfterTurn = ( PokemonClass pokemon ) =>
                { 
                    pokemon.UpdateHP( pokemon.MaxHP / 8 );
                }}},

        {   //--BURN
            ConditionID.BRN, new ConditionClass()
            {
                ConditionName = "Burn",
                OnAfterTurn = ( PokemonClass pokemon ) =>
                {
                    pokemon.UpdateHP( pokemon.MaxHP / 16 );
                    Debug.Log( $"{pokemon.PokeSO.pName} is hurt by its burn!" );
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
                        //--we're going to change paralysis to 1/3rd speed (up from 1/2, but not as severe as its old 1/4)
                        //--and instead, we're going to prevent only the turn it was paralyzed on from happening, removing the paralysis chance
                        //--perhaps 1/4 speed will be the best compensation for the lack of turn-chance paralysis
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
                        Debug.Log( $"{pokemon.PokeSO.pName} woke up!" );
                        return true;
                    }

                    Debug.Log( $"{pokemon.PokeSO.pName} is fast asleep!" );
                    pokemon.SevereStatusTime--;
                    return false;
                }}},

        {   //--FROSTBITE
            ConditionID.FRST, new ConditionClass()
            {
                ConditionName = "Frostbite",
                OnAfterTurn = ( PokemonClass pokemon ) =>
                {
                    pokemon.UpdateHP( pokemon.MaxHP / 16 );
                    Debug.Log( $"{pokemon.PokeSO.pName} is hurt by its frostbite!" );
                }}},

        {   //--FAINT
            ConditionID.FNT, new ConditionClass()
            {
                ConditionName = "Faint",
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
}

public enum ConditionID
{
    NONE,
    PSN,
    TOX,
    BRN,
    PAR,
    SLP,
    FRST,
    FNT,

    CONFUSION,
}