using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class MoveSuccessDB
{
    public static Dictionary<string, MoveSuccess> MoveSuccess { get; set; }

    public static void Init()
    {
        SetDictionary();

        //--Auto sets the name/id of each entry
        foreach( var kvp in MoveSuccess )
        {
            var name = kvp.Key;
            var success = kvp.Value;

            success.Name = name;
        }
    }

    public static string ItDoesntEffect( Pokemon pokemon )
    {
        string name = pokemon.NickName;

        return $"It doesn't effect {name}...";
    }

    public static string HadNoEffect()
    {
        return "It had no effect!";
    }

    public static string ButItFailed()
    {
        return "But the move failed!";
    }

    public static string WasBlockedBy( string blocked )
    {
        return $"But it was blocked by {blocked}!";
    }

    public static string Protect( Pokemon pokemon )
    {
        return $"{pokemon.NickName} protects itself!";
    }

    public static void SetDictionary()
    {
        MoveSuccess = new()
        {
            {
                "Fake Out", new()
                {
                    FailureMessage = ( Pokemon user ) => ButItFailed(),
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        Debug.Log( $"Fake Out user {attacker.Pokemon.NickName}'s Turn Count: {attacker.Flags[UnitFlags.TurnsTaken].Count}" );
                        if( attacker.Flags[UnitFlags.TurnsTaken].Count > 0 )
                        {
                            return false;
                        }
                        else
                            return true;
                    }
                }
            },
            {
                "Sunny Day", new()
                {
                    FailureMessage = ( Pokemon user ) => ButItFailed(),
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( bs.Field.Weather?.ID == WeatherConditionID.SUNNY )
                        {
                            return false;
                        }
                        else
                            return true;
                    }
                }
            },
            {
                "Rain Dance", new()
                {
                    FailureMessage = ( Pokemon user ) => ButItFailed(),
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( bs.Field.Weather?.ID == WeatherConditionID.RAIN )
                        {
                            return false;
                        }
                        else
                            return true;
                    }
                }
            },
            {
                "Sand Storm", new()
                {
                    FailureMessage = ( Pokemon user ) => ButItFailed(),
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( bs.Field.Weather?.ID == WeatherConditionID.SANDSTORM )
                        {
                            return false;
                        }
                        else
                            return true;
                    }
                }
            },
            {
                "Snowscape", new()
                {
                    FailureMessage = ( Pokemon user ) => ButItFailed(),
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( bs.Field.Weather?.ID == WeatherConditionID.SNOW )
                        {
                            return false;
                        }
                        else
                            return true;
                    }
                }
            },
            {
                "Tailwind", new()
                {
                    FailureMessage = ( Pokemon user ) => ButItFailed(),
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        var court = bs.Field.GetUnitCourt( attacker );
                        if( court.Conditions.ContainsKey( CourtConditionID.Tailwind ) )
                        {
                            return false;
                        }
                        else
                            return true;
                    }
                }
            },
            {
                "Reflect", new()
                {
                    FailureMessage = ( Pokemon user ) => ButItFailed(),
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        var court = bs.Field.GetUnitCourt( attacker );
                        if( court.Conditions.ContainsKey( CourtConditionID.Reflect ) )
                        {
                            return false;
                        }
                        else
                            return true;
                    }
                }
            },
            {
                "Light Screen", new()
                {
                    FailureMessage = ( Pokemon user ) => ButItFailed(),
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        var court = bs.Field.GetUnitCourt( attacker );
                        if( court.Conditions.ContainsKey( CourtConditionID.LightScreen ) )
                        {
                            return false;
                        }
                        else
                            return true;
                    }
                }
            },
            {
                "Thunder Wave", new()
                {
                    FailureMessage = ( Pokemon user ) => ItDoesntEffect( user ),
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.Pokemon.CheckTypes( PokemonType.Ground ) || target.Pokemon.CheckTypes( PokemonType.Electric ) )
                            return false;
                        else
                            return true;
                    }
                }
            },
            {
                "Will-O-Wisp", new()
                {
                    FailureMessage = ( Pokemon user ) => ItDoesntEffect( user ),
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.Pokemon.CheckTypes( PokemonType.Fire ) )
                            return false;
                        else
                            return true;
                    }
                }
            },
            {
                "Toxic", new()
                {
                    FailureMessage = ( Pokemon user ) => ItDoesntEffect( user ),
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.Pokemon.CheckTypes( PokemonType.Steel ) )
                            return false;
                        else
                            return true;
                    },

                    OnCheckAccuracy = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( attacker.Pokemon.CheckTypes( PokemonType.Poison ) )
                        {
                            move.OverrideAccuracyType( AccuracyType.AlwaysHits );
                        }
                    },

                    OnMoveCompleted = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        move.OverrideAccuracyType( AccuracyType.Once );
                    }
                }
            },
            {
                "Stun Spore", new()
                {
                    FailureMessage = ( Pokemon user ) => ItDoesntEffect( user ),
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.Pokemon.CheckTypes( PokemonType.Electric ) )
                            return false;
                        else
                            return true;
                    }
                }
            },
            {
                "Protect", new()
                {
                    // SuccessMessage = ( Pokemon user ) => Protect( user ),
                    FailureMessage = ( Pokemon user ) => ButItFailed(),

                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        int uses = attacker.Flags[UnitFlags.SuccessiveProtectUses].Count;
                        float successChance = Mathf.Pow( 1f / 3f, uses );
                        bool success;

                        Debug.Log( $"[Move Success][Protect] {attacker.Pokemon.NickName} has used protect: {uses} times!" );
                        
                        if( uses > 0 )
                        {
                            Debug.Log( $"[Move Success][Protect] Checking {attacker.Pokemon.NickName}'s protect attempt success..." );
                            success = UnityEngine.Random.value <= successChance;

                            if( success )
                            {
                                Debug.Log( $"[Move Success][Protect] Checking {attacker.Pokemon.NickName} Succeeded protecting! Increasing success use count." );
                                attacker.Flags[UnitFlags.SuccessiveProtectUses].Count++;
                                return true;
                            }
                            else
                            {
                                Debug.Log( $"[Move Success][Protect] Checking {attacker.Pokemon.NickName} Failed protecting! Resetting success use count to 0." );
                                attacker.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;
                                return false;
                            }
                        }
                        else
                        {
                            attacker.Flags[UnitFlags.SuccessiveProtectUses].Count++;
                            return true;
                        }
                    }
                }
            },
            {
                "Endure", new()
                {
                    FailureMessage = ( Pokemon user ) => ButItFailed(),

                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        int uses = attacker.Flags[UnitFlags.SuccessiveProtectUses].Count;
                        float successChance = Mathf.Pow( 1f / 3f, uses );
                        bool success;

                        Debug.Log( $"[Move Success][Endure] {attacker.Pokemon.NickName} has used protect/endure: {uses} times!" );
                        
                        if( uses > 0 )
                        {
                            Debug.Log( $"[Move Success][Endure] Checking {attacker.Pokemon.NickName}'s endure attempt success..." );
                            success = UnityEngine.Random.value <= successChance;

                            if( success )
                            {
                                Debug.Log( $"[Move Success][Endure] Checking {attacker.Pokemon.NickName} Succeeded endure! Increasing success use count." );
                                attacker.Flags[UnitFlags.SuccessiveProtectUses].Count++;
                                return true;
                            }
                            else
                            {
                                Debug.Log( $"[Move Success][Endure] Checking {attacker.Pokemon.NickName} Failed endure! Resetting success use count to 0." );
                                attacker.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;
                                return false;
                            }
                        }
                        else
                        {
                            attacker.Flags[UnitFlags.SuccessiveProtectUses].Count++;
                            return true;
                        }
                    }
                }
            },
            {
                "Blizzard", new()
                {
                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) => true,

                    OnCheckAccuracy = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( bs.Field.Weather?.ID == WeatherConditionID.SNOW )
                        {
                            move.OverrideAccuracyType( AccuracyType.AlwaysHits );
                        }
                    },

                    OnMoveCompleted = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        move.OverrideAccuracyType( AccuracyType.Once );
                    },
                }
            },
            {
                "Substitute", new()
                {
                    FailureMessage = ( Pokemon user ) => ButItFailed(),

                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( attacker.Flags[UnitFlags.Substitute].IsActive )
                            return false;
                        else
                            return true;
                    }
                }
            },
            {
                "Spite", new()
                {
                    FailureMessage = ( Pokemon user ) => ButItFailed(),

                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.LastUsedMove == null )
                            return false;

                        if( target.LastUsedMove.PP == 0 )
                            return false;

                        return true;
                    }
                }
            },
            {
                "Sucker Punch", new()
                {
                    FailureMessage = ( Pokemon user ) => ButItFailed(),

                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.Flags[UnitFlags.CompletedTurn].IsActive )
                            return false;

                        IBattleCommand targetCommand = null;
                        foreach( var command in bs.CommandQueue )
                        {
                            if( command.User == target )
                            {
                                targetCommand = command;
                                break;
                            }
                            else
                                continue;
                        }

                        if( targetCommand == null )
                            return false;

                        if( targetCommand is UseMoveCommand moveCommand )
                        {
                            if( moveCommand.Move.MoveSO.MoveCategory == MoveCategory.Status )
                                return false;
                            else
                                return true;
                        }
                        else
                            return false;

                    }
                }
            },
            {
                "Dream Eater", new()
                {
                    FailureMessage = ( Pokemon user ) => HadNoEffect(),

                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.Pokemon.SevereStatus == null )
                            return false;
                        else if( target.Pokemon.SevereStatus?.ID != SevereConditionID.SLP )
                            return true;
                        else
                            return false;
                    }
                }
            },
            {
                "Trick", new()
                {
                    FailureMessage = ( Pokemon user ) => HadNoEffect(),

                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( attacker.Pokemon.HeldItem == null && target.Pokemon.HeldItem == null )
                            return false;

                        if( target.Flags[UnitFlags.Substitute].IsActive )
                            return false;

                        if( target.Pokemon.AbilityID == AbilityID.StickyHold )
                            return false;

                        if( target.Pokemon.HeldItem.CanNotSteal )
                            return false;

                        if( target.Pokemon == bs.WildPokemon )
                            return false;

                        return true;
                    }
                }
            },
            {
                "Phantom Force", new()
                {
                    OnCheckNeedsToCharge = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( !attacker.Flags[UnitFlags.Charging].IsActive )
                        {
                            attacker.SetCharging( target, move );
                            attacker.SetFlagActive( UnitFlags.SemiInvulnerable, true );
                            bs.AddDialogue( $"{attacker.Pokemon.NickName} vanished!" );
                            return true;
                        }
                        else
                        {
                            attacker.ClearCharging();
                            attacker.SetFlagActive( UnitFlags.SemiInvulnerable, false );
                            return false;
                        }
                    },
                }
            },
            {
                "Dig", new()
                {
                    OnCheckNeedsToCharge = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( !attacker.Flags[UnitFlags.Charging].IsActive )
                        {
                            attacker.SetCharging( target, move );
                            attacker.SetFlagActive( UnitFlags.SemiInvulnerable, true );
                            bs.AddDialogue( $"{attacker.Pokemon.NickName} boroughed under ground!" );
                            return true;
                        }
                        else
                        {
                            attacker.ClearCharging();
                            attacker.SetFlagActive( UnitFlags.SemiInvulnerable, false );
                            return false;
                        }
                    },
                }
            },
            {
                "Fly", new()
                {
                    OnCheckNeedsToCharge = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( !attacker.Flags[UnitFlags.Charging].IsActive )
                        {
                            attacker.SetCharging( target, move );
                            attacker.SetFlagActive( UnitFlags.SemiInvulnerable, true );
                            bs.AddDialogue( $"{attacker.Pokemon.NickName} flew into the sky!" );
                            return true;
                        }
                        else
                        {
                            attacker.ClearCharging();
                            attacker.SetFlagActive( UnitFlags.SemiInvulnerable, false );
                            return false;
                        }
                    },
                }
            },
            {
                "Poltergeist", new()
                {
                    FailureMessage = ( user ) => HadNoEffect(),

                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.Pokemon.HeldItem != null )
                        {
                            bs.AddDialogue( $"{target.Pokemon.NickName} is about to be attacked by its {target.Pokemon.HeldItem.ItemName}!" );
                            return true;
                        }
                        else
                        {
                            bs.AddDialogue( $"{target.Pokemon.NickName} has no item for {attacker.Pokemon.NickName} to manipulate!" );
                            return false;
                        }
                    }
                }
            },
            {
                "Counter", new()
                {
                    FailureMessage = ( user ) => ButItFailed(),

                    OnCheckSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        var tookDamage = target.Flags[UnitFlags.TookDamage];
                        if( tookDamage.IsActive && tookDamage.Move.MoveSO.MoveCategory == MoveCategory.Physical )
                            return true;
                        else
                            return false;
                    }
                }
            },
            {
                "Copycat", new()
                {
                    FailureMessage = ( user ) => "But there was no move to copy!",

                    OnCheckSuccess = ( attacker, target, move, bs ) =>
                    {
                        if( bs.LastUsedMove == null )
                            return false;
                        else
                            return true;
                    }
                }
            },
            {
                "Upper Hand", new()
                {
                    FailureMessage = ( user ) => ButItFailed(),

                    OnCheckSuccess = ( attacker, target, move, bs ) =>
                    {
                        if( target.Flags[UnitFlags.CompletedTurn].IsActive )
                            return false;

                        IBattleCommand targetCommand = null;
                        foreach( var command in bs.CommandQueue )
                        {
                            if( command.User == target )
                            {
                                targetCommand = command;
                                break;
                            }
                            else
                                continue;
                        }

                        if( targetCommand == null )
                            return false;

                        if( targetCommand is UseMoveCommand moveCommand )
                        {
                            if( moveCommand.Move.MoveSO.MoveCategory == MoveCategory.Status )
                                return false;
                            else if( moveCommand.Move.Priority < MovePriority.One )
                                return false;
                            else if( moveCommand.Move.Priority > MovePriority.Three )
                                return false;
                            else
                                return true;
                        }
                        else
                            return false;
                    }
                }
            },
            {
                "Yawn", new()
                {
                    FailureMessage = ( user ) => ButItFailed(),

                    OnCheckSuccess = ( attacker, target, move, bs ) =>
                    {
                        if( target.Pokemon.SevereStatus != null )
                            return false;

                        if( bs.BattleFlags[BattleFlag.Uproar] && target.Pokemon.AbilityID != AbilityID.Soundproof )
                            return false;

                        if( target.Flags[UnitFlags.Substitute].IsActive )
                            return false;

                        return true;
                    }
                }
            },
            {
                "After You", new()
                {
                    FailureMessage = ( user ) => ButItFailed(),

                    OnCheckSuccess = ( attacker, target, move, bs ) =>
                    {
                        if( target.Flags[UnitFlags.CompletedTurn].IsActive )
                            return false;

                        IBattleCommand targetCommand = null;
                        foreach( var command in bs.CommandQueue )
                        {
                            if( command.User == target )
                            {
                                targetCommand = command;
                                break;
                            }
                            else
                                continue;
                        }

                        if( targetCommand == null )
                            return false;

                        return true;
                    }
                }
            },
            {
                "Solar Beam", new()
                {
                    OnCheckNeedsToCharge = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( !attacker.Flags[UnitFlags.Charging].IsActive )
                        {
                            if( bs.Field.Weather?.ID == WeatherConditionID.SUNNY )
                            {
                                bs.AddDialogue( $"The harsh sun fully charged {attacker.Pokemon.NickName}'s attack!" );
                                return false;
                            }
                            else
                            {
                                attacker.SetCharging( target, move );
                                bs.AddDialogue( $"{attacker.Pokemon.NickName} is drawing in sunlight!" );
                                return true;
                            }
                        }
                        else
                        {
                            attacker.ClearCharging();
                            return false;
                        }
                    },
                }
            },
            {
                "Encore", new()
                {
                    FailureMessage = ( user ) => ButItFailed(),

                    OnCheckSuccess = ( attacker, target, move, bs ) =>
                    {
                        if( target.LastUsedMove == null )
                            return false;
                        else
                            return true;
                    }
                }
            },
            {
                "Uproar", new()
                {
                    FailureMessage = ( user ) => $"{user.NickName} has calmed down from its Uproar.",

                    OnCheckSuccess = ( attacker, target, move, bs ) =>
                    {
                        if( attacker.Pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.ThroatChop ) )
                            return false;

                        move.PP++;
                        return true;
                    }
                }
            }
        };
    }
}
