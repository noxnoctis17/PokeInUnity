using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class VolatileConditionsDB : MonoBehaviour
{
    public static Dictionary<VolatileConditionID, VolatileCondition> Conditions { get; set; } 

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

    public static void Clear()
    {
        Conditions = null;
    }

    private static void SetDictionary()
    {
        Conditions = new Dictionary<VolatileConditionID, VolatileCondition>()
        {
            {   //--CONFUSION
                VolatileConditionID.Confusion, new VolatileCondition()
                {
                    Name = "Confusion",
                    StartMessage = "became confused!",
                    Duration = Random.Range( 2, 6 ),
                    Passable = true,

                    OnStart = ( Pokemon pokemon ) =>
                    {
                        Debug.Log( $"[Volatile Status] {pokemon.NickName} has become confused!" );
                        //--Confuse for 2-5 turns
                        pokemon.SetVolatileStatusTime( VolatileConditionID.Confusion, Random.Range( 2, 6 ) );
                    },

                    OnBeforeTurn = ( Pokemon pokemon ) =>
                    {
                        var confusion = pokemon.VolatileStatuses[VolatileConditionID.Confusion];
                        
                        if( confusion.Duration == 0 )
                        {
                            pokemon.CureVolatileStatus( VolatileConditionID.Confusion );
                            pokemon.AddStatusEvent( $"{pokemon.NickName} snapped out of confusion!" );

                            return true;
                        }
                        
                        pokemon.AddStatusEvent( $"{pokemon.NickName} is confused!" );
                        confusion.Duration--;
                        pokemon.VolatileStatuses[VolatileConditionID.Confusion] = confusion;

                        //--33% Chance to Hurt Itself
                        if( Random.Range( 1, 4 ) == 1 )
                        {
                            pokemon.DecreaseHP( Mathf.FloorToInt( pokemon.MaxHP / 16 ) );
                            pokemon.AddStatusEvent( StatusEventType.Damage, $"{pokemon.NickName} hurt itself in confusion!" );
                            return false;
                        }

                        //--Perform Move
                        return true;
                    }}
            },
            {
                VolatileConditionID.AquaRing, new()
                {
                    Name = "Aqua Ring",
                    StartMessage = "surrounded itself with a veil of water!",
                    Passable = true,
                    OnBeforeTurn = ( Pokemon pokemon ) => true,

                    OnRoundEndPhase = ( BattleUnit unit ) =>
                    {
                        Debug.Log( $"[Volatile Status] {unit.Pokemon.NickName} is being healed by Aqua Ring!" );
                        int heal = Mathf.FloorToInt( unit.Pokemon.MaxHP / 16 );
                        unit.Pokemon.IncreaseHP( heal );
                        unit.Pokemon.AddStatusEvent( StatusEventType.Heal, $"{unit.Pokemon.NickName}'s hp was restored by Aqua Ring!" );
                    },
                }
            },
            {
                VolatileConditionID.HelpingHand, new()
                {
                    Name = "Helping Hand",
                    StartMessage = "is ready to lend a hand!",
                    OnBeforeTurn = ( Pokemon pokemon ) => true,
                }
            },
            {
                VolatileConditionID.Infatuation, new()
                {
                    Name = "Infatuation",
                    StartMessage = "fell in love!",

                    OnApplyStatus = ( BattleUnit attacker, BattleUnit target, BattleSystem bs) =>
                    {
                        if( target.InfatuationTarget != null )
                            return;

                        target.SetInfatuationTarget( attacker.Pokemon );
                    },

                    OnStart = ( Pokemon pokemon ) =>
                    {
                        //--Infatuation doesn't even have a turn limit...
                        //--It depends entirely on the subject of infatuation being in battle.
                        //--if it is swapped or faints, infatuation ends.
                    },

                    OnBeforeTurn = ( Pokemon pokemon ) =>
                    {
                        var unit = BattleSystem.Instance.GetPokemonBattleUnit( pokemon );
                        var opps = BattleSystem.Instance.GetOpposingUnits( unit );
                        Pokemon target = null;

                        for( int i = 0; i < opps.Count; i++ )
                        {
                            if( opps[i].Pokemon == unit.InfatuationTarget )
                            {
                                target = opps[i].Pokemon;
                            }
                            else
                                continue;
                        }

                        if( target == null )
                        {
                            pokemon.CureVolatileStatus( VolatileConditionID.Infatuation );
                            pokemon.AddStatusEvent( StatusEventType.Text, $"{pokemon.NickName} is no longer in love!" );
                            return true;
                        }

                        pokemon.AddStatusEvent( StatusEventType.Text, $"{pokemon.NickName} is in love with {target.NickName}!" );

                        //--33% chance to not attack
                        if( Random.Range( 1, 4 ) == 1 )
                        {
                            pokemon.AddStatusEvent( StatusEventType.Text, $"It's too busy fantasizing about them to attack!" );
                            return false;
                        }

                        return true;
                    },
                }
            },
            {
                VolatileConditionID.Rampage, new()
                {
                    Name = "Rampage",
                    StartMessage = "is on a rampage!",

                    OnStart = ( Pokemon pokemon ) =>
                    {
                        // increment status counter
                    },

                    OnBeforeTurn = ( Pokemon pokemon ) => true, //--This is where rampage actually lives i think

                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        // decrement status counter
                    },
                }
            },
            {
                VolatileConditionID.Substitute, new()
                {
                    Name = "Substitute",
                    StartMessage = "created a substitute!",
                    Passable = true,
                    OnBeforeTurn = ( Pokemon pokemon ) => true,

                    OnApplyStatus = ( BattleUnit attacker, BattleUnit target, BattleSystem bs ) =>
                    {
                        StatusEffectSource source = new()
                        {
                            Pokemon = attacker.Pokemon,
                            Source = EffectSource.Move,
                        };

                        attacker.SetSubstitute();
                        attacker.Pokemon.SetVolatileStatus( VolatileConditionID.Substitute, source );
                    },

                    
                }
            },
            {
                VolatileConditionID.Cursed, new()
                {
                    Name = "Cursed",
                    StartMessage = "has been cursed!",
                    Passable = true,
                    OnBeforeTurn = ( Pokemon pokemon ) => true,

                    OnRoundEndPhase = ( BattleUnit unit ) =>
                    {
                        int damage = unit.Pokemon.MaxHP / 4;
                        unit.Pokemon.DecreaseHP( damage );
                        unit.Pokemon.AddStatusEvent( StatusEventType.Damage, $"{unit.Pokemon.NickName} is hurt by its curse!" );
                    }
                }
            },
            {
                VolatileConditionID.Taunt, new()
                {
                    Name = "Taunt",
                    StartMessage = "has been taunted!",
                    OnBeforeTurn = ( Pokemon pokemon ) => true, //--too bad so sad

                    OnStart = ( Pokemon pokemon ) =>
                    {
                        Debug.Log( $"[Volatile Status] {pokemon.NickName} became taunted!" );
                        pokemon.SetVolatileStatusTime( VolatileConditionID.Taunt, 4 );
                    },

                    OnAfterTurn = ( Pokemon pokemon ) =>
                    {
                        if( pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.Taunt ) )
                        {
                            var taunt = pokemon.VolatileStatuses[VolatileConditionID.Taunt];

                            taunt.Duration--;
                            pokemon.VolatileStatuses[VolatileConditionID.Taunt] = taunt;

                            if( pokemon.VolatileStatuses[VolatileConditionID.Taunt].Duration == 0 )
                                pokemon.CureVolatileStatus( VolatileConditionID.Taunt );
                        }
                    }
                }
            },
            {
                VolatileConditionID.Yawn, new()
                {
                    Name = "Drowsy",
                    StartMessage = "is getting sleepy!",

                    OnStart = ( pokemon ) =>
                    {
                        pokemon.SetVolatileStatusTime( VolatileConditionID.Yawn, 0 );
                    },

                    OnAfterTurn = ( pokemon ) =>
                    {
                        if( pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.Yawn ) )
                        {
                            var yawn = pokemon.VolatileStatuses[VolatileConditionID.Yawn];
                            if( yawn.Duration == 0 )
                            {
                                yawn.Duration++;
                                return;
                            }
                            else
                            {
                                StatusEffectSource source = new()
                                {
                                    Pokemon = pokemon,
                                    Source = EffectSource.Drowsy,
                                };

                                pokemon.CureVolatileStatus( VolatileConditionID.Yawn );
                                pokemon.SetSevereStatus( SevereConditionID.SLP, source );
                            }
                        }
                    }
                }
            },
            {
                VolatileConditionID.Encore, new()
                {
                    Name = "Encore",
                    StartMessage = "must do an encore!",

                    OnStart = ( pokemon ) =>
                    {
                        pokemon.SetVolatileStatusTime( VolatileConditionID.Encore, 3 );
                    },

                    OnBeforeMoveUsed = ( unit ) =>
                    {
                        if( unit.LastUsedMove != null )
                            return unit.LastUsedMove;
                        else
                            return null; //--Struggle
                    },

                    OnAfterTurn = ( pokemon ) =>
                    {
                        if( pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.Encore ) )
                        {
                            var encore = pokemon.VolatileStatuses[VolatileConditionID.Encore];
                            if( encore.Duration > 0 )
                            {
                                encore.Duration--;
                                pokemon.VolatileStatuses[VolatileConditionID.Encore] = encore;
                            }
                            else
                                pokemon.CureVolatileStatus( VolatileConditionID.Encore );
                        }
                    }
                }
            },
            {
                VolatileConditionID.Uproar, new()
                {
                    Name = "Uproar",
                    StartMessage = "started an uproar!",

                    OnStart = ( pokemon ) =>
                    {
                        pokemon.SetVolatileStatusTime( VolatileConditionID.Uproar, 3 );
                    },

                    OnBeforeMoveUsed = ( unit ) =>
                    {
                        if( unit.LastUsedMove != null && unit.LastUsedMove.MoveSO.Name == "Uproar" )
                            return unit.LastUsedMove;
                        else
                            return null; //--Struggle
                    },

                    OnAfterTurn = ( pokemon ) =>
                    {
                        if( pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.Uproar ) )
                        {
                            var uproar = pokemon.VolatileStatuses[VolatileConditionID.Uproar];
                            if( uproar.Duration > 0 )
                            {
                                uproar.Duration--;
                                pokemon.VolatileStatuses[VolatileConditionID.Uproar] = uproar;
                            }
                            else
                                pokemon.CureVolatileStatus( VolatileConditionID.Uproar );
                                
                        }
                    }
                }
            }
        };
    }
}

public enum VolatileConditionID
{
    None,
    Confusion,
    Infatuation,
    AquaRing,
    Taunt,
    HelpingHand,
    Rampage,
    Substitute,
    Cursed,
    Yawn,
    Encore,
    Uproar,
    ThroatChop,
}
