using System.Collections.Generic;
using UnityEngine;

public class ConditionsDB
{
    public static Dictionary<ConditionID, Condition> Conditions { get; set; } 

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
        Conditions = new Dictionary<ConditionID, Condition>()
        {
            {   //--POISON
                ConditionID.PSN, new Condition()
                {
                    ConditionName = "Poison",
                    AfflictionDialogue = "was poisoned!",
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    { 
                        pokemon.UpdateHP( pokemon.MaxHP / 8 );
                        pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.pName} is hurt by poison!" );
                    }}},
                    
            {   //--TOXIC
                ConditionID.TOX, new Condition()
                {
                    ConditionName = "Toxic",
                    AfflictionDialogue = "was inflicted with toxic poison!",
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    { 
                        pokemon.UpdateHP( pokemon.MaxHP / 8 );
                        pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.pName} is hurt by its horrible poisoning!" );
                    }}},

            {   //--BURN
                ConditionID.BRN, new Condition()
                {
                    ConditionName = "Burn",
                    AfflictionDialogue = "was burned!",
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        // Debug.Log( pokemon.CurrentHP );
                        pokemon.UpdateHP( pokemon.MaxHP / 16 );
                        pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.pName} is hurt by its burn!" );
                        // Debug.Log( pokemon.CurrentHP );
                    }}},

            {   //-PARAYLSIS
                ConditionID.PAR, new Condition()
                {
                    ConditionName = "Paralysis",
                    AfflictionDialogue = "has been paralyzed!",
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
                    }}},

            {   //--SLEEP
                ConditionID.SLP, new Condition()
                {
                    ConditionName = "Sleep",
                    AfflictionDialogue = "has fallen asleep!",
                    OnRoundStart = ( Pokemon pokemon ) =>
                    {
                        //--Sleep is for 1-3 turns? i'm gunna make it a guaranteed 2 turns only
                        pokemon.SevereStatusTime = 2;
                    },

                    OnBeforeTurn = ( Pokemon pokemon ) =>
                    {
                        if( pokemon.SevereStatusTime == 0 )
                        {
                            pokemon.CureSevereStatus();
                            pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.pName} woke up!" );
                            return true;
                        }

                        pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.pName} is fast asleep!" );
                        pokemon.SevereStatusTime--;
                        return false;
                    }}},

            {   //--FROSTBITE
                ConditionID.FBT, new Condition()
                {
                    ConditionName = "Frostbite",
                    AfflictionDialogue = "has become frostbitten!",
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        pokemon.UpdateHP( pokemon.MaxHP / 16 );
                        pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.pName} is hurt by its frostbite!" );
                    }}},

            {   //--FAINT
                ConditionID.FNT, new Condition()
                {
                    ConditionName = "Faint",
                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        pokemon.CurrentHP = 0;
                    }
                }},

            {   //--CONFUSION
                ConditionID.CONFUSION, new Condition()
                {
                    ConditionName = "Confusion",
                    AfflictionDialogue = "became confused!",
                    OnRoundStart = ( Pokemon pokemon ) =>
                    {
                        //--Confuse for 2-5 turns
                        pokemon.VolatileStatusTime = Random.Range( 2, 6 );
                    },

                    OnBeforeTurn = ( Pokemon pokemon ) =>
                    {
                        if( pokemon.VolatileStatusTime == 0 )
                        {
                            pokemon.CureVolatileStatus();
                            pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.pName} snapped out of confusion!" );
                            return true;
                        }

                        pokemon.VolatileStatusTime--;

                        //--33% Chance to Hurt Itself
                        if( Random.Range( 1,4 ) == 1 )
                        {
                            pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.pName} is confused!" );
                            pokemon.UpdateHP( pokemon.MaxHP / 16 );
                            pokemon.StatusChanges.Enqueue( $"{pokemon.PokeSO.pName} hurt itself in confusion!" );
                            return false;
                        }

                        //--Perform Move
                        return true;
                    }}},

        };
    }

    public static float GetStatusBonus( Condition condition ){
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
    BRN, //--Burn. 1/16th max hp at the end of every round, cuts attack by 25% as part of the SpecialStatChange attribute
    PAR, //--Paralysis. 75% speed as part of the SpecialStatChange attribute
    SLP, //--Sleep. Guaranteed 2 turns of inactivity
    FBT, //--Frostbite. 1/16th max hp at the end of every round, cuts special attack by 25% as part of the SpecialStatChange attribute

    //--Faint
    FNT, //-You're fuckin dead bro

    //--Volatile Statuses. Give them their own icon. maybe with a counter on it to show amount of turns left afflicted?
    CONFUSION, //--Lasts for a preset 2-5 turns. 33% chance to inflict self damage for a set 1/16th max hp
}