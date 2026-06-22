using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.UIElements;

public class BattleAI_Projection
{
    private readonly BattleAI _ai;
    private readonly BattleAI_UnitSim _unitSim;
    
    
    public BattleAI_Projection( BattleAI ai )
    {
        _ai = ai;
        _unitSim = _ai.UnitSim;
    }

    public ProjectedBoardState BuildPBS( ActionEvaluation action, BoardContext bc, SurvivalClass sc )
    {
        var top1 = action.Top1;
        var top2 = action.Top2;

        int myRemainingPieces = bc.MyRemainingPieces;
        int oppRemainingPieces = bc.OppRemainingPieces;

        var ee = action.ExchangeEvaluation;

        bool iAmKO = top1.Attacker_EndOfTurnHP <= 0;
        bool oppIsKO = top1.Opponent_EndOfTurnHP <= 0 ;

        var futureExchangeEval = _ai.Projection.EvaluateExchange( top2.Attacker, top2.Opponent ); //--We use TOP1 attacker and opponent here because their HP is directly mutated by the simulation, so EE gets an accurate beginning to the next round.
        var futureTempoState = _ai.Projection.GetTempoState( futureExchangeEval );

        _ai.CurrentLog.Add( $"========================" );
        _ai.CurrentLog.Add( $"=====[BUILDING PBS]=====" );
        _ai.CurrentLog.Add( $"========================" );
        _ai.CurrentLog.Add( $"" );

        //--Material
        if( iAmKO )
        {
            myRemainingPieces--;
            _ai.CurrentLog.Add( $"[Build PBS] Attacker faints this turn! My remaining pieces reduced from {myRemainingPieces + 1} to {myRemainingPieces}! Attacker KO is {iAmKO}." );
        }

        if( oppIsKO )
        {
            oppRemainingPieces--;
            _ai.CurrentLog.Add( $"[Build PBS] Opponent faints this turn! Opponent's remaining pieces reduced from {oppRemainingPieces + 1} to {oppRemainingPieces}! Opponent KO is {oppIsKO}." );
        }

        var myTeamPieceValues = _ai.OurTeamPieceValues;
        var oppTeamPieceValues = _ai.TheirTeamPieceValues;
        int myValue = myTeamPieceValues[top1.Attacker.Pokemon].OffensiveValue;
        int oppValue = oppTeamPieceValues[top1.Opponent.Pokemon].OffensiveValue;

        //--Turn Economy
        //--My Turns
        int myTurnsRemaining = ee.AttackerMovesFirst ? ( top1.Attacker_EndOfTurnHP > 0 ? 1 : 0 ) : ( ee.AttackerSurvives ? 1 : 0 );

        //--Opponent Turns
        int oppTurnsRemaining = ee.OpponentMovesFirst ? ( top1.Opponent_EndOfTurnHP > 0 ? 1 : 0 ) : ( ee.OpponentSurvives ? 1 : 0 );

        _ai.CurrentLog.Add( $"[Build PBS] My Turns Remaining {myTurnsRemaining}. Opponent Turns Remaining: {oppTurnsRemaining}" );

        //--Threat this turn
        bool iThreaten = top1.AttackerPTKO >= PotentialToKO.Dangerous;
        bool oppThreatens = top1.OpponentPTKO >= PotentialToKO.Dangerous;

        //--Future State, from TOP2
        bool iSurviveNext = top2.Attacker_EndOfTurnHP > 0;
        bool oppSurviveNext = top2.Opponent_EndOfTurnHP > 0;

        bool iThreatenNext = top2.AttackerPTKO >= PotentialToKO.Dangerous;
        bool oppThreatenNext = top2.OpponentPTKO >= PotentialToKO.Dangerous;

        bool iKillNext = top2.Opponent_DiesBeforeActing || top2.Opponent_EndOfTurnHP <= 0f;
        bool oppKillNext = top2.Attacker_DiesBeforeActing || top2.Attacker_EndOfTurnHP <= 0f;

        _ai.CurrentLog.Add( $"[Build PBS] I Live Next Round: {iSurviveNext}, Opp Lives Next Round: {oppSurviveNext}" );
        _ai.CurrentLog.Add( $"[Build PBS] I Threaten Next Round: {iThreatenNext}, Opp Threatens Next Round: {oppThreatenNext}" );

        //--Utility Checks
        bool iCreatesDecisiveState = false;
        bool iCreatesPressureState = false;
        bool oppCreatesDecisiveState = false;
        bool oppCreatesPressureState = false;
        bool hazardsSetOnOpp = false;
        bool hazardsSetOnUs = false;
        var attStatus1 = top1.Attacker.SevereStatus;
        var attStatus2 = top2.Attacker.SevereStatus;
        var oppStatus1 = top1.Opponent.SevereStatus;
        var oppStatus2 = top2.Opponent.SevereStatus;
        var attackerMove1 = top1.Attacker.MTR?.Move ?? null;
        var opponentMove1 = top1.Opponent.MTR?.Move ?? null;
        var attCourt = top1.Attacker.CourtLocation;
        var oppCourt = top1.Opponent.CourtLocation;
        var attCourtConditions = attCourt == CourtLocation.TopCourt ? top1.Field.TopCourtConditions : top1.Field.BottomCourtConditions;
        var oppCourtConditions = oppCourt == CourtLocation.TopCourt ? top1.Field.TopCourtConditions : top1.Field.BottomCourtConditions;

        //--Our Utility Flags
        if( oppCourtConditions.ContainsKey( CourtConditionID.StealthRock ) || oppCourtConditions.ContainsKey( CourtConditionID.LeechSeed ) || oppCourtConditions.ContainsKey( CourtConditionID.Spikes ) || oppCourtConditions.ContainsKey( CourtConditionID.ToxicSpikes ) || oppCourtConditions.ContainsKey( CourtConditionID.StickyWeb ) )
            hazardsSetOnOpp = true;

        if( oppStatus1 == SevereConditionID.None && ( oppStatus2 == SevereConditionID.SLP || oppStatus2 == SevereConditionID.PAR ) && !_unitSim.PokemonBenefitsFromSevereStatus( top2.Opponent.Pokemon ) )
            iCreatesDecisiveState = true;

        if( attackerMove1?.MoveSO.MoveEffects.SwitchType == SwitchEffectType.ForceOpponentOut && ( hazardsSetOnOpp || top1.Field.Weather == WeatherConditionID.SANDSTORM ) )
            iCreatesDecisiveState = true;

        if( oppStatus1 == SevereConditionID.None && ( oppStatus2 == SevereConditionID.BRN || oppStatus2 == SevereConditionID.FBT ) && !_unitSim.PokemonBenefitsFromSevereStatus( top2.Opponent.Pokemon ) )
            iCreatesPressureState = true;

        if( _unitSim.PokemonHasLoweredStats( top2.Opponent.Pokemon ) )
            iCreatesPressureState = true;

        if( _unitSim.CheckHasPhazeMove( top1.Attacker.Pokemon ) && ( hazardsSetOnOpp || top1.Field.Weather == WeatherConditionID.SANDSTORM ) )
            iCreatesPressureState = true;

        //--Their Utility Flags
        if( attCourtConditions.ContainsKey( CourtConditionID.StealthRock ) || attCourtConditions.ContainsKey( CourtConditionID.LeechSeed ) || attCourtConditions.ContainsKey( CourtConditionID.Spikes ) || attCourtConditions.ContainsKey( CourtConditionID.ToxicSpikes ) || attCourtConditions.ContainsKey( CourtConditionID.StickyWeb ) )
            hazardsSetOnUs = true;

        if( attStatus1 == SevereConditionID.None && ( attStatus2 == SevereConditionID.SLP || attStatus2 == SevereConditionID.PAR ) && !_unitSim.PokemonBenefitsFromSevereStatus( top2.Attacker.Pokemon ) )
            oppCreatesDecisiveState = true;

        if( opponentMove1?.MoveSO.MoveEffects.SwitchType == SwitchEffectType.ForceOpponentOut && ( hazardsSetOnUs || top1.Field.Weather == WeatherConditionID.SANDSTORM ) )
            oppCreatesDecisiveState = true;

        if( attStatus1 == SevereConditionID.None && ( attStatus2 == SevereConditionID.BRN || attStatus2 == SevereConditionID.FBT ) && !_unitSim.PokemonBenefitsFromSevereStatus( top2.Attacker.Pokemon ) )
            oppCreatesPressureState = true;

        if( _unitSim.PokemonHasLoweredStats( top2.Attacker.Pokemon ) )
            oppCreatesPressureState = true;

        if( _unitSim.CheckHasPhazeMove( top1.Opponent.Pokemon ) && ( hazardsSetOnUs || top1.Field.Weather == WeatherConditionID.SANDSTORM ) )
            oppCreatesPressureState = true;

        //--Role Fulfillment
        var ourRP = top1.Attacker.RoleProfile;
        var oppRP = top1.Opponent.RoleProfile;

        float roleFulfillmentProgress = 0;
        float maxScore = 0;
        float progress = 0;
        float penalties = 0;

        bool utilityRole = false;

        //--General comparisons/data gathering
        float attackerDamageTaken = top1.Attacker.BeginningHPR - top1.Attacker_EndOfTurnHP;
        float opponentDamageTaken = top1.Opponent.BeginningHPR - top1.Opponent_EndOfTurnHP;

        bool weHaveRecovery = ourRP.Traits.Contains( RoleTrait.RecoveryAbility ) || ourRP.Traits.Contains( RoleTrait.RecoveryItem ) || ourRP.Traits.Contains( RoleTrait.RecoveryMove );
        bool oppHasRecovery = oppRP.Traits.Contains( RoleTrait.RecoveryAbility ) || oppRP.Traits.Contains( RoleTrait.RecoveryItem ) || oppRP.Traits.Contains( RoleTrait.RecoveryMove );

        bool lockedDownAttacker = !top1.Attacker.VolatileStatuses.Contains( VolatileConditionID.Encore ) && top2.Attacker.VolatileStatuses.Contains( VolatileConditionID.Encore ) || !top1.Attacker.VolatileStatuses.Contains( VolatileConditionID.Taunt ) && top2.Attacker.VolatileStatuses.Contains( VolatileConditionID.Taunt );
        bool lockedDownOpponent = !top1.Opponent.VolatileStatuses.Contains( VolatileConditionID.Encore ) && top2.Opponent.VolatileStatuses.Contains( VolatileConditionID.Encore ) || !top1.Opponent.VolatileStatuses.Contains( VolatileConditionID.Taunt ) && top2.Opponent.VolatileStatuses.Contains( VolatileConditionID.Taunt );

        bool attackerInflictedSevere = top1.Opponent.SevereStatus == SevereConditionID.None && top2.Opponent.SevereStatus != SevereConditionID.None;
        var attackerCourtConditions1 = attCourt == CourtLocation.TopCourt ? _ai.CurrentFieldSnapshot.TopCourtConditions : _ai.CurrentFieldSnapshot.BottomCourtConditions;
        var attackerCourtConditions2 = attCourt == CourtLocation.TopCourt ? top2.Field.TopCourtConditions : top2.Field.BottomCourtConditions;
        var oppCourtConditions2 = oppCourt == CourtLocation.TopCourt ? top2.Field.TopCourtConditions : top2.Field.BottomCourtConditions;
        bool attackerSetHazards = bc.BattlefieldState.EntryHazardsOn_TheirSide <= 0 && ( oppCourtConditions2.ContainsKey( CourtConditionID.StealthRock ) || oppCourtConditions2.ContainsKey( CourtConditionID.LeechSeed ) || oppCourtConditions2.ContainsKey( CourtConditionID.Spikes ) || oppCourtConditions2.ContainsKey( CourtConditionID.ToxicSpikes ) || oppCourtConditions2.ContainsKey( CourtConditionID.StickyWeb ) );
        bool attackerRemovedHazards = bc.BattlefieldState.EntryHazardsOn_MySide > 0 && ( attackerCourtConditions2.ContainsKey( CourtConditionID.StealthRock ) || attackerCourtConditions2.ContainsKey( CourtConditionID.LeechSeed ) || attackerCourtConditions2.ContainsKey( CourtConditionID.Spikes ) || attackerCourtConditions2.ContainsKey( CourtConditionID.ToxicSpikes ) || attackerCourtConditions2.ContainsKey( CourtConditionID.StickyWeb ) );

        var fieldConditions1 = _ai.CurrentFieldSnapshot.FieldConditions;
        var fieldConditions2 = top2.Field.FieldConditions;

        //--Stability
        bool iAmStable = iSurviveNext && !( oppKillNext || ( oppThreatenNext && !futureExchangeEval.AttackerMovesFirst ) );
        bool oppIsStable = oppSurviveNext && !( iKillNext || ( iThreatenNext && !futureExchangeEval.OpponentMovesFirst ) );

        //--Role Fulfilment Evaluation
        switch( ourRP.PrimaryRole )
        {
            case RoleClass.Sweeper:
                maxScore = 0;

                maxScore++;
                if( oppIsKO )
                    progress += 1;
                else if( iThreaten && top2.AttackerMovedFirst )
                    progress += 1;
                else if( opponentDamageTaken >= 0.35f )
                    progress += 1;
                else
                    penalties += 1;

                maxScore++;
                if( top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                    progress += 1;
                else if( !top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                    progress += 1;
                else
                    penalties += 1;

                maxScore++;
                if( attackerDamageTaken <= 0.2f )
                    progress += 1;
                else
                    penalties += 1;

                if( !top2.AttackerMovedFirst && top2.Attacker_EndOfTurnHP <= 0f )
                    penalties += 1;

                if( iAmKO )
                    penalties += 1;

            break;

            case RoleClass.BulkyAttacker:
                maxScore = 0;

                maxScore++;
                if( oppIsKO )
                    progress += 1;
                else if( iThreaten && ( attackerDamageTaken <= 0.25f || top2.Attacker_EndOfTurnHP >= 0.4f ) )
                    progress += 1;
                else if( opponentDamageTaken >= 0.25f )
                    progress += 1;
                else
                    penalties += 1;

                maxScore++;
                if( !top1.AttackerMovedFirst && ( attackerDamageTaken <= 0.25f || top2.Attacker_EndOfTurnHP >= 0.4f ) )
                    progress += 1;

                maxScore++;
                if( attackerInflictedSevere || top2.AttackerPTKO > top1.AttackerPTKO || !top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                    progress += 1;

                if( attackerDamageTaken > 0.25f || top2.Attacker_EndOfTurnHP < 0.4f )
                    penalties += 1;

                if( iAmKO || top1.Attacker_EndOfTurnHP <= 0f )
                    penalties += 1;
                
            break;

            case RoleClass.RevengeKiller:
                maxScore = 0;

                maxScore++;
                if( oppIsKO )
                    progress += 1;
                else if( iThreaten && top1.Opponent.BeginningHPR < 0.5f )
                    progress += 1;
                else if( opponentDamageTaken >= 0.45f )
                    progress += 1;
                else
                    penalties += 1;

                maxScore++;
                bool opponentdamaged = top1.Opponent_EndOfTurnHP < 1f;
                bool weThreatenKO = top1.AttackerPTKO >= PotentialToKO.Dangerous;
                if( opponentdamaged && weThreatenKO && ( top1.AttackerMovedFirst || attackerDamageTaken < 0.3f ) )
                    progress += 1;
                else
                    penalties += 1;

                maxScore++;
                if( attackerDamageTaken <= 0.2f )
                    progress += 1;
                else
                    penalties += 1;

                if( iAmKO )
                    penalties += 1;

            break;

            case RoleClass.WallBreaker:
                maxScore = 0;

                maxScore++;
                if( oppIsKO )
                    progress += 1;
                else if( iThreaten && ( oppRP.PrimaryRole == RoleClass.Wall || oppRP.PrimaryRole == RoleClass.DefensiveSetup ) )
                    progress += 1;
                else if( opponentDamageTaken >= 0.5f )
                    progress += 1;
                else
                    penalties += 1;

                maxScore++;
                if( !oppIsStable )
                    progress += 1;

                maxScore++;
                if( attackerInflictedSevere || top2.AttackerPTKO > top1.AttackerPTKO )
                    progress += 1;
                else if( !top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                    progress += 1;
                else
                    penalties += 1;

                maxScore++;
                if( attackerDamageTaken <= 0.2f && opponentDamageTaken >= 0.25f )
                    progress += 1;
                else if( attackerDamageTaken > 0.2f && opponentDamageTaken < 0.25f && oppHasRecovery )
                    penalties += 1;

                if( opponentDamageTaken < 0.45f && oppHasRecovery )
                    penalties += 1;

                //--If we're slower the round we came in as a revenge killer and then die next round is the intent here
                if( !top1.AttackerMovedFirst && top2.Attacker_EndOfTurnHP <= 0f )
                    penalties += 1;

                if( iAmKO )
                    penalties += 1;

            break;

            case RoleClass.SetupSweeper:
                maxScore = 0;

                maxScore++;
                if( oppIsKO )
                    progress += 1;
                else if( iThreaten && top1.AttackerMovedFirst )
                    progress += 1;
                else if( opponentDamageTaken >= 0.4f )
                    progress += 1;
                else
                    penalties += 1;

                maxScore++;
                bool setupImprovesNextRound = top2.AttackerPTKO > top1.AttackerPTKO || !top1.AttackerMovedFirst && top2.AttackerMovedFirst;
                if( action.Type == ActionType.Setup && top1.Attacker_EndOfTurnHP > 0 && top2.Attacker_EndOfTurnHP > 0 && setupImprovesNextRound )
                    progress += 1;
                else
                    penalties += 1;

                maxScore++;
                if( action.Type != ActionType.Setup && top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                    progress += 1;
                else
                    penalties += 1;

                maxScore++;
                if( top1.Attacker_EndOfTurnHP >= 0.55f )
                    progress += 1;
                else
                    penalties += 1;

                if( !top2.AttackerMovedFirst && top2.Attacker_EndOfTurnHP <= 0f )
                    penalties += 1;

                if( iAmKO )
                    penalties += 1;

            break;

            case RoleClass.TrickRoomAbuser:
                maxScore = 0;

                if( top1.Field.TrickRoomActive )
                {
                    maxScore++;
                    if( oppIsKO )
                        progress += 1;
                    else if( iThreaten && top2.AttackerMovedFirst )
                        progress += 1;
                    else if( opponentDamageTaken >= 0.35f )
                        progress += 1;
                    else
                        penalties += 1;

                    maxScore++;
                    if( top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                        progress += 1;
                    else if( !top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                        progress += 1;
                    else
                        penalties += 1;

                    maxScore++;
                    if( attackerDamageTaken <= 0.2f )
                        progress += 1;
                    else
                        penalties += 1;

                    if( !top2.AttackerMovedFirst && top2.Attacker_EndOfTurnHP <= 0f )
                        penalties += 1;

                    if( iAmKO )
                        penalties += 1;
                }
                else
                {
                    maxScore++;
                    if( oppIsKO )
                        progress += 1;
                    else if( iThreaten && ( attackerDamageTaken <= 0.25f || top2.Attacker_EndOfTurnHP >= 0.4f ) )
                        progress += 1;
                    else if( opponentDamageTaken >= 0.25f )
                        progress += 1;
                    else
                        penalties += 1;

                    maxScore++;
                    if( !top1.AttackerMovedFirst && ( attackerDamageTaken <= 0.25f || top2.Attacker_EndOfTurnHP >= 0.4f ) )
                        progress += 1;

                    maxScore++;
                    if( attackerInflictedSevere || top2.AttackerPTKO > top1.AttackerPTKO || !top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                        progress += 1;

                    if( attackerDamageTaken > 0.25f || top2.Attacker_EndOfTurnHP < 0.4f )
                        penalties += 1;

                    if( iAmKO || top1.Attacker_EndOfTurnHP <= 0f )
                        penalties += 1;
                }

            break;

            case RoleClass.Wall:
                maxScore = 0;

                maxScore++;
                if( iAmStable )
                    progress++;

                maxScore++;
                if( attackerDamageTaken <= 0.3f )
                    progress++;
                else if( opponentDamageTaken > attackerDamageTaken )
                    progress++;

                maxScore++;
                bool goodChipNextTurn = top2.Opponent.BeginningHPR - top1.Opponent_EndOfTurnHP >= 0.2f || attackerInflictedSevere;
                if( goodChipNextTurn )
                    progress++;

                maxScore++;
                if( !oppIsStable )
                    progress++;
                else if( !oppThreatens )
                    progress++;
                else
                    penalties++;
                
                if( attackerDamageTaken >= 0.5f && !weHaveRecovery )
                    penalties++;

                if( top1.Attacker_EndOfTurnHP < 0 )
                    penalties++;

                if( top2.Attacker_EndOfTurnHP < 0 && !goodChipNextTurn )
                    penalties++;

            break;

            case RoleClass.DefensiveSetup:
                maxScore = 0;

                maxScore++;
                if( iAmStable )
                    progress++;

                maxScore++;
                if( attackerDamageTaken <= 0.3f )
                    progress++;
                else if( opponentDamageTaken > attackerDamageTaken )
                    progress++;

                maxScore++;
                if( oppThreatens && !oppThreatenNext )
                    progress++;
                else if( action.Type == ActionType.Setup && oppThreatenNext )
                    penalties++;

                maxScore++;
                bool setupMakesProgress = _unitSim.PokemonIsIronDefenseBodyPress( top1.Attacker.Pokemon ) || top2.AttackerPTKO > top1.AttackerPTKO || top1.OpponentPTKO > top2.OpponentPTKO;
                if( action.Type == ActionType.Setup && iAmStable )
                    progress++;

                if( setupMakesProgress )
                    progress++;

                maxScore++;
                if( !oppIsStable )
                    progress++;
                else if( !oppThreatens )
                    progress++;
                else
                    penalties++;
                
                if( attackerDamageTaken >= 0.5f && !weHaveRecovery )
                    penalties++;

                if( top1.Attacker_EndOfTurnHP < 0 )
                    penalties++;

            break;

            case RoleClass.UtilitySupport:
                utilityRole = true;
                maxScore = 0;

                bool usingStatusMove = action.Type == ActionType.OffensiveStatus || action.Type == ActionType.Support;

                var effectTarget = action.MovePayload.MoveSO.MoveEffects.Target;
                var moveTarget = action.MovePayload.MoveSO.MoveTarget;

                bool targetsAlly = usingStatusMove && ( effectTarget == EffectTarget.AllySide || moveTarget == MoveTarget.Ally || moveTarget == MoveTarget.AllySide );

                bool iAmCompromised = iAmKO && attackerDamageTaken >= 0.5f;
                bool usingSupportMove = usingStatusMove && _ai.UnitSim.MoveIsSupport( action.MovePayload );
                bool settingReflect = usingSupportMove && action.MovePayload.MoveSO.Name == "Reflect" && !attackerCourtConditions1.ContainsKey( CourtConditionID.Reflect );
                bool settingLightScreen = usingSupportMove && action.MovePayload.MoveSO.Name == "Light Screen" && !attackerCourtConditions1.ContainsKey( CourtConditionID.LightScreen );
                bool settingAuroraVeil = usingSupportMove && action.MovePayload.MoveSO.Name == "Aurora Veil" && !attackerCourtConditions1.ContainsKey( CourtConditionID.AuroraVeil );

                bool settingScreens = settingReflect || settingLightScreen || settingAuroraVeil;

                bool usingRedirection = usingStatusMove && _ai.UnitSim.MoveIsRedirection( action.MovePayload );
                bool buffingAlly = _ai.IsDoubleBattle && usingStatusMove && ( action.MovePayload.MoveSO.Name == "Coaching" || action.MovePayload.MoveSO.Name == "Howl" );
                bool healingAlly = _ai.IsDoubleBattle && usingSupportMove && targetsAlly && action.MovePayload.MoveSO.HealType != HealType.None;

                bool fakeOut = action.Type == ActionType.Attack && _ai.CanUseFakeOut( top1.Attacker, top1.Opponent ) && action.MovePayload.MoveSO.Name == "Fake Out";

                bool acceptableChip = opponentDamageTaken >= 0.2f;

                bool didNothing = !settingReflect && !settingLightScreen && !settingAuroraVeil && !usingRedirection && !buffingAlly && !healingAlly && !fakeOut && !acceptableChip;
                bool doublesSupport = _ai.IsDoubleBattle && !didNothing;

                maxScore++;
                if( usingSupportMove && !iAmCompromised )
                    progress++;

                maxScore++;
                if( settingScreens && !iAmCompromised )
                    progress++;

                maxScore++;
                if( doublesSupport )
                    progress++;

                if( lockedDownAttacker )
                    penalties++;

                if( !top1.AttackerMovedFirst && !iAmKO && attackerDamageTaken > 0.5f )
                    penalties++;

                if( iAmKO || iAmCompromised && didNothing )
                    penalties++;

            break;

            case RoleClass.Disrupter:
                utilityRole = true;
                maxScore = 0;

                maxScore++;
                if( ( attackerInflictedSevere || attackerSetHazards || attackerRemovedHazards ) && ( !iAmKO || attackerDamageTaken < 0.5f ) )
                    progress++;
                else
                    penalties++;
                
                maxScore++;
                if( ( action.Type == ActionType.OffensiveStatus && !iAmKO && attackerDamageTaken < 0.5f ) || ( action.Type == ActionType.OffensiveStatus && _ai.IsDoubleBattle ) )
                    progress++;
                else
                    penalties++;

                maxScore++;
                if( ( action.Type == ActionType.Support && !iAmKO && attackerDamageTaken < 0.5f ) || ( action.Type == ActionType.Support && _ai.IsDoubleBattle ) )
                    progress++;
                else
                    penalties++;

                maxScore++;
                if( !top1.OpponentCanAct || !top2.OpponentCanAct )
                    progress++;

                maxScore++;
                if( lockedDownOpponent )
                    progress++;

                maxScore++;
                if( action.Type == ActionType.Attack && _ai.CanUseFakeOut( top1.Attacker, top1.Opponent ) && action.MovePayload.MoveSO.Name == "Fake Out" )
                    progress++;

                maxScore++;
                if( opponentDamageTaken >= 0.2f )
                    progress++;

                if( lockedDownAttacker )
                    penalties++;

                if( top2.Attacker_EndOfTurnHP < 0 && ( !lockedDownOpponent || !attackerInflictedSevere || !attackerSetHazards || !attackerRemovedHazards ) )
                    penalties++;

                if( !top1.AttackerMovedFirst && !iAmKO && attackerDamageTaken > 0.5f )
                    penalties++;

            break;

            case RoleClass.Pivot:
                utilityRole = true;
                maxScore = 0;

                maxScore++;
                if( top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                    progress++;
                else if( !top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                    progress++;
                else
                    penalties++;

                maxScore++;
                if( top2.AttackerPTKO > top1.AttackerPTKO )
                    progress++;
                else if( iThreatenNext )
                    progress++;
                
                maxScore++;
                if( attackerDamageTaken <= 0.3f )
                    progress++;
                else
                    penalties++;

                maxScore++;
                if( ee.OpponentSwitchProbability >= 0.7f )
                    progress++;

                maxScore++;
                if( oppThreatens && !oppThreatenNext )
                    progress++;

            break;

            case RoleClass.HazardControl:
                utilityRole = true;
                maxScore = 0;

                maxScore++;
                if( attackerSetHazards || attackerRemovedHazards )
                    progress++;

                if( !attackerSetHazards && !attackerRemovedHazards && iAmKO )
                    penalties++;

            break;

            case RoleClass.FieldControl:
                utilityRole = true;
                maxScore = 0;

                bool weatherChanged = _ai.CurrentFieldSnapshot.Weather != top2.Field.Weather;

                bool sunSetter = top1.Attacker.Ability == AbilityID.Drought || top2.Attacker.Ability == AbilityID.Drought || _ai.UnitSim.CheckHasMove( top1.Attacker, "Sunny Day" );
                bool rainSetter = top1.Attacker.Ability == AbilityID.Drizzle || top2.Attacker.Ability == AbilityID.Drizzle || _ai.UnitSim.CheckHasMove( top1.Attacker, "Rain Dance" );
                bool sandSetter = top1.Attacker.Ability == AbilityID.Sandstream || top2.Attacker.Ability == AbilityID.Sandstream || _ai.UnitSim.CheckHasMove( top1.Attacker, "Sandstorm" );
                bool snowSetter = top1.Attacker.Ability == AbilityID.SnowWarning || top2.Attacker.Ability == AbilityID.SnowWarning || _ai.UnitSim.CheckHasMove( top1.Attacker, "Snowscape" );

                bool sunSet = _ai.CurrentFieldSnapshot.Weather == WeatherConditionID.None && top2.Field.Weather == WeatherConditionID.SUNNY && sunSetter;
                bool rainSet = _ai.CurrentFieldSnapshot.Weather == WeatherConditionID.None && top2.Field.Weather == WeatherConditionID.RAIN && rainSetter;
                bool sandSet = _ai.CurrentFieldSnapshot.Weather == WeatherConditionID.None && top2.Field.Weather == WeatherConditionID.SANDSTORM && sandSetter;
                bool snowSet = _ai.CurrentFieldSnapshot.Weather == WeatherConditionID.None && top2.Field.Weather == WeatherConditionID.SNOW && snowSetter;

                bool setTailwind = top1.Attacker.RoleProfile.Traits.Contains( RoleTrait.TailwindSetter ) && !attackerCourtConditions1.ContainsKey( CourtConditionID.Tailwind ) && attackerCourtConditions2.ContainsKey( CourtConditionID.Tailwind );
                bool setTrickRoom = top1.Attacker.RoleProfile.Traits.Contains( RoleTrait.TrickRoomSetter ) && !fieldConditions1.ContainsKey( FieldConditionID.TrickRoom ) && fieldConditions2.ContainsKey( FieldConditionID.TrickRoom );
                bool setWeather = sunSet || rainSet || sandSet || snowSet;

                didNothing = !setTailwind && !setTrickRoom && !setWeather && opponentDamageTaken < 0.2f;

                maxScore++;
                if( weatherChanged && setWeather )
                    progress++;

                maxScore++;
                if( setTailwind || setTrickRoom )
                    progress++;

                maxScore++;
                if( opponentDamageTaken >= 0.2f )
                    progress++;

                if( didNothing )
                    penalties++;

                if( iAmKO || ( didNothing && top2.Attacker_EndOfTurnHP < 0f ) )
                    penalties++;

            break;

            case RoleClass.AllRounder:
                maxScore = 0;

                bool offensive = opponentDamageTaken >= 0.25f || iThreaten || iThreatenNext;
                bool defensive = attackerDamageTaken <= 0.3f;
                bool stability = !oppIsStable || iAmStable;
                bool improvedPosition = ( !top1.AttackerMovedFirst && top2.AttackerMovedFirst ) || ( top2.AttackerPTKO > top1.AttackerPTKO );
                bool utility = attackerInflictedSevere || attackerSetHazards || attackerRemovedHazards;

                didNothing = !offensive && !defensive && !stability && !improvedPosition && !utility; 

                maxScore++;
                if( offensive )
                    progress++;

                maxScore++;
                if( defensive )
                    progress++;
                
                maxScore++;
                if( stability )
                    progress++;

                maxScore++;
                if( !iAmKO )
                    progress++;
                else
                    penalties++;

                maxScore++;
                if( improvedPosition )
                    progress++;

                maxScore++;
                if( utility )
                    progress++;

                if( didNothing )
                    penalties++;

            break;
        }

        roleFulfillmentProgress = Mathf.Clamp( ( progress - penalties ) / maxScore, -1f, 1f );
        _ai.CurrentLog.Add( $"[Build PBS] Role Fulfillment checked. Attacker's Primary Role: {ourRP.PrimaryRole}." );
        _ai.CurrentLog.Add( $"[Build PBS] Progress: {progress}, Penalties: {penalties}, Max Score: {maxScore}" );
        _ai.CurrentLog.Add( $"[Build PBS] Role Fulfillment Progress: {roleFulfillmentProgress}" );

        //--Post Loss Revenge Quality
        int revengeScore = 0;
        if( iAmKO && !oppIsKO )
        {
            List<IBattleAIUnit> opps = new() { top1.Opponent };
            var revengeKiller = _ai.SwitchCommand.GetSwitch_Revenge( opps );
            if( revengeKiller.Top.Opponent_DiesBeforeActing )
            {
                revengeScore += 15;
                _ai.CurrentLog.Add( $"[Build PBS] Revenge Score: {revengeScore}" );
            }
            else if( revengeKiller.Top.AttackerPTKO >= PotentialToKO.Dangerous && revengeKiller.Top.OpponentPTKO <= PotentialToKO.Risky )
            {
                revengeScore += 10;
                _ai.CurrentLog.Add( $"[Build PBS] Revenge Score: {revengeScore}" );
            }
            else if( revengeKiller.Top.AttackerMovedFirst && revengeKiller.Top.AttackerPTKO >= PotentialToKO.Safe )
            {
                revengeScore += 5;
                _ai.CurrentLog.Add( $"[Build PBS] Revenge Score: {revengeScore}" );
            }
        }

        return new()
        {
            //--Sim Units
            Current_Attacker = top1.Attacker,
            Current_Opponent = top1.Opponent,
            Next_Attacker = top2.Attacker,
            Next_Opponent = top2.Opponent,

            //--Material
            IGetImmediateKO = oppIsKO && top1.Opponent_DiesBeforeActing,
            IAmKONow = iAmKO,
            OppIsKONow = oppIsKO,
            MutualKO = top1.MutualKO,
            MyRemainingPieces = myRemainingPieces,
            OppRemainingPieces = oppRemainingPieces,
            MaterialDelta = myRemainingPieces - oppRemainingPieces,
            MyActiveValue_AfterTurn = myValue,
            OppActiveValue_AfterTurn = oppValue,
            ValueDelta_AfterTurn = myValue - oppValue,

            //--Economy
            IControlNextTurn = futureExchangeEval.AttackerMovesFirst && ( iKillNext || ( iThreatenNext && !oppThreatenNext ) ),
            OppControlNextTurn = futureExchangeEval.OpponentMovesFirst && ( oppKillNext || ( oppThreatenNext && !iThreatenNext ) ),

            //--Stability
            IAmStable = iAmStable,
            OppIsStable = oppIsStable,

            //--Utility
            ICreateDecisiveState = iCreatesDecisiveState,
            ICreatePressureState = iCreatesPressureState,
            OppCreatesDecisiveState = oppCreatesDecisiveState,
            OppCreatesPressureState = oppCreatesPressureState,

            //--Role Fulfillment
            // AttackerFulfilledRole = attackerFulfilledRole,
            // OpponentFulfilledRole = opponentFulfilledRole,
            // AttackerFulfilledUtilityRole = attackerUtilityRoleFulfilled,
            // OpponentFulfilledUtilityRole = opponentUtilityRoleFulfilled,
            RoleFulfillmentProgress = roleFulfillmentProgress,
            UtilityRole = utilityRole,

            //--Tempo
            RevengeScore = revengeScore,
            FutureTempoState = futureTempoState.TempoState,

            //--Raw Info
            IWillSurviveNext = iSurviveNext,
            OppWillSurviveNext = oppSurviveNext,

            IThreatenImmediate = iThreaten,
            OppThreatensImmediate = oppThreatens,

            IThreatenNext = iThreatenNext,
            OppThreatenNext = oppThreatenNext,

            IKillNext = iKillNext,
            OppKillNext = oppKillNext,

            AttackerWillMoveFirst = futureExchangeEval.AttackerMovesFirst,
            OpponentWillMoveFirst = futureExchangeEval.OpponentMovesFirst,
        };
    }

    public int EvaluatePBS( ProjectedBoardState pbs )
    {
        int score = 0;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"==========================" );
        _ai.CurrentLog.Add( $"=====[EVALUATING PBS]=====" );
        _ai.CurrentLog.Add( $"==========================" );
        _ai.CurrentLog.Add( $"" );

        //--------------------------------------------------
        //--Material
        //--------------------------------------------------

        int materialScore = 0;

        // Piece count matters, but lightly
        materialScore += pbs.MaterialDelta * 10;

        //--Material Take Bonus
        if( !pbs.IAmKONow && pbs.OppIsKONow )
            materialScore += 10;

        //--Safe KO Check is a larger positive material outcome
        if( pbs.OppIsKONow && !pbs.IWillSurviveNext )
            materialScore += 10;
        else if( pbs.IGetImmediateKO && ( pbs.IWillSurviveNext || pbs.IThreatenNext ) )
            materialScore += 20;

        // Active piece value matters slightly more
        materialScore += Mathf.Clamp( pbs.ValueDelta_AfterTurn / 2, -15, 15 );

        score += materialScore;
        _ai.CurrentLog.Add( $"[PBS] Material Score: {materialScore}. Score: {score}" );

        //--------------------------------------------------
        //--Converstion
        //--------------------------------------------------
        int conversionScore = 0;

        bool immediateAdvantage = pbs.OppIsKONow && !pbs.OppThreatensImmediate;
        if( immediateAdvantage )
            conversionScore += 60;
        else if( pbs.OppIsKONow && !pbs.OppThreatenNext && pbs.MyRemainingPieces >= pbs.OppRemainingPieces - 1 )
            conversionScore += 40;
        else if( pbs.OppIsKONow )
            conversionScore += 20;

        score += conversionScore;

        _ai.CurrentLog.Add( $"[PBS] Checking Conversion. I Get Immediate KO: {pbs.OppIsKONow}, I have immediate advantage: {immediateAdvantage}. Conversion Score: {conversionScore}" );

        //--------------------------------------------------
        //--Stability
        //--------------------------------------------------

        int stabilityScore = 0;

        if( pbs.IAmStable && !pbs.OppIsStable )
            stabilityScore += 30;
        else if( !pbs.IAmStable && pbs.OppIsStable )
            stabilityScore -= 30;
        else if( !pbs.IAmStable && !pbs.OppIsStable )
            stabilityScore -= 10; // chaotic state slightly bad

        score += stabilityScore;
        _ai.CurrentLog.Add( $"[PBS] Stability Score: {stabilityScore}. Score: {score}" );

        //--------------------------------------------------
        //--Control (Initiative / who dictates next turn)
        //--------------------------------------------------

        int controlScore = 0;

        if( pbs.IControlNextTurn && !pbs.OppControlNextTurn )
            controlScore += 15;
        else if( !pbs.IControlNextTurn && pbs.OppControlNextTurn )
            controlScore -= 20;

        score += controlScore;
        _ai.CurrentLog.Add( $"[PBS] Control Score: {controlScore}. Score: {score}" );

        //--------------------------------------------------
        //--Pressure
        //--------------------------------------------------

        int pressureScore = 0;

        //--KO Pressure
        float myKOThreat = GetKOThreatMultiplier( pbs.IKillNext, pbs.IThreatenNext, pbs.AttackerWillMoveFirst, pbs.IWillSurviveNext, pbs.MyActiveValue_AfterTurn );
        float oppKOThreat = GetKOThreatMultiplier( pbs.OppKillNext, pbs.OppThreatenNext, pbs.OpponentWillMoveFirst, pbs.OppWillSurviveNext, pbs.OppActiveValue_AfterTurn );

        //--Utility Pressure
        float myUtilityThreat = GetUtilityThreatMultiplier( pbs.ICreateDecisiveState, pbs.ICreatePressureState, pbs.AttackerWillMoveFirst, pbs.IWillSurviveNext, pbs.MyActiveValue_AfterTurn, pbs.OppActiveValue_AfterTurn );
        float oppUtilityThreat = GetUtilityThreatMultiplier( pbs.OppCreatesDecisiveState, pbs.OppCreatesPressureState, pbs.OpponentWillMoveFirst, pbs.OppWillSurviveNext, pbs.OppActiveValue_AfterTurn, pbs.MyActiveValue_AfterTurn );
        float myTotalPressure = myKOThreat + myUtilityThreat;
        float oppTotalPressure = oppKOThreat + oppUtilityThreat;

        float threatDelta = myTotalPressure - oppTotalPressure;

        pressureScore = Mathf.RoundToInt( Mathf.Clamp( threatDelta * 0.75f, -25f, 25f ) );

        score += pressureScore;
        _ai.CurrentLog.Add( $"[PBS] Pressure Score: {pressureScore} (My KO Threat: {myKOThreat}, Opp KO Threat: {oppKOThreat}, My Utility Threat: {myUtilityThreat}, Opp Utility Threat: {oppUtilityThreat}). Score: {score}" );

        //--------------------------------------------------
        //--Role Fulfillment
        //--------------------------------------------------

        int roleScore = 0;

        // if( pbs.AttackerFulfilledRole && !pbs.OpponentFulfilledRole )
        //     roleScore += 15;
        // else if( !pbs.AttackerFulfilledRole && pbs.OpponentFulfilledRole )
        //     roleScore -= 15;

        // //--Utility being effective bias
        // if( pbs.AttackerFulfilledUtilityRole && !pbs.OpponentFulfilledRole )
        //     roleScore += 10;
        // else if( pbs.OpponentFulfilledUtilityRole && pbs.AttackerFulfilledRole )
        //     roleScore -= 10;

        roleScore += Mathf.RoundToInt( pbs.RoleFulfillmentProgress * 50f );

        //--Utility Bias
        if( pbs.UtilityRole )
            roleScore += Mathf.RoundToInt( pbs.RoleFulfillmentProgress * 25f );

        score += roleScore;
        _ai.CurrentLog.Add( $"[PBS] Role Score: {roleScore}. Score: {score}" );

        //--------------------------------------------------
        //--Tempo
        //--------------------------------------------------

        int tempoScore = 0;

        tempoScore += pbs.FutureTempoState switch
        {
            TempoState.WinningHard  => +10,
            TempoState.Winning      => +5,
            TempoState.Neutral      => 0,
            TempoState.Losing       => -5,
            TempoState.LosingHard   => -10,
            _ => 0
        };

        //--Revenge handling (only if relevant)
        if( pbs.IAmKONow && !pbs.OppIsKONow )
        {
            tempoScore += Mathf.FloorToInt( pbs.RevengeScore * 0.5f );
            _ai.CurrentLog.Add( $"[PBS] Revenge Scenario Triggered. Revenge Score: {pbs.RevengeScore}. Tempo Score Adjusted: {tempoScore}. Score: {score}" );
        }

        //--Tempo Lock
        if( pbs.IGetImmediateKO && !pbs.OppThreatensImmediate )
        {
            tempoScore += 25;

            if( pbs.IThreatenNext && pbs.AttackerWillMoveFirst && !pbs.OppThreatenNext )
                tempoScore += 20;
            else if( !pbs.OppThreatenNext )
                tempoScore += 10;
        }

        score += tempoScore;
        _ai.CurrentLog.Add( $"[PBS] Tempo Score: {tempoScore}. Score: {score}" );

        //--------------------------------------------------
        //--Trade Pieces Check
        //--------------------------------------------------

        if( pbs.IAmKONow && pbs.OppIsKONow )
        {
            if( pbs.ValueDelta_AfterTurn < 0 )
                score += 10; //--good trade
            else if( pbs.ValueDelta_AfterTurn > 0 )
                score -= 10; //--bad trade

            _ai.CurrentLog.Add( $"[PBS] Trade Scenario Detected. Value Delta: {pbs.ValueDelta_AfterTurn}. Score: {score}" );
        }

        //--------------------------------------------------
        //--Sacrifice Quality Check
        //--------------------------------------------------

        int sacScore = 0;

        if( pbs.IAmKONow && !pbs.OppIsKONow )
        {
            bool createdKill = pbs.IKillNext;
            bool createdThreat = pbs.IThreatenNext;
            bool strongRevenge = pbs.RevengeScore >= 10;

            // Good sacrifice: we meaningfully advance position
            if( createdKill || ( createdThreat && strongRevenge ) )
            {
                sacScore += 10;
            }
            // Neutral-light: some pressure but unclear payoff
            else if( createdThreat || strongRevenge )
            {
                sacScore += 3;
            }
            // Bad sacrifice: we die and accomplish nothing
            else
            {
                sacScore -= 12;
            }

            // Slight value awareness (don’t overtrade high-value pieces)
            if( pbs.MyActiveValue_AfterTurn > pbs.OppActiveValue_AfterTurn )
            {
                sacScore -= 4;
            }

            _ai.CurrentLog.Add( $"[PBS] Sacrifice Score: {sacScore}. Score: {score + sacScore}" );
        }

        score += sacScore;

        //--------------------------------------------------
        //--------------------------------------------------
        pbs.MaterialScore   = materialScore;
        pbs.ConversionScore = conversionScore;
        pbs.Stabilityscore  = stabilityScore;
        pbs.ControlScore    = controlScore;
        pbs.PressureScore   = pressureScore;
        pbs.RoleScore       = roleScore;
        pbs.TempoScore      = tempoScore;
        pbs.SacScore        = sacScore;
        //--------------------------------------------------
        //--------------------------------------------------

        return score;
    }

    private float GetKOThreatMultiplier( bool getsKill, bool threatens, bool movesFirst, bool survives, int pieceValue )
    {
        //--Outcome severity
        float severity = getsKill ? 1.0f : threatens ? 0.6f : 0.25f;

        //--Tempo modifier
        float speedMod = movesFirst ? 1.15f : 0.85f;

        //--Survival modifier
        float survivalMod = 1.0f;

        if( !survives )
        {
            if( getsKill )
                survivalMod = 0.55f;
            else if( threatens )
                survivalMod = 0.4f;
            else
                survivalMod = 0.25f;
        }

        //--Agency check on Survival
        bool hasAgency = survives && ( getsKill || threatens );
        if( !hasAgency )
            survivalMod *= 0.7f;

        float result = pieceValue * severity * speedMod * survivalMod;
        return result;
    }

    private float GetUtilityThreatMultiplier( bool createsDecisive, bool createsPressure, bool movesfirst, bool survives, int ourPieceValue, int theirPieceValue )
    {
        float severity = 0f;

        severity += createsDecisive ? 1.0f : 0.1f;
        severity += createsPressure ? 0.5f : 0.15f;

        float speedMod = movesfirst ? 1.15f : 0.85f;

        float survivalMod = 1.0f;

        if( !survives )
        {
            if( createsDecisive && createsPressure )
                survivalMod = 0.85f;
            else if( createsDecisive )
                survivalMod = 0.7f;
            else if( createsPressure )
                survivalMod = 0.5f;
            else
                survivalMod = 0.3f;
        }

        bool hasAgency = createsDecisive || createsPressure;
        if( !hasAgency )
            survivalMod *= 0.7f;

        float valueFactor = ( ourPieceValue * 0.4f + theirPieceValue * 0.6f );

        float result = valueFactor * severity * speedMod * survivalMod;
        return result;
    }

    public TempoStateResult GetTempoState( ExchangeEvaluation eval )
    {
        // Debug.Log( $"[AI Scoring][Get Tempo] Starting Tempo State Check for Attacker: {attacker.Pokemon.NickName} vs Target: {target.Pokemon.NickName}" );
        var tempo = ClassifyTempo( eval );

        bool attackerHasPriorityAdvantage = eval.AttackerMovesFirst && !eval.OpponentMovesFirst;
        bool targetHasPriorityAdvantage = eval.OpponentMovesFirst && !eval.AttackerMovesFirst;

        // Debug.Log( $"[AI Scoring][Get Tempo] Final Tempo State: {tempo}, Attacker: {attacker.Pokemon.NickName} vs Target: {target.Pokemon.NickName}" );

        return new(){ TempoState = tempo, AttackerHasPriority = attackerHasPriorityAdvantage, TargetHasPriority = targetHasPriorityAdvantage };
    }

    public ExchangeEvaluation EvaluateExchange( IBattleAIUnit attacker, IBattleAIUnit target, bool log = false )
    {
        //--Potential to KO
        //--Attacker PTKO Target
        var attackerMTR = _ai.MoveCommand.GetMove_BestAttack( attacker, target, false, "Evaluate Exchange (attacker vs target)" );
        var targetWSR = Get_EstimatedDamageResult( attacker, target, attackerMTR );
        float targetHP = target.BeginningHPR;

        PotentialToKOResult attackerPTKO_target = Get_PotentialToKOResult( targetWSR, attackerMTR, targetHP );

        //--Target PTKO Attacker
        var targetMTR = _ai.MoveCommand.GetMove_BestAttack( target, attacker, false, "Evaluate Exchange (target vs attacker)" );
        var attackerWSR = Get_EstimatedDamageResult( target, attacker, targetMTR );
        float attackerHP = attacker.BeginningHPR;

        PotentialToKOResult targetPTKO_attacker = Get_PotentialToKOResult( attackerWSR, targetMTR, attackerHP );

        // Debug.Log( $"[AI Scoring][Get Tempo] PTKO's Checked! Results: Attacker PTKO Target: {attackerPTKO_target.PTKO}, Target PTKO Attacker: {targetPTKO_attacker.PTKO}" );

        //--Speed Check
        int attackerSpeed = _ai.GetUnitContextualSpeed( attacker );
        int targetSpeed = _ai.GetUnitContextualSpeed( target );
        bool attackerMovesFirst;
        bool targetMovesFirst;
        var attMovePrio = attackerMTR.Move.Priority;
        var tarMovePrio = targetMTR.Move.Priority;

        //--Move priority handling
        if( attMovePrio != tarMovePrio )
            attackerMovesFirst = attMovePrio > tarMovePrio;
        else
            attackerMovesFirst = attackerSpeed > targetSpeed;

        if( !attackerMovesFirst )
            targetMovesFirst = true;
        else
            targetMovesFirst = false;

        // Debug.Log( $"[AI Scoring][Get Tempo] Made speed comparisons! Results: Attacker Speed: {attackerSpeed}, Target Speed: {targetSpeed}, Attacker Priority: {attackerHasPriorityAdvantage}, Target Priority: {targetHasPriorityAdvantage}, Attacker Moves First: {attackerMovesFirst}, Target Moves First: {targetMovesFirst}" );

        bool attackerThreatensKO_onTarget       = attackerPTKO_target.PTKO > PotentialToKO.Risky; //--revert back to >= if not good
        bool targetThreatensKO_onAttacker       = targetPTKO_attacker.PTKO > PotentialToKO.Risky; //--revert back to >= if not good
        bool attackerSurvives_targetAttack      = targetPTKO_attacker.PTKO <= PotentialToKO.Risky;
        bool targetSurvives_attackerAttack      = attackerPTKO_target.PTKO <= PotentialToKO.Risky;

        // Debug.Log( $"[AI Scoring][Get Tempo] Final Comparisons Made! Results: Attacker Threatens KO: {attackerThreatensKO_onTarget}, Target Threatens KO: {targetThreatensKO_onAttacker}, Attacker Survives: {attackerSurvives_targetAttack}, Target Survives: {targetSurvives_attackerAttack}" );
        
        //--Predict Forced Switch for this turn
        float attackerSwitchesProbability = _unitSim.PredictSwitchProbability( targetPTKO_attacker.PTKO, attackerPTKO_target.PTKO, targetMovesFirst, targetHP, attackerHP, attacker.Expendability, log, attacker.Name );
        float opponentSwitchesProbability = _unitSim.PredictSwitchProbability( attackerPTKO_target.PTKO, targetPTKO_attacker.PTKO, attackerMovesFirst, attackerHP, targetHP, target.Expendability, log, target.Name );
        bool attackerForcesSwitch = opponentSwitchesProbability > 0.7f;
        bool targetForcesSwitch = attackerSwitchesProbability > 0.7f;

        ExchangeState state = ExchangeState.Neutral;

        if( attackerForcesSwitch )
            state = ExchangeState.OpponentForcedOut;
        
        if( attackerThreatensKO_onTarget && !targetThreatensKO_onAttacker )
            state = ExchangeState.Pressure;

        ExchangeEvaluation eval = new()
        {
            AttackerName = attacker.Name,
            OpponentName = target.Name,

            AttackerMovesFirst = attackerMovesFirst,
            OpponentMovesFirst = targetMovesFirst,

            AttackerHasPriorityMove = _ai.Check_UnitHasPriority( attacker, target ),
            OpponentHasPriorityMove = _ai.Check_UnitHasPriority( target, attacker ),

            AttackerThreatensKO = attackerThreatensKO_onTarget,
            OpponentThreatensKO = targetThreatensKO_onAttacker,

            AttackerKillsFirst = attackerMovesFirst && attackerThreatensKO_onTarget,
            OpponentKillsFirst = targetMovesFirst && targetThreatensKO_onAttacker,

            AttackerSurvives = attackerSurvives_targetAttack,
            OpponentSurvives = targetSurvives_attackerAttack,

            AttackerPTKOR = attackerPTKO_target,
            OpponentPTKOR = targetPTKO_attacker,

            AttackerHPR = attackerHP,
            OpponentHPR = targetHP,

            OpponentSwitches = attackerForcesSwitch,
            AttackerSwitches = targetForcesSwitch,

            AttackerMoveName = attackerMTR.Move.MoveSO.Name,
            OpponentMoveName = targetMTR.Move.MoveSO.Name,

            ExchangeState = state,
        };

        return eval;
    }

    public TempoState ClassifyTempo( ExchangeEvaluation eval )
    {
        //--Immediate Kill control
        if( eval.AttackerKillsFirst )
            return TempoState.WinningHard;

        if( eval.OpponentKillsFirst )
            return TempoState.LosingHard;

        //--Both potentially survive to attack
        if( eval.AttackerSurvives && !eval.OpponentSurvives )
            return TempoState.Winning;

        if( eval.OpponentSurvives && !eval.AttackerSurvives )
            return TempoState.Losing;
        
        //--Neutral, if we made it this far.
        return TempoState.Neutral;
    }

    public BoardContext GetBoardContext( IBattleAIUnit attacker, IBattleAIUnit target, ExchangeEvaluation eval )
    {
        //--Safe Pivot Check
        bool safePivotExists = CheckForSafePivot( target );

        //--Is Forced Trade Detection
        bool lowHP = eval.AttackerHPR < 0.3f;
        bool likelyDying = eval.OpponentPTKOR.PTKO >= PotentialToKO.Dangerous;
        bool isForced = ( likelyDying && !safePivotExists ) || ( lowHP && eval.OpponentPTKOR.PTKO >= PotentialToKO.Risky );

        //--Material Information
        var myTeamAlive = _ai.GetRemainingPartyAs_IBattleAIUnits( attacker.Pokemon );
        var oppTeamAlive = _ai.GetRemainingPartyAs_IBattleAIUnits( target.Pokemon );

        int myAlive = myTeamAlive.Count;
        int oppAlive = oppTeamAlive.Count;

        float myTeamHPPercent = GetRemainingTeamHP( myTeamAlive );
        float oppTeamHPPercent = GetRemainingTeamHP( oppTeamAlive );

        bool isTerminal = myAlive <= 2;

        //--Our Expendability Check
        float hp = _ai.Get_HPRatio( _ai.Unit.Pokemon );
        float expendability = GetExpendability( _ai.ThisUnitAdapter, hp );

        //--Material Status
        bool isAhead = false;
        bool isBehind = false;

        if( myAlive > oppAlive )
        {
            if( myTeamHPPercent > oppTeamHPPercent * 0.6f )
                isAhead = true;
        }
        else if( myAlive < oppAlive )
        {
            if( myTeamHPPercent < oppTeamHPPercent * 1.4f )
                isBehind = true;
        }
        else
        {
            float ratio = 1f;
            
            if( oppTeamHPPercent > 0.0001 )
                ratio = myTeamHPPercent / oppTeamHPPercent;

            if( ratio >= 1.25f )
                isAhead = true;
            else if( ratio <= 0.75f )
                isBehind = true;
        }

        BattlefieldState bfs = GetBattlefieldState( _ai.UnitSim.BuildSimField() );

        BoardContext context = new()
        {
            IsForcedTrade = isForced,

            HasSafePivot = safePivotExists,

            IsAhead = isAhead,
            IsBehind = isBehind,

            MyTeamHPPercent = myTeamHPPercent,
            OppTeamHPPercent = oppTeamHPPercent,

            MyRemainingPieces = myAlive,
            OppRemainingPieces = oppAlive,
            IsTerminal = isTerminal,

            MyExpendability = expendability,

            MyTeamAlive = myTeamAlive,
            OppTeamAlive = oppTeamAlive,

            BattlefieldState = bfs,
        };

        return context;
    }

    private bool CheckForSafePivot( IBattleAIUnit opponent )
    {
        int pivots = 0;
        var myTeam = _ai.BattleSystem.GetAllyParty( _ai.Unit.Pokemon );

        for( int i = 0; i < myTeam.Count; i++ )
        {
            var mon = myTeam[i];
            if( mon != _ai.Unit.Pokemon )
            {
                var pivotHP = _ai.Get_HPRatio( mon );
                if( !mon.IsFainted && pivotHP > 0.35f )
                {
                    BattleAI_PokemonAdapter monAdapter = _ai.GetPokemonAs_Adapter( mon );
                    var targetThreateningMove = _ai.MoveCommand.GetMove_BestAttack( opponent, monAdapter, false, "Get Safe Pivot" );
                    var attackerWSR = Get_EstimatedDamageResult( opponent, monAdapter, targetThreateningMove );
                    float targetHP = _ai.Get_HPRatio( opponent );
                    PotentialToKOResult pivotPTKO_target = Get_PotentialToKOResult( attackerWSR, targetThreateningMove, targetHP );

                    if( pivotPTKO_target.PTKO < PotentialToKO.Risky )
                        pivots++;
                    else
                        continue;
                }
            }
        }

        return pivots > 0;
    }

    public float GetRemainingTeamHP( List<Pokemon> team )
    {
        float currentHPTotal = 0;
        float maxHPTotal = 0;

        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            currentHPTotal += mon.CurrentHP;
            maxHPTotal += mon.MaxHP;
        }

        return currentHPTotal / maxHPTotal;
    }

    public float GetRemainingTeamHP( List<IBattleAIUnit> team )
    {
        float currentHPTotal = 0;
        float maxHPTotal = 0;

        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            currentHPTotal += mon.CurrentHPR;
            maxHPTotal++;
        }

        return currentHPTotal / maxHPTotal;
    }

    public float GetExpendability( IBattleAIUnit mon, float hp )
    {
        // Debug.Log( $"===[Getting Expendability for {mon.NickName}]===" );

        float score = 0.5f;

        if( hp < 0.4f )     score += 0.2f;
        if( hp < 0.25f )    score += 0.2f;
        if( hp < 0.1f )     score += 0.2f;

        if( mon.SevereStatus != SevereConditionID.None && !_unitSim.PokemonBenefitsFromSevereStatus( mon.Pokemon ) )
            score += 0.2f;

        // Debug.Log( $"HP Ratio: {hp}, Score: {score}" );
        var team = _ai.GetTeamAs_IBattleAIUnit( mon.Pokemon );
        var pv = _ai.GetPokemon_PieceValue( mon.Pokemon );
        float offensiveWeight;

        offensiveWeight = pv.OffensiveValue / 100f;

        score -= offensiveWeight * 0.4f;

        // Debug.Log( $"Offensive Weight: {offensiveWeight}. Score: {score}" );

        float expendability = Mathf.Clamp01( score );

        // Debug.Log( $"===[{mon.NickName}'s Final clamped Expendability Score: {expendability}]===" );

        return expendability;
    }

    public EstimatedDamageResult Get_EstimatedDamageResult( IBattleAIUnit attacker, IBattleAIUnit target, MoveThreatResult moveThreat )
    {
        const float MID_ROLL = 0.925f;
        const float LOW_ROLL = 0.85f;
        float attack = 1f;
        float defense = 1f;
        Stat attackingStat = Stat.Attack;
        Stat defendingStat = Stat.Defense;
        string key = "none";
        MoveSO moveSO = null;
        float movePower = 0f;
        float modifier = 1f;
        float brnOrfbt = 1f;

        if( moveThreat != null && moveThreat.Move != null )
        {
            key = moveThreat.Move.MoveSO.Name;
            moveSO = moveThreat.Move.MoveSO;
            movePower = moveThreat.Move.MovePower;
            modifier = moveThreat.Modifier;

            if( _unitSim.MovePowerConditions.TryGetValue( key, out var mod ) )
                movePower = mod( attacker, target, moveThreat.Move );
        }
        else if( moveThreat == null || moveThreat.Move == null || moveThreat.Move.MoveSO.MoveCategory == MoveCategory.Status )
        {
            EstimatedDamageResult zeroEDR = new()
            {
                // Score = score,
                DamageEstimate = 0f,
                LowRollEstimate = 0f,
                AttackingStatStage = attacker.StatStages[attackingStat],
                DefendingStatStage = target.StatStages[defendingStat],

                AttackingDirectModifier = attacker.DirectStatModifiers[attackingStat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm ),
                DefendingDirectModifier = target.DirectStatModifiers[defendingStat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm ),

                Attacker = attacker,
                Target = target,
            };

            return zeroEDR;
        }

        //--Multi hit move power projection
        if( moveSO.HitRange.x >= 2 && moveSO.HitRange.y != 0 )
        {
            int minHits = moveSO.HitRange.x;
            int maxHits = moveSO.HitRange.y;

            int expectedHits = Mathf.FloorToInt( ( minHits + maxHits ) * 0.5f );

            movePower *= expectedHits;
        }
        else if( moveSO.HitRange.x >= 2 && moveSO.HitRange.y == 0 )
        {
            movePower *= moveSO.HitRange.x;
        }

        //--Get Stats used
        if( _ai.UniqueStatCalls.ContainsKey( key ) )
        {
            // Debug.Log( $"[AI Scoring][Get Walling Score] Getting Walling Score! Unique Wall Scores found move {moveThreat.Move.MoveSO.Name} in its dictionary with key: {key}" );
            attackingStat = _ai.UniqueStatCalls[key].AttackingStat;
            defendingStat = _ai.UniqueStatCalls[key].DefendingStat;
            attack = _ai.GetUnitStatValue( attacker, attackingStat );
            defense = _ai.GetUnitStatValue( target, defendingStat );
        }
        else
        {
            //--Right now MoveThreatResult has scenarios where it isn't returning a move. I need to iron this out asap!!!
            MoveCategory cat;
            if( moveThreat.Move != null )
                cat = moveThreat.Move.MoveSO.MoveCategory;
            else
                cat = MoveCategory.Status;

            if( cat == MoveCategory.Physical )
            {
                attackingStat = Stat.Attack;
                defendingStat = Stat.Defense;
                attack = _ai.GetUnitStatValue( attacker, attackingStat );
                defense = _ai.GetUnitStatValue( target, defendingStat );

                if( attacker.SevereStatus == SevereConditionID.BRN && attacker.Ability != AbilityID.Guts )
                    brnOrfbt = 0.5f;
            }
            else if( cat == MoveCategory.Special )
            {
                attackingStat = Stat.SpAttack;
                defendingStat = Stat.SpDefense;
                attack = _ai.GetUnitStatValue( attacker, attackingStat );
                defense = _ai.GetUnitStatValue( target, defendingStat );

                if( attacker.SevereStatus == SevereConditionID.FBT )
                    brnOrfbt = 0.5f;
            }
            else
            {
                //--Status move used, we may need to alter this somehow
                attack = attacker.Attack;
                defense = target.Defense;
            }
        }

        float targetMHP = target.HP;
        float levelFactor = ( 2f * attacker.Level / 5f + 2f );
        float damage = 0f;
        float damagePercentage = 0f;
        float lowRoll = 0f;
        float lowRollPercentage = 0f;

        if( moveSO.SetDamageType != SetDamageType.None )
        {
            if( moveSO.SetDamageType == SetDamageType.Level )
            {
                damage = targetMHP - attacker.Level;
                damagePercentage = Mathf.Floor( ( damage / targetMHP ) * 1000f ) / 1000f;
            }

        }
        else
        {
            damage = ( ( levelFactor * movePower * ( attack / defense ) / 50 ) + 2 ) * modifier * brnOrfbt * MID_ROLL;
            damagePercentage = Mathf.Floor( ( damage / targetMHP ) * 1000f ) / 1000f;
            
            lowRoll = ( ( levelFactor * movePower * ( attack / defense ) / 50 ) + 2 ) * modifier * brnOrfbt * LOW_ROLL;
            lowRollPercentage = Mathf.Floor( ( lowRoll / targetMHP ) * 1000f ) / 1000f;
        }

        if( !_unitSim.CanActOnTurn( attacker ) )
            damagePercentage = 0;

        moveThreat.EstimatedDamage = damagePercentage; //--store damage in MTR for sim use

        // Debug.Log( $"[AI Scoring][Estimated Damage Result] Calculation Results: Target {target.Name}'s Assumed Defending Stat: {defendingStat}, {defense}, Assumed Max HP: {targetMHP}. Level {attacker.Level} ({levelFactor}) Attacker {attacker.Name}'s Assumed Attacking stat {attackingStat}, {attack}. Move: {moveThreat.Move.MoveSO.Name}, Power: {movePower}, Modifier: {modifier}, BRN/FBT: {brnOrfbt}. Final Damage Estimate: {damage}, Percentage of target's assumed Max HP: {damagePercentage}" );
        
        EstimatedDamageResult edr = new()
        {
            // Score = score,
            DamageEstimate = damagePercentage,
            LowRollEstimate = lowRollPercentage,
            AttackingStatStage = attacker.StatStages[attackingStat],
            DefendingStatStage = target.StatStages[defendingStat],

            AttackingDirectModifier = attacker.DirectStatModifiers[attackingStat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm ),
            DefendingDirectModifier = target.DirectStatModifiers[defendingStat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm ),

            Attacker = attacker,
            Target = target,
        };

        return edr;
    }

    public PotentialToKOResult Get_PotentialToKOResult( EstimatedDamageResult edr, MoveThreatResult mtr, float targetHPR )
    {
        PotentialToKO ptko = GetPTKO_FromDamageEstimate( edr, targetHPR );
        int score = Get_PotentialToKOScoreFromEnum( ptko );
        // Debug.LogError( $"PTKO: {ptko}, Score: {score}");

        PotentialToKOResult ptkor = new()
        {
            Score = score,
            PTKO = ptko,
            Modifier = mtr != null ? mtr.Modifier : 0f
        };

        return ptkor;
    }

    private PotentialToKOResult Get_PTKOResultPreview( EstimatedDamageResult edr, MoveThreatResult mtr )
    {
        PotentialToKO basePTKO = GetPTKO_FromDamageEstimate( edr, 1f );
        float moveModifier = mtr.Modifier;

        return new()
        {
            Score = Get_PotentialToKOScoreFromEnum( basePTKO ),
            PTKO = basePTKO,
            Modifier = moveModifier,
        };
    }
    
    public int Get_PotentialToKOScoreFromEnum( PotentialToKO koClass )
    {
        //--This is a damn pretty switch, sheesh //--shift safe, sturdy, hardwall scores up a bit, maybe by 5-10, and shift neutral and lower down quite a lot, with bigger negative values for dangerous and ohko than their safe equivalents.
        return koClass switch
        {
            PotentialToKO.Untouchable       => +120,
            PotentialToKO.HardWall          => +70,
            PotentialToKO.Sturdy            => +40,
            PotentialToKO.Safe              => +20,
            PotentialToKO.TwoHKO            => 0,
            PotentialToKO.Risky             => -25,
            PotentialToKO.Dangerous         => -65,
            PotentialToKO.OHKO              => -100,
            _ => 0
        };
    }

    public int Get_OffensivePTKOScore( int score )
    {
        int off = -score;
        return Mathf.FloorToInt( off * 1.2f ); //--the higher chance of ko, the more incentivized you are because the score increases more due to being a percentage increase.
    }

    public float Get_PTKODamagePercent( PotentialToKO ptko )
    {
        return ptko switch
        {
            PotentialToKO.HardWall      => 0.08f,
            PotentialToKO.Sturdy        => 0.22f,
            PotentialToKO.Safe          => 0.38f,
            PotentialToKO.TwoHKO        => 0.55f,
            PotentialToKO.Risky         => 0.72f,
            PotentialToKO.Dangerous     => 0.88f,
            PotentialToKO.OHKO          => 1.05f,
            _ => 0f
        };
    }

    public PotentialToKO GetPTKO_FromDamageEstimate( EstimatedDamageResult edr, float targetHPR )
    {
        float damage = edr.DamageEstimate / targetHPR;
        float lowRoll = edr.LowRollEstimate / targetHPR;
        // Debug.Log( $"[AI Scoring][Get Walling Score] Damage Estimate: {damageEstimate}, Target HPR: {targetHPR}, Final Damage Done Ratio: {damage}" );

        if( lowRoll > 0.98f )           return PotentialToKO.OHKO;

        if( damage <= 0f )              return PotentialToKO.Untouchable;
        else if( damage <= 0.15f )      return PotentialToKO.HardWall;
        else if( damage <= 0.30f )      return PotentialToKO.Sturdy;
        else if( damage <= 0.49f )      return PotentialToKO.Safe;
        else if( damage <= 0.59f )      return PotentialToKO.TwoHKO;
        else if( damage <= 0.78f )      return PotentialToKO.Risky;
        else if( damage <= 0.98f )      return PotentialToKO.Dangerous;
        else if( damage >= 0.99f )      return PotentialToKO.OHKO;
        else                            return PotentialToKO.TwoHKO;
    }

    public PotentialToKO Get_NeutralPTKO( IBattleAIUnit attacker, IBattleAIUnit target )
    {
        var move    = _ai.Get_MostThreateningMove( attacker, target, true );
        var wsr     = Get_EstimatedDamageResult( attacker, target, move );
        var result  = Get_PTKOResultPreview( wsr, move );

        return result.PTKO;
    }

    public TeamVSTeamAnalysis Get_TeamVSTeamAnalysis( List<Pokemon> ourTeam, List<Pokemon> theirTeam )
    {
        List<int> ourPTKOS = new();
        List<int> theirPTKOS = new();

        int ourBestPTKO = 0;
        int theirBestPTKO = 0;

        int ourThreatCount = 0;
        int theirThreatCount = 0;

        int ourLikelySwitches = 0;
        int theirLikelySwitches = 0;

        int theirFavorATK = 0;
        int theirFavorSpATK = 0;

        int ourOutspeeds = 0;
        int theirOutspeeds = 0;

        //--Anal
        for( int i = 0; i < ourTeam.Count; i++ )
        {
            BattleAI_PokemonAdapter ourMon = _ai.GetPokemonAs_Adapter( ourTeam[i] );

            for( int t = 0; t < theirTeam.Count; t++ )
            {
                BattleAI_PokemonAdapter theirMon = _ai.GetPokemonAs_Adapter( theirTeam[t] );

                //--MTRs
                var ourMTR = _ai.MoveCommand.GetMove_BestAttack( ourMon, theirMon );
                var theirMTR = _ai.MoveCommand.GetMove_BestAttack( theirMon, ourMon );

                //--EDRs
                var ourEDR = Get_EstimatedDamageResult( ourMon, theirMon, ourMTR );
                var theirEDR = Get_EstimatedDamageResult( theirMon, ourMon, theirMTR );

                //--PTKOs
                var ourPTKO = Get_PotentialToKOResult( ourEDR, ourMTR, theirMon.CurrentHPR ).PTKO;
                var theirPTKO = Get_PotentialToKOResult( theirEDR, theirMTR, ourMon.CurrentHPR ).PTKO;

                ourPTKOS.Add( (int)ourPTKO );
                theirPTKOS.Add( (int)theirPTKO );

                if( ourPTKO - 1 > theirPTKO )
                    ourThreatCount++;

                if( theirPTKO - 1 > ourPTKO )
                    theirThreatCount++;

                bool weSurvive = theirPTKO <= PotentialToKO.Safe;
                bool weMoveFirst = ourMTR.Move.Priority > theirMTR.Move.Priority || ( ourMTR.Move.Priority == theirMTR.Move.Priority && ourMon.Speed > theirMon.Speed );
                bool weThreaten = ourPTKO >= PotentialToKO.Dangerous && ( weMoveFirst || weSurvive );

                bool theySurvive = ourPTKO <= PotentialToKO.Safe;
                bool theyThreaten = theirPTKO >= PotentialToKO.Dangerous && ( !weMoveFirst || theySurvive );

                if( weThreaten && theirTeam.Count > 1 )
                    theirLikelySwitches++;

                if( theyThreaten && ourTeam.Count > 1 )
                    ourLikelySwitches++;

                if( theirMTR.Move.MoveSO.MoveCategory == MoveCategory.Physical )
                    theirFavorATK++;

                if( theirMTR.Move.MoveSO.MoveCategory == MoveCategory.Special )
                    theirFavorSpATK++;

                if( ourMon.Speed > theirMon.Speed )
                    ourOutspeeds++;
                else
                    theirOutspeeds++;

            }
        }

        //--Average PTKO
        int ourTotalPTKOS = 0;
        for( int i = 0; i < ourPTKOS.Count; i++ )
        {
            ourTotalPTKOS += ourPTKOS[i];

            if( theirPTKOS[i] > ourBestPTKO )
                ourBestPTKO = ourPTKOS[i];

        }

        PotentialToKO ourAveragePTKO = (PotentialToKO)( ourTotalPTKOS / ourPTKOS.Count );

        int theirTotalPTKOS = 0;
        for( int i = 0; i < theirPTKOS.Count; i++ )
        {
            theirTotalPTKOS += theirPTKOS[i];

            if( theirPTKOS[i] > theirBestPTKO )
                theirBestPTKO = theirPTKOS[i];
        }

        PotentialToKO theirAveragePTKO = (PotentialToKO)( theirTotalPTKOS / theirPTKOS.Count );

        return new()
        {
            Our_BestPTKO = (PotentialToKO)ourBestPTKO,
            Their_BestPTKO = (PotentialToKO)theirBestPTKO,

            Our_AveragePTKO = ourAveragePTKO,
            Their_AveragePTKO = theirAveragePTKO,

            Our_ThreatCount = ourThreatCount,
            Their_ThreatCount = theirThreatCount,

            Our_LikelySwitches = ourLikelySwitches,
            Their_LikelySwitches = theirLikelySwitches,

            TheirFavorCount_ATK = theirFavorATK,
            TheirFavorCount_SpATK = theirFavorSpATK,

            Our_Outspeeds = ourOutspeeds,
            Their_Outspeeds = theirOutspeeds,
        };
    }

    public CurrentPlan EvaluateCurrentPlan( ExchangeEvaluation ee, BoardContext bc, ThreatProfile tp, GamePlan gp, CurrentPlan prevPlan )
    {
        CurrentPlan nextPlan = new()
        {
            Type = PlanType.None,
            FocusMon = null,
            Confidence = 0f
        };

        bool previousIsNull = prevPlan == null;
        float currentConfidence = 0f;
        PlanType bestPlan = PlanType.None;
        PlanType currentPlan = previousIsNull ? PlanType.None : prevPlan.Type;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"================================================================================" );
        _ai.CurrentLog.Add( $"=====[Evaluating Current Plan. Previous Plan Exists: {!previousIsNull}. Current Confidence: {currentConfidence}. Current Plan Type: {currentPlan}]=====" );
        _ai.CurrentLog.Add( $"================================================================================" );
        _ai.CurrentLog.Add( $"" );

        float stabilizeScore = 0;
        float tradeScore = 0;
        float aggressScore = 0;
        float enableSweepScore = 0;
        float preventSweepScore = 0;

        //----------------------------------------
        //--Gather context
        //----------------------------------------

        int materialDelta = bc.MyRemainingPieces - bc.OppRemainingPieces;
        bool iAmStable = ee.AttackerSurvives && ( !ee.OpponentThreatensKO || ee.AttackerThreatensKO );
        bool oppIsStable = ee.OpponentSurvives && ( !ee.AttackerThreatensKO || ee.OpponentThreatensKO );

        _ai.CurrentLog.Add( $"[Current Plan] Gathered some context. Material Delta: {materialDelta}. I am Stable: {iAmStable}, Opp is Stable : {oppIsStable}" );

        //----------------------------------------
        //--Stabilize
        //----------------------------------------

        if( !iAmStable )
            stabilizeScore += 2.5f;

        stabilizeScore += tp.Urgency >= ThreatUrgency.High ? 2f : 0f;
        stabilizeScore += tp.ThreatensImmediateKO ? 1.5f : 0f;
        stabilizeScore += tp.ForcesSwitch ? 1.0f : 0f;
        _ai.CurrentLog.Add( $"[Current Plan] Stabilize Score: {stabilizeScore}" );

        //----------------------------------------
        //--Prevent Sweep
        //----------------------------------------

        if( tp.SweepPotential )
        {
            preventSweepScore += 3f;
            stabilizeScore += 1.5f;
        }

        if( tp.OutspeedsAlliesCount >= bc.MyTeamAlive.Count - 1 )
            preventSweepScore += 2f;

        if( gp.TheirPrimaryWinCon != null )
        {
            for( int i = 0; i < _ai.TheirBattleAIUnits.Count; i++ )
            {
                var theirUnit = _ai.TheirBattleAIUnits[i];
                if( theirUnit.Pokemon == gp.TheirPrimaryWinCon )
                {
                    _ai.CurrentLog.Add( $"[Current Plan] (Prevent Sweep) Their Primary WinCon is on the field." );
                    preventSweepScore += 1.5f;
                }
            }
        }

        preventSweepScore += tp.Urgency >= ThreatUrgency.High ? 1.5f : 0f;
        _ai.CurrentLog.Add( $"[Current Plan] Prevent Sweep Score: {preventSweepScore}" );

        //----------------------------------------
        //--Enable Sweep
        //----------------------------------------

        Pokemon bestSweeper = null;
        float bestSweepScore = 0f;
        var winConPR = _ai.GetPokemonAs_Adapter( gp.OurPrimaryWinCon ).RoleProfile.PrimaryRole;
        bool winConIsOffensiveRole = winConPR == RoleClass.Sweeper || winConPR == RoleClass.BulkyAttacker || winConPR == RoleClass.SetupSweeper || winConPR == RoleClass.WallBreaker;

        foreach( var unit in bc.MyTeamAlive )
        {
            int threats = 0;
            int safeMatchups = 0;

            foreach( var opp in bc.OppTeamAlive )
            {
                var ex = EvaluateExchange( unit, opp );

                bool threatens = ex.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous;
                bool safe = ex.OpponentPTKOR.PTKO < PotentialToKO.Dangerous;

                if( threatens ) threats++;
                if( safe ) safeMatchups++;
            }

            float score = threats * 1.0f + safeMatchups * 0.5f;
            if( unit.Pokemon == gp.OurPrimaryWinCon && winConIsOffensiveRole )
            {
                score += 1.5f;
                _ai.CurrentLog.Add( $"[Current Plan] (Enable Sweep) Evaluating our Primary WinCon's sweep potential." );
            }
            // _ai.CurrentLog.Add( $"[Win Con] Checking for sweep potential for {unit.Name}. Threats: {threats}, Safe Matchups: {safeMatchups}. Score: {score}" );

            if( score > bestSweepScore )
            {
                bestSweepScore = score;
                bestSweeper = unit.Pokemon;
            }
        }

        enableSweepScore += bestSweepScore;

        if( tp.Urgency >= ThreatUrgency.High )
        {
            if( tp.Type == ThreatType.Immediate || tp.SweepPotential )
                enableSweepScore -= 2.5f;
            else if( ( tp.Type == ThreatType.Persistent || tp.Type == ThreatType.Disruptive ) && iAmStable )
                enableSweepScore += 1.5f;
            else
                enableSweepScore -= 1f;
        }

        if( !iAmStable )
            enableSweepScore -= 1.5f;

        _ai.CurrentLog.Add( $"[Current Plan] Enable Sweep Score: {enableSweepScore}" );

        //----------------------------------------
        //--Aggress them
        //----------------------------------------

        Pokemon worstWall = null;
        int blockCount = 0;

        foreach( var opp in bc.OppTeamAlive )
        {
            int blocks = 0;

            foreach( var mine in bc.MyTeamAlive )
            {
                var ex = EvaluateExchange( mine, opp );

                bool iStruggle = ex.AttackerPTKOR.PTKO < PotentialToKO.Risky;
                bool theyThreaten = ex.OpponentPTKOR.PTKO > PotentialToKO.Risky;

                if( iStruggle && theyThreaten )
                    blocks++;
            }

            if( gp.TheirBlockers.Contains( opp.Pokemon ) )
            {
                blocks++;
                _ai.CurrentLog.Add( $"[Current Plan] (Aggress) Primary blocker still alive. Adding." );
            }

            if( blocks > blockCount )
            {
                blockCount = blocks;
                worstWall = opp.Pokemon;
            }
        }

        aggressScore += blockCount;
        aggressScore += tp.ConstrainingPressure >= 2f ? 1.5f : 0f;

        if( tp.Type == ThreatType.Persistent || tp.ConstrainingPressure >= 4f || gp.TheirBlockers.Contains( tp.ThreatUnit.Pokemon ) )
            aggressScore += 2f;

        _ai.CurrentLog.Add( $"[Current Plan] Aggress Score: {aggressScore}" );

        //----------------------------------------
        //--Trade
        //----------------------------------------

        if( materialDelta > 0 )
            tradeScore += 2f;
        
        if( bc.MyTeamHPPercent > bc.OppTeamHPPercent )
            tradeScore += 1.5f;

        if( tp.Urgency <= ThreatUrgency.Medium )
            tradeScore += 1f;

        //----------------------------------------
        //--Select Best Plan via score
        //----------------------------------------

        Dictionary<PlanType, float> planScores = new()
        {
            { PlanType.Stabilize, stabilizeScore },
            { PlanType.Trade, tradeScore },
            { PlanType.Aggress, aggressScore },
            { PlanType.EnableSweep, enableSweepScore },
            { PlanType.PreventSweep, preventSweepScore },
        };

        float bestScore = float.MinValue;

        foreach( var kvp in planScores )
        {
            if( kvp.Value > bestScore )
            {
                bestScore = kvp.Value;
                bestPlan = kvp.Key;
            }
        }

        float total = planScores.Values.Sum();
        nextPlan.Confidence = total > 0 ? bestScore / total : 0f;
        nextPlan.Type = bestPlan;

        if( bestPlan == PlanType.EnableSweep )
            nextPlan.FocusMon = bestSweeper;

        if( bestPlan == PlanType.Aggress )
            nextPlan.FocusMon = worstWall;

        var finalPlan = MergeWithPrevious( prevPlan, nextPlan, tp );

        bool allowSacrifice;
        allowSacrifice = finalPlan.Type switch
        {
            PlanType.EnableSweep => nextPlan.Confidence >= 0.4f,
            PlanType.Aggress => nextPlan.Confidence >= 0.55f,
            PlanType.Trade => nextPlan.Confidence >= 0.65f && materialDelta >= 1,
            _ => false,
        };

        finalPlan.AllowSacrifice = allowSacrifice;
        finalPlan.SweepPotential = tp.SweepPotential;

        _ai.CurrentLog.Add( $"[Current Plan] Final Plan: {finalPlan.Type}, Confidence: {finalPlan.Confidence}, Sacrifice Allowed: {finalPlan.AllowSacrifice}" );
        _ai.CurrentLog.Add( $"===================================================================================================" );
        _ai.CurrentLog.Add( $"" );

        return finalPlan;
    }

    public CurrentPlan MergeWithPrevious( CurrentPlan prev, CurrentPlan next, ThreatProfile tp )
    {
        if( prev != null && prev.Type == next.Type )
        {
            next.Confidence = Mathf.Min( 1f, next.Confidence + 0.1f );
            next.TurnsActive = prev.TurnsActive + 1;
        }
        else if( prev != null && prev.Type != next.Type )
        {
            float decay = 0.05f * prev.TurnsActive;
            float threshold = prev.Confidence + 0.1f;

            threshold -= decay;

            if( next.Confidence < threshold && tp.Urgency < ThreatUrgency.High )
            {
                prev.Confidence *= 0.9f;
                return prev;
            }
        }

        return next;
    }

    public int GetCurrentPlanBias( ActionEvaluation action, ProjectedBoardState pbs, BoardContext bc, CurrentPlan plan, SurvivalClass sc )
    {
        int score = 0;
        var top1 = action.Top1;
        var top2 = action.Top2;

        float damageTaken = ( ( top1.Attacker.BeginningHPR - top1.Attacker_EndOfTurnHP ) * 100f ) / 100f;
        float damageDone = ( ( top1.Opponent.BeginningHPR - top1.Opponent_EndOfTurnHP ) * 100f ) / 100f;

        float sackModifier = ( 1 - bc.MyExpendability * 0.7f );

        switch( plan.Type )
        {
            //----------------------------------------------------------------------------------------------------------------------
            //--------------------------------------[ENABLE SWEEP]------------------------------------------------------------------
            //----------------------------------------------------------------------------------------------------------------------
            case PlanType.EnableSweep:

                if( action.Type == ActionType.Attack )
                {
                    score += 10;

                    if( action.Actor == plan.FocusMon )
                        score += 5;

                    List<Pokemon> oppTeamAlive = new();
                    for( int i = 0; i < bc.OppTeamAlive.Count; i++ )
                        oppTeamAlive.Add( bc.OppTeamAlive[i].Pokemon );

                    var threatsToSweeper = _ai.GetTopThreats( oppTeamAlive, plan.FocusMon );
                    for( int i = 0; i < threatsToSweeper.Count; i++ )
                    {
                        if( i == 2 )
                            break;

                        var mon = threatsToSweeper[i].Mon;
                        if( action.Target == null || action.Target.Pokemon == null )
                            break;

                        if( action.Target.Pokemon == mon )
                        {
                            score += 15;
                        }
                    }
                }

                if( action.Type == ActionType.DefensiveSwitch )
                {
                    score -= 25;
                }

                if( action.Type == ActionType.OffensiveSwitch )
                {
                    score += 10;

                    if( action.SwitchPayload == plan.FocusMon )
                    {
                        score += 5;
                    }
                }

                if( action.Type == ActionType.Setup )
                {
                    score += 25;

                    if( top2.AttackerPTKO >= PotentialToKO.Dangerous && top2.Attacker_EndOfTurnHP > 0f )
                        score += 15;
                }

                if( action.Type == ActionType.OffensiveStatus )
                {
                    if( !_unitSim.MoveIsEntryHazard( action.MovePayload ) && top1.Attacker_EndOfTurnHP > 0f && ( !top1.OpponentCanAct || !top2.OpponentCanAct ) )
                    {
                        score += 10;
                    }
                    else
                    {
                        score -= 15;
                    }
                }

                if( pbs.IAmKONow && action.Actor == plan.FocusMon )
                    score -= 25;
                else if( action.Actor == plan.FocusMon )
                    score += 10;

                if( ( action.Type == ActionType.OffensiveSwitch || action.Type == ActionType.DefensiveSwitch ) && action.SwitchPayload == plan.FocusMon )
                {
                    if( top1.Attacker_EndOfTurnHP <= 0f )
                    {
                        score -= 25;
                    }
                }

            break;

            //----------------------------------------------------------------------------------------------------------------------
            //-------------------------------------------[AGGRESS]------------------------------------------------------------------
            //----------------------------------------------------------------------------------------------------------------------

            case PlanType.Aggress:

                if( action.Type == ActionType.Attack )
                {
                    if( action.Target.Pokemon == plan.FocusMon )
                    {
                        score += 20;

                        if( top1.AttackerPTKO >= PotentialToKO.Dangerous )
                        {
                            score += 10;
                        }
                    }
                    else
                    {
                        score -= 15;
                    }
                }

                if( action.Type == ActionType.DefensiveSwitch )
                {
                    score -= 25;
                }

                if( action.Type == ActionType.OffensiveSwitch )
                {
                    score -= 10;

                    if( top2.Attacker_EndOfTurnHP <= 0 || top2.AttackerPTKO < PotentialToKO.Dangerous )
                    {
                        score -= 5;
                    }
                }

                if( action.Type == ActionType.Setup )
                {
                    score += 10;

                    if( top1.Attacker_EndOfTurnHP > 0f && top2.Attacker_EndOfTurnHP > 0f && top2.AttackerPTKO >= PotentialToKO.Dangerous )
                    {
                        if( top2.AttackerMovedFirst )
                        {
                            score += 10;
                        }
                        else
                        {
                            score += 5;
                        }
                    }
                }

                if( action.Type == ActionType.OffensiveStatus && _unitSim.MoveIsEntryHazard( action.MovePayload ) )
                {
                    score -= 25;
                }

                if( action.Target != null && action.Target.Pokemon != null && action.Target.Pokemon != plan.FocusMon && action.Type != ActionType.Setup )
                {
                    score -= 15;
                }
                    
            break;

            //----------------------------------------------------------------------------------------------------------------------
            //---------------------------------------------[TRADE]------------------------------------------------------------------
            //----------------------------------------------------------------------------------------------------------------------

            case PlanType.Trade:

                if( action.Type == ActionType.Attack )
                {
                    score += 15;
                }
                else
                {
                    score -= 25;
                }

                if( pbs.IAmStable )
                {
                    score += 15;
                }
                else if( !pbs.IAmStable )
                {
                    score -= 25;
                }

                if( pbs.MaterialDelta > 0 )
                {
                    score += 15;
                }

                if( pbs.IGetImmediateKO )
                {
                    score += 30;
                }

                if( pbs.OppIsKONow && damageTaken < 0.33f  )
                {
                    score += 20;
                }

                if( damageTaken >= 0.33f || damageDone < 0.33f )
                {
                    score -= 30;
                }

                if( top1.OpponentPTKO <= PotentialToKO.TwoHKO && top1.AttackerMovedFirst || top1.OpponentPTKO <= PotentialToKO.Safe )
                {
                    if( action.Type == ActionType.DefensiveSwitch || action.Type == ActionType.Setup )
                        score -= 35;

                    if( action.Type == ActionType.OffensiveSwitch && top2.AttackerPTKO < PotentialToKO.Dangerous )
                        score -= 35;
                }

                //--Expendability weight
                score -= Mathf.RoundToInt( 20 * sackModifier );

            break;

            //----------------------------------------------------------------------------------------------------------------------
            //---------------------------------------------[STABILIZE]--------------------------------------------------------------
            //----------------------------------------------------------------------------------------------------------------------

            case PlanType.Stabilize:
                
                if( pbs.IAmStable )
                {
                    score += 25;
                }

                if( pbs.IAmKONow || pbs.OppIsStable && !pbs.IAmStable )
                {
                    score -= 10;
                }

                if( action.Type == ActionType.DefensiveSwitch )
                {
                    score += 25;
                }

                if( action.Type == ActionType.OffensiveSwitch )
                {
                    score += 15;

                    if( !pbs.OppKillNext || top2.AttackerPTKO >= PotentialToKO.Dangerous && top2.AttackerMovedFirst )
                        score += 10;
                }

                if( action.Type == ActionType.Setup )
                {
                    if( !pbs.IAmKONow && !pbs.OppKillNext )
                        score += 10;
                    else
                        score -= 30;
                }

                if( top1.AttackerPTKO >= PotentialToKO.Risky && top1.OpponentPTKO >= PotentialToKO.Risky && top1.Attacker_EndOfTurnHP > 0f && top1.Opponent_EndOfTurnHP > 0f )
                {
                    score -= 20;
                }

                if( top1.OpponentPTKO >= PotentialToKO.Risky )
                {
                    if( top1.AttackerPTKO < PotentialToKO.Risky )
                        score -= 5;

                    if( top1.Attacker_EndOfTurnHP <= 0.3f )
                        score -= 5;

                    if( !top1.AttackerMovedFirst )
                        score -= 5;

                    if( !top1.AttackerCanAct )
                        score -= 5;

                    if( top1.Attacker_DiesBeforeActing )
                        score -= 15;
                }
                
            break;

            //----------------------------------------------------------------------------------------------------------------------
            //-------------------------------------------[PREVENT SWEEP]------------------------------------------------------------
            //----------------------------------------------------------------------------------------------------------------------

            case PlanType.PreventSweep:

                if( action.Type == ActionType.Attack )
                {
                    score += 10;

                    if( pbs.IGetImmediateKO || pbs.OppIsKONow && !pbs.IAmKONow )
                    {
                        score += 10;
                    }
                }

                if( action.Type == ActionType.DefensiveSwitch )
                {
                    if( damageTaken < 0.25f )
                    {
                        score += 15;
                    }
                    else if( damageTaken > 0.33f )
                    {
                        score -= 20;
                    }

                    if( top2.OpponentPTKO > PotentialToKO.Dangerous )
                    {
                        if( top1.OpponentPTKO <= PotentialToKO.Dangerous )
                        {
                            score += 10;
                        }
                        else
                        {
                            score -= 10;
                        }
                    }
                }

                if( !top1.AttackerMovedFirst && top2.AttackerMovedFirst )
                {
                    score += 5;
                }

                if( action.Target != null && action.Target.Pokemon != null && action.Target.Pokemon != top1.Opponent.Pokemon )
                {
                    score -= 20;
                }

                if( action.Type == ActionType.Setup )
                {
                    score -= 25;
                }

                if( action.Type == ActionType.OffensiveStatus )
                {
                    if( !_unitSim.MoveIsEntryHazard( action.MovePayload ) )
                    {
                        if( !top1.OpponentCanAct )
                        {
                            score += 20;
                        }
                        
                        if( !top2.OpponentCanAct )
                        {
                            if( top1.Attacker_EndOfTurnHP > 0 )
                            {
                                score += 20;
                            }
                            else
                                score += 10;
                        }

                        if( top1.Opponent.SevereStatus == SevereConditionID.None && top2.Opponent.SevereStatus != SevereConditionID.None && !_unitSim.PokemonBenefitsFromSevereStatus( top1.Opponent.Pokemon ) )
                        {
                            score += 10;
                        }
                    }
                    else
                    {
                        score -= 15;
                    }
                }

            break;
        }

        //---------------------------------------------------
        //-------------------Global Scores-------------------
        //---------------------------------------------------

        if( pbs.IControlNextTurn )
        {
            score += 20;
        }
        else if( pbs.OppControlNextTurn )
        {
            score -= 25;
        }

        if( action.Target == null || action.Target.Pokemon == null )
        {
            //--getting odd null errors for action targets, let's leave this as is for now like this as a null check
        }
        else
        {
            if( action.Target.Pokemon == plan.FocusMon )
            {
                score += 15;
            }
        }

        //------------------------
        //--if plan aligns with broader win condition from future long term strategy planning function, reward all
        //------------------------

        //---------------------------------------------------
        //----------------Survival Class Bias----------------
        //---------------------------------------------------

        if( sc == SurvivalClass.Safe )
        {
            if( plan.Type == PlanType.Stabilize || plan.Type == PlanType.EnableSweep )
                score += 10;
        }

        if( sc == SurvivalClass.FragileCounterPressure )
        {
            if( plan.Type == PlanType.Aggress )
                score += 15;

            if( plan.Type == PlanType.Stabilize )
                score -= 10;
        }

        if( sc == SurvivalClass.UsefulSacrifice )
        {
            if( plan.Type == PlanType.Trade )
                score += Mathf.RoundToInt( 15f * sackModifier );

            if( plan.Type == PlanType.PreventSweep )
                score += 10;

            if( plan.Type == PlanType.Stabilize )
                score -= 15;
        }

        if( sc == SurvivalClass.FailedSacrifice )
        {
            score -= Mathf.RoundToInt( 40f * sackModifier );

            if( plan.Type == PlanType.PreventSweep )
                score -= 20;
        }

        return score;
    }

    public BattlefieldState GetBattlefieldState( SimulatedField field )
    {
        BattlefieldState bfs = new()
        {
            Round = _ai.Round,
            IsEarlyGame = _ai.Round <= 5,
            IsMidGame = _ai.Round > 6 && _ai.Round < 16,
            IsLateGame = _ai.Round > 15,
            Weather = field.Weather,
            Terrain = field.Terrain,
            WeatherDuration = field.WeatherDuration,
            TerrainDuration = field.TerrainDuration,
            TrickRoomActive = field.TrickRoomActive,
            TrickRoomDuration = field.TrickRoomDuration,
        };

        //--Court Conditions
        //--Top Court
        int topHazardCount = 0;
        bool topCourtTailwind = false;
        bool topCourtReflect = false;
        bool topCourtLightScreen = false;
        bool topCourtAuroraVeil = false;

        int topTailwindDuration = 0;
        int topReflectDuration = 0;
        int topLightScreenDuration = 0;
        int topAuroraVeilDuration = 0;

        foreach( var condition in field.TopCourtConditions )
        {
            if( condition.Key == CourtConditionID.StealthRock || condition.Key == CourtConditionID.Spikes || condition.Key == CourtConditionID.ToxicSpikes || condition.Key == CourtConditionID.LeechSeed || condition.Key == CourtConditionID.StickyWeb )
                topHazardCount++;

            if( condition.Key == CourtConditionID.Tailwind )
            {
                topCourtTailwind = true;
                topTailwindDuration = condition.Value;
            }

            if( condition.Key == CourtConditionID.Reflect )
            {
                topCourtReflect = true;
                topReflectDuration = condition.Value;
            }

            if( condition.Key == CourtConditionID.LightScreen )
            {
                topCourtLightScreen = true;
                topLightScreenDuration = condition.Value;
            }

            if( condition.Key == CourtConditionID.AuroraVeil )
            {
                topCourtAuroraVeil = true;
                topAuroraVeilDuration = condition.Value;
            }
        }

        //--Bottom Court
        int bottomHazardCount = 0;
        bool bottomCourtTailwind = false;
        bool bottomCourtReflect = false;
        bool bottomCourtLightScreen = false;
        bool bottomCourtAuroraVeil = false;

        int bottomTailwindDuration = 0;
        int bottomReflectDuration = 0;
        int bottomLightScreenDuration = 0;
        int bottomAuroraVeilDuration = 0;

        foreach( var condition in field.BottomCourtConditions )
        {
            if( condition.Key == CourtConditionID.StealthRock || condition.Key == CourtConditionID.Spikes || condition.Key == CourtConditionID.ToxicSpikes || condition.Key == CourtConditionID.LeechSeed || condition.Key == CourtConditionID.StickyWeb )
                bottomHazardCount++;

            if( condition.Key == CourtConditionID.Tailwind )
            {
                bottomCourtTailwind = true;
                bottomTailwindDuration = condition.Value;
            }

            if( condition.Key == CourtConditionID.Reflect )
            {
                bottomCourtReflect = true;
                bottomReflectDuration = condition.Value;
            }

            if( condition.Key == CourtConditionID.LightScreen )
            {
                bottomCourtLightScreen = true;
                bottomLightScreenDuration = condition.Value;
            }

            if( condition.Key == CourtConditionID.AuroraVeil )
            {
                bottomCourtAuroraVeil = true;
                bottomAuroraVeilDuration = condition.Value;
            }
        }

        var ourCourt = _ai.BattleSystem.Field.GetPokemonCourtLocationFromTrainer( _ai.Unit.Pokemon );
        var topCourtParty = _ai.BattleSystem.TopTrainer1.Party;
        var bottomCourtParty = _ai.BattleSystem.BottomTrainer1.Party;
        var topCourt = _ai.BattleSystem.Field.ActiveCourts[CourtLocation.TopCourt];
        var bottomCourt = _ai.BattleSystem.Field.ActiveCourts[CourtLocation.BottomCourt];

        //-----------------------------------------------------------------------------
        //--Battlefield Control Check--------------------------------------------------
        //-----------------------------------------------------------------------------

        int topFieldControl = 0;
        int bottomFieldControl = 0;

        int topWeatherContext = 0;
        int topTerrainContext = 0;
        int topTrickRoomContext = 0;

        var topRemaining = topCourtParty.Where( p => p.CurrentHP > 0 ).ToList();
        for( int i = 0; i < topRemaining.Count; i++ )
        {
            topWeatherContext += _unitSim.Get_WeatherContextScore( topRemaining[i] );
            topTerrainContext += _unitSim.Get_TerrainContextScore( topRemaining[i] );
            topTrickRoomContext += _unitSim.Get_TrickRoomContextScore( topRemaining[i] );
        }

        topWeatherContext /= Mathf.Max( topRemaining.Count, 1 );
        topTerrainContext /= Mathf.Max( topRemaining.Count, 1 );
        topTrickRoomContext /= Mathf.Max( topRemaining.Count, 1 );

        int bottomWeatherContext = 0;
        int bottomTerrainContext = 0;
        int bottomTrickRoomContext = 0;

        var bottomRemaining = bottomCourtParty.Where( p => p.CurrentHP > 0 ).ToList();
        for( int i = 0; i < bottomRemaining.Count; i++ )
        {
            bottomWeatherContext += _unitSim.Get_WeatherContextScore( bottomRemaining[i] );
            bottomTerrainContext += _unitSim.Get_TerrainContextScore( bottomRemaining[i] );
            bottomTrickRoomContext += _unitSim.Get_TrickRoomContextScore( bottomRemaining[i] );
        }

        bottomWeatherContext /= bottomRemaining.Count;
        bottomTerrainContext /= bottomRemaining.Count;
        bottomTrickRoomContext /= bottomRemaining.Count;

        //-------------------
        //--Weather Control--
        //-------------------

        int topWeatherControl = 0;
        int bottomWeatherControl = 0;
        WeatherConditionID topsWeather = WeatherConditionID.None;
        WeatherConditionID bottomsWeather = WeatherConditionID.None;

        bool topWeatherSetter = false;
        for( int i = 0; i < topRemaining.Count; i++ )
        {
            var mon = topRemaining[i];
            if( _unitSim.PokemonHasWeatherSetter_Ability( mon ) )
            {
                switch( mon.AbilityID )
                {
                    case AbilityID.Drought: topsWeather = WeatherConditionID.SUNNY; break;
                    case AbilityID.Drizzle: topsWeather = WeatherConditionID.RAIN; break;
                    case AbilityID.Sandstream: topsWeather = WeatherConditionID.SANDSTORM; break;
                    case AbilityID.SnowWarning: topsWeather = WeatherConditionID.SNOW; break;
                }

                topWeatherSetter = true;
                break;
            }

            if( _unitSim.PokemonHasWeatherSetter_Move( mon ) )
            {
                for( int m = 0; m < mon.ActiveMoves.Count; m++ )
                {
                    var move = mon.ActiveMoves[m];
                    switch( move.MoveSO.MoveEffects.Weather )
                    {
                        case WeatherConditionID.SUNNY: topsWeather = WeatherConditionID.SUNNY; break;
                        case WeatherConditionID.RAIN: topsWeather = WeatherConditionID.RAIN; break;
                        case WeatherConditionID.SANDSTORM: topsWeather = WeatherConditionID.SANDSTORM; break;
                        case WeatherConditionID.SNOW: topsWeather = WeatherConditionID.SNOW; break;
                    }
                }

                topWeatherSetter = true;
                break;
            }
        }

        bool bottomWeatherSetter = false;
        for( int i = 0; i < bottomRemaining.Count; i++ )
        {
            var mon = bottomRemaining[i];
            if( _unitSim.PokemonHasWeatherSetter_Ability( mon ) )
            {
                switch( mon.AbilityID )
                {
                    case AbilityID.Drought: bottomsWeather = WeatherConditionID.SUNNY; break;
                    case AbilityID.Drizzle: bottomsWeather = WeatherConditionID.RAIN; break;
                    case AbilityID.Sandstream: bottomsWeather = WeatherConditionID.SANDSTORM; break;
                    case AbilityID.SnowWarning: bottomsWeather = WeatherConditionID.SNOW; break;
                }

                bottomWeatherSetter = true;
                break;
            }

            if( _unitSim.PokemonHasWeatherSetter_Move( mon ) )
            {
                for( int m = 0; m < mon.ActiveMoves.Count; m++ )
                {
                    var move = mon.ActiveMoves[m];
                    switch( move.MoveSO.MoveEffects.Weather )
                    {
                        case WeatherConditionID.SUNNY: bottomsWeather = WeatherConditionID.SUNNY; break;
                        case WeatherConditionID.RAIN: bottomsWeather = WeatherConditionID.RAIN; break;
                        case WeatherConditionID.SANDSTORM: bottomsWeather = WeatherConditionID.SANDSTORM; break;
                        case WeatherConditionID.SNOW: bottomsWeather = WeatherConditionID.SNOW; break;
                    }
                }

                bottomWeatherSetter = true;
                break;
            }
        }

        //--Top Court
        if( topWeatherSetter )
        {
            topWeatherControl += 1;

            if( field.Weather == WeatherConditionID.None || field.Weather == topsWeather )
                topWeatherControl += 2;
        }

        if( topWeatherContext > bottomWeatherContext )
            topWeatherControl += 1;

        if( topWeatherSetter && !bottomWeatherSetter )
            topWeatherControl += 2;

        if( field.Weather == topsWeather )
            topWeatherControl += Mathf.RoundToInt( Mathf.Clamp( field.WeatherDuration, 0, 5 ) / 2 );

        //--Bottom Court
        if( bottomWeatherSetter )
        {
            bottomWeatherControl += 1;

            if( field.Weather == WeatherConditionID.None || field.Weather == bottomsWeather )
                bottomWeatherControl += 2;
        }

        if( bottomWeatherContext > topWeatherContext )
            bottomWeatherControl += 1;

        if( bottomWeatherSetter && !topWeatherSetter )
            bottomWeatherControl += 2;

        if( field.Weather == bottomsWeather )
            bottomWeatherControl += Mathf.RoundToInt( Mathf.Clamp( field.WeatherDuration, 0, 5 ) / 2 );

        //-------------------
        //--Terrain Control--
        //-------------------

        int topTerrainControl = 0;
        int bottomTerrainControl = 0;

        bool topTerrainSetter = _unitSim.TeamHasTerrainSetter_Ability( topRemaining ) || _unitSim.TeamHasTerrainSetter_Move( topRemaining );
        if( topTerrainSetter )
            topTerrainControl += 1;

        if( topTerrainContext > bottomTerrainContext )
            topTerrainControl += 2;

        //--Bottom Court
        bool bottomTerrainSetter = _unitSim.TeamHasTerrainSetter_Ability( bottomRemaining ) || _unitSim.TeamHasTerrainSetter_Move( bottomRemaining );
        if( bottomTerrainSetter )
            bottomTerrainControl += 1;

        if( bottomTerrainContext > topTerrainContext )
            bottomTerrainControl += 2;

        //-----------------
        //--Speed Control--
        //-----------------

        int topSpeedControl = 0;
        int bottomSpeedControl = 0;

        bool topTailwindSetter = _unitSim.TeamHasTailwindSetter( topRemaining );
        bool bottomTailwindSetter = _unitSim.TeamHasTailwindSetter( bottomRemaining );

        bool topTrickRoomSetter = _unitSim.TeamHasTrickRoomSetter( topRemaining );
        bool bottomTrickRoomSetter = _unitSim.TeamHasTrickRoomSetter( bottomRemaining );

        bool topTrickRoomAdvantage = false;
        bool bottomTrickRoomAdvantage = false;

        if( topTrickRoomContext > bottomTrickRoomContext )
            topTrickRoomAdvantage = true;
        else if( bottomTrickRoomContext > topTrickRoomContext )
            bottomTrickRoomAdvantage = true;

        //--Top Court
        if( topTrickRoomAdvantage )
            topSpeedControl += 3;

        if( topCourt.Conditions.ContainsKey( CourtConditionID.Tailwind ) )
        {
            if( field.TrickRoomActive )
            {
                if( bottomTrickRoomAdvantage )
                    topSpeedControl -= 5;
                else
                    topSpeedControl -= 3;
            }
            else
                topSpeedControl += 3;
        }

        if( topTailwindSetter )
            topSpeedControl += 1;

        if( topTrickRoomSetter )
            topSpeedControl += 1;

        //--Bottom Court
        if( bottomTrickRoomAdvantage )
            bottomSpeedControl += 3;

        if( bottomCourt.Conditions.ContainsKey( CourtConditionID.Tailwind ) )
        {
            if( field.TrickRoomActive )
            {
                if( topTrickRoomAdvantage )
                    bottomSpeedControl -= 5;
                else
                    bottomSpeedControl -= 3;
            }
            else
                bottomSpeedControl += 3;
        }

        if( bottomTailwindSetter )
            bottomSpeedControl += 1;

        if( bottomTrickRoomSetter )
            bottomSpeedControl += 1;

        //-------------------
        //--Screens Control--
        //-------------------

        int topScreensControl = 0;
        int bottomScreensControl = 0;

        bool topReflectSetter = false;
        bool topLightScreenSetter = false;
        bool topAuroraSetter = false;

        bool bottomReflectSetter = false;
        bool bottomLightScreenSetter = false;
        bool bottomAuroraSetter = false;

        //--Top Court
        if( topCourt.Conditions.ContainsKey( CourtConditionID.Reflect ) )
        {
            if( topCourt.Conditions[CourtConditionID.Reflect].Duration >= 5 ) //--Max turns, or holding light clay for duration extension
                topScreensControl += 3;
            else if( topCourt.Conditions[CourtConditionID.Reflect].Duration >= 3 )
                topScreensControl += 2;
            else
                topScreensControl += 1;
        }

        if( topCourt.Conditions.ContainsKey( CourtConditionID.LightScreen ) )
        {
            if( topCourt.Conditions[CourtConditionID.LightScreen].Duration >= 5 ) //--Max turns, or holding light clay for duration extension
                topScreensControl += 3;
            else if( topCourt.Conditions[CourtConditionID.LightScreen].Duration >= 3 )
                topScreensControl += 2;
            else
                topScreensControl += 1;
        }

        if( topCourt.Conditions.ContainsKey( CourtConditionID.AuroraVeil ) )
        {
            if( topCourt.Conditions[CourtConditionID.AuroraVeil].Duration >= 5 ) //--Max turns, or holding light clay for duration extension
                topScreensControl += 4;
            else if( topCourt.Conditions[CourtConditionID.AuroraVeil].Duration >= 3 )
                topScreensControl += 3;
            else
                topScreensControl += 2;
        }

        topReflectSetter = _unitSim.TeamHasReflectSetter( topRemaining );
        topLightScreenSetter = _unitSim.TeamHasLightScreenSetter( topRemaining );
        topAuroraSetter = _unitSim.TeamHasAuroraSetter( topRemaining );

        if( topReflectSetter )
            topScreensControl += 1;

        if( topLightScreenSetter )
            topScreensControl += 1;

        if( topAuroraSetter )
            topScreensControl += 1;

        //--Bottom Court
        if( bottomCourt.Conditions.ContainsKey( CourtConditionID.Reflect ) )
        {
            if( bottomCourt.Conditions[CourtConditionID.Reflect].Duration >= 5 ) //--Max turns, or holding light clay for duration extension
                bottomScreensControl += 3;
            else if( bottomCourt.Conditions[CourtConditionID.Reflect].Duration >= 3 )
                bottomScreensControl += 2;
            else
                bottomScreensControl += 1;
        }

        if( bottomCourt.Conditions.ContainsKey( CourtConditionID.LightScreen ) )
        {
            if( bottomCourt.Conditions[CourtConditionID.LightScreen].Duration >= 5 ) //--Max turns, or holding light clay for duration extension
                bottomScreensControl += 3;
            else if( bottomCourt.Conditions[CourtConditionID.LightScreen].Duration >= 3 )
                bottomScreensControl += 2;
            else
                bottomScreensControl += 1;
        }

        if( bottomCourt.Conditions.ContainsKey( CourtConditionID.AuroraVeil ) )
        {
            if( bottomCourt.Conditions[CourtConditionID.AuroraVeil].Duration >= 5 ) //--Max turns, or holding light clay for duration extension
                bottomScreensControl += 4;
            else if( bottomCourt.Conditions[CourtConditionID.AuroraVeil].Duration >= 3 )
                bottomScreensControl += 3;
            else
                bottomScreensControl += 2;
        }

        bottomReflectSetter = _unitSim.TeamHasReflectSetter( bottomRemaining );
        bottomLightScreenSetter = _unitSim.TeamHasLightScreenSetter( bottomRemaining );
        bottomAuroraSetter = _unitSim.TeamHasAuroraSetter( bottomRemaining );

        if( bottomReflectSetter )
            bottomScreensControl += 1;

        if( bottomLightScreenSetter )
            bottomScreensControl += 1;

        if( bottomAuroraSetter )
            bottomScreensControl += 1;

        //-------------------
        //--Hazards Control--
        //-------------------

        int topHazardControl = 0;
        int bottomHazardControl = 0;

        topHazardControl += topHazardCount * Mathf.Clamp( topRemaining.Count - 1, 1, 4 );
        bottomHazardControl += bottomHazardCount * Mathf.Clamp( bottomRemaining.Count - 1, 1, 4 );

        if( _unitSim.TeamHasHazardSetter( topRemaining ) && topHazardCount <= 1 )
            topHazardControl += 1;

        if( _unitSim.TeamHasHazardSetter( bottomRemaining ) && bottomHazardCount <= 1 )
            bottomHazardControl += 1;


        //--Final Field Control Calc
        topFieldControl = topWeatherControl + topTerrainControl + topSpeedControl + topScreensControl + topHazardControl;
        bottomFieldControl = bottomWeatherControl + bottomTerrainControl + bottomSpeedControl + bottomScreensControl + bottomHazardControl;
        bool topHasFieldControl = topFieldControl > bottomFieldControl + 2;
        bool bottomHasFieldControl =  bottomFieldControl > topFieldControl + 2;
        
        //---------------------------
        //--Court Based Assignments--
        //---------------------------

        if( ourCourt == CourtLocation.TopCourt )
        {
            bfs.EntryHazardsOn_MySide = topHazardCount;
            bfs.EntryHazardsOn_TheirSide = bottomHazardCount;

            bfs.WeHave_Tailwind = topCourtTailwind;
            bfs.WeHave_Reflect = topCourtReflect;
            bfs.WeHave_LightScreen = topCourtLightScreen;
            bfs.WeHave_AuroraVeil = topCourtAuroraVeil;

            bfs.TheyHave_Tailwind = bottomCourtTailwind;
            bfs.TheyHave_Reflect = bottomCourtReflect;
            bfs.TheyHave_LightScreen = bottomCourtLightScreen;
            bfs.TheyHave_AuroraVeil = bottomCourtAuroraVeil;

            if( topWeatherControl > bottomWeatherControl )
                bfs.WeHave_WeatherControl = true;

            if( bottomWeatherControl > topWeatherControl )
                bfs.TheyHave_WeatherControl = true;

            bfs.FieldControlDelta = topFieldControl - bottomFieldControl;

            //--Top Court (Us)
            bfs.WeHave_TailwindSetter           = topTailwindSetter;

            bfs.WeHave_ReflectSetter            = topReflectSetter;
            bfs.WeHave_LightScreenSetter        = topLightScreenSetter;
            bfs.WeHave_AuroraSetter             = topAuroraSetter;

            bfs.WeHave_TrickRoomSetter          = topTrickRoomSetter;

            bfs.WeHave_WeatherSetter_Ability    = _unitSim.TeamHasWeatherSetter_Ability( topCourtParty );
            bfs.WeHave_WeatherSetter_Move       = _unitSim.TeamHasWeatherSetter_Move( topCourtParty );

            bfs.WeHave_TerrainSetter_Ability    = _unitSim.TeamHasTerrainSetter_Ability( topCourtParty );
            bfs.WeHave_TerrainSetter_Move       = _unitSim.TeamHasTerrainSetter_Move( topCourtParty );
            bfs.WeHave_FieldControl             = topHasFieldControl;

            //--Bottom Court (Them)
            bfs.TheyHave_TailwindSetter         = bottomTailwindSetter;

            bfs.TheyHave_ReflectSetter          = bottomReflectSetter ;
            bfs.TheyHave_LightScreenSetter      = bottomLightScreenSetter;
            bfs.TheyHave_AuroraSetter           = bottomAuroraSetter;

            bfs.TheyHave_TrickRoomSetter        = bottomTrickRoomSetter;

            bfs.TheyHave_WeatherSetter_Ability  = _unitSim.TeamHasWeatherSetter_Ability( bottomCourtParty );
            bfs.TheyHave_WeatherSetter_Move     = _unitSim.TeamHasWeatherSetter_Move( bottomCourtParty );
            
            bfs.TheyHave_TerrainSetter_Ability  = _unitSim.TeamHasTerrainSetter_Ability( bottomCourtParty );
            bfs.TheyHave_TerrainSetter_Move     = _unitSim.TeamHasTerrainSetter_Move( bottomCourtParty );
        }
        else if( ourCourt == CourtLocation.BottomCourt )
        {
            bfs.EntryHazardsOn_MySide = bottomHazardCount;
            bfs.EntryHazardsOn_TheirSide = topHazardCount;

            bfs.WeHave_Tailwind = bottomCourtTailwind;
            bfs.WeHave_Reflect = bottomCourtReflect;
            bfs.WeHave_LightScreen = bottomCourtLightScreen;
            bfs.WeHave_AuroraVeil = bottomCourtAuroraVeil;

            bfs.TheyHave_Tailwind = topCourtTailwind;
            bfs.TheyHave_Reflect = topCourtReflect;
            bfs.TheyHave_LightScreen = topCourtLightScreen;
            bfs.TheyHave_AuroraVeil = topCourtAuroraVeil;

            if( topWeatherControl > bottomWeatherControl )
                bfs.TheyHave_WeatherControl = true;

            if( bottomWeatherControl > topWeatherControl )
                bfs.WeHave_WeatherControl = true;

            bfs.FieldControlDelta = bottomFieldControl - topFieldControl;

            //--Top Court (Them)
            bfs.TheyHave_TailwindSetter             = topTailwindSetter;

            bfs.TheyHave_ReflectSetter              = topReflectSetter;
            bfs.TheyHave_LightScreenSetter          = topLightScreenSetter;
            bfs.TheyHave_AuroraSetter               = topAuroraSetter;

            bfs.TheyHave_TrickRoomSetter            = topTrickRoomSetter;

            bfs.TheyHave_WeatherSetter_Ability      = _unitSim.TeamHasWeatherSetter_Ability( topCourtParty );
            bfs.TheyHave_WeatherSetter_Move         = _unitSim.TeamHasWeatherSetter_Move( topCourtParty );

            bfs.TheyHave_TerrainSetter_Ability      = _unitSim.TeamHasTerrainSetter_Ability( topCourtParty );
            bfs.TheyHave_TerrainSetter_Move         = _unitSim.TeamHasTerrainSetter_Move( topCourtParty );

            //--Bottom Court (Us)
            bfs.WeHave_TailwindSetter               = bottomTailwindSetter;

            bfs.WeHave_ReflectSetter                = bottomReflectSetter;
            bfs.WeHave_LightScreenSetter            = bottomLightScreenSetter;
            bfs.WeHave_AuroraSetter                 = bottomAuroraSetter;

            bfs.WeHave_TrickRoomSetter              = bottomTrickRoomSetter;

            bfs.WeHave_WeatherSetter_Ability        = _unitSim.TeamHasWeatherSetter_Ability( bottomCourtParty );
            bfs.WeHave_WeatherSetter_Move           = _unitSim.TeamHasWeatherSetter_Move( bottomCourtParty );
            
            bfs.WeHave_TerrainSetter_Ability        = _unitSim.TeamHasTerrainSetter_Ability( bottomCourtParty );
            bfs.WeHave_TerrainSetter_Move           = _unitSim.TeamHasTerrainSetter_Move( bottomCourtParty );
        }

        return bfs;
    }

    public SurvivalClass ClassifySurvival( ActionEvaluation eval, DoomedOutcome doomed )
    {
        TurnOutcomeProjection top1 = eval.Top1;
        TurnOutcomeProjection top2 = eval.Top2;

        //--Bools
        //--This turn KOs
        bool weDieThisTurn = top1.Attacker_EndOfTurnHP <= 0f;
        bool theyDieThisTurn = top1.Opponent_EndOfTurnHP <= 0f;
        bool weDieBeforeActing = top1.Attacker_DiesBeforeActing;
        bool theyDieBeforeActing = top1.Opponent_DiesBeforeActing;

        //--Next turn survival
        bool weSurviveNextTurn = top2.Attacker_EndOfTurnHP > 0f;
        bool theySurviveNextTurn = top2.Opponent_EndOfTurnHP > 0f;
        bool weThreatenNextTurn = top2.AttackerPTKO >= PotentialToKO.Dangerous;
        bool theyThreatenNextTurn = top2.OpponentPTKO >= PotentialToKO.Dangerous;

        //--Other
        bool breaksSweep = false;
        if( doomed.SweepIncoming && ( top1.Opponent_EndOfTurnHP < 0.55f || top2.Opponent_EndOfTurnHP <= 0f ) )
            breaksSweep = true;

        float theySwitchNextTurnProb = _unitSim.PredictSwitchProbability( top2.AttackerPTKO, top2.OpponentPTKO, top2.AttackerMovedFirst, top2.Attacker.BeginningHPR, top2.Opponent.BeginningHPR, top2.Opponent.Expendability );
        bool theySwitchNextTurn = UnityEngine.Random.value <= theySwitchNextTurnProb;

        //------------------------
        //----Failed Sacrifice----
        //------------------------
        if( weDieThisTurn && !theyDieThisTurn && !breaksSweep )
        {
            eval.SurvivalClass = SurvivalClass.FailedSacrifice;
            return SurvivalClass.FailedSacrifice;
        }

        //------------------------
        //----Useful Sacrifice----
        //------------------------
        if( weDieThisTurn || breaksSweep )
        {
            eval.SurvivalClass = SurvivalClass.UsefulSacrifice;
            return SurvivalClass.UsefulSacrifice;
        }

        //------------------------
        //----------Safe----------
        //------------------------
        if( !weDieThisTurn && weSurviveNextTurn && !theyThreatenNextTurn )
        {
            eval.SurvivalClass = SurvivalClass.Safe;
            return SurvivalClass.Safe;
        }

        //--------------------------------
        //----Fragile Counter Pressure----
        //--------------------------------
        if( !weDieThisTurn && ( weThreatenNextTurn || theySwitchNextTurn ) && theyThreatenNextTurn )
        {
            eval.SurvivalClass = SurvivalClass.FragileCounterPressure;
            return SurvivalClass.FragileCounterPressure;
        }

        //--Fallback
        eval.SurvivalClass = SurvivalClass.FragileCounterPressure;
        return SurvivalClass.FragileCounterPressure;
    }
}

public struct TeamVSTeamAnalysis
{
    public PotentialToKO Our_BestPTKO;
    public PotentialToKO Their_BestPTKO;

    public PotentialToKO Our_AveragePTKO;
    public PotentialToKO Their_AveragePTKO;

    public int Our_ThreatCount;
    public int Their_ThreatCount;

    public int Our_LikelySwitches;
    public int Their_LikelySwitches;

    public int TheirFavorCount_ATK;
    public int TheirFavorCount_SpATK;

    public int Our_Outspeeds;
    public int Their_Outspeeds;
}

public struct TurnOutcomeProjection
{
    public SimulatedUnit Attacker;
    public SimulatedUnit Opponent;

    public SimulatedField Field;

    public PotentialToKO AttackerPTKO;
    public PotentialToKO OpponentPTKO;

    public float Attacker_EndOfTurnHP;
    public float Opponent_EndOfTurnHP;

    public bool Attacker_DiesBeforeActing;
    public bool Opponent_DiesBeforeActing;

    public bool AttackerCanAct;
    public bool OpponentCanAct;

    public bool MutualKO;
    public bool AttackerMovedFirst;
    public bool AttackerHasSweepHorizon;

    public string SimulationLog;
}

public struct ProjectedBoardState
{
    //--Sim Units from Top1 and Top2
    public SimulatedUnit Current_Attacker;
    public SimulatedUnit Current_Opponent;
    public SimulatedUnit Next_Attacker;
    public SimulatedUnit Next_Opponent;

    //--Immediate KO Results
    public bool IGetImmediateKO;
    public bool IAmKONow;
    public bool OppIsKONow;
    public bool MutualKO;

    //--Material
    public int MyRemainingPieces;
    public int OppRemainingPieces;
    public int MaterialDelta;

    //--Value
    public int MyActiveValue_AfterTurn;
    public int OppActiveValue_AfterTurn;
    public int ValueDelta_AfterTurn;

    //--Board Control
    public bool IControlNextTurn;
    public bool OppControlNextTurn;

    //--Stability
    public bool IAmStable;
    public bool OppIsStable;

    //--Pressure
    public bool IWillSurviveNext;
    public bool OppWillSurviveNext;

    public bool IThreatenImmediate;
    public bool OppThreatensImmediate;

    public bool IThreatenNext;
    public bool OppThreatenNext;
    public bool IKillNext;
    public bool OppKillNext;
    public bool AttackerWillMoveFirst;
    public bool OpponentWillMoveFirst;

    //--Tempo
    public int RevengeScore;
    public TempoState FutureTempoState;

    //--Utility
    public bool ICreateDecisiveState;
    public bool ICreatePressureState;
    public bool OppCreatesDecisiveState;
    public bool OppCreatesPressureState;

    //--Role Fulfillment
    public bool AttackerFulfilledRole;
    public bool OpponentFulfilledRole;
    public bool AttackerFulfilledUtilityRole;
    public bool OpponentFulfilledUtilityRole;
    public float RoleFulfillmentProgress;
    public bool UtilityRole;

    //--Individual Scores
    public int MaterialScore;
    public int ConversionScore;
    public int Stabilityscore;
    public int ControlScore;
    public int PressureScore;
    public int RoleScore;
    public int TempoScore;
    public int SacScore;
}

public struct DoomedOutcome
{
    public bool NearGuaranteedPieceLoss;
    public bool AlwaysLoseAPiece;

    public bool OpponentThreatensKO;
    public bool AttackerMovesFirst;
    public bool AttackerCannotAct;

    public int ViableSwitches;
    public bool AllSwitchesDoomed;

    public bool SweepIncoming;

    public bool NoTempoRecoveryLine;
    public TurnOutcomeProjection TempoRecoveredTOP;

    public float PressureScore;
    public bool DoomedTurn;
}
