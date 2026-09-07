using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MoveConditionDB
{
    public static Dictionary<string, MoveCondition> Conditions { get; private set; }

    public static void Init()
    {
        SetDictionary();

        //--Auto sets the name/id of each entry
        foreach( var kvp in Conditions )
        {
            var name = kvp.Key;
            var success = kvp.Value;

            success.Name = name;
        }
    }

    public static void SetDictionary()
    {
        Conditions = new()
        {
            {
                "False Swipe", new()
                {
                    OnModifyMoveDamage = ( BattleUnit attacker, BattleUnit target, Move move, int damage ) =>
                    {
                        if( target.Pokemon.CurrentHP - damage <= 0 )
                        {
                            damage -= 1;
                            return damage;
                        }
                        else
                            return damage;
                    },
                }
            },
            {
                "Brick Break", new()
                {
                    OnMoveHitTarget = ( BattleUnit attacker, BattleUnit target, Move move, int damage, int hits, BattleSystem bs ) =>
                    {
                        var field = bs.Field;
                        var targetCourt = field.GetUnitCourt( target );
                        List<string> endMessages = new();

                        //--Reflect
                        if( targetCourt.Conditions.ContainsKey( CourtConditionID.Reflect ) )
                        {
                            var condition = targetCourt.Conditions[CourtConditionID.Reflect];

                            for( int i = 0; i < targetCourt.Units.Count; i++ )
                            {
                                var unit = targetCourt.Units[i];
                                if( condition.EndMessage != null )
                                {
                                    condition?.OnEnd?.Invoke( bs, field );
                                    endMessages.Add( condition.EndMessage );
                                }

                                condition?.OnExitCourt?.Invoke( unit, field );
                            }

                            targetCourt.RemoveCondition( CourtConditionID.Reflect );
                        }

                        //--Light Screen
                        if( targetCourt.Conditions.ContainsKey( CourtConditionID.LightScreen ) )
                        {
                            var condition = targetCourt.Conditions[CourtConditionID.LightScreen];

                            for( int i = 0; i < targetCourt.Units.Count; i++ )
                            {
                                var unit = targetCourt.Units[i];
                                if( condition.EndMessage != null )
                                {
                                    condition?.OnEnd?.Invoke( bs, field );
                                    endMessages.Add( condition.EndMessage );
                                }

                                condition?.OnExitCourt?.Invoke( unit, field );
                            }

                            targetCourt.RemoveCondition( CourtConditionID.LightScreen );
                        }

                        //--Aurora Veil
                        if( targetCourt.Conditions.ContainsKey( CourtConditionID.AuroraVeil ) )
                        {
                            var condition = targetCourt.Conditions[CourtConditionID.AuroraVeil];

                            for( int i = 0; i < targetCourt.Units.Count; i++ )
                            {
                                var unit = targetCourt.Units[i];
                                if( condition.EndMessage != null )
                                {
                                    condition?.OnEnd?.Invoke( bs, field );
                                    endMessages.Add( condition.EndMessage );
                                }

                                condition?.OnExitCourt?.Invoke( unit, field );
                            }

                            targetCourt.RemoveCondition( CourtConditionID.AuroraVeil );
                        }

                        if( endMessages.Count > 0 )
                        {

                            if( endMessages.Count > 1 )
                                bs.AddDialogue( "It shattered the barriers!" );
                            else
                                bs.AddDialogue( "It shattered the barrier!" );

                            for( int i = 0; i < endMessages.Count; i++ )
                            {
                                bs.AddDialogue( endMessages[i] );
                            }
                        }
                    },
                }
            },
            {
                "Sleep Talk", new()
                {
                    OnMoveChanged = ( BattleUnit user, Move move, BattleSystem bs ) =>
                    {
                        var changed = user.Pokemon.GetRandomMoveExcluding( move );
                        bs.AddDialogue( $"{user.Pokemon.NickName} used {changed.MoveSO.Name}!" );
                        
                        return changed;
                    }
                }
            },
            {
                "Flail", new()
                {
                    OnModifyMovePower = ( BattleUnit attacker, BattleUnit target, Move move, int hits ) =>
                    {
                        float currentHP = attacker.Pokemon.CurrentHP;
                        float maxHP = attacker.Pokemon.MaxHP;
                        float hp = currentHP / maxHP;
                        int power;

                        if( hp >= 68.8 )        power = 20;
                        else if( hp >= 35.4 )   power = 40;
                        else if( hp >= 20.8 )   power = 80;
                        else if( hp >= 10.4 )   power = 100;
                        else if( hp >= 4.2 )    power = 150;
                        else                    power = 200;

                        return power;
                    }
                }
            },
            {
                "Splash", new()
                {
                    OnMoveSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        bs.AddDialogue( $"... But nothing happened..." );
                    }
                }
            },
            {
                "Avalanche", new()
                {
                    OnModifyMovePower = ( BattleUnit attacker, BattleUnit target, Move move, int hits ) =>
                    {
                        int power = 120;
                        if( target.DamagedBy != null && target.DamagedBy == attacker.Pokemon )
                            return power;
                        else
                            return move.MovePower;
                    }
                }
            },
            {
                "Psych Up", new()
                {
                    OnMoveHitTarget = ( BattleUnit attacker, BattleUnit target, Move move, int damage, int hits, BattleSystem bs ) =>
                    {
                        attacker.Pokemon.ResetStatChanges();
                        var copyChanges = target.Pokemon.StatStages;
                        List<StatStage> changes = new();

                        foreach( var kvp in copyChanges )
                        {
                            changes.Add( new() { Stat = kvp.Key, Change = kvp.Value } );
                        }

                        if( changes.Count > 0 )
                        {
                            StageChangeSource source = new()
                            {
                                Pokemon = target.Pokemon,
                                MoveName = "Psych Up",
                                Source = StageChangeSourceType.Move
                            };

                            attacker.Pokemon.ApplyStatStageChange( changes, source );
                        }
                    }
                }
            },
            {
                "Triple Axel", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        if( hit == 1 )      return 20;
                        else if( hit == 2 ) return 40;
                        else if( hit == 3 ) return 60;
                        else                return move.MovePower;
                    }
                }
            },
            {
                "Haze", new()
                {
                    OnMoveHitTarget = ( BattleUnit attacker, BattleUnit target, Move move, int damage, int hit, BattleSystem bs ) =>
                    {
                        var activeUnits = bs.GetActiveUnits();

                        for( int i = 0; i < activeUnits.Count; i++ )
                        {
                            var unit = activeUnits[i];
                            unit.Pokemon.ResetStatChanges();
                            unit.Pokemon.AddStatusEvent( StatusEventType.Text, $"{unit.Pokemon.NickName} had its stat stage changes cleared!" );
                        }
                    },
                }
            },
            {
                "Growth", new()
                {
                    OnStatStageChange = ( Dictionary<Stat, int> stages, Pokemon attacker, Pokemon target ) =>
                    {
                        var bs = BattleSystem.Instance;
                        if( bs == null )
                            return;

                        if( bs.Field.Weather?.ID != WeatherConditionID.Sun )
                            return;

                        Debug.Log( $"It's sunny! Growth will increase by 2 instead of 1!" );
                        
                        List<Stat> removeChanges = new();

                        foreach( var kvp in stages )
                        {
                            if( kvp.Key == Stat.Attack || kvp.Key == Stat.SpAttack )
                                removeChanges.Add( kvp.Key );
                        }

                        for( int i = 0; i < removeChanges.Count; i++ )
                        {
                            var stat = removeChanges[i];
                            removeChanges.Remove( stat );
                        }

                        List<StatStage> improvedGrowth = new()
                        {
                            new(){ Stat = Stat.Attack, Change = 2 },
                            new(){ Stat = Stat.SpAttack, Change = 2 },
                        };

                        StageChangeSource source = new()
                        {
                            Pokemon = attacker,
                            MoveName = "Growth",
                            Source = StageChangeSourceType.Move
                        };

                        attacker.ApplyStatStageChange( improvedGrowth, source );
                    },
                }
            },
            {
                "Synthesis", new()
                {
                    OnMoveSuccess = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( bs.Field.Weather?.ID == WeatherConditionID.None )
                            return;

                        else if( bs.Field.Weather?.ID == WeatherConditionID.Sun )
                            move.OverrideHealing( 66 );

                        else
                            move.OverrideHealing( 25 );
                    },

                    OnMoveCompleted = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( move.HealAmount != move.MoveSO.HealAmount )
                            move.OverrideHealing( move.MoveSO.HealAmount );
                    }
                }
            },
            {
                "Mortal Spin", new()
                {
                    OnMoveCompleted = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        var court = bs.Field.GetUnitCourt( attacker );
                        List<CourtConditionID> remove = new();

                        foreach( var effect in court.Conditions )
                        {
                            if( effect.Value.ConditionType == ConditionType.OpposingSide_Hazard )
                            {
                                remove.Add( effect.Key );
                            }
                            else
                                continue;
                        }

                        if( remove.Count == 0 )
                            return;

                        for( int i = 0; i < remove.Count; i++ )
                        {
                            court.RemoveCondition( remove[i] );
                        }

                        bs.AddDialogue( $"{attacker.Pokemon.NickName} has cleared the hazards from its side of the field!" );
                    }
                }
            },
            {
                "Night Shade", new()
                {
                    OnModifyMoveDamage = ( BattleUnit attacker, BattleUnit target, Move move, int damage ) =>
                    {
                        return attacker.Pokemon.Level;
                    }
                }
            },
            {
                "Spite", new()
                {
                    OnMoveHitTarget = ( BattleUnit attacker, BattleUnit target, Move move, int damage, int hits, BattleSystem bs ) =>
                    {
                        if( target.LastUsedMove == null )
                            return;

                        int pp = target.LastUsedMove.PP;
                        target.LastUsedMove.PP = Mathf.Max( 0, pp - 4 );
                    }
                }
            },
            {
                "Payback", new()
                {
                    OnModifyMovePower = ( BattleUnit attacker, BattleUnit target, Move move, int hit ) =>
                    {
                        if( target.Flags[UnitFlags.CompletedTurn].IsActive )
                            return move.MovePower * 2;
                        else
                            return move.MovePower;
                    }
                }
            },
            {
                //--Knock off has a lot of interesting interactions. check back regularly as you add things --02/09/26
                "Knock Off", new()
                {
                    OnModifyMovePower = ( BattleUnit attacker, BattleUnit target, Move move, int hit ) =>
                    {
                        if( target.Pokemon.HeldItem != null )
                            return Mathf.FloorToInt( move.MovePower * 1.5f );
                        else
                            return move.MovePower;
                    },

                    OnMoveCompleted = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.Pokemon.HeldItem != null && !attacker.Pokemon.IsFainted )
                        {
                            if( target.Pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.ChoiceLocked ) )
                                target.Pokemon.CureVolatileStatus( VolatileConditionID.ChoiceLocked );
                                
                            target.Pokemon.RemoveHeldItem();
                            target.Pokemon.AddStatusEvent( StatusEventType.Text, $"{target.Pokemon.NickName} had its item knocked away!" ); //--Add knock off status even for unique anim??
                        }
                    }
                }
            },
            {
                "Hex", new()
                {
                    OnModifyMovePower = ( BattleUnit attacker, BattleUnit target, Move move, int hit ) =>
                    {
                        if( target.Pokemon.SevereStatus != null )
                            return 130;
                        else
                            return move.MovePower;
                    }
                }
            },
            {
                "Curse", new()
                {

                    OnMoveHitTarget = ( BattleUnit attacker, BattleUnit target, Move move, int damage, int hits, BattleSystem bs ) =>
                    {
                        if( attacker.Pokemon.CheckTypes( PokemonType.Ghost ) )
                        {
                            int hpLoss = Mathf.FloorToInt( attacker.Pokemon.MaxHP / 2 );
                            attacker.Pokemon.DecreaseHP( hpLoss );
                            attacker.Pokemon.AddStatusEvent( StatusEventType.Damage, $"{attacker.Pokemon.NickName} cut its HP to curse {target.Pokemon.NickName}!" );

                            StatusEffectSource source = new()
                            {
                                Pokemon = attacker.Pokemon,
                                Source = EffectSource.Move,
                            };

                            target.Pokemon.SetVolatileStatus( VolatileConditionID.Cursed, source );
                        }
                        else
                        {
                            List<StatStage> curseChanges = new()
                            {
                                new() { Stat = Stat.Speed,      Change = -1 },
                                new() { Stat = Stat.Attack,     Change = 1 },
                                new() { Stat = Stat.Defense,    Change = 1 },
                            };

                            StageChangeSource source = new()
                            {
                                Pokemon = attacker.Pokemon,
                                MoveName = "Curse",
                                Source = StageChangeSourceType.Move,
                            };

                            attacker.Pokemon.ApplyStatStageChange( curseChanges, source );
                        }
                    }
                }
            },
            {
                "Thief", new()
                {
                    OnMoveCompleted = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.Pokemon.HeldItem == null ) 
                            return;
                        if( attacker.Pokemon.IsFainted )
                            return;

                        if( attacker.Pokemon.HeldItem != null )
                            return;

                        var item = target.Pokemon.HeldItem;
                        attacker.Pokemon.GiveHeldItem( item );
                        target.Pokemon.RemoveHeldItem();

                        target.Pokemon.AddStatusEvent( StatusEventType.Text, $"{target.Pokemon.NickName} had its {item.ItemName} stolen!" );
                    }
                }
            },
            {
                "Stored Power", new()
                {
                    OnModifyMovePower = ( BattleUnit attacker, BattleUnit target, Move move, int damage ) =>
                    {
                        if( attacker.Pokemon.StatStages.Count <= 0 )
                            return move.MovePower;

                        int power = move.MovePower; //--20 base

                        foreach( var kvp in attacker.Pokemon.StatStages )
                        {
                            if( kvp.Value > 0 )
                                power += 20;
                        }

                        return power;
                    }
                }
            },
            {
                "Psyshock", new()
                {
                    OnOverrideDefensiveStat = ( BattleUnit attacker, BattleUnit target, Move move ) =>
                    {
                        return target.Pokemon.Defense;
                    }
                }
            },
            {
                "Body Press", new()
                {
                    OnOverrideAttackingStat = ( BattleUnit attacker, BattleUnit target, Move move ) =>
                    {
                        Debug.Log( $"Body Press detected, using defense!" );
                        return attacker.Pokemon.Defense;
                    }
                }
            },
            {
                "Foul Play", new()
                {
                    OnOverrideAttackingStat = ( BattleUnit attacker, BattleUnit target, Move move ) =>
                    {
                        //--Target's Base Attack
                        float targetAttack = target.Pokemon.Stats[Stat.Attack];

                        //--User's Attack Modifiers
                        float attackerModifiers = attacker.Pokemon.DirectStatModifiers[Stat.Attack].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm );

                        //--Get and calculate target's attack stage changes.
                        int stage = target.Pokemon.StatStages[Stat.Attack];
                        var stageModifier = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f };

                        if( stage >= 0 )
                            targetAttack *= stageModifier[stage];
                        else
                            targetAttack /= stageModifier[-stage];

                        targetAttack *= attackerModifiers;
                        int foulPlay = Mathf.FloorToInt( targetAttack );

                        return foulPlay;
                    }
                }
            },
            {
                "Skill Swap", new()
                {
                    OnMoveHitTarget = ( BattleUnit attacker, BattleUnit target, Move move, int damage, int hit, BattleSystem bs ) =>
                    {
                        AbilityID attackerAbility = attacker.Pokemon.AbilityID;
                        AbilityID targetAbility = target.Pokemon.AbilityID;

                        attacker.Pokemon.MakeAbility( targetAbility );
                        target.Pokemon.MakeAbility( attackerAbility );

                        attacker.SetFlagActive( UnitFlags.AbilityChanged, true );
                        target.SetFlagActive( UnitFlags.AbilityChanged, true );

                        bs.TriggerAbilityCutIn( attacker.Pokemon );
                        attacker.Pokemon.Ability?.OnAbilityEnter?.Invoke( attacker.Pokemon, bs.GetOpposingUnits( attacker ), bs.Field );

                        bs.TriggerAbilityCutIn( target.Pokemon );
                        target.Pokemon.Ability?.OnAbilityEnter?.Invoke( target.Pokemon, bs.GetOpposingUnits( target ), bs.Field );
                    }
                }
            },
            {
                "Trick", new()
                {
                    OnMoveHitTarget = ( BattleUnit attacker, BattleUnit target, Move move, int damage, int hit, BattleSystem bs ) =>
                    {
                        var attackerItem = attacker.Pokemon.HeldItem;
                        var targetItem = target.Pokemon.HeldItem;

                        attacker.Pokemon.RemoveHeldItem();
                        target.Pokemon.RemoveHeldItem();

                        attacker.Pokemon.GiveHeldItem( targetItem );
                        target.Pokemon.GiveHeldItem( attackerItem );

                        bs.AddDialogue( $"{attacker.Pokemon.NickName} swapped items with {target.Pokemon.NickName}!" );
                    }
                }
            },
            {
                "Phantom Force", new()
                {
                    OnMoveCompleted = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.Pokemon.TransientStatus != null && target.Pokemon.TransientStatus.ID == TransientConditionID.Protect )
                        {
                            target.Pokemon.CureTransientStatus();
                            target.Pokemon.AddStatusEvent( StatusEventType.Text, $"{target.Pokemon.NickName} had its protective field broken!" );
                        }
                    }
                }
            },
            {
                "Burning Jealousy", new()
                {
                    OnMoveCompleted = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.Flags[UnitFlags.IncreasedStatStage].IsActive )
                        {
                            StatusEffectSource source = new()
                            {
                                Pokemon = attacker.Pokemon,
                                Source = EffectSource.Move,
                            };

                            target.Pokemon.SetSevereStatus( SevereConditionID.BRN, source );
                        }
                    }
                }
            },
            {
                "Lash Out", new()
                {
                    OnModifyMovePower = ( BattleUnit attacker, BattleUnit target, Move move, int hits ) =>
                    {
                        if( target.Flags[UnitFlags.LoweredStatStage].IsActive )
                            return move.MovePower * 2;
                        else
                            return move.MovePower;
                    }
                }
            },
            {
                "Pain Split", new()
                {
                    OnMoveCompleted = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        var attHP = attacker.Pokemon.CurrentHP;
                        var tarHP = target.Pokemon.CurrentHP;
                        float average = Mathf.FloorToInt( ( attHP + tarHP ) / 2 );

                        var attFinal = Mathf.FloorToInt( average - attHP );
                        attacker.Pokemon.SetHP( attFinal );

                        if( attFinal > 0 )
                            attacker.Pokemon.AddStatusEvent( StatusEventType.Heal, string.Empty );
                        else if( attFinal < 0 )
                            attacker.Pokemon.AddStatusEvent( StatusEventType.Damage, string.Empty );

                        var tarFinal = Mathf.FloorToInt( average - tarHP );
                        target.Pokemon.SetHP( tarFinal );

                        if( tarFinal > 0 )
                            target.Pokemon.AddStatusEvent( StatusEventType.Heal, string.Empty );
                        else if( tarFinal < 0 )
                            target.Pokemon.AddStatusEvent( StatusEventType.Damage, string.Empty );

                        bs.AddDialogue( $"{attacker.Pokemon.NickName} split their HP with {target.Pokemon.NickName}!" );
                    }
                }
            },
            {
                "Feint", new()
                {
                    OnMoveCompleted = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        if( target.Pokemon.TransientStatus != null && target.Pokemon.TransientStatus.ID == TransientConditionID.Protect )
                        {
                            target.Pokemon.CureTransientStatus();
                            target.Pokemon.AddStatusEvent( StatusEventType.Text, $"{target.Pokemon.NickName} had its protective field broken!" );
                        }
                    }
                }
            },
            {
                "Counter", new()
                {
                    OnModifyMoveDamage = ( BattleUnit attacker, BattleUnit target, Move move, int damage ) =>
                    {
                        var tookDamage = target.Flags[UnitFlags.TookDamage];
                        return tookDamage.Count * 2;
                    },

                    OnTargetRedirect = ( BattleUnit attacker, BattleUnit target, Move move, BattleSystem bs ) =>
                    {
                        var newTarget = target.Flags[UnitFlags.TookDamage];
                        return newTarget.Attacker;
                    }
                }
            },
            {
                "Copycat", new()
                {
                    OnMoveChanged = ( BattleUnit attacker, Move move, BattleSystem bs ) =>
                    {
                        return bs.LastUsedMove;
                    }
                }
            },
            {
                "Final Gambit", new()
                {
                    OnModifyMoveDamage = ( BattleUnit attacker, BattleUnit target, Move move, int damage ) =>
                    {
                        var attHP = attacker.Pokemon.CurrentHP;
                        attacker.Pokemon.DecreaseHP( attHP );
                        attacker.Pokemon.AddStatusEvent( StatusEventType.Damage, string.Empty );

                        return attHP;
                    }
                }
            },
            {
                "Reversal", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        float currentHP = attacker.Pokemon.CurrentHP;
                        float maxHP = attacker.Pokemon.MaxHP;
                        float hp = currentHP / maxHP;
                        int power;

                        if( hp >= 68.8 )        power = 20;
                        else if( hp >= 35.4 )   power = 40;
                        else if( hp >= 20.8 )   power = 80;
                        else if( hp >= 10.4 )   power = 100;
                        else if( hp >= 4.2 )    power = 150;
                        else                    power = 200;

                        return power;
                    }
                }
            },
            {
                "Low Kick", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        float w = target.Pokemon.PokeSO.Weight;
                        int power;

                        if( w <= 21.8f )        power = 20;
                        else if( w <= 54.9f )   power = 40;
                        else if( w <= 110f )    power = 60;
                        else if( w <= 220f )    power = 80;
                        else if( w <= 440.7 )   power = 100;
                        else                    power = 120;

                        return power;
                    }
                }
            },
            {
                "After You", new()
                {
                    OnModifyCommandQueue = ( attacker, target, bs ) =>
                    {
                        if( bs.TryGetPokemonCommand( target.Pokemon, out var command ) )
                        {
                            command.SetAfterYou();
                        }
                    }
                }
            },
            {
                "Quash", new()
                {
                    OnModifyCommandQueue = ( attacker, target, bs ) =>
                    {
                        if( bs.TryGetPokemonCommand( target.Pokemon, out var command ) )
                        {
                            command.SetQuash();
                        }
                    }
                }
            },
            {
                "Wish", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        Debug.Log( $"[Wish] OnMoveCompelted" );
                        attacker.SetWish( attacker, move );
                    },

                    OnAfterRound = ( unit, move, bs ) =>
                    {
                        Debug.Log( $"[Wish] OnAfterRound" );
                        var wish = unit.Flags[UnitFlags.Wish];
                        if( wish.Count > 0 )
                        {
                            // Debug.Log( $"[Wish] Wish Count is: {wish.Count}" );
                            unit.Flags[UnitFlags.Wish] = wish;
                            wish.Count--;
                            return;
                        }
                        else
                        {
                            var heal = Mathf.CeilToInt( wish.User.MaxHP / 2 );
                            unit.Pokemon.IncreaseHP( heal );
                            unit.Pokemon.AddStatusEvent( StatusEventType.Heal, $"{ wish.User.NickName}'s wish came true!" );
                            unit.ClearWish();
                        }
                    }
                }
            },
            {
                "Baton Pass", new()
                {
                    OnMoveSuccess = ( attacker, target, move, bs ) =>
                    {
                        //--Store Baton pass info in the flag
                        attacker.SetBatonPass( attacker );
                    },

                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        //--Extract baton pass info from the flag and give to what SHOULD be the new unit
                        var pass = attacker.Flags[UnitFlags.BatonPass];
                        var prevMon = pass.User;
                        var statChanges = pass.StatStages;
                        var volatileStatuses = pass.VolatileStatuses;

                        //--Pass Stat Stage Changes
                        if( statChanges != null && statChanges.Count > 0)
                        {
                            StageChangeSource stageChangeSource = new()
                            {
                                Pokemon = prevMon,
                                Source = StageChangeSourceType.Move,
                                MoveName = "Baton Pass",
                            };

                            attacker.Pokemon.ApplyStatStageChange( statChanges, stageChangeSource );
                        }
                        
                        //--Pass Volatile Statuses
                        if( volatileStatuses != null && volatileStatuses.Count > 0 )
                        {
                            foreach( var kvp in volatileStatuses )
                            {
                                StatusEffectSource statusSource = new()
                                {
                                    Pokemon = pass.User,
                                    Source = EffectSource.Move,
                                };

                                attacker.Pokemon.SetVolatileStatus( kvp.Key, statusSource, kvp.Value.Duration );
                            }
                        }

                        attacker.ClearBatonPass();
                    }
                }
            },
            {
                "Future Sight", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        target.SetFutureSight( attacker, move );
                        bs.AddDialogue( $"{attacker.Pokemon.NickName} foresaw an attack!" );
                    },

                    OnAfterRound = ( unit, move, bs ) =>
                    {
                        var fs = unit.Flags[UnitFlags.FutureSight];
                        if( fs.Count > 0 )
                        {
                            fs.Count--;
                            return;
                        }
                        else
                        {
                            var cacheAttacker = fs.Attacker.Pokemon;
                            fs.Attacker.TempUsage( fs.User );
                            unit.TakeDamage( fs.Move, fs.Attacker, bs.Field.Weather, bs.Field.Terrain, 1, 1 );
                            unit.Pokemon.AddStatusEvent( StatusEventType.Heal, $"{ fs.User.NickName} was hurt by the predicited attack!" );
                            unit.ClearWish();
                            fs.Attacker.TempUsage( cacheAttacker );
                        }
                    }
                }
            },
            {
                "Uproar", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        var activeUnits = bs.GetActiveUnits();

                        for( int i = 0; i < activeUnits.Count; i++ )
                        {
                            var unit = activeUnits[i];
                            if( unit.Pokemon.SevereStatus != null && unit.Pokemon.SevereStatus?.ID == SevereConditionID.SLP )
                                unit.Pokemon.CureSevereStatus();
                            else
                                continue;
                        }
                    }
                }
            },
            {
                "Grass Knot", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        float w = target.Pokemon.PokeSO.Weight;
                        int power;

                        if( w <= 21.8f )        power = 20;
                        else if( w <= 54.9f )   power = 40;
                        else if( w <= 110f )    power = 60;
                        else if( w <= 220f )    power = 80;
                        else if( w <= 440.7 )   power = 100;
                        else                    power = 120;

                        return power;
                    }
                }
            },
            {
                "Retaliate", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        //--If previous pokemon fainted, double power to 140.
                        //--I guess i'll add a unit flag that tracks if a pokemon fainted the previous turn.
                        if( attacker.Flags[UnitFlags.FaintedPreviousTurn].IsActive )
                            return 140;
                        else
                            return move.MovePower;
                    }
                }
            },
            {
                "Heavy Slam", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        //--Does damage relative to how much heavier the user is. or, rather, how much lighter the target is, techncially. i guess we get a ratio of weights and then use the chart.
                        float weightDiff = attacker.Pokemon.PokeSO.Weight / target.Pokemon.PokeSO.Weight;
                        int power = 40;

                        if( weightDiff > 0.5f )             power = 40;
                        else if( weightDiff > 0.3335f )     power = 60;
                        else if( weightDiff > 0.2501f )     power = 80;
                        else if( weightDiff > 0.2001f )     power = 100;
                        else                                power = 120;

                        Debug.Log( $"Attacker {attacker.Pokemon.NickName}'s Weight: {attacker.Pokemon.PokeSO.Weight} / Target {target.Pokemon.NickName}'s Weight: {target.Pokemon.PokeSO.Weight} = {weightDiff}. Power: {power}" );

                        return power;     
                    }
                }
            },
            {
                "Hard Press", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        float currentHP = target.Pokemon.CurrentHP;
                        float maxHP = target.Pokemon.MaxHP;
                        int power = Mathf.RoundToInt( 100f * ( currentHP / maxHP ) );

                        return power;
                    }
                }
            },
            {
                "Venoshock", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        if( target.Pokemon.SevereStatus?.ID == SevereConditionID.PSN || target.Pokemon.SevereStatus?.ID == SevereConditionID.TOX )
                            return move.MovePower * 2;
                        else
                            return move.MovePower;
                    }
                }
            },
            {
                "Covet", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        if( attacker.Pokemon.CurrentHP > 0 && attacker.Pokemon.HeldItem == null && target.Pokemon.HeldItem != null )
                            attacker.Pokemon.GiveHeldItem( target.Pokemon.HeldItem );
                    }
                }
            },
            {
                "Echoed Voice", new()
                {
                    OnMoveSuccess = ( attacker, target, move, bs ) =>
                    {
                        attacker.Flags[UnitFlags.EchoedVoiceCount].Count++;
                    },

                    OnModifyMovePower = ( attacker, target, move, hits ) =>
                    {
                        var count = attacker.Flags[UnitFlags.EchoedVoiceCount].Count;
                        return count switch
                        {
                            0 => 40,
                            1 => 80,
                            2 => 120,
                            3 => 160,
                            >= 4 => 200,
                            _ => 40,
                        };
                    }
                }
            },
            {
                "Healing Wish", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        //--do this nonsense some other time
                    }
                }
            },
            {
                "Weather Ball", new()
                {
                    
                }
            },
            {
                "Aromatherapy", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        var allyParty = bs.GetAllyParty( attacker.Pokemon );
                        bool cured = false;
                        for( int i = 0; i < allyParty.Count; i++ )
                        {
                            var mon = allyParty[i];
                            if( mon.SevereStatus?.ID != SevereConditionID.None )
                            {
                                mon.CureSevereStatus();
                                cured = true;
                            }
                        }

                        if( cured )
                        {
                            bs.AddDialogue( $"{attacker.Pokemon.NickName} cured its entire party of severe status conditions!" );
                        }
                    }
                }
            },
            {
                "Seismic Toss", new()
                {
                    OnModifyMoveDamage = ( attacker, target, move, damage ) =>
                    {
                        return attacker.Pokemon.Level;
                    }
                }
            },
            {
                "Beat Up", new()
                {
                    OnModifyMoveHitCount = ( attacker, target, move, bs ) =>
                    {
                        var allyParty = bs.GetAllyParty( attacker.Pokemon );
                        int hits = 1;
                        for( int i = 0; i < allyParty.Count; i++ )
                        {
                            var mon = allyParty[i];
                            if( !mon.IsFainted && ( mon.SevereStatus == null || mon.SevereStatus?.ID != SevereConditionID.None ) )
                                hits++;
                        }

                        return hits;
                    },

                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        var allyParty = BattleSystem.Instance.GetAllyParty( attacker.Pokemon );
                        List<Pokemon> beatUppers = new();

                        for( int i = 0; i < allyParty.Count; i++ )
                        {
                            var mon = allyParty[i];
                            if( !mon.IsFainted && ( mon.SevereStatus == null || mon.SevereStatus?.ID != SevereConditionID.None ) )
                            {
                                beatUppers.Add( mon );
                            }
                        }

                        int attackNumber = Mathf.Max( 0, hit - 1 );
                        var attackingAlly = beatUppers[attackNumber];

                        int power = ( attackingAlly.PokeSO.Attack / 10 ) + 5;

                        return power;
                    }
                }
            },
            {
                "Assurance", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        if( target.Flags[UnitFlags.TookDamage].IsActive )
                            return move.MovePower * 2;
                        else
                            return move.MovePower;
                    }
                }
            },
            {
                "Ice Spinner", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        if( attacker.Pokemon.CurrentHP > 0 )
                        {
                            if( bs.Field.Terrain?.ID != TerrainID.None )
                            {
                                bs.Field.Terrain.ID = TerrainID.None;
                                bs.AddDialogue( $"{attacker.Pokemon.NickName} cleared the terrain!" );
                            }
                        }
                    }
                }
            },
            {
                "Fury Cutter", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        return move.ConsecutiveUses switch
                        {
                            0 => 40,
                            1 => 80,
                            2 => 120,
                            >= 3 => 160,
                            _ => 40,
                        };
                    }
                }
            },
            {
                "Acrobatics", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        if( attacker.Pokemon.HeldItem == null )
                            return move.MovePower * 2;
                        else
                            return move.MovePower;
                    }
                }
            },
            {
                "Bug Bite", new()
                {
                    OnMoveHitTarget = ( attacker, target, move, damage, hit, bs ) =>
                    {
                        if( target.Pokemon.HeldItem != null && target.Pokemon.HeldItem.ItemCategory == ItemCategory.Berry )
                        {
                            //--idk yet. we'll return to this.
                        }
                    }
                }
            },
            {
                "Brine", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        if( target.Pokemon.IsBelowHPPercent( 50 ) )
                        {
                            return move.MovePower * 2;
                        }
                        else
                            return move.MovePower;
                    }
                }
            },
            {
                "Conversion", new()
                {
                    OnMoveSuccess = ( attacker, target, move, bs ) =>
                    {
                        attacker.Pokemon.TempChangeType( attacker.Pokemon.ActiveMoves[0].MoveType );
                    }
                }
            },
            {
                "Conversion 2", new()
                {
                    OnMoveSuccess = ( attacker, target, move, bs ) =>
                    {
                        var targetLastMove = target.LastUsedMove;

                        if( targetLastMove == null )
                            return;

                        var types = ( PokemonType[] )Enum.GetValues( typeof(PokemonType) );
                        List<PokemonType> resistances = new();

                        foreach( var type in types )
                        {
                            if( attacker.Pokemon.CheckTypes( type ) )
                                continue;

                            float effectiveness = TypeChart.GetTotalEffectiveness( targetLastMove.MoveType, attacker.Pokemon.Type1, attacker.Pokemon.Type2 );
                            if( effectiveness < 0 )
                                resistances.Add( type );
                        }

                        if( resistances.Count > 0 )
                        {
                            int r = UnityEngine.Random.Range( 0, resistances.Count );
                            attacker.Pokemon.TempChangeType( resistances[r] );
                        }
                        else
                            return;
                    }
                }
            },
            {
                "Magnet Rise", new()
                {
                    OnMoveSuccess = ( attacker, target, move, bs ) =>
                    {
                        attacker.SetFlagActive( UnitFlags.Ungrounded, true );
                    }
                }
            },
            {
                "Lock-On", new()
                {
                    OnMoveSuccess = ( attacker, target, move, bs ) =>
                    {
                        attacker.SetLockOn( target );
                    }
                }
            },
            {
                "Power Swap", new()
                {
                    OnMoveSuccess = ( attacker, target, move, bs ) =>
                    {
                        Dictionary<Stat, int> targetNewStatStages = new();
                        Dictionary<Stat, int> attackerNewStatStages = new();

                        foreach( var sc in target.Pokemon.StatStages )
                        {
                            if( sc.Key == Stat.Attack || sc.Key == Stat.SpAttack )
                                attackerNewStatStages.Add( sc.Key, sc.Value );
                        }

                        foreach( var sc in attacker.Pokemon.StatStages )
                        {
                            if( sc.Key == Stat.Attack || sc.Key == Stat.SpAttack )
                                targetNewStatStages.Add( sc.Key, sc.Value );
                        }

                        foreach( var sc in target.Pokemon.StatStages )
                        {
                            if( sc.Key == Stat.Attack || sc.Key == Stat.SpAttack )
                                continue;

                            targetNewStatStages.Add( sc.Key, sc.Value );
                        }

                        foreach( var sc in attacker.Pokemon.StatStages )
                        {
                            if( sc.Key == Stat.Attack || sc.Key == Stat.SpAttack )
                                continue;

                            attackerNewStatStages.Add( sc.Key, sc.Value );
                        }

                        attacker.Pokemon.ReplaceStatStages( attackerNewStatStages );
                        target.Pokemon.ReplaceStatStages( targetNewStatStages );
                        bs.AddDialogue( $"{attacker.Pokemon.NickName} swapped its offensive stat changes with {target.Pokemon.NickName}!" );
                    }
                }
            },
            {
                "Guard Swap", new()
                {
                    OnMoveSuccess = ( attacker, target, move, bs ) =>
                    {
                        Dictionary<Stat, int> targetNewStatStages = new();
                        Dictionary<Stat, int> attackerNewStatStages = new();

                        foreach( var sc in target.Pokemon.StatStages )
                        {
                            if( sc.Key == Stat.Defense || sc.Key == Stat.SpDefense )
                                attackerNewStatStages.Add( sc.Key, sc.Value );
                        }

                        foreach( var sc in attacker.Pokemon.StatStages )
                        {
                            if( sc.Key == Stat.Defense || sc.Key == Stat.SpDefense )
                                targetNewStatStages.Add( sc.Key, sc.Value );
                        }

                        foreach( var sc in target.Pokemon.StatStages )
                        {
                            if( sc.Key == Stat.Defense || sc.Key == Stat.SpDefense )
                                continue;

                            targetNewStatStages.Add( sc.Key, sc.Value );
                        }

                        foreach( var sc in attacker.Pokemon.StatStages )
                        {
                            if( sc.Key == Stat.Defense || sc.Key == Stat.SpDefense )
                                continue;

                            attackerNewStatStages.Add( sc.Key, sc.Value );
                        }

                        attacker.Pokemon.ReplaceStatStages( attackerNewStatStages );
                        target.Pokemon.ReplaceStatStages( targetNewStatStages );
                        bs.AddDialogue( $"{attacker.Pokemon.NickName} swapped its defensive stat changes with {target.Pokemon.NickName}!" );
                    }
                }
            },
            {
                "Stomp", new()
                {
                    OnModifyMoveDamage = ( attacker, target, move, damage ) =>
                    {
                        if( target.Pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.Minimize ) )
                            return damage * 2;
                        else
                            return damage;
                    }
                }
            },
            {
                "Endeavor", new()
                {
                    OnModifyMoveDamage = ( attacker, target, move, damage ) =>
                    {
                        return target.Pokemon.CurrentHP - attacker.Pokemon.CurrentHP;
                    }
                }
            },
            {
                "Substitute", new()
                {
                    OnMoveCompleted = ( attacker, target, move, damage ) =>
                    {
                        int subDamage = Mathf.FloorToInt( attacker.Pokemon.MaxHP * 0.25f );

                        attacker.Pokemon.DecreaseHP( subDamage );
                        attacker.Pokemon.AddStatusEvent( StatusEventType.Damage, string.Empty );

                        int hp = Mathf.FloorToInt( attacker.Pokemon.MaxHP * 0.25f );
                        attacker.SetSubstitute( hp );
                    }
                }
            },
            {
                "Shed Tail", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        //--apply effects of shed tail manually.
                        int subDamage = Mathf.FloorToInt( attacker.Pokemon.MaxHP * 0.5f );
                        int hp = attacker.Pokemon.CurrentHP;

                        attacker.Pokemon.DecreaseHP( subDamage );
                        attacker.Pokemon.AddStatusEvent( StatusEventType.Damage, string.Empty );
                    },

                    OnMoveEffectsCompleted = ( attacker, target, move, bs ) =>
                    {
                        StatusEffectSource source = new()
                        {
                            Pokemon = attacker.Pokemon,
                            Source = EffectSource.Move,
                        };
                            
                        attacker.Pokemon.SetVolatileStatus( VolatileConditionID.Substitute, source );

                        int hp = Mathf.FloorToInt( attacker.Pokemon.MaxHP * 0.25f );
                        attacker.SetSubstitute( hp );
                    }
                }
            },
            {
                "Surf", new()
                {
                    OnModifyMoveDamage = ( attacker, target, move, damage ) =>
                    {
                        if( target.Pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.Submerged ) )
                            return damage * 2;
                        else
                            return damage;
                    }
                }
            },
            {
                "Earthquake", new()
                {
                    OnModifyMoveDamage = ( attacker, target, move, damage ) =>
                    {
                        if( target.Pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.Boroughed ) )
                            return damage * 2;
                        else
                            return damage;
                    }
                }
            },
            {
                "Stockpile", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        var attackerStatStages = attacker.Pokemon.StatStages;
                        bool canBoostDef = true;
                        bool canBoostSpDef = true;

                        ( VolatileCondition condition, int duration ) stockpile = ( null, 0 );
                        if( attacker.Pokemon.VolatileStatuses.TryGetValue( VolatileConditionID.Stockpile, out stockpile ) )
                        {
                            if( stockpile.duration >= 3 )
                                return;
                        }

                        foreach( var sc in attackerStatStages )
                        {
                            if( sc.Key == Stat.Defense && sc.Value >= 6 )
                                canBoostDef = false;

                            if( sc.Key == Stat.SpDefense && sc.Value >= 6 )
                                canBoostSpDef = false;
                        }

                        List<StatStage> stockpileChanges = new();

                        if( canBoostDef )
                        {
                            StatStage defense = new()
                            {
                                Stat = Stat.Defense,
                                Change = 1,
                            };

                            stockpileChanges.Add( defense );
                            stockpile.condition.StockpiledDEF++;
                        }
                        
                        if( canBoostSpDef )
                        {
                            StatStage spDefense = new()
                            {
                                Stat = Stat.SpDefense,
                                Change = 1,
                            };
                        
                            stockpileChanges.Add( spDefense );
                            stockpile.condition.StockpiledSpDEF++;
                        }

                        if( stockpileChanges.Count > 0 )
                        {
                            StageChangeSource source = new()
                            {
                                Pokemon = attacker.Pokemon,
                                MoveName = "Stockpile",
                                Ability = AbilityID.None,
                                Source = StageChangeSourceType.Move,
                            };

                            attacker.Pokemon.ApplyStatStageChange( stockpileChanges, source );
                        }
                    },

                    OnMoveEffectsCompleted = ( attacker, target, move, bs ) =>
                    {
                        if( attacker.Pokemon.VolatileStatuses.TryGetValue( VolatileConditionID.Stockpile, out var stockpile ) )
                        {
                            bs.AddDialogue( $"{attacker.Pokemon.NickName} stockpiled! It has {stockpile.Duration} stacks!" );
                        }
                    }
                }
            },
            {
                "Spit Up", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        int power = 0;
                        attacker.Pokemon.VolatileStatuses.TryGetValue( VolatileConditionID.Stockpile, out var stockpile );
                        power = stockpile.Duration * 100;

                        //--Using Spit Up consumes all stockpile stacks and removes all def and spdef boosts gained.
                        List<StatStage> stockpileChanges = new();

                        int defBoosts = stockpile.Condition.StockpiledDEF;
                        int spdefBoosts = stockpile.Condition.StockpiledSpDEF;

                        for( int i = 0; i < defBoosts; i++ )
                        {
                            StatStage defense = new()
                            {
                                Stat = Stat.Defense,
                                Change = -1,
                            };

                            stockpileChanges.Add( defense );
                        }

                        for( int i = 0; i < spdefBoosts; i++ )
                        {
                            StatStage spDefense = new()
                            {
                                Stat = Stat.Defense,
                                Change = -1,
                            };

                            stockpileChanges.Add( spDefense );
                        }

                        if( stockpileChanges.Count > 0 )
                        {
                            StageChangeSource source = new()
                            {
                                Pokemon = attacker.Pokemon,
                                MoveName = "Stockpile",
                                Ability = AbilityID.None,
                                Source = StageChangeSourceType.Move,
                            };

                            attacker.Pokemon.ApplyStatStageChange( stockpileChanges, source );
                        }

                        attacker.Pokemon.CureVolatileStatus( VolatileConditionID.Stockpile );
                        return power;
                    }
                }
            },
            {
                "Swallow", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        if( attacker.Pokemon.VolatileStatuses.TryGetValue( VolatileConditionID.Stockpile, out var stockpile ) )
                        {
                            int heal = 0;
                            int stacks = stockpile.Duration;
                            int maxHP = attacker.Pokemon.MaxHP;

                            if( stacks == 3 )
                                heal = maxHP;
                            else if( stacks == 2 )
                                heal = Mathf.RoundToInt( maxHP * 0.5f );
                            else if( stacks == 1 )
                                heal = Mathf.RoundToInt( maxHP * 0.25f );

                            //--Using Swallow consumes all stockpile stacks and removes all def and spdef boosts gained.
                            List<StatStage> stockpileChanges = new();

                            int defBoosts = stockpile.Condition.StockpiledDEF;
                            int spdefBoosts = stockpile.Condition.StockpiledSpDEF;

                            for( int i = 0; i < defBoosts; i++ )
                            {
                                StatStage defense = new()
                                {
                                    Stat = Stat.Defense,
                                    Change = -1,
                                };

                                stockpileChanges.Add( defense );
                            }

                            for( int i = 0; i < spdefBoosts; i++ )
                            {
                                StatStage spDefense = new()
                                {
                                    Stat = Stat.Defense,
                                    Change = -1,
                                };

                                stockpileChanges.Add( spDefense );
                            }

                            if( stockpileChanges.Count > 0 )
                            {
                                StageChangeSource source = new()
                                {
                                    Pokemon = attacker.Pokemon,
                                    MoveName = "Stockpile",
                                    Ability = AbilityID.None,
                                    Source = StageChangeSourceType.Move,
                                };

                                attacker.Pokemon.ApplyStatStageChange( stockpileChanges, source );
                            }
                        }
                    }
                }
            },
            {
                "Rapid Spin", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        attacker.Pokemon.CureAllBindingStatuses();

                        var court = bs.Field.GetPokemonCourtFromTrainer( attacker.Pokemon );

                        List<CourtConditionID> hazardsToRemove = new();
                        foreach( var kvp in court.Conditions )
                        {
                            if( kvp.Value.ConditionType == ConditionType.OpposingSide_Hazard )
                                hazardsToRemove.Add( kvp.Key );
                        }

                        foreach( var hazard in hazardsToRemove )
                        {
                            court.RemoveCondition( hazard );
                        }

                        bs.AddDialogue( $"{attacker.Pokemon.NickName} cleared its side of the field and freed itself!" );
                    }
                }
            },
            {
                "Clear Smog", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        target.Pokemon.ClearStatStages();
                        bs.AddDialogue( $"{target.Pokemon.NickName} had all of its stat stages reset!" );
                    }
                }
            },
            {
                "Eruption", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        int hp = attacker.Pokemon.CurrentHP;
                        int maxHP = attacker.Pokemon.MaxHP;
                        int power = ( 150 * hp ) / maxHP;

                        return Mathf.Max( power, 1 );
                    }
                }
            },
            {
                "Water Spout", new()
                {
                    OnModifyMovePower = ( attacker, target, move, hit ) =>
                    {
                        int hp = attacker.Pokemon.CurrentHP;
                        int maxHP = attacker.Pokemon.MaxHP;
                        int power = ( 150 * hp ) / maxHP;

                        return Mathf.Max( power, 1 );
                    }
                }
            },
            {
                "Entrainment", new()
                {
                    OnMoveSuccess = ( attacker, target, move, bs ) =>
                    {
                        target.Pokemon.MakeAbility( attacker.Pokemon.AbilityID );
                        target.SetFlagActive( UnitFlags.AbilityChanged, true );
                        bs.AddDialogue( $"{attacker.Pokemon.NickName} made {target.Pokemon.NickName}'s ability {attacker.Pokemon.Ability?.Name}" );

                        bs.TriggerAbilityCutIn( target.Pokemon );
                        target.Pokemon.Ability?.OnAbilityEnter?.Invoke( target.Pokemon, bs.GetOpposingUnits( target ), bs.Field );
                    }
                }
            }
        };
    }
}
