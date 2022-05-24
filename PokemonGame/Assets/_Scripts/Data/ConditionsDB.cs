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
                OnAfterTurn = (PokemonClass pokemon) =>
                { 
                    pokemon.UpdateHP(pokemon.MaxHP / 8);
                }}},
        {   //--TOXIC
            ConditionID.TOX, new ConditionClass()
            {
                ConditionName = "Toxic",
                OnAfterTurn = (PokemonClass pokemon) =>
                { 
                    pokemon.UpdateHP(pokemon.MaxHP / 8);
                }}},

        {   //--BURN
            ConditionID.BRN, new ConditionClass()
            {
                ConditionName = "Burn",
                OnAfterTurn = (PokemonClass pokemon) =>
                {
                    pokemon.UpdateHP(pokemon.MaxHP / 16);
                }}},

        {   //-PARAYLSIS
            ConditionID.PAR, new ConditionClass()
            {
                ConditionName = "Paralysis",
                OnBeforeTurn = (PokemonClass pokemon) =>
                {
                    if(Random.Range(1, 5) == 1)
                    {
                        return false;
                    }

                    return true;
                }}},

        {   //--SLEEP
            ConditionID.SLP, new ConditionClass()
            {
                ConditionName = "Sleep",
                OnRoundStart = (PokemonClass pokemon) =>
                {
                    //--Sleep is for 1-3 turns? i'm gunna make it a guaranteed 2 turns only
                    pokemon.SevereStatusTime = 2;
                },

                OnBeforeTurn = (PokemonClass pokemon) =>
                {
                    if(pokemon.SevereStatusTime ==0)
                    {
                        pokemon.CureSevereStatus();
                        Debug.Log($"{pokemon.PokeSO.pName} woke up!");
                        return true;
                    }

                    Debug.Log($"{pokemon.PokeSO.pName} is fast asleep!");
                    pokemon.SevereStatusTime--;
                    return false;
                }}},

        {   //--FREEZE
            ConditionID.FRZ, new ConditionClass()
            {
                ConditionName = "Freeze",
                OnBeforeTurn = (PokemonClass pokemon) =>
                {
                    if(Random.Range(1, 5) == 1)
                    {
                        pokemon.CureSevereStatus();
                        Debug.Log($"{pokemon.PokeSO.pName} is no longer frozen!");
                        return true;
                    }

                    Debug.Log($"{pokemon.PokeSO.pName} is frozen solid!");
                    return false;
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
                OnRoundStart = (PokemonClass pokemon) =>
                {
                    //--Confuse for 2-5 turns
                    pokemon.VolatileStatusTime = Random.Range(2, 6);
                },

                OnBeforeTurn = (PokemonClass pokemon) =>
                {
                    if(pokemon.VolatileStatusTime ==0)
                    {
                        pokemon.CureVolatileStatus();
                        Debug.Log($"{pokemon.PokeSO.pName} snapped out of confusion!");
                        return true;
                    }

                    pokemon.VolatileStatusTime--;

                    //--33% Chance to Hurt Itself
                    if(Random.Range(1,4) == 1)
                    {
                        Debug.Log($"{pokemon.PokeSO.pName} is confused!");
                        pokemon.UpdateHP(pokemon.MaxHP / 8);
                        Debug.Log($"{pokemon.PokeSO.pName} hurt itself in confusion!");
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
    FRZ,
    FNT,

    CONFUSION,
}