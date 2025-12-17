using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleItemDB
{
    public static Dictionary<BattleItemEffectID, BattleItemEffect> BattleItemEffects = new()
    {
        {
            BattleItemEffectID.FlameOrb, new()
            {
                ID = BattleItemEffectID.FlameOrb,
                
                OnItemRoundEnd = ( Pokemon pokemon ) =>
                {
                    if( pokemon.SevereStatus != null && pokemon.SevereStatus?.ID != StatusConditionID.NONE )
                        return;

                    if( pokemon.SevereStatus != null && pokemon.SevereStatus?.ID == StatusConditionID.NONE || pokemon.SevereStatus == null )
                    {
                        pokemon.SetSevereStatus( StatusConditionID.BRN );
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
                    if( pokemon.SevereStatus != null && pokemon.SevereStatus?.ID != StatusConditionID.NONE )
                        return;

                    if( pokemon.SevereStatus != null && pokemon.SevereStatus?.ID == StatusConditionID.NONE || pokemon.SevereStatus == null )
                    {
                        pokemon.SetSevereStatus( StatusConditionID.PSN );
                        Debug.Log( $"{pokemon.NickName} has been Poisoned by its Toxic Orb!" );
                    }
                }
            }

        },
        {
            BattleItemEffectID.ParaOrb, new()
            {
                ID = BattleItemEffectID.ParaOrb,
                
                OnItemRoundEnd = ( Pokemon pokemon ) =>
                {
                    if( pokemon.SevereStatus != null && pokemon.SevereStatus?.ID != StatusConditionID.NONE )
                        return;

                    if( pokemon.SevereStatus != null && pokemon.SevereStatus?.ID == StatusConditionID.NONE || pokemon.SevereStatus == null )
                    {
                        pokemon.SetSevereStatus( StatusConditionID.PAR );
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
                    Debug.Log( $"{attacker.Pokemon.NickName} is holding a life orb! 1.3x damage baybeee!" );
                    return 1.3f;
                },

                OnItemAfterTurn = ( BattleUnit unit ) =>
                {
                    if( unit.Flags[UnitFlags.DidDamage].IsActive )
                        unit.Pokemon.DecreaseHP( unit.Pokemon.MaxHP / 10 );
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
                    unit.SetFlagActive( UnitFlags.FocusSash, false );
                    unit.Pokemon.AddStatusEvent( $"{unit.Pokemon.NickName} was able to hold on due to its Focus Sash!" );
                },

                OnItemEnter = ( BattleUnit unit ) =>
                {
                    if( unit.Pokemon.CurrentHP == unit.Pokemon.MaxHP )
                    {
                        if( unit.Flags[UnitFlags.FocusSash].Count == 1 )
                            unit.SetFlagActive( UnitFlags.FocusSash, true );
                        else
                            unit.SetFlagActive( UnitFlags.FocusSash, false );
                    }
                    else
                    {
                        unit.SetFlagActive( UnitFlags.FocusSash, false );
                    }
                },

                OnItemExit = ( BattleUnit unit ) =>
                {
                    unit.SetFlagActive( UnitFlags.FocusSash, false );
                },

                OnTakeMoveDamage = ( BattleUnit attacker, BattleUnit target, Move move, float damage ) =>
                {
                    if( target.Flags[UnitFlags.FocusSash].IsActive )
                    {
                        Debug.Log( $"Damage done to Focus Sash: {damage}/{target.Pokemon.CurrentHP}" );
                        if( target.Pokemon.CurrentHP - damage == 0 )
                        {
                            damage--;
                            target.Pokemon.BattleItemEffect?.OnEnd?.Invoke( target );
                            Debug.Log( $"Damage after Focus Sash: {damage}" );
                        }
                        else
                            target.SetFlagActive( UnitFlags.FocusSash, false );

                        Debug.Log( $"{target.Pokemon.NickName} lost its Focus Sash!" );
                    }

                    return (int)damage;
                }
            }
        },
        {
            BattleItemEffectID.SitrusBerry, new()
            {
                ID = BattleItemEffectID.SitrusBerry,

                OnItemEnter = ( BattleUnit unit ) =>
                {
                    Debug.Log( $"Sitrus Berry Count: {unit.Flags[UnitFlags.SitrusBerry].Count}" );
                    if( unit.Flags[UnitFlags.SitrusBerry].Count == 1 )
                        unit.SetFlagActive( UnitFlags.SitrusBerry, true );
                    else
                        unit.SetFlagActive( UnitFlags.SitrusBerry, false );
                },

                OnAfterTakeDamage = ( BattleUnit unit ) =>
                {
                    if( unit.Flags[UnitFlags.SitrusBerry].IsActive )
                    {
                        Debug.Log( $"{unit.Pokemon.NickName} is holding a Sitrus Berry!" );
                        if( unit.Pokemon.IsBelowHPPercent( 50 ) )
                        {
                            Debug.Log( $"{unit.Pokemon.NickName} previous hp: {unit.Pokemon.CurrentHP}" );
                            int healBy = Mathf.FloorToInt( unit.Pokemon.MaxHP / 4 );
                            unit.Pokemon.IncreaseHP( healBy );
                            unit.SetFlagActive( UnitFlags.SitrusBerry, false );
                            unit.SetFlagCount( UnitFlags.SitrusBerry, 0 );
                            Debug.Log( $"{unit.Pokemon.NickName} new hp: {unit.Pokemon.CurrentHP}" );
                            unit.Pokemon.AddStatusEvent( $"{unit.Pokemon.NickName}'s ate its Sitrus Berry to restore HP!" );
                        }
                    }
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
                        Debug.Log( $"Prev HP: {pokemon.CurrentHP}" );
                        int healBy = Mathf.FloorToInt( pokemon.MaxHP / 16 );
                        pokemon.IncreaseHP( healBy );
                        Debug.Log( $"New HP: {pokemon.CurrentHP}" );
                        pokemon.AddStatusEvent( $"{pokemon.NickName}'s ate some leftovers to restore its HP!" );
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
                    if( move.MoveSO.Type == PokemonType.Water )
                    {
                        Debug.Log( $"{attacker.Pokemon.NickName} is holding a Mystic Water! 1.2x water move damage baybeee!" );
                        return 1.2f;
                    }
                    else
                        return 1f;
                },
            }
        },
    };
}

public enum BattleItemEffectID
{
    NONE,
    FlameOrb,
    ToxicOrb,
    ParaOrb,
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
}
