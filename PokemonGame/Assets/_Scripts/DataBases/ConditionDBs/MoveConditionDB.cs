using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Xml;
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
                                bs.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It shattered the barriers!" ) );
                            else
                                bs.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It shattered the barrier!" ) );

                            for( int i = 0; i < endMessages.Count; i++ )
                            {
                                bs.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( endMessages[i] ) );
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
                        bs.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{user.Pokemon.NickName} used {changed.MoveSO.Name}!" ) );
                        
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
                        bs.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"... But nothing happened..." ) );
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
                    OnModifyMovePower = ( BattleUnit attacker, BattleUnit target, Move move, int hit ) =>
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
                        var activeUnits = bs.GetActivePokemon();

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

                        if( bs.Field.Weather?.ID != WeatherConditionID.SUNNY )
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

                        else if( bs.Field.Weather?.ID == WeatherConditionID.SUNNY )
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
                        if( target.Pokemon.HeldItem != null && !attacker.Pokemon.IsFainted() )
                        {
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
                        if( attacker.Pokemon.IsFainted() )
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

                        attacker.Pokemon.SkillSwap( targetAbility );
                        target.Pokemon.SkillSwap( attackerAbility );

                        attacker.SetFlagActive( UnitFlags.SkillSwapped, true );
                        target.SetFlagActive( UnitFlags.SkillSwapped, true );

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
                    OnModifyCommandQueue = ( attacker, target, move, bs ) =>
                    {
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
                        {
                            Debug.LogError( "After You failed when it shouldn't have! Target command not found!" );
                            return;
                        }

                        var commandList = bs.CommandQueue.ToList();
                        commandList.Remove( targetCommand );

                        bs.CommandQueue.Clear();
                        bs.CommandQueue.Enqueue( targetCommand );

                        for( int i = 0; i < commandList.Count; i++ )
                        {
                            var command = commandList[i];
                            bs.CommandQueue.Enqueue( command );
                        }
                    }
                }
            },
            {
                "Wish", new()
                {
                    OnMoveCompleted = ( attacker, target, move, bs ) =>
                    {
                        attacker.SetWish( attacker, move );
                    },

                    OnAfterNextRound = ( unit, move, bs ) =>
                    {
                        var wish = unit.Flags[UnitFlags.Wish];
                        if( wish.Count == 0 )
                        {
                            wish.Count++;
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

                    OnAfterNextRound = ( unit, move, bs ) =>
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
                        var activeUnits = bs.GetActivePokemon();

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
            }
        };
    }
}
