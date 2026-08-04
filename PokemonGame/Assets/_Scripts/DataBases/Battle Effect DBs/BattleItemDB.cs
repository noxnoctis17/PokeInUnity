using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleItemDB
{
    public static Dictionary<ItemBattleEffectID, BattleItemEffect> BattleItemEffects = new()
    {
        {
            ItemBattleEffectID.None, new()
            {
                //--None
            }
        },
        {
            ItemBattleEffectID.FlameOrb, new()
            {
                ID = ItemBattleEffectID.FlameOrb,
                
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
            ItemBattleEffectID.ToxicOrb, new()
            {
                ID = ItemBattleEffectID.ToxicOrb,
                
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
            ItemBattleEffectID.StaticOrb, new()
            {
                ID = ItemBattleEffectID.StaticOrb,
                
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
            ItemBattleEffectID.LifeOrb, new()
            {
                ID = ItemBattleEffectID.LifeOrb,
                
                OnDamageModify = ( BattleUnit attacker, Pokemon target, Move move ) =>
                {
                    Debug.Log( $"[Life Orb] {attacker.Pokemon.NickName} is holding a life orb! 1.3x damage baybeee!" );
                    return 1.3f;
                },

                OnItemAfterTurn = ( BattleUnit unit ) =>
                {
                    if( unit.Flags[UnitFlags.DidDamage].IsActive )
                    {
                        unit.Pokemon.DecreaseHP( unit.Pokemon.MaxHP / 10 );
                        unit.Pokemon.AddStatusEvent( StatusEventType.Damage, string.Empty );
                    }
                }
            }

        },
        {
            ItemBattleEffectID.ChoiceBand, new()
            {
                ID = ItemBattleEffectID.ChoiceBand,

                OnItemEnter = ( unit ) =>
                {
                    Debug.Log( $"Choice Band detected! Setting Choice Item to true, adding 1.5x modifier to Attack" );
                    StatusEffectSource source = new(){ Pokemon = unit.Pokemon, Source = EffectSource.Item };
                    unit.Pokemon.SetVolatileStatus( VolatileConditionID.ChoiceLocked, source );
                    unit.Pokemon.ApplyDirectStatModifier( Stat.Attack, DirectModifierCause.ChoiceBand, 1.5f );
                },

                OnItemExit = ( unit ) =>
                {
                    Debug.Log( $"Choice Band user leaving, fainted, Battle Ended, or lost choice band! Setting Choice Item to false, removing 1.5x modifier from Attack" );
                    unit.Pokemon.CureVolatileStatus( VolatileConditionID.ChoiceLocked );
                    unit.Pokemon.RemoveDirectStatModifier( Stat.Attack, DirectModifierCause.ChoiceBand );
                },
            }

        },
        {
            ItemBattleEffectID.ChoiceSpecs, new()
            {
                ID = ItemBattleEffectID.ChoiceBand,

                OnItemEnter = ( unit ) =>
                {
                    Debug.Log( $"Choice Specs detected! Setting Choice Item to true, adding 1.5x modifier to SpAttack" );
                    StatusEffectSource source = new(){ Pokemon = unit.Pokemon, Source = EffectSource.Item };
                    unit.Pokemon.SetVolatileStatus( VolatileConditionID.ChoiceLocked, source );
                    unit.Pokemon.ApplyDirectStatModifier( Stat.SpAttack, DirectModifierCause.ChoiceSpecs, 1.5f );
                },

                OnItemExit = ( unit ) =>
                {
                    Debug.Log( $"Choice Specs user leaving, fainted, Battle Ended, or lost choice band! Setting Choice Item to false, removing 1.5x modifier from SpAttack" );
                    unit.Pokemon.CureVolatileStatus( VolatileConditionID.ChoiceLocked );
                    unit.Pokemon.RemoveDirectStatModifier( Stat.SpAttack, DirectModifierCause.ChoiceSpecs );
                },
            }
        },
        {
            ItemBattleEffectID.ChoiceScarf, new()
            {
                ID = ItemBattleEffectID.ChoiceScarf,

                OnItemEnter = ( unit ) =>
                {
                    Debug.Log( $"Choice Scarf detected! Setting Choice Item to true, adding 1.5x modifier to Speed" );
                    StatusEffectSource source = new(){ Pokemon = unit.Pokemon, Source = EffectSource.Item };
                    unit.Pokemon.SetVolatileStatus( VolatileConditionID.ChoiceLocked, source );
                    unit.Pokemon.ApplyDirectStatModifier( Stat.Speed, DirectModifierCause.ChoiceScarf, 1.5f );
                },

                OnItemExit = ( unit ) =>
                {
                    Debug.Log( $"Choice Scarf user leaving, fainted, Battle Ended, or lost choice band! Setting Choice Item to false, removing 1.5x modifier from Speed" );
                    unit.Pokemon.CureVolatileStatus( VolatileConditionID.ChoiceLocked );
                    unit.Pokemon.RemoveDirectStatModifier( Stat.Attack, DirectModifierCause.ChoiceScarf );
                },
            }
        },
        {
            ItemBattleEffectID.FocusSash, new()
            {
                ID = ItemBattleEffectID.FocusSash,

                OnEnd = ( unit ) =>
                {
                    unit.Pokemon.AddStatusEvent( $"{unit.Pokemon.NickName} was able to hold on due to its Focus Sash!" );
                },

                OnTakeMoveDamage = ( attacker, target, move, damage ) =>
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

                OnTakePassiveDamage = ( unit, damage ) =>
                {
                    if( unit.Pokemon.CurrentHP != unit.Pokemon.MaxHP )
                        return;
                    else if( unit.Pokemon.CurrentHP - damage == 0 )
                    {
                        damage--; //--if damage will kill, we reduce the total damage by 1, leaving the unit with 1hp. damage is clamped to never go over currenthp, so this should always work
                        unit.Pokemon.BattleItemEffect?.OnEnd?.Invoke( unit );
                    }

                    Debug.Log( $"{unit.Pokemon.NickName} lost its Focus Sash!" );
                    unit.Pokemon.RemoveHeldItem();
                },
            }
        },
        {
            ItemBattleEffectID.SitrusBerry, new()
            {
                ID = ItemBattleEffectID.SitrusBerry,

                OnAfterTakeDamage = ( unit ) =>
                {
                    Debug.Log( $"{unit.Pokemon.NickName} is holding a Sitrus Berry!" );
                    if( unit.Pokemon.IsBelowHPPercent( 50 ) && unit.Pokemon.CurrentHP > 0 )
                    {
                        int healBy = Mathf.FloorToInt( unit.Pokemon.MaxHP / 4f );
                        unit.Pokemon.IncreaseHP( healBy );
                        unit.Pokemon.AddStatusEvent( StatusEventType.Heal, $"{unit.Pokemon.NickName} ate its Sitrus Berry to restore HP!" );
                        unit.Pokemon.RemoveHeldItem();
                    }
                },

                OnTakePassiveDamage = ( unit, damage ) =>
                {
                    Debug.Log( $"{unit.Pokemon.NickName} is holding a Sitrus Berry!" );
                    if( unit.Pokemon.IsBelowHPPercent( 50 ) && unit.Pokemon.CurrentHP > 0 )
                    {
                        int healBy = Mathf.FloorToInt( unit.Pokemon.MaxHP / 4f );
                        unit.Pokemon.IncreaseHP( healBy );
                        unit.Pokemon.AddStatusEvent( StatusEventType.Heal, $"{unit.Pokemon.NickName} ate its Sitrus Berry to restore HP!" );
                        unit.Pokemon.RemoveHeldItem();
                    }
                }
            }
        },
        {
            ItemBattleEffectID.Leftovers, new()
            {
                ID = ItemBattleEffectID.Leftovers,

                OnItemRoundEnd = ( pokemon ) =>
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
            ItemBattleEffectID.BlackSludge, new()
            {
                ID = ItemBattleEffectID.BlackSludge,

                OnItemRoundEnd = ( pokemon ) =>
                {
                    Debug.Log( $"Black Sludge Triggered!" );
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
            ItemBattleEffectID.MysticWater, new()
            {
                ID = ItemBattleEffectID.MysticWater,
                
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
            ItemBattleEffectID.Charcoal, new()
            {
                ID = ItemBattleEffectID.Charcoal,
                
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
            ItemBattleEffectID.LightBall, new()
            {
                ID = ItemBattleEffectID.LightBall,
                
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
            ItemBattleEffectID.RockyHelmet, new()
            {
                ID = ItemBattleEffectID.RockyHelmet,

                OnMoveContactTarget = ( attacker, target, move ) =>
                {
                    if( move.MoveSO.HasFlag( MoveFlags.Contact ) )
                    {
                        int damage = Mathf.FloorToInt( attacker.Pokemon.MaxHP / 6f );
                        attacker.Pokemon.DecreaseHP( damage );
                        attacker.Pokemon.AddStatusEvent( StatusEventType.Damage, $"{attacker.Pokemon.NickName} is hurt by {target.Pokemon.NickName}'s Rocky Helmet!" );

                        attacker.Pokemon.BattleItemEffect?.OnTakePassiveDamage?.Invoke( attacker, damage );
                    }
                },
            }
        },
        {
            ItemBattleEffectID.ExpertBelt, new()
            {
                ID = ItemBattleEffectID.ExpertBelt,

                OnDamageModify = ( attacker, target, move ) =>
                {
                    var effectiveness = TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type2 );
                    if( effectiveness > 1 )
                        return 4915f/4096f;
                    else
                        return 1f;
                }
            }
        },
        {
            ItemBattleEffectID.MetalCoat, new()
            {
                ID = ItemBattleEffectID.MetalCoat,

                OnDamageModify = ( attacker, target, move ) =>
                {
                    if( move.MoveType == PokemonType.Steel )
                    {
                        Debug.Log( $"{attacker.Pokemon.NickName} is holding a Metal Coat! 1.2x steel move damage baybeee!" );
                        return 1.2f;
                    }
                    else
                        return 1f;
                }
            }
        },
        {
            ItemBattleEffectID.HeavyDutyBoots, new()
            {
                ID = ItemBattleEffectID.HeavyDutyBoots,
                //--Effect for this item is handled in all places entry hazard damage is checked, including the AI system.
            }
        },
        {
            ItemBattleEffectID.SafetyGoggles, new()
            {
                ID = ItemBattleEffectID.SafetyGoggles,
                //--Effect for this item should be handled in sandstorm damage check + powder move check
            }
        },
        {
            ItemBattleEffectID.Eviolite, new()
            {
                ID = ItemBattleEffectID.Eviolite,

                OnItemEnter = ( unit ) =>
                {
                    bool evos = unit.Pokemon.PokeSO.Evolutions.Count > 0;
                    if( evos )
                    {
                        unit.Pokemon.ApplyDirectStatModifier( Stat.Defense, DirectModifierCause.Eviolite, 1.5f );
                        unit.Pokemon.ApplyDirectStatModifier( Stat.SpDefense, DirectModifierCause.Eviolite, 1.5f );
                    }
                },

                OnItemExit = ( unit ) =>
                {
                    unit.Pokemon.RemoveDirectStatModifier( Stat.Defense, DirectModifierCause.Eviolite );
                    unit.Pokemon.RemoveDirectStatModifier( Stat.SpDefense, DirectModifierCause.Eviolite );
                }
            }
        },
        {
            ItemBattleEffectID.AssaultVest, new()
            {
                ID = ItemBattleEffectID.AssaultVest,

                OnItemEnter = ( unit ) =>
                {
                    unit.Pokemon.ApplyDirectStatModifier( Stat.SpDefense, DirectModifierCause.AssaultVest, 1.5f );
                },

                OnItemExit = ( unit ) =>
                {
                    unit.Pokemon.RemoveDirectStatModifier( Stat.SpDefense, DirectModifierCause.AssaultVest );
                }
            }
        }
    };
}

public enum ItemBattleEffectID
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
    AirBalloon,
    MetalCoat,
    AssaultVest,
    LumBerry,
    SafetyGoggles,
    Eviolite,
    MentalHerb,
    CovertCloak,
}
