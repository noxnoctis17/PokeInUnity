using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleItemDB
{
    public static Dictionary<BattleItemEffectID, BattleItemEffect> BattleItemEffects = new()
    {
        {
            BattleItemEffectID.None, new()
            {
                //--None
            }
        },
        {
            BattleItemEffectID.FlameOrb, new()
            {
                ID = BattleItemEffectID.FlameOrb,
                
                OnItemRoundEnd = ( Pokemon pokemon ) =>
                {
                    if( pokemon.SevereStatus != null && pokemon.SevereStatus?.ID != SevereConditionID.None )
                        return;

                    if( pokemon.SevereStatus != null && pokemon.SevereStatus?.ID == SevereConditionID.None || pokemon.SevereStatus == null )
                    {
                        StatusEffectSource source = new()
                        {
                            Pokemon = pokemon,
                            Source = EffectSource.Item,
                        };

                        pokemon.SetSevereStatus( SevereConditionID.BRN, source );
                        Debug.Log( $"{pokemon.NickName} has been Burned by its Flame Orb!" );
                    }
                }
            }

        },
        {
            BattleItemEffectID.ToxicOrb, new()
            {
                ID = BattleItemEffectID.ToxicOrb,
                
                OnItemRoundEnd = ( Pokemon pokemon ) =>
                {
                    if( pokemon.SevereStatus != null && pokemon.SevereStatus?.ID != SevereConditionID.None )
                        return;

                    if( pokemon.SevereStatus != null && pokemon.SevereStatus?.ID == SevereConditionID.None || pokemon.SevereStatus == null )
                    {
                        StatusEffectSource source = new()
                        {
                            Pokemon = pokemon,
                            Source = EffectSource.Item,
                        };

                        pokemon.SetSevereStatus( SevereConditionID.PSN, source );
                        Debug.Log( $"{pokemon.NickName} has been Poisoned by its Toxic Orb!" );
                    }
                }
            }

        },
        {
            BattleItemEffectID.StaticOrb, new()
            {
                ID = BattleItemEffectID.StaticOrb,
                
                OnItemRoundEnd = ( Pokemon pokemon ) =>
                {
                    if( pokemon.SevereStatus != null && pokemon.SevereStatus?.ID != SevereConditionID.None )
                        return;

                    if( pokemon.SevereStatus != null && pokemon.SevereStatus?.ID == SevereConditionID.None || pokemon.SevereStatus == null )
                    {
                        StatusEffectSource source = new()
                        {
                            Pokemon = pokemon,
                            Source = EffectSource.Item,
                        };

                        pokemon.SetSevereStatus( SevereConditionID.PAR, source );
                        Debug.Log( $"{pokemon.NickName} has been Paralyzed by its Paralysis Orb!" );
                    }
                }
            }

        },
        {
            BattleItemEffectID.LifeOrb, new()
            {
                ID = BattleItemEffectID.LifeOrb,
                
                OnDamageModify = ( BattleUnit attacker, Pokemon target, Move move ) =>
                {
                    Debug.Log( $"[Life Orb] {attacker.Pokemon.NickName} is holding a life orb! 1.3x damage baybeee!" );
                    return 1.3f;
                },

                OnItemAfterTurn = ( BattleUnit unit ) =>
                {
                    Debug.Log( $"[Life Orb] We here at on item after turn for life orb and...did damage is: {unit.Flags[UnitFlags.DidDamage].IsActive}" );
                    if( unit.Flags[UnitFlags.DidDamage].IsActive )
                    {
                        unit.Pokemon.DecreaseHP( unit.Pokemon.MaxHP / 10 );
                        unit.Pokemon.AddStatusEvent( StatusEventType.Damage, string.Empty );
                    }
                }
            }

        },
        {
            BattleItemEffectID.ChoiceBand, new()
            {
                ID = BattleItemEffectID.ChoiceBand,

                OnItemEnter = ( BattleUnit unit ) =>
                {
                    Debug.Log( $"Choice Band detected! Setting Choice Item to true, adding 1.5x modifier to Attack" );
                    unit.SetFlagActive( UnitFlags.ChoiceItem, true );
                    unit.Pokemon.ApplyDirectStatModifier( Stat.Attack, DirectModifierCause.ChoiceBand, 1.5f );
                },

                OnItemExit = ( BattleUnit unit ) =>
                {
                    Debug.Log( $"Choice Band user leaving, fainted, Battle Ended, or lost choice band! Setting Choice Item to false, removing 1.5x modifier from Attack" );
                    unit.SetFlagActive( UnitFlags.ChoiceItem, false );
                    unit.Pokemon.RemoveDirectStatModifier( Stat.Attack, DirectModifierCause.ChoiceBand );
                },
            }

        },
        {
            BattleItemEffectID.ChoiceSpecs, new()
            {
                ID = BattleItemEffectID.ChoiceBand,

                OnItemEnter = ( BattleUnit unit ) =>
                {
                    Debug.Log( $"Choice Specs detected! Setting Choice Item to true, adding 1.5x modifier to SpAttack" );
                    unit.SetFlagActive( UnitFlags.ChoiceItem, true );
                    unit.Pokemon.ApplyDirectStatModifier( Stat.SpAttack, DirectModifierCause.ChoiceSpecs, 1.5f );
                },

                OnItemExit = ( BattleUnit unit ) =>
                {
                    Debug.Log( $"Choice Specs user leaving, fainted, Battle Ended, or lost choice band! Setting Choice Item to false, removing 1.5x modifier from SpAttack" );
                    unit.SetFlagActive( UnitFlags.ChoiceItem, false );
                    unit.Pokemon.RemoveDirectStatModifier( Stat.SpAttack, DirectModifierCause.ChoiceSpecs );
                },
            }
        },
        {
            BattleItemEffectID.ChoiceScarf, new()
            {
                ID = BattleItemEffectID.ChoiceScarf,

                OnItemEnter = ( BattleUnit unit ) =>
                {
                    Debug.Log( $"Choice Scarf detected! Setting Choice Item to true, adding 1.5x modifier to Speed" );
                    unit.SetFlagActive( UnitFlags.ChoiceItem, true );
                    unit.Pokemon.ApplyDirectStatModifier( Stat.Speed, DirectModifierCause.ChoiceScarf, 1.5f );
                },

                OnItemExit = ( BattleUnit unit ) =>
                {
                    Debug.Log( $"Choice Scarf user leaving, fainted, Battle Ended, or lost choice band! Setting Choice Item to false, removing 1.5x modifier from Speed" );
                    unit.SetFlagActive( UnitFlags.ChoiceItem, false );
                    unit.Pokemon.RemoveDirectStatModifier( Stat.Attack, DirectModifierCause.ChoiceScarf );
                },
            }
        },
        {
            BattleItemEffectID.FocusSash, new()
            {
                ID = BattleItemEffectID.FocusSash,

                OnEnd = ( BattleUnit unit ) =>
                {
                    // unit.SetFlagActive( UnitFlags.FocusSash, false );
                    unit.Pokemon.AddStatusEvent( $"{unit.Pokemon.NickName} was able to hold on due to its Focus Sash!" );
                },

                // OnItemEnter = ( BattleUnit unit ) =>
                // {
                //     if( unit.Pokemon.CurrentHP == unit.Pokemon.MaxHP )
                //             unit.SetFlagActive( UnitFlags.FocusSash, true );
                //     else
                //         unit.SetFlagActive( UnitFlags.FocusSash, false );
                // },

                // OnItemExit = ( BattleUnit unit ) =>
                // {
                //     unit.SetFlagActive( UnitFlags.FocusSash, false );
                // },

                OnTakeMoveDamage = ( BattleUnit attacker, BattleUnit target, Move move, float damage ) =>
                {
                    if( target.Pokemon.CurrentHP != target.Pokemon.MaxHP )
                        return (int)damage;

                    else if( target.Pokemon.CurrentHP - damage == 0 )
                    {
                        damage--; //--if damage will kill, we reduce the total damage by 1, leaving the target with 1hp. damage is clamped to never go over currenthp, so this should always work
                        target.Pokemon.BattleItemEffect?.OnEnd?.Invoke( target );
                    }
                    // else
                        // target.SetFlagActive( UnitFlags.FocusSash, false );

                    Debug.Log( $"{target.Pokemon.NickName} lost its Focus Sash!" );
                    target.Pokemon.RemoveHeldItem();
                    return (int)damage;
                },

                OnTakePassiveDamage = ( BattleUnit unit, float damage ) =>
                {
//                     if( unit.Flags[UnitFlags.FocusSash].IsActive )
                    // {
                    if( unit.Pokemon.CurrentHP != unit.Pokemon.MaxHP )
                        return;
                    else if( unit.Pokemon.CurrentHP - damage == 0 )
                    {
                        damage--; //--if damage will kill, we reduce the total damage by 1, leaving the unit with 1hp. damage is clamped to never go over currenthp, so this should always work
                        unit.Pokemon.BattleItemEffect?.OnEnd?.Invoke( unit );
                    }
                    // else
                    //     unit.SetFlagActive( UnitFlags.FocusSash, false );

                    Debug.Log( $"{unit.Pokemon.NickName} lost its Focus Sash!" );
                    unit.Pokemon.RemoveHeldItem();
                    // }
                },
            }
        },
        {
            BattleItemEffectID.SitrusBerry, new()
            {
                ID = BattleItemEffectID.SitrusBerry,

                // OnItemEnter = ( BattleUnit unit ) =>
                // {
                //     Debug.Log( $"Sitrus Berry Count: {unit.Flags[UnitFlags.SitrusBerry].Count}" );
                //     if( unit.Flags[UnitFlags.SitrusBerry].Count == 1 )
                //         unit.SetFlagActive( UnitFlags.SitrusBerry, true );
                //     else
                //         unit.SetFlagActive( UnitFlags.SitrusBerry, false );
                // },

                OnAfterTakeDamage = ( BattleUnit unit ) =>
                {
                    // if( unit.Flags[UnitFlags.SitrusBerry].IsActive )
                    // {
                    Debug.Log( $"{unit.Pokemon.NickName} is holding a Sitrus Berry!" );
                    if( unit.Pokemon.IsBelowHPPercent( 50 ) && unit.Pokemon.CurrentHP > 0 )
                    {
                        // Debug.Log( $"{unit.Pokemon.NickName} previous hp: {unit.Pokemon.CurrentHP}" );
                        int healBy = Mathf.FloorToInt( unit.Pokemon.MaxHP / 4f );
                        unit.Pokemon.IncreaseHP( healBy );
                        // unit.SetFlagActive( UnitFlags.SitrusBerry, false );
                        // unit.SetFlagCount( UnitFlags.SitrusBerry, 0 );
                        // Debug.Log( $"{unit.Pokemon.NickName} new hp: {unit.Pokemon.CurrentHP}" );
                        unit.Pokemon.AddStatusEvent( StatusEventType.Heal, $"{unit.Pokemon.NickName} ate its Sitrus Berry to restore HP!" );
                        unit.Pokemon.RemoveHeldItem();
                    }
                    // }
                }
            }
        },
        {
            BattleItemEffectID.Leftovers, new()
            {
                ID = BattleItemEffectID.Leftovers,

                OnItemRoundEnd = ( Pokemon pokemon ) =>
                {
                    Debug.Log( $"Leftovers Triggered!" );
                    if( pokemon.CurrentHP < pokemon.MaxHP )
                    {
                        // Debug.Log( $"Prev HP: {pokemon.CurrentHP}" );
                        int healBy = Mathf.FloorToInt( pokemon.MaxHP / 16 );
                        pokemon.IncreaseHP( healBy );
                        // Debug.Log( $"New HP: {pokemon.CurrentHP}" );
                        pokemon.AddStatusEvent( StatusEventType.Heal, $"{pokemon.NickName} ate some leftovers to restore its HP!" );
                    }
                }
            }
        },
        {
            BattleItemEffectID.BlackSludge, new()
            {
                ID = BattleItemEffectID.BlackSludge,

                OnItemRoundEnd = ( Pokemon pokemon ) =>
                {
                    Debug.Log( $"Leftovers Triggered!" );
                    if( pokemon.CheckTypes( PokemonType.Poison ) )
                    {
                        int healBy = Mathf.FloorToInt( pokemon.MaxHP / 16 );
                        pokemon.IncreaseHP( healBy );
                        pokemon.AddStatusEvent( StatusEventType.Heal, $"{pokemon.NickName} ate some leftovers to restore its HP!" );
                    }
                    else
                    {
                        int damage = Mathf.FloorToInt( pokemon.MaxHP / 16 );
                        pokemon.DecreaseHP( damage );
                        pokemon.AddStatusEvent( StatusEventType.Heal, $"{pokemon.NickName} ate some leftovers to restore its HP!" );
                    }
                }
            }
        },
        {
            BattleItemEffectID.MysticWater, new()
            {
                ID = BattleItemEffectID.MysticWater,
                
                OnDamageModify = ( BattleUnit attacker, Pokemon target, Move move ) =>
                {
                    if( move.MoveType == PokemonType.Water )
                    {
                        Debug.Log( $"{attacker.Pokemon.NickName} is holding a Mystic Water! 1.2x water move damage baybeee!" );
                        return 1.2f;
                    }
                    else
                        return 1f;
                },
            }
        },
        {
            BattleItemEffectID.Charcoal, new()
            {
                ID = BattleItemEffectID.Charcoal,
                
                OnDamageModify = ( BattleUnit attacker, Pokemon target, Move move ) =>
                {
                    if( move.MoveType == PokemonType.Fire )
                    {
                        Debug.Log( $"{attacker.Pokemon.NickName} is holding a Charcoal! 1.2x fire move damage baybeee!" );
                        return 1.2f;
                    }
                    else
                        return 1f;
                },
            }
        },
        {
            BattleItemEffectID.LightBall, new()
            {
                ID = BattleItemEffectID.LightBall,
                
                OnItemEnter = ( BattleUnit unit ) =>
                {
                    if( unit.Pokemon.PokeSO.Species == "Pikachu" || unit.Pokemon.PokeSO.Species == "Pikachu_Red" )
                    {
                        Debug.Log( $"{unit.Pokemon.NickName} is holding a Light Ball! Its Attack and SpAttack have been doubled!" );
                        unit.Pokemon.ApplyDirectStatModifier( Stat.Attack, DirectModifierCause.LightBall, 2f );
                        unit.Pokemon.ApplyDirectStatModifier( Stat.SpAttack, DirectModifierCause.LightBall, 2f );
                    }
                },

                OnItemExit = ( BattleUnit unit ) =>
                {
                    if( unit.Pokemon.PokeSO.Species == "Pikachu" || unit.Pokemon.PokeSO.Species == "Pikachu_Red" )
                    {
                        Debug.Log( $"{unit.Pokemon.NickName} is holding a Light Ball! Its Attack and SpAttack modifiers have been removed!" );
                        unit.Pokemon.RemoveDirectStatModifier( Stat.Attack, DirectModifierCause.LightBall );
                        unit.Pokemon.RemoveDirectStatModifier( Stat.SpAttack, DirectModifierCause.LightBall );
                    }
                }
            }
        },
        {
            BattleItemEffectID.RockyHelmet, new()
            {
                ID = BattleItemEffectID.RockyHelmet,

                OnMoveContact = ( BattleUnit attacker, BattleUnit target, Move move ) =>
                {
                    if( move.MoveSO.HasFlag( MoveFlags.Contact ) )
                    {
                        int damage = Mathf.FloorToInt( attacker.Pokemon.MaxHP / 6f );
                        attacker.Pokemon.DecreaseHP( damage );
                        attacker.Pokemon.AddStatusEvent( StatusEventType.Damage, $"{attacker.Pokemon.NickName} is hurt by {target.Pokemon.NickName}'s Rocky Helmet!" );
                    }
                },
            }
        },
        {
            BattleItemEffectID.ExpertBelt, new()
            {
                ID = BattleItemEffectID.ExpertBelt,

                OnDamageModify = ( attacker, target, move ) =>
                {
                    var effectiveness = TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type2 );
                    if( effectiveness > 1 )
                        return 4915f/4096f;
                    else
                        return 1f;
                }
            }
        }
    };
}

public enum BattleItemEffectID
{
    None,
    FlameOrb,
    ToxicOrb,
    StaticOrb,
    LifeOrb,
    ChoiceBand,
    ChoiceSpecs,
    ChoiceScarf,
    FocusSash,
    SitrusBerry,
    Leftovers,
    LightClay,
    HeatRock,
    DampRock,
    SmoothRock,
    IcyRock,
    MysticWater,
    Charcoal,
    LightBall,
    RockyHelmet,
    ExpertBelt,
    HeavyDutyBoots,
    BlackSludge,
}
