using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Analytics;

public class BattleCommandCenter : MonoBehaviour
{
    public BattleSystem BattleSystem { get; private set; }

    public void Setup( BattleSystem bs )
    {
        BattleSystem = bs;
    }

    private List<BattleUnit> SortTargetsBySpeed( List<BattleUnit> targets )
    {
        return targets.OrderByDescending( p => p.Pokemon.Speed ).ToList();
    }

//-------------------------------------------------------------------------------------------------------
//--------------------------------------------[ MOVE COMMAND ]-----------------------------------------------
//-------------------------------------------------------------------------------------------------------
    //--Perform any Move
    public IEnumerator PerformMoveCommand( Move move, BattleUnit attacker, List<BattleUnit> targets )
    {
        //--Catch a fainted pokemon trying to perform an attack
        if( attacker.Pokemon.IsFainted() )
            yield break;

        //--Assign last used move.
        attacker.SetLastUsedMove( move );

        if( move.MoveSO.Name != "Protect" || move.MoveSO.Name != "Endure" || move.MoveSO.Name != "Detect" )
            attacker.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;

        //--Handle Imprison
        if( BattleSystem.IsImprisoned( move, attacker ) )
        {
            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{attacker.Pokemon.NickName}'s {move.MoveSO.Name} is imprisoned! The move failed!" ) );
            yield return null;
            yield return BattleSystem.WaitForUIQueue();
            yield break;
        }

        //--Checks if there's a status impeding the pokemon from using a move this turn, such as sleep, flinch, first turn para, confusion, etc.
        bool severe = attacker.Pokemon.OnBeforeTurn_Severe();
        bool volatileStatus = attacker.Pokemon.OnBeforeTurn_Volatile();
        bool transient = attacker.Pokemon.OnBeforeTurn_Transient();

        bool passedStatusBeforeTurn =  severe && volatileStatus && transient;
        if( !passedStatusBeforeTurn )
        {
            BattleSystem.SetLastUsedMove( null );
            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
            yield return null;
            yield return BattleSystem.WaitForEventQueue();
            yield break;
        }

        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();

        //--We remove targets that have fainted before the attack was executed, but were passed in the targets list because they were alive
        //--at the time of move selection. For example, chomp selected dclaw and ttar selected rock slide. dclaw kills one target, but rock slide
        //--is still considering that fainted unit a target by this point in the code. We have to remove it so that rock slide only wants to hit
        //--the unit that's still alive, and, get the full damage and not the spread move damage from targets.Count.
        List<BattleUnit> faintedUnits = new();
        foreach( var target in targets )
        {
            if( target.Pokemon.IsFainted() )
            {
                faintedUnits.Add( target );
            }
        }

        //--If all targets have fainted, we shouldn't try executing an attack. Hopefully this fixes that issue entirely, finally. --02/08/26
        if( faintedUnits.Count == targets.Count )
            yield break;

        foreach( var unit in faintedUnits )
        {
            targets.Remove( unit );
        }

        var damageDetails = new DamageDetails();
        var sortedTargets = SortTargetsBySpeed( targets );

        //--Handle Encore
        if( attacker.Pokemon.VolatileStatuses.ContainsKey( VolatileConditionID.Encore ) )
        {
            var encore = attacker.Pokemon.VolatileStatuses[VolatileConditionID.Encore];
            move = encore.Condition?.OnBeforeMoveUsed?.Invoke( attacker ) ?? move;
        }

        //--Handle Use Struggle
        //--We definitely need to implement this soon. scenarios with move locking are absolutely possible now

        //--Handle my pp daddy, put pressure on it please daddy please
        bool pressure = false;
        for( int i = 0; i < sortedTargets.Count; i++ )
        {
            if( sortedTargets[i].Pokemon.AbilityID == AbilityID.Pressure )
            {
                move.PP = Mathf.Max( 0, move.PP - 2 );
                pressure = true;
                break;
            }
            else
                continue;
        }

        if( !pressure )
            move.PP--; //--Reduces the move's PP by 1

        BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{attacker.Pokemon.NickName} used {move.MoveSO.Name}!" ) );
        yield return BattleSystem.WaitForUIQueue();

        List<BattleUnit> targetsHit = new();
        for( int t = 0; t < sortedTargets.Count; t++ )
        {
            var target = sortedTargets[t];

            //--Handle Target redirection (Moves that need to retarget/rand target in doubles, also redirection like follow me and rage powder)
            if( BattleSystem.BattleFlags[BattleFlag.Redirect] )
                target = BattleSystem.HandleRedirection( attacker, target, move );
            
            //--Handle Conditional move redirection, 
            if( MoveConditionDB.Conditions.ContainsKey( move.MoveSO.Name ) )
            {
                var redirect = MoveConditionDB.Conditions[move.MoveSO.Name].OnTargetRedirect?.Invoke( attacker, target, move, BattleSystem );
                target = redirect != null ? redirect : target;
            }
            
            if( BattleSystem.MoveSuccess( attacker, target, move ) )
            {
                if( MoveConditionDB.Conditions.ContainsKey( move.MoveSO.Name ) )
                {
                    MoveConditionDB.Conditions[move.MoveSO.Name].OnMoveSuccess?.Invoke( attacker, target, move, BattleSystem );
                    move = MoveConditionDB.Conditions[move.MoveSO.Name].OnMoveChanged?.Invoke( attacker, move, BattleSystem ) ?? move; //--Sleep Talk, Metronome, etc.
                }
                
                yield return null;
                yield return BattleSystem.WaitForUIQueue();

                if( CheckMoveAccuracy( move, attacker, target ) )
                {
                    //--Charging is checked for in CheckMoveAccuracy(). If a move needs to charge, it skips the accuracy check (as true) and the database entry handles setting the flag
                    //--The flag being set means we're in a charging state, and should not follow through with a full attack loop, so we break out of PerformMoveCommand() entirely.
                    if( attacker.Flags[UnitFlags.Charging].IsActive && attacker.Flags[UnitFlags.Charging].Count == 0 )
                        yield break;

                    targetsHit.Add( target );
                }
                else
                {
                    BattleSystem.SetLastUsedMove( null );
                    BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{attacker.Pokemon.NickName}'s attack missed!" ) );
                    yield return BattleSystem.WaitForUIQueue();
                }
            }
            else
            {
                BattleSystem.SetLastUsedMove( null );
                yield return BattleSystem.WaitForUIQueue();
            }
        }

        if( targetsHit.Count > 0 )
        {
            //--Target count should be the total amount of targets on the field, which is stored in sortedTargets.
            int targetCount = sortedTargets.Count;

            //--When running attack position and attack type animations, the target is really only important for single-target moves.
            //--All spread moves will likely be shoot style animations (rock slide, dgleam), earthquake which has its own, or
            if( move.MoveTarget != MoveTarget.Enemy && move.MoveTarget != MoveTarget.Self )
            {
                int totalHits = 1;
                int hit = 1;
                yield return BattleSystem.BattleComposer.RunMoveToAttackPosition( move, attacker, targetsHit[0] );
                yield return new WaitForSeconds( 0.25f );
                yield return BattleSystem.BattleComposer.RunAttackAnimation( move, attacker, targetsHit[0], totalHits, hit );
            }

            foreach( var target in targetsHit )
            {
                Debug.Log( $"[Perform Move Command] {attacker.Pokemon.NickName} is attacking with {move.MoveSO.Name}! Current Target: {target.Pokemon.NickName}" );

                //--Move Use Abilities, such as Technician -- 02/08/26
                attacker.Pokemon.Ability?.OnMoveUsed?.Invoke( attacker, target, move, BattleSystem );

                //--Check if move is a multi-hit, and return the amount of hits rolled
                int totalHits = move.MoveSO.GetHitAmount();
                int hits = 1;
                float typeEffectiveness = 1f;
                
                for( int i = 1; i <= totalHits; i++ )
                {
                    hits = i;
                    
                    if( move.MoveSO.MoveCategory == MoveCategory.Status )
                    {
                        //--Look at all of this shit just for magic bounce lol. I can probably reuse it for Mirror Armor n shit. --12/21/25
                        var effectTarget = move.MoveSO.MoveEffects.Target;
                        var newTarget = effectTarget;
                        if( move.MoveSO.Flags.Contains( MoveFlags.Reflectable ) && target.Pokemon.Ability?.Name == "Magic Bounce" )
                        {
                            if( effectTarget == EffectTarget.Enemy )
                                newTarget = EffectTarget.Self;
                            else if( effectTarget == EffectTarget.OpposingSide )
                                newTarget = EffectTarget.AllySide;

                            BattleSystem.TriggerAbilityCutIn( target.Pokemon );
                            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{target.Pokemon.NickName}'s Magic Bounce reflected it back!" ) );
                            yield return BattleSystem.WaitForUIQueue();
                        }

                        if( move.MoveTarget == MoveTarget.Enemy || move.MoveTarget == MoveTarget.Self )
                            yield return BattleSystem.BattleComposer.RunStatusAttackScene( move, attacker, target );

                        if( MoveConditionDB.Conditions.ContainsKey( move.MoveSO.Name ) )
                        {
                            MoveConditionDB.Conditions[move.MoveSO.Name].OnMoveHitTarget?.Invoke( attacker, target, move, damageDetails.DamageDealt, hits, BattleSystem );
                            yield return null;
                            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
                            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
                            yield return null;
                            yield return BattleSystem.WaitForEventQueue();
                        }

                        yield return RunMoveEffects( move, move.MoveSO.MoveEffects, newTarget, attacker, target ); //--Run Move Effects must be a Command Level queue event, it cannot be an Event Queue level event! Event Queue Items cannot add more items to the Event Queue and then wait on them! it will hang!!!!!
                        yield return null;
                        yield return BattleSystem.WaitForEventQueue();
                    }
                    else
                    {
                        if( move.MoveTarget == MoveTarget.Enemy )
                        {
                            yield return BattleSystem.BattleComposer.RunMoveToAttackPosition( move, attacker, targetsHit[0] );
                            yield return new WaitForSeconds( 0.25f );
                            yield return BattleSystem.BattleComposer.RunAttackAnimation( move, attacker, target, totalHits, hits );
                        }

                        if( MoveConditionDB.Conditions.ContainsKey( move.MoveSO.Name ) )
                        {
                            MoveConditionDB.Conditions[move.MoveSO.Name].OnMoveHitTarget?.Invoke( attacker, target, move, damageDetails.DamageDealt, hits, BattleSystem );
                            yield return null;
                            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
                            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
                            yield return null;
                            yield return BattleSystem.WaitForEventQueue();
                        }
                        
                        damageDetails = target.TakeDamage( move, attacker, BattleSystem.Field.Weather, BattleSystem.Field.Terrain, targetCount, hits );
                        typeEffectiveness = damageDetails.TypeEffectiveness;
                        attacker.SetDidDamage( attacker, target, move, damageDetails.DamageDealt );

                        if( typeEffectiveness != 0 )
                            yield return BattleSystem.BattleComposer.RunTakeDamagePhase( typeEffectiveness, target );

                        BattleSystem.AddToEventQueue( () => target.BattleHUD.UpdateHPCoroutine() );
                        BattleSystem.AddToEventQueue( () => target.BattleHUD.WaitForHPUpdate() );
                        BattleSystem.AddToEventQueue( () => ShowDamageDetails( damageDetails ) );
                        yield return null;
                        yield return BattleSystem.WaitForEventQueue();

                        if( move.MoveSO.MoveEffects.Trigger == MoveEffectTrigger.LastHit && hits == totalHits ) //--Fucking Scale Shot lol. --01/16/26
                            yield return RunMoveEffects( move, move.MoveSO.MoveEffects, move.MoveSO.MoveEffects.Target, attacker, target );
                        else if( move.MoveSO.MoveEffects.Trigger == MoveEffectTrigger.PerHit )
                            yield return RunMoveEffects( move, move.MoveSO.MoveEffects, move.MoveSO.MoveEffects.Target, attacker, target );

                        yield return null;
                        yield return BattleSystem.WaitForEventQueue();
                    }

                    if( move.MoveSO.SecondaryMoveEffects != null && move.MoveSO.SecondaryMoveEffects.Count > 0 && target.Pokemon.CurrentHP > 0 )
                    {
                        foreach( var secondary in move.MoveSO.SecondaryMoveEffects )
                        {
                            var rand = UnityEngine.Random.Range( 1, 101 );
                            float chanceModifier = attacker.Pokemon.Ability?.OnSecondaryEffectChanceModify?.Invoke() ?? 1f;
                            float chance = secondary.Chance * chanceModifier;
                            if( rand <= secondary.Chance )
                            {
                                if( secondary.Trigger == MoveEffectTrigger.LastHit && hits == totalHits ) //--Fucking Scale Shot lol. --01/16/26
                                    yield return RunMoveEffects( move, secondary, secondary.Target, attacker, target );
                                else if( secondary.Trigger == MoveEffectTrigger.PerHit )
                                    yield return RunMoveEffects( move, secondary, secondary.Target, attacker, target );

                                yield return null;
                                yield return BattleSystem.WaitForEventQueue();
                            }
                        }
                    }

                    target.Pokemon.BattleItemEffect?.OnAfterTakeDamage?.Invoke( target );
                    yield return null;
                    BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );

                    target.Pokemon.BattleItemEffect?.OnMoveContact?.Invoke( attacker, target, move ); //--Nothing implemented yet. This will be for something like Rocky Helmet. --01/09/26
                    yield return null;
                    BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
                    
                    target.Pokemon.Ability?.OnMoveContact?.Invoke( attacker, target, move ); //--Does the target have an ability that activates on the attacker's move making contact, such as Flame Body or Static?
                    yield return null;
                    BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );

                    yield return null;
                    yield return BattleSystem.WaitForEventQueue();

                    if( move.MoveSO.AccuracyType == AccuracyType.PerHit && target.Pokemon.CurrentHP > 0 )
                    {
                         bool hitAgain = CheckMoveAccuracy( move, attacker, target );

                         if( !hitAgain )
                            break;
                    }
                    
                    if( target.Pokemon.CurrentHP <= 0 )
                        break;
                }

                yield return attacker.PokeAnimator.PlayReturnToDefaultPosition();

                if( MoveSuccessDB.MoveSuccess.ContainsKey( move.MoveSO.Name ) )
                    MoveSuccessDB.MoveSuccess[move.MoveSO.Name].OnMoveCompleted?.Invoke( attacker, target, move, BattleSystem );

                if( MoveConditionDB.Conditions.ContainsKey( move.MoveSO.Name ) )
                    MoveConditionDB.Conditions[move.MoveSO.Name].OnMoveCompleted?.Invoke( attacker, target, move, BattleSystem );

                //--Move Use Abilities, such as Technician -- 02/08/26
                attacker.Pokemon.Ability?.OnMoveCompleted?.Invoke( attacker, target, move, BattleSystem );

                BattleSystem.SetLastUsedMove( move );

                yield return ShowTypeEffectiveness( typeEffectiveness );

                if( totalHits > 1 )
                {
                    BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"The Pokemon was hit {hits} times!" ) );
                    yield return BattleSystem.WaitForUIQueue();
                }

                Debug.Log( $"[Move Command] Finished {attacker.Pokemon.NickName}'s full attack loop! Running After Move!" );

                yield return RunAfterMove( damageDetails, move.MoveSO, attacker, target );

                //--Check for faint after a move is used on the target
                yield return BattleSystem.CheckForFaint( target );
            }
        }
        
        yield return null;
    }

    //--Check a Move's accuracy and determine if it hits or misses
    private bool CheckMoveAccuracy( Move move, BattleUnit attacker, BattleUnit target )
    {
        if( MoveSuccessDB.MoveSuccess.ContainsKey( move.MoveSO.Name ) )
            MoveSuccessDB.MoveSuccess[move.MoveSO.Name].OnCheckAccuracy?.Invoke( attacker, target, move, BattleSystem );

        if( move.AccuracyType == AccuracyType.AlwaysHits )
            return true;

        if( move.MoveSO.Flags.Count > 0 && ( move.MoveSO.Flags.Contains( MoveFlags.Charge ) || move.MoveSO.Flags.Contains( MoveFlags.TwoTurnMove ) ) )
        {
            if( MoveSuccessDB.MoveSuccess.ContainsKey( move.MoveSO.Name ) )
            {
                bool needsToCharge = MoveSuccessDB.MoveSuccess[move.MoveSO.Name].OnCheckNeedsToCharge( attacker, target, move, BattleSystem );
                Debug.Log( $"[Charge] Checking if a move needs to be charged: {needsToCharge}" );
                
                if( needsToCharge )
                    return true;
            }
        }

        float moveAccuracy = move.MoveSO.Accuracy;

        int accuracy = attacker.Pokemon.StatStages[ Stat.Accuracy ];
        int evasion = target.Pokemon.StatStages[ Stat.Evasion ];

        var modifierValue = new float[] { 1f, 4f / 3f, 5f / 3f, 2f, 7f / 3f, 8f / 3f, 3f };

        if( accuracy > 0 )
            moveAccuracy *= modifierValue[accuracy];
        else
            moveAccuracy /= modifierValue[-accuracy];

        if( evasion < 0 )
            moveAccuracy /= modifierValue[evasion];
        else
            moveAccuracy *= modifierValue[-evasion];

        moveAccuracy = Mathf.FloorToInt( attacker.Pokemon.Modify_ACC( moveAccuracy, target.Pokemon, move ) );

        return UnityEngine.Random.Range( 1, 101 ) <= moveAccuracy;
    }

    //--If a Move has secondary effects, apply them appropriately
    //--Command Level
    private IEnumerator RunMoveEffects( Move move, MoveEffects effects, EffectTarget effectTarget, BattleUnit attacker, BattleUnit target ){
        Debug.Log( $"Running Move Effects! Move: {move}, effects: {effects}, move target: {effectTarget}, attacker: {attacker.Pokemon.NickName}, target: {target.Pokemon.NickName}" );
        if( move.MoveSO.MoveCategory != MoveCategory.Status && effectTarget == EffectTarget.Enemy && target.Pokemon.IsFainted() )
            yield break;

        //--Modify Stats
        if( effects.StatChangeList != null )
        {
            if( effectTarget == EffectTarget.Self )
            {
                StageChangeSource source = new()
                {
                    Pokemon = attacker.Pokemon,
                    MoveName = move.MoveSO.Name,
                    Source = StageChangeSourceType.Move,
                };

                attacker.Pokemon.ApplyStatStageChange( effects.StatChangeList, source ); //--Apply stat change to self, like ddance or swords dance
            }
            else if( effectTarget == EffectTarget.Enemy )
            {
                StageChangeSource source = new()
                {
                    Pokemon = attacker.Pokemon,
                    MoveName = move.MoveSO.Name,
                    Source = StageChangeSourceType.Move,
                };

                target.Pokemon.ApplyStatStageChange( effects.StatChangeList, source ); //--Apply stat change to target, like growl or tail whip
            }
        }

        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();

        //--Apply Severe Status Effects like BRN, FRZ, PSN
        if( effects.SevereStatus != SevereConditionID.None )
        {
            if( effectTarget == EffectTarget.Self )
            {
                StatusEffectSource source = new()
                {
                    Pokemon = attacker.Pokemon,
                    Source = EffectSource.Move,
                };

                attacker.Pokemon.SetSevereStatus( effects.SevereStatus, source );
            }
            else if( effectTarget == EffectTarget.Enemy )
            {
                StatusEffectSource source = new()
                {
                    Pokemon = attacker.Pokemon,
                    Source = EffectSource.Move,
                };

                target.Pokemon.SetSevereStatus( effects.SevereStatus, source );
            }
        }

        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();

        //--Apply Volatile Status Effects like Confusion, Affection --and fucking everything else apparently 02/02/26
        if( effects.VolatileStatus != VolatileConditionID.None )
        {
            if( effectTarget == EffectTarget.Self )
            {
                StatusEffectSource source = new()
                {
                    Pokemon = attacker.Pokemon,
                    Source = EffectSource.Move,
                };

                attacker.Pokemon.SetVolatileStatus( effects.VolatileStatus, source );
                var status = attacker.Pokemon.VolatileStatuses[effects.VolatileStatus].Condition;
                status.OnApplyStatus?.Invoke( attacker, target, BattleSystem );
            }
            else if( effectTarget == EffectTarget.Enemy )
            {
                StatusEffectSource source = new()
                {
                    Pokemon = attacker.Pokemon,
                    Source = EffectSource.Move,
                };

                target.Pokemon.SetVolatileStatus( effects.VolatileStatus, source );
                var status = target.Pokemon.VolatileStatuses[effects.VolatileStatus].Condition;
                status.OnApplyStatus?.Invoke( attacker, target, BattleSystem );
            }
        }

        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();

        //--Apply Transient Status Effects like Flinch, Protect, Follow me, etc.
        if( effects.TransientStatus != TransientConditionID.None )
        {
            if( effectTarget == EffectTarget.Self )
            {
                StatusEffectSource source = new()
                {
                    Pokemon = attacker.Pokemon,
                    Source = EffectSource.Move,
                };

                attacker.Pokemon.SetTransientStatus( effects.TransientStatus, source );
            }
            else if( effectTarget == EffectTarget.Enemy )
            {
                StatusEffectSource source = new()
                {
                    Pokemon = attacker.Pokemon,
                    Source = EffectSource.Move,
                };

                target.Pokemon.SetTransientStatus( effects.TransientStatus, source );
            }
        }

        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();

        //--Apply Extra Status Effects like Fire Spin, Whirlpool, and Sand Tomb
        if( effects.BindingStatus != BindingConditionID.None )
        {
            if( effectTarget == EffectTarget.Self )
            {
                StatusEffectSource source = new()
                {
                    Pokemon = attacker.Pokemon,
                    Source = EffectSource.Move,
                };

                attacker.Pokemon.SetBindingStatus( effects.BindingStatus, source );
            }
            else if( effectTarget == EffectTarget.Enemy )
            {
                StatusEffectSource source = new()
                {
                    Pokemon = attacker.Pokemon,
                    Source = EffectSource.Move,
                };

                target.Pokemon.SetBindingStatus( effects.BindingStatus, source );
            }
        }

        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();

        //--Start Weather Effects
        if( effects.Weather != WeatherConditionID.None )
        {
            var activePokemon = BattleSystem.GetActivePokemon();

            //--First we call OnExitWeather while the previous weather was active, if there was one, so they can exit that weather (and lose their speed boosts, fuckers!)
            for( int i = 0; i < activePokemon.Count; i++ )
            {
                BattleSystem.Field.Weather?.OnExitWeather?.Invoke( activePokemon[i].Pokemon );
                yield return null;
            }

            //--Wait for UI Queue just in case exiting a weather causes some UI event. I don't think it ever does? guess we'll see. --12/03/25
            yield return BattleSystem.WaitForUIQueue();

            //--Then we set the new weather due to the move changing the weather
            BattleSystem.Field.SetWeather( effects.Weather, 5 );
            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( BattleSystem.Field.Weather?.StartByMoveMessage ?? BattleSystem.Field.Weather?.StartMessage ) );

            yield return BattleSystem.WaitForUIQueue();
        }

        //--Start Terrain Effects
        if( effects.Terrain != TerrainID.None )
        {
            var activePokemon = BattleSystem.GetActivePokemon();

            //--First we call OnExitWeather while the previous weather was active, if there was one, so they can exit that weather (and lose their speed boosts, fuckers!)
            foreach( var unit in activePokemon )
            {
                BattleSystem.Field.Terrain?.OnExitTerrain?.Invoke( unit );
                yield return null;
            }

            //--Wait for UI Queue just in case exiting a weather causes some UI event. I don't think it ever does? guess we'll see. --12/03/25
            yield return BattleSystem.WaitForUIQueue();

            //--Then we set the new weather due to the move changing the weather
            BattleSystem.Field.SetTerrain( effects.Terrain, 5 );
            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( BattleSystem.Field.Terrain?.StartByMoveMessage ?? BattleSystem.Field.Terrain?.StartMessage ) );

            yield return BattleSystem.WaitForUIQueue();
        }

        //--Start Court Condition
        if( effects.CourtCondition != CourtConditionID.None )
        {
            CourtLocation location;

            Debug.Log( $"Running effect: {effects.CourtCondition}!" );

            if( effectTarget == EffectTarget.Enemy || effectTarget == EffectTarget.OpposingSide )
                location = BattleSystem.Field.GetUnitCourt( target ).Location;
            else
                location = BattleSystem.Field.GetUnitCourt( attacker ).Location;
            
            BattleSystem.Field.ActiveCourts[location]?.AddCondition( effects.CourtCondition );

            if( BattleSystem.Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.StartMessage != null )
                BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( BattleSystem.Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.StartMessage ) );
            
            if( effects.CourtCondition == CourtConditionID.TrickRoom )
            {
                if( !BattleSystem.BattleFlags[BattleFlag.TrickRoom] )
                    BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( BattleSystem.Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.TrickRoomStartMessage?.Invoke( BattleSystem, attacker.Pokemon ) ) );
                else
                    BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( BattleSystem.Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.TrickRoomAlreadyActiveMessage?.Invoke( BattleSystem, attacker.Pokemon ) ) );
            }

            yield return BattleSystem.WaitForUIQueue();

            BattleSystem.Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.OnStart?.Invoke( BattleSystem, BattleSystem.Field, location, attacker );

            foreach( var unit in BattleSystem.Field.ActiveCourts[location].Units )
            {
                if( BattleSystem.Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.ConditionType != ConditionType.OpposingSide_Hazard )
                    BattleSystem.Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.OnEnterCourt?.Invoke( unit, BattleSystem.Field );
            }

            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
            yield return null;
            yield return BattleSystem.WaitForEventQueue();
        }

        if( effects.SwitchType != SwitchEffectType.None )
        {
            Debug.Log( $"[Switch Effect] Switch effect is: {effects.SwitchType}, from move: {move.MoveSO.Name}." );
            var activePokemon = BattleSystem.PlayerUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
            var remainingPokemon = BattleSystem.BottomTrainer1.GetHealthyPokemon( dontInclude: activePokemon );

            var activeEnemyPokemon = BattleSystem.EnemyUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
            Pokemon remainingEnemyPokemon = null;

            if( BattleSystem.BattleType != BattleType.WildBattle_1v1 )
                remainingEnemyPokemon = BattleSystem.TopTrainer1.GetHealthyPokemon( dontInclude: activeEnemyPokemon );

            if( effects.SwitchType == SwitchEffectType.SelfPivot )
            {
                Debug.Log( $"{attacker.Pokemon.NickName} is trying to Pivot out!" );

                if( BattleSystem.EnemyUnits.Contains( attacker ) && remainingEnemyPokemon != null )
                {
                    if( attacker.IsAI )
                    {
                        BattleSystem.SetForcedSwitch( true );
                        var switchIn = attacker.BattleAI.RequestedForcedSwitch();
                        yield return PerformSwitchPokemonCommand( switchIn, attacker, true );
                        yield return new WaitUntil( () => !BattleSystem.IsForcedSwitch );
                    }
                    else
                    {
                        yield return ForcedSwitchPartyMenu( attacker ); //--Doing this is how we'll be able to setup same screen 2 human player battles :]
                        yield return new WaitUntil( () => !BattleSystem.IsForcedSwitch );
                    }
                }
                else if( BattleSystem.PlayerUnits.Contains( attacker ) && remainingPokemon != null )
                {
                    Debug.Log( $"Pivot move was used by a player unit, {attacker.Pokemon.NickName}!" );
                    if( attacker.IsAI )
                    {
                        BattleSystem.SetForcedSwitch( true );
                        var switchIn = attacker.BattleAI.RequestedForcedSwitch();
                        yield return PerformSwitchPokemonCommand( switchIn, attacker, true );
                        yield return new WaitUntil( () => !BattleSystem.IsForcedSwitch );
                    }
                    else
                    {
                        yield return ForcedSwitchPartyMenu( attacker );
                        yield return new WaitUntil( () => !BattleSystem.IsForcedSwitch );
                    }
                }
            }
            
            if( effects.SwitchType == SwitchEffectType.ForceOpponentOut )
            {
                Debug.Log( $"{attacker.Pokemon.NickName} is trying to force its opponent, {target.Pokemon.NickName}, out!" );

                if( remainingEnemyPokemon != null )
                {
                    if( target.IsAI )
                    {
                        BattleSystem.SetForcedSwitch( true );
                        var switchIn = target.BattleAI.RequestedForcedSwitch();
                        target.SetFlagActive( UnitFlags.Phazed, true );
                        yield return PerformSwitchPokemonCommand( switchIn, target, true );
                        yield return new WaitUntil( () => !BattleSystem.IsForcedSwitch );
                    }
                    else
                    {
                        target.SetFlagActive( UnitFlags.Phazed, true );
                        yield return ForcedSwitchPartyMenu( target );
                        yield return new WaitUntil( () => !BattleSystem.IsForcedSwitch );
                    }
                }
            }

            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
            yield return null;
            yield return BattleSystem.WaitForEventQueue();
        }

        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();
    }

    private IEnumerator RunAfterMove( DamageDetails details, MoveSO move, BattleUnit attacker, BattleUnit target )
    {
        Debug.Log( $"[Move Command][Run After Move] Beginning RunAfterMove(): Damage Details: {details}, Move: {move.Name}, Attacker: {attacker.Pokemon.NickName}, Target: {target.Pokemon.NickName}" );

        if( details == null )
            yield break;

        if( move.DrainPercentage != 0 )
        {
            int healedHP = Mathf.Clamp( Mathf.CeilToInt( details.DamageDealt / 100f * move.DrainPercentage ), 1, attacker.Pokemon.MaxHP );
            attacker.Pokemon.IncreaseHP( healedHP );

            if( target.Pokemon == BattleSystem.WildPokemon )
                attacker.Pokemon.AddStatusEvent( StatusEventType.Heal, $"The wild {target.Pokemon.NickName} had its energy drained!" );
            else
                attacker.Pokemon.AddStatusEvent( StatusEventType.Heal, $"The enemy {target.Pokemon.NickName} had its energy drained!" );
        }

        if( move.HealType != HealType.None )
        {
            if( move.HealType == HealType.PercentOfMaxHP )
            {
                float healAmount = move.HealAmount; //--Just in case to avoid integer division resulting in 0 or 100
                float percent = healAmount / 100f;
                int healedHP = Mathf.Clamp( Mathf.CeilToInt( attacker.Pokemon.MaxHP * percent ), 1, attacker.Pokemon.MaxHP );
                attacker.Pokemon.IncreaseHP( healedHP );
                
                if( attacker.Pokemon == BattleSystem.WildPokemon )
                    attacker.Pokemon.AddStatusEvent( StatusEventType.Heal, $"The wild {target.Pokemon.NickName} recovered its HP!" );
                else
                    attacker.Pokemon.AddStatusEvent( StatusEventType.Heal, $"{attacker.Pokemon.NickName} recovered its HP!" );
            }
        }

        if( move.Recoil.RecoilType != RecoilType.None ){
            int damage = 0;

            switch( move.Recoil.RecoilType )
            {
                case RecoilType.RecoilByMaxHP:
                    int maxHP = attacker.Pokemon.MaxHP;
                    damage = Mathf.FloorToInt( maxHP * ( move.Recoil.RecoilDamage / 100f ) );
                    attacker.TakeRecoilDamage( damage );
                break;

                case RecoilType.RecoilByDamage:
                    damage = Mathf.FloorToInt( details.DamageDealt * ( move.Recoil.RecoilDamage / 100f ) );
                    attacker.TakeRecoilDamage( damage );
                break;

                case RecoilType.RecoilByCurrentHP:
                    int currentHP = attacker.Pokemon.CurrentHP;
                    damage = Mathf.FloorToInt( currentHP * ( move.Recoil.RecoilDamage / 100f ) );
                    attacker.TakeRecoilDamage( damage );
                break;

                default:
                    Debug.LogError( "Unknown Recoil Effect!!" );
                break;
            }
        }

        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();
    }

    //--Display text update based on damage done
    private IEnumerator ShowDamageDetails( DamageDetails damageDetails ){
        //--critical hit dialogue
        if( damageDetails.Critical > 1 )
        {
            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It was a critical hit!" ) );
            yield return BattleSystem.WaitForUIQueue();
        }
    }

    private IEnumerator ShowTypeEffectiveness( float typeEffectiveness )
    {
        //--Super Effective dialogue
        if ( typeEffectiveness == 2f )
            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It's super effective!" ) );

        if ( typeEffectiveness == 4f )
            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It's extremely effective! " ) );

        //--Not Very Effective dialogue
        if ( typeEffectiveness == 0.5f )
            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It wasn't very effective." ) );
        if ( typeEffectiveness == 0.25f )
            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It had a minimal effect!!" ) );

        //--No Effect dialogue
        else if ( typeEffectiveness == 0 )
            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It had no effect at all!" ) );

        yield return BattleSystem.WaitForUIQueue();
    }

//-------------------------------------------------------------------------------------------------------------
//--------------------------------------------[ SWITCH COMMAND ]-----------------------------------------------
//-------------------------------------------------------------------------------------------------------------

    private void ClearBattleUnitFlags( BattleUnit unit )
    {
        unit.Pokemon.ClearAllVolatileStatus(); //--Cure all of the volatile statuses of the previous pokemon.
        unit.ResetTurnsTakenInBattle();
        unit.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;
        unit.SetFlagActive( UnitFlags.ChoiceItem, false );
        unit.SetLastUsedMove( null );
        unit.ClearCharging();
        unit.SetFlagActive( UnitFlags.IncreasedStatStage, false );

        if( unit.Flags[UnitFlags.SkillSwapped].IsActive )
        {
            unit.Pokemon.ResetSkillSwap();
            unit.SetFlagActive( UnitFlags.SkillSwapped, false );
        }

        if( unit.Flags[UnitFlags.Substitute].IsActive && !unit.Flags[UnitFlags.BatonPass].IsActive )
        {
            unit.Flags[UnitFlags.Substitute].SubstituteHP = 0;
            unit.SetFlagActive( UnitFlags.Substitute, false );
        }

        unit.SetFlagActive( UnitFlags.BatonPass, false );
    }

    //--When the player's pokemon faints, this is called explicitly, rather than as a command added to the command queue
    //--Command Level
    public IEnumerator PerformSwitchPokemonCommand( Pokemon pokemon, BattleUnit unit, bool forcedAI = false )
    {
        BattleSystem.SetBusyState();

        if( BattleSystem.BattleType != BattleType.AI_Singles && BattleSystem.BattleType != BattleType.AI_Doubles )
        {
            if( BattleSystem.PlayerUnits.Contains( unit ) )
            {
                Debug.Log( $"Swapping pokemon on the party screen!" );
                var a = unit.Pokemon;
                var b = pokemon;
                BattleSystem.BottomTrainer1.SwitchPokemonPosition( a, b );
            }
        }

        CourtLocation courtLocation = BattleSystem.Field.GetUnitCourt( unit ).Location;
        List<BattleUnit> opposingUnits = BattleSystem.GetOpposingUnits( unit );
        BattleTrainer trainer;

        if( courtLocation == CourtLocation.TopCourt )
            trainer = BattleSystem.TopTrainer1;
        else
            trainer = BattleSystem.BottomTrainer1;

        ClearBattleUnitFlags( unit );

        if( !BattleSystem.IsForcedSwitch )
        {
            if( !forcedAI )
            {
                BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlayTrainerDialogueCR( $"{unit.Pokemon.NickName}, come back!", trainer ) );
                yield return BattleSystem.WaitForUIQueue();
            }
        }

        if( courtLocation == CourtLocation.TopCourt )
            yield return unit.PokeAnimator.PlayExitBattleAnimation( BattleSystem.TrainerCenter_Top1.transform );
        else
            yield return unit.PokeAnimator.PlayExitBattleAnimation( BattleSystem.TrainerCenter_Bottom1.transform );

        //--Check for phase-out
        Debug.Log( $"AI {unit.Pokemon.NickName}'s Phased flag is: {unit.Flags[UnitFlags.Phazed].IsActive}" );
        var isPhasedSwitch = unit.Flags[UnitFlags.Phazed].IsActive;

        //--Raise OnExit for ability, weather, and held item conditions on the returning Pokemon
        unit.Pokemon.Ability?.OnAbilityExit?.Invoke( unit.Pokemon, opposingUnits, BattleSystem.Field );
        BattleSystem.Field.Weather?.OnExitWeather?.Invoke( unit.Pokemon );
        unit.Pokemon.BattleItemEffect?.OnItemExit?.Invoke( unit );

        //--If the previous Pokemon exits while any court conditions are active on its side (Enemy = Top, Player = Bottom), raise OnExitCourt
        if( BattleSystem.Field.ActiveCourts[courtLocation].Conditions.Count > 0 )
        {
            foreach( var condition in BattleSystem.Field.ActiveCourts[courtLocation].Conditions )
            {
                condition.Value?.OnExitCourt?.Invoke( unit, BattleSystem.Field );
                yield return BattleSystem.WaitForUIQueue();
            }
        }

        var allyUnits = BattleSystem.GetAllyUnits( unit );
        
        //--Grab the appropriate unit position and assign the incoming pokemon to it
        for( int i = 0; i < allyUnits.Count; i++)
        {
            if( allyUnits[i].Pokemon == unit.Pokemon )
            {
                unit.UpdateUnit( pokemon ); //--Assign and setup the new pokemon
                unit.ResetTurnsTakenInBattle(); //--Sets turns taken to -1, so they may be incremented to 0 during RoundEndUpdate()
                unit.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;
            }
        }

        BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlayTrainerDialogueCR( $"Go, {pokemon.NickName}!", trainer ) );
        yield return BattleSystem.WaitForUIQueue();
        AudioController.Instance.PlaySFX( SoundEffect.BattleBallThrow );

        //--Consider adding "exit" and "enter" functions or something -- 02/02/26
        //--Enter weather.
        BattleSystem.Field.Weather?.OnEnterWeather?.Invoke( unit.Pokemon );

        if( courtLocation == CourtLocation.TopCourt )
            yield return unit.PokeAnimator.PlayEnterBattleAnimation( unit.transform, BattleSystem.TrainerCenter_Top1.transform );
        else
            yield return unit.PokeAnimator.PlayEnterBattleAnimation( unit.transform, BattleSystem.TrainerCenter_Bottom1.transform );

        yield return new WaitForSeconds( 0.25f );

        if( isPhasedSwitch )
        {
            StatusEffectSource source = new()
            {
                Pokemon = null,
                Source = EffectSource.Phazed,
            };

            unit.Pokemon.SetTransientStatus( TransientConditionID.Phazed, source );
        }

        //--Call OnEnterCourt from all existing court conditions on the incoming pokemon
        if( BattleSystem.Field.ActiveCourts[courtLocation].Conditions.Count > 0 )
        {
            foreach( var condition in BattleSystem.Field.ActiveCourts[courtLocation].Conditions )
            {
                condition.Value?.OnEnterCourt?.Invoke( unit, BattleSystem.Field );
                BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( unit ) );
                yield return null;
                yield return BattleSystem.WaitForEventQueue();
                yield return null;
                BattleSystem.AddToEventQueue( () => BattleSystem.CheckForFaint( unit ) );
                yield return null;
                yield return BattleSystem.WaitForEventQueue();
            }

            BattleSystem.AddToEventQueue( () => BattleSystem.CheckForFaint( unit ) );
            yield return null;
            yield return BattleSystem.WaitForEventQueue();

            if( unit.Pokemon.IsFainted() )
            {
                BattleSystem.SetHandleFaintCompleted( false );
                yield return BattleSystem.HandleFaintedPokemon( unit );
                yield return new WaitUntil( () => BattleSystem.HandleFaintCompleted );
            }

            yield return BattleSystem.WaitForUIQueue();
        }

        //--Check if the Pokemon has an entrace ability, then we need to show status changes for all pokemon on the field in case they are effected.
        pokemon.Ability?.OnAbilityEnter?.Invoke( pokemon, opposingUnits, BattleSystem.Field );
        
        var activeUnits = BattleSystem.GetActivePokemon();
        foreach( var activeUnit in activeUnits )
        {
            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( activeUnit ) );
        }

        yield return null;
        yield return BattleSystem.WaitForEventQueue();

        //--Apply a held item's entry effect, if it has one
        pokemon.BattleItemEffect?.OnItemEnter?.Invoke( unit ); //--we use battleUnit here because the swapped in Pokemon should now be swapped into the unit position after being setup earlier.
        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( unit ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();

        if( BattleSystem.IsForcedSwitch ){
            //--During a fainted switch, the menu gets paused, but because fainted
            //--switch happens after the command queue, there's never an opportunity for
            //--the menu to become unpaused, therefore it needs to happen here in this
            //--fainted switch conditional area
            BattleSystem.SetForcedSwitch( false );
        }

        yield return new WaitForSeconds( 0.1f );
    }

    public IEnumerator ForcedSwitchPartyMenu( BattleUnit unitPosition ){
        BattleSystem.SetForcedSwitch( true );
        BattleSystem.SwitchUnitToPosition = unitPosition;
        BattleSystem.EnablePokemonSelectScreen();
        BattleUIActions.OnSubMenuOpened?.Invoke();
        BattleUIActions.OnPkmnMenuOpened?.Invoke();
        // yield return new WaitUntil( () => !BattleSystem.IsForcedSwitch );
        yield return new WaitForSeconds( 0.1f );
        yield return null;
    }
    
    //--Gunna need to make a "Forced Switch" Pokemon state for fainted unit and move-forced switching resolutions.
    //--It will likely help handle if the ai and the player both faint at the same time as well, right now it's somewhat of a problem
    public void SetForcedSwitchMon( Pokemon incomingMon, BattleUnit unitPosition ){
        StartCoroutine( PerformSwitchPokemonCommand( incomingMon, unitPosition ) );
    }

//-------------------------------------------------------------------------------------------------------
//--------------------------------------------[ ITEM COMMAND ]-----------------------------------------------
//-------------------------------------------------------------------------------------------------------

    public IEnumerator PerformUseItemCommand( Pokemon pokemon, Item item ){
        var itemUsed = BattleSystem.PlayerInventory.UseItem( item, pokemon );

        if( itemUsed != null ){
            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( itemUsed.UseText( pokemon ) ) );
        }
        else{
            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( "It didn't have any effect!" ) );
        }

        yield return BattleSystem.WaitForUIQueue();
        yield return null;
    }

//-------------------------------------------------------------------------------------------------------
//--------------------------------------------[ RUN COMMAND ]-----------------------------------------------
//-------------------------------------------------------------------------------------------------------

    public IEnumerator PerformRunFromBattleCommand(){
        // Debug.Log( "You got away!" );
        yield return new WaitForSeconds( 1f );
        BattleSystem.EndBattle();
    }
}
