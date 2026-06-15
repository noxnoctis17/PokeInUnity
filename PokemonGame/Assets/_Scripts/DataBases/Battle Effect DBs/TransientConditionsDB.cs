using System.Collections;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;

public class TransientConditionsDB : MonoBehaviour
{
    public static Dictionary<TransientConditionID, TransientCondition> Conditions { get; set; } 

    public static void Init()
    {
        SetDictionary();

        foreach( var kvp in Conditions )
        {
            var conditionID = kvp.Key;
            var condition = kvp.Value;

            condition.ID = conditionID;
        }
    }

    public static void Clear(){
        Conditions = null;
    }

    private static void SetDictionary(){
        Conditions = new Dictionary<TransientConditionID, TransientCondition>()
        {
            {
              TransientConditionID.Flinch, new()
              {
                Name = "Flinch",
                  
                OnStart = ( Pokemon pokemon ) =>
                {
                    Debug.Log( "Flinch OnStart" );
                    var commandQueue = BattleSystem.Instance.CommandQueue; 
                    foreach( var command in commandQueue )
                    {
                        if( command.User.Pokemon == pokemon && command is UseMoveCommand  ) //--Doesn't account for non-move commands. only move commands should get flinched. let's make it work first. --12/02/25 Checking if command IS UseMoveCommand --12/25/25
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

                        if( pokemon.AbilityID == AbilityID.Steadfast )
                        {
                            List<StatStage> speedBoost = new() { new() { Stat = Stat.Speed, Change = 1 } };
                            StageChangeSource source = new() { Pokemon = pokemon, Source = StageChangeSourceType.Ability };
                            pokemon.ApplyStatStageChange( speedBoost, source );
                        }

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
              TransientConditionID.Protect, new()
              {
                Name = "Protect",
                  
                OnStart = ( Pokemon pokemon ) =>
                {
                    Debug.Log( "Protect OnStart" );
                    pokemon.TransientStatusActive = true;
                },
              }
            },

            {
              TransientConditionID.Phazed, new()
              {
                Name = "Phased",
                  
                OnStart = ( Pokemon pokemon ) =>
                {
                    Debug.Log( "Phased OnStart" );
                    pokemon.TransientStatusActive = true;
                    Debug.Log( $"{pokemon.NickName} has been phased!" );
                },

                OnBeforeTurn = ( Pokemon pokemon ) =>
                {
                    Debug.Log( $"{pokemon.NickName}'s TransientStatus is: {pokemon.TransientStatusActive}" );

                    if( pokemon.TransientStatusActive )
                    {
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
                TransientConditionID.Endure, new()
                {
                    Name = "Endure",
                    StartMessage = "braced itself!",

                    OnStart = ( Pokemon pokemon ) =>
                    {
                        Debug.Log( "Endure OnStart" );
                        pokemon.TransientStatusActive = true;
                    },

                    OnTakeDamage = ( BattleUnit unit, int damage ) =>
                    {
                        if( unit.Pokemon.CurrentHP - damage <= 0 )
                        {
                            damage--;
                            return damage;
                        }
                        else
                            return damage;
                        
                    },

                    OnBeforeTurn = ( Pokemon pokemon ) => true,

                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        pokemon.AddStatusEvent( StatusEventType.Text, $"{pokemon.NickName} endured the hit!" );
                    },
                }
            },
            {
                TransientConditionID.CenterOfAttention, new()
                {
                    Name = "Center of Attention",
                    StartMessage = "became the center of attention!",

                    OnStart = ( Pokemon pokemon ) =>
                    {
                        BattleSystem.Instance.SetBattleFlag( BattleFlag.Redirect, true );
                    },

                    OnExit = ( Pokemon pokemon ) =>
                    {
                        BattleSystem.Instance.SetBattleFlag( BattleFlag.Redirect, false );
                    }
                }
            }
        };
    }
}

public enum TransientConditionID
{
    //--Transient Statuses. Occur only during the round they're applied.
    None,
    Flinch,
    Protect,
    Phazed,
    Endure,
    CenterOfAttention,
}
