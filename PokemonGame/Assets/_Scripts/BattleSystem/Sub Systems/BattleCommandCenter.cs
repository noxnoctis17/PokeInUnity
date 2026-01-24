using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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
    public IEnumerator PerformMoveCommand( Move move, BattleUnit attacker, List<BattleUnit> targets ){
        Debug.Log( $"[Perform Move Command] Starting Perform Move Command. Attacker: {attacker.Pokemon.NickName}, target count: {targets.Count}" );

        if( attacker.Pokemon.IsFainted() )
            yield break;

        //--Assign last used move.
        attacker.SetLastUsedMove( move );

        if( move.MoveSO.Name != "Protect" )
            attacker.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;

        // Debug.Log( $"{attacker.Pokemon.NickName} has used Protect: {attacker.Flags[UnitFlags.SuccessiveProtectUses].Count} times!" );

        //--Checks if there's a status impeding the pokemon from using a move this turn, such as sleep, flinch, first turn para, confusion, etc.
        bool canAttack = attacker.Pokemon.OnBeforeTurn();
        if( !canAttack )
        {
            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
            yield return null;
            yield return BattleSystem.WaitForEventQueue();
            yield break;
        }

        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();

        move.PP--; //--Reduces the move's PP by 1

        BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{attacker.Pokemon.NickName} used {move.MoveSO.Name}!" ) );
        yield return BattleSystem.WaitForUIQueue();

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

        foreach( var unit in faintedUnits )
        {
            targets.Remove( unit );
        }

        var damageDetails = new DamageDetails();
        var sortedTargets = SortTargetsBySpeed( targets );

        List<BattleUnit> targetsHit = new();
        foreach( var target in sortedTargets )
        {
            if( BattleSystem.MoveSuccess( attacker, target, move ) )
            {
                if( CheckMoveAccuracy( move, attacker, target ) )
                {
                    targetsHit.Add( target );
                }
                else
                {
                    BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{attacker.Pokemon.NickName}'s attack missed!" ) );
                    yield return BattleSystem.WaitForUIQueue();
                }
            }
            else
            {
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
                        var newTarget = target;
                        if( move.MoveSO.Flags.Contains( MoveFlags.Reflectable ) && target.Pokemon.Ability?.Name == "Magic Bounce" )
                        {
                            newTarget = attacker;
                            BattleSystem.TriggerAbilityCutIn( target.Pokemon );
                            BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlaySystemMessageCoroutine( $"{target.Pokemon.NickName}'s Magic Bounce reflected it back!" ) );
                            yield return BattleSystem.WaitForUIQueue();
                        }

                        if( move.MoveTarget == MoveTarget.Enemy || move.MoveTarget == MoveTarget.Self )
                            yield return BattleSystem.BattleComposer.RunStatusAttackScene( move, attacker, target );

                        yield return RunMoveEffects( move, move.MoveSO.MoveEffects, move.MoveSO.MoveEffects.Target, attacker, newTarget ); //--Run Move Effects must be a Command Level queue event, it cannot be an Event Queue level event! Event Queue Items cannot add more items to the Event Queue and then wait on them! it will hang!!!!!
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
                        
                        attacker.SetFlagActive( UnitFlags.DidDamage, true );
                        damageDetails = target.TakeDamage( move, attacker, BattleSystem.Field.Weather, BattleSystem.Field.Terrain, targetCount );
                        typeEffectiveness = damageDetails.TypeEffectiveness;

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

                    if( target.Pokemon.CurrentHP <= 0 )
                        break;
                }

                yield return attacker.PokeAnimator.PlayReturnToDefaultPosition();

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
    private bool CheckMoveAccuracy( Move move, BattleUnit attacker, BattleUnit target ){
        if( move.MoveSO.Alwayshits )
            return true;

        if( move.MoveType == PokemonType.Poison && move.MoveSO.Name == "Toxic" && attacker.Pokemon.CheckTypes( PokemonType.Poison ) )
            return true;

        float moveAccuracy = move.MoveSO.Accuracy;

        int accuracy = attacker.Pokemon.StatStage[ Stat.Accuracy ];
        int evasion = target.Pokemon.StatStage[ Stat.Evasion ];

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
                attacker.Pokemon.ApplyStatStageChange( effects.StatChangeList, attacker.Pokemon ); //--Apply stat change to self, like ddance or swords dance
            else if( effectTarget == EffectTarget.Enemy )
                target.Pokemon.ApplyStatStageChange( effects.StatChangeList, attacker.Pokemon ); //--Apply stat change to target, like growl or tail whip
        }

        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();

        //--Apply Severe Status Effects like BRN, FRZ, PSN
        if( effects.SevereStatus != StatusConditionID.NONE )
        {
            if( effectTarget == EffectTarget.Self )
                attacker.Pokemon.SetSevereStatus( effects.SevereStatus );
            else if( effectTarget == EffectTarget.Enemy )
                target.Pokemon.SetSevereStatus( effects.SevereStatus );
        }

        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();

        //--Apply Volatile Status Effects like Confusion, Affection
        if( effects.VolatileStatus != StatusConditionID.NONE )
        {
            if( effectTarget == EffectTarget.Self ) //--Outrage!!!!!! needed to do this adjustment anyway... 01/16/26
                attacker.Pokemon.SetVolatileStatus( effects.VolatileStatus );
            else if( effectTarget == EffectTarget.Enemy )
                target.Pokemon.SetVolatileStatus( effects.VolatileStatus );
        }

        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
        BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
        yield return null;
        yield return BattleSystem.WaitForEventQueue();

        //--Apply Transient Status Effects like Flinch, Protect, Follow me, etc.
        if( effects.TransientStatus != StatusConditionID.NONE )
        {
            if( effectTarget == EffectTarget.Self )
                attacker.Pokemon.SetTransientStatus( effects.TransientStatus );
            else if( effectTarget == EffectTarget.Enemy )
                target.Pokemon.SetTransientStatus( effects.TransientStatus );
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
            foreach( var unit in activePokemon )
            {
                BattleSystem.Field.Weather?.OnExitWeather?.Invoke( unit.Pokemon );
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
        if( effects.CourtCondition != CourtConditionID.NONE )
        {
            CourtLocation location;

            Debug.Log( $"Running effect: {effects.CourtCondition}!" );

            if( BattleSystem.PlayerUnits.Contains( attacker ) )
            {
                if( effectTarget == EffectTarget.Enemy || effectTarget == EffectTarget.OpposingSide )
                    location = CourtLocation.TopCourt;
                else
                    location = CourtLocation.BottomCourt;
            }
            else
            {
                if( effectTarget == EffectTarget.Enemy || effectTarget == EffectTarget.OpposingSide )
                    location = CourtLocation.BottomCourt;
                else
                    location = CourtLocation.TopCourt;
            }

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
                if( BattleSystem.Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.ConType != ConditionType.OpposingSide_Hazard )
                    BattleSystem.Field.ActiveCourts[location]?.Conditions[effects.CourtCondition]?.OnEnterCourt?.Invoke( unit, BattleSystem.Field );
            }

            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( attacker ) );
            BattleSystem.AddToEventQueue( () => BattleSystem.ShowStatusChanges( target ) );
            yield return null;
            yield return BattleSystem.WaitForEventQueue();
            yield return null;
        }

        if( effects.SwitchEffect != null && effects.SwitchEffect?.SwitchType != SwitchEffectType.None )
        {
            var activePokemon = BattleSystem.PlayerUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
            var remainingPokemon = BattleSystem.BottomTrainerParty.GetHealthyPokemon( dontInclude: activePokemon );

            var activeEnemyPokemon = BattleSystem.EnemyUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
            Pokemon remainingEnemyPokemon = null;

            if( BattleSystem.BattleType != BattleType.WildBattle_1v1 )
                remainingEnemyPokemon = BattleSystem.TopTrainerParty.GetHealthyPokemon( dontInclude: activeEnemyPokemon );

            if( effects.SwitchEffect?.SwitchType == SwitchEffectType.SelfPivot )
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
            
            if( effects.SwitchEffect?.SwitchType == SwitchEffectType.ForceOpponentOut )
            {
                Debug.Log( $"{attacker.Pokemon.NickName} is trying to force its opponent, {target.Pokemon.NickName}, out!" );

                if( remainingEnemyPokemon != null )
                {
                    if( target.IsAI )
                    {
                        BattleSystem.SetForcedSwitch( true );
                        var switchIn = target.BattleAI.RequestedForcedSwitch();
                        target.SetFlagActive( UnitFlags.Phased, true );
                        yield return PerformSwitchPokemonCommand( switchIn, target, true );
                        yield return new WaitUntil( () => !BattleSystem.IsForcedSwitch );
                    }
                    else
                    {
                        target.SetFlagActive( UnitFlags.Phased, true );
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

    private IEnumerator RunAfterMove( DamageDetails details, MoveSO move, BattleUnit attacker, BattleUnit target ){
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

        if( move.Recoil.RecoilType != RecoilType.none ){
            int damage = 0;

            switch( move.Recoil.RecoilType )
            {
                case RecoilType.RecoilByMaxHP:
                    int maxHP = attacker.Pokemon.MaxHP;
                    damage = Mathf.FloorToInt( maxHP * ( move.Recoil.RecoilDamage / 100f ) );
                    attacker.TakeRecoilDamage( damage );
                    attacker.Pokemon.AddStatusEvent( StatusEventType.Damage, $"{attacker.Pokemon.NickName} was hurt by recoil!" );
                break;

                case RecoilType.RecoilByDamage:
                    damage = Mathf.FloorToInt( details.DamageDealt * ( move.Recoil.RecoilDamage / 100f ) );
                    attacker.TakeRecoilDamage( damage );
                    attacker.Pokemon.AddStatusEvent( StatusEventType.Damage, $"{attacker.Pokemon.NickName} was hurt by recoil!" );
                break;

                case RecoilType.RecoilByCurrentHP:
                    int currentHP = attacker.Pokemon.CurrentHP;
                    damage = Mathf.FloorToInt( currentHP * ( move.Recoil.RecoilDamage / 100f ) );
                    attacker.TakeRecoilDamage( damage );
                    attacker.Pokemon.AddStatusEvent( StatusEventType.Damage, $"{attacker.Pokemon.NickName} was hurt by recoil!" );
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

//-------------------------------------------------------------------------------------------------------
//--------------------------------------------[ SWITCH COMMAND ]-----------------------------------------------
//-------------------------------------------------------------------------------------------------------

    //--When the player's pokemon faints, this is called explicitly, rather than as a command added to the command queue
    //--Command Level
    public IEnumerator PerformSwitchPokemonCommand( Pokemon pokemon, BattleUnit unit, bool forcedAI = false )
    {
        BattleSystem.SetBusyState();

        if( BattleSystem.BattleType != BattleType.AI_Singles && BattleSystem.BattleType != BattleType.AI_Doubles )
        {
            var chosenMon = unit.Pokemon;
            var switchMon = pokemon;
            BattleSystem.BottomTrainerParty.SwitchPokemonPosition( chosenMon, switchMon );
        }

        CourtLocation courtLocation = BattleSystem.Field.GetUnitCourt( unit ).Location;
        Trainer trainer;

        if( courtLocation == CourtLocation.TopCourt )
            trainer = BattleSystem.TopTrainer1;
        else
            trainer = BattleSystem.BottomTrainer1;

        Debug.Log( $"[Switch Pokemon] Switching unit: {unit.Pokemon.NickName} for {pokemon.NickName} in the court: {courtLocation}" );

        unit.Pokemon.CureVolatileStatus(); //--Cure the volatile status of the previous pokemon. Will need to set a previous pokemon soon
        unit.ResetTurnsTakenInBattle();
        unit.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;
        unit.SetFlagActive( UnitFlags.ChoiceItem, false );
        unit.SetLastUsedMove( null );

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
        Debug.Log( $"AI {unit.Pokemon.NickName}'s Phased flag is: {unit.Flags[UnitFlags.Phased].IsActive}" );
        var isPhasedSwitch = unit.Flags[UnitFlags.Phased].IsActive;

        //--Raise OnExit for ability, weather, and held item conditions on the returning Pokemon
        unit.Pokemon.Ability?.OnAbilityExit?.Invoke( unit.Pokemon, BattleSystem.EnemyUnits, BattleSystem.Field );
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
                unit.Setup( pokemon, allyUnits[i].BattleHUD, BattleSystem ); //--Assign and setup the new pokemon
                unit.ResetTurnsTakenInBattle(); //--Sets turns taken to -1, so they may be incremented to 0 during RoundEndUpdate()
                unit.Flags[UnitFlags.SuccessiveProtectUses].Count = 0;
            }
        }

        BattleSystem.AddToUIQueue( () => DialogueManager.Instance.PlayTrainerDialogueCR( $"Go, {pokemon.NickName}!", trainer ) );
        yield return BattleSystem.WaitForUIQueue();
        AudioController.Instance.PlaySFX( SoundEffect.BattleBallThrow );

        if( courtLocation == CourtLocation.TopCourt )
            yield return unit.PokeAnimator.PlayEnterBattleAnimation( unit.transform, BattleSystem.TrainerCenter_Top1.transform );
        else
            yield return unit.PokeAnimator.PlayEnterBattleAnimation( unit.transform, BattleSystem.TrainerCenter_Bottom1.transform );

        yield return new WaitForSeconds( 0.25f );

        if( isPhasedSwitch )
            unit.Pokemon.SetTransientStatus( StatusConditionID.Phased );

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
        pokemon.Ability?.OnAbilityEnter?.Invoke( pokemon, BattleSystem.EnemyUnits, BattleSystem.Field );
        
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
