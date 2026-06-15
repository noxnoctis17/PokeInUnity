using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FieldConditionDB
{
    public static Dictionary<FieldConditionID, FieldCondition> Conditions { get; private set; }

    public static void Init()
    {
        foreach( var kvp in Conditions )
        {
            var conditionID = kvp.Key;
            var condition = kvp.Value;

            condition.ID = conditionID;
        }
    }

    public static void Clear()
    {
        Conditions.Clear();
    }

    public static void SetDictionary()
    {
        Conditions = new()
        {
            {
                FieldConditionID.TrickRoom, new( 5, 0 ) //--Duration + modifier get set in constructor. OnStart sets TimeLeft via public function. TimeLeft is what is actually ticked down.
                {
                    StartMessage = ( BattleSystem bs, Pokemon pokemon ) =>
                    {
                        var bf = bs.Field;

                        if( bf.FieldConditions.ContainsKey( FieldConditionID.TrickRoom ) )
                            return $"{pokemon.NickName} has returned the dimensions of speed back to normal!";
                        else
                            return $"{pokemon.NickName} has twisted the dimensions of speed!";
                    },

                    EndMessage = "The dimensions of speed have returned to normal.",

                    OnStart = ( bs, field, user ) =>
                    {
                        Debug.Log( "Trick Room OnStart" );
                        if( bs.BattleFlags[BattleFlag.TrickRoom] )
                        {
                            Debug.Log( "Trick Room is already up! Reversing Trick Room!" );
                            field.FieldConditions[FieldConditionID.TrickRoom].OnEnd?.Invoke( bs, field, user );
                        }
                        else
                        {
                            Debug.Log( "Trick Room Begins Baybeeeeeeeeeeee" );
                            var tr = field.FieldConditions[FieldConditionID.TrickRoom];

                            //--Set/Reset TimeLeft OnStart. For conditions with modifiers, we'll have to probably add the unit using the move and check for their held item or ability or w/e for modifier influence
                            tr.TimeLeft = tr.Duration;
                            Debug.Log( $"Trick Room's duration is: {tr.Duration}" );
                            bs.SetBattleFlag( BattleFlag.TrickRoom, true );
                        }
                    },

                    OnEnd = ( bs, field, user ) =>
                    {
                        Debug.Log( "Trick Room OnEnd" );
                        bs.SetBattleFlag( BattleFlag.TrickRoom, false );
                    }
                }  
            },
        };
    }
}

public enum FieldConditionID
{
    None,
    TrickRoom,
    MagicRoom,
    WonderRoom,
    PsychicField
}
