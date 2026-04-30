using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Windows.Speech;

public class BattleAI_Projection
{
    private readonly BattleAI _ai;
    private readonly BattleAI_UnitSim _unitSim;
    
    
    public BattleAI_Projection( BattleAI ai )
    {
        _ai = ai;
        _unitSim = _ai.UnitSim;
    }

    public ProjectedBoardState BuildPBS( TurnOutcomeProjection top1, TurnOutcomeProjection top2, ExchangeEvaluation immediateExchangeEval, int myRemainingPieces, int oppRemainingPieces )
    {
        bool iAmKO = top1.Attacker_EndOfTurnHP <= 0;
        bool oppIsKO = top1.Opponent_EndOfTurnHP <= 0 ;

        var futureExchangeEval = _ai.Projection.EvaluateExchange( top2.Attacker, top2.Opponent ); //--We use TOP1 attacker and opponent here because their HP is directly mutated by the simulation, so EE gets an accurate beginning to the next round.
        var futureTempoState = _ai.Projection.GetTempoState( futureExchangeEval );

        _ai.CurrentLog.Add( $"========================" );
        _ai.CurrentLog.Add( $"=====[BUILDING PBS]=====" );
        _ai.CurrentLog.Add( $"========================" );
        _ai.CurrentLog.Add( $"" );
        // _ai.CurrentLog.Add( $"===[Displaying Simulation Log from Chosen Action for below PBS logs]===" );
        // _ai.CurrentLog.Add( top1.SimulationLog );

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

        var myTeam = _ai.GetPartyAsIBattleAIUnits( top1.Attacker.PID );
        var oppTeam = _ai.GetPartyAsIBattleAIUnits( top1.Opponent.PID );
        var myTeamPieceValues = _ai.GetTeamPieceValues( myTeam );
        var oppTeamPieceValues = _ai.GetTeamPieceValues( oppTeam );
        int myValue = myTeamPieceValues[top1.Attacker.PID].OffensiveValue;
        int oppValue = oppTeamPieceValues[top1.Opponent.PID].OffensiveValue;

        //--Turn Economy
        //--My Turns
        int myTurnsRemaining = immediateExchangeEval.AttackerMovesFirst ? ( top1.Attacker_EndOfTurnHP > 0 ? 1 : 0 ) : ( immediateExchangeEval.AttackerSurvives ? 1 : 0 );

        //--Opponent Turns
        int oppTurnsRemaining = immediateExchangeEval.OpponentMovesFirst ? ( top1.Opponent_EndOfTurnHP > 0 ? 1 : 0 ) : ( immediateExchangeEval.OpponentSurvives ? 1 : 0 );

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

        _ai.CurrentLog.Add( $"[Build PBS] Future → I Live: {iSurviveNext}, Opp Lives: {oppSurviveNext}" );
        _ai.CurrentLog.Add( $"[Build PBS] Future Threat → I Threaten: {iThreatenNext}, Opp Threatens: {oppThreatenNext}" );

        //--Role Fulfillment
        float attackerDamageTaken = top1.Attacker.BeginningHPR - top1.Attacker_EndOfTurnHP;
        bool attackRoleFulfilled = iThreaten || oppIsKO;
        bool tankRoleFulfilled = attackerDamageTaken <= 0.3f && !oppThreatens;
        bool attackerFulfilledRole = !iAmKO && ( attackRoleFulfilled || tankRoleFulfilled );

        float opponentDamageTaken = top1.Opponent.BeginningHPR - top1.Opponent_EndOfTurnHP;
        bool oppAttackRoleFulfilled = oppThreatens || iAmKO;
        bool oppTankRoleFulfilled = opponentDamageTaken <= 0.3f && !iThreaten;
        bool opponentFulfilledRole = !oppIsKO && ( oppAttackRoleFulfilled || oppTankRoleFulfilled );

        //--Stability
        bool iAmStable = iSurviveNext && !( oppKillNext || ( oppThreatenNext && !futureExchangeEval.AttackerMovesFirst ) );
        bool oppIsStable = oppSurviveNext && !( iKillNext || ( iThreatenNext && !futureExchangeEval.OpponentMovesFirst ) );

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

            //--Role Fulfillment
            AttackerFulfilledRole = attackerFulfilledRole,
            OpponentFulfilledRole = opponentFulfilledRole,

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

        float myThreat = GetThreatMultiplier( pbs.IKillNext, pbs.IThreatenNext, pbs.AttackerWillMoveFirst, pbs.IWillSurviveNext, pbs.MyActiveValue_AfterTurn );
        float oppThreat = GetThreatMultiplier( pbs.OppKillNext, pbs.OppThreatenNext, pbs.OpponentWillMoveFirst, pbs.OppWillSurviveNext, pbs.OppActiveValue_AfterTurn );

        float threatDelta = myThreat - oppThreat;

        pressureScore = Mathf.RoundToInt( Mathf.Clamp( threatDelta * 0.75f, -25f, 25f ) );

        score += pressureScore;
        _ai.CurrentLog.Add( $"[PBS] Pressure Score: {pressureScore} (My: {myThreat}, Opp: {oppThreat}). Score: {score}" );

        //--------------------------------------------------
        //--Role Fulfillment
        //--------------------------------------------------

        int roleScore = 0;

        if( pbs.AttackerFulfilledRole && !pbs.OpponentFulfilledRole )
            roleScore += 15;
        else if( !pbs.AttackerFulfilledRole && pbs.OpponentFulfilledRole )
            roleScore -= 15;
        else if( !pbs.AttackerFulfilledRole && !pbs.OpponentFulfilledRole )
            roleScore -= 5;

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

    private float GetThreatMultiplier( bool getsKill, bool threatens, bool movesFirst, bool survives, int pieceValue )
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

    public TempoStateResult GetTempoState( ExchangeEvaluation eval )
    {
        // Debug.Log( $"[AI Scoring][Get Tempo] Starting Tempo State Check for Attacker: {attacker.Pokemon.NickName} vs Target: {target.Pokemon.NickName}" );
        var tempo = ClassifyTempo( eval );

        bool attackerHasPriorityAdvantage = eval.AttackerMovesFirst && !eval.OpponentMovesFirst;
        bool targetHasPriorityAdvantage = eval.OpponentMovesFirst && !eval.AttackerMovesFirst;

        // Debug.Log( $"[AI Scoring][Get Tempo] Final Tempo State: {tempo}, Attacker: {attacker.Pokemon.NickName} vs Target: {target.Pokemon.NickName}" );

        return new(){ TempoState = tempo, AttackerHasPriority = attackerHasPriorityAdvantage, TargetHasPriority = targetHasPriorityAdvantage };
    }

    public ExchangeEvaluation EvaluateExchange( IBattleAIUnit attacker, IBattleAIUnit target )
    {
        //--Potential to KO
        //--Attacker PTKO Target
        var attackerMTR = _ai.MoveCommand.GetMove_BestAttack( attacker, target, "Evaluate Exchange (attacker vs target)" );
        var targetWSR = Get_EstimatedDamageResult( attacker, target, attackerMTR );
        float targetHP = target.BeginningHPR;

        PotentialToKOResult attackerPTKO_target = Get_PotentialToKOResult( targetWSR, attackerMTR, targetHP );

        //--Target PTKO Attacker
        var targetMTR = _ai.MoveCommand.GetMove_BestAttack( target, attacker, "Evaluate Exchange (target vs attacker)" );
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
        bool attackerForcesSwitch = _unitSim.PredictSwitchProbability( attackerPTKO_target.PTKO, targetPTKO_attacker.PTKO, attackerMovesFirst, attackerHP, targetHP ) > 0.8f;
        bool targetForcesSwitch = _unitSim.PredictSwitchProbability( targetPTKO_attacker.PTKO, attackerPTKO_target.PTKO, targetMovesFirst, targetHP, attackerHP ) > 0.8f;

        ExchangeState state = ExchangeState.Neutral;

        if( attackerForcesSwitch )
            state = ExchangeState.OpponentForcedOut;
        else if( attackerThreatensKO_onTarget && !targetThreatensKO_onAttacker )
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

    public BoardContext GetBoardContext( IBattleAIUnit target, ExchangeEvaluation eval )
    {
        //--The unit attached to this AI.
        BattleAI_PokemonAdapter ourAdapter = _ai.ThisUnitAdapter;

        //--Safe Pivot Check
        bool safePivotExists = CheckForSafePivot( target );

        //--Is Forced Trade Detection
        bool lowHP = eval.AttackerHPR < 0.3f;
        bool likelyDying = eval.OpponentPTKOR.PTKO >= PotentialToKO.Dangerous;
        bool isForced = ( likelyDying && !safePivotExists ) || ( lowHP && eval.OpponentPTKOR.PTKO >= PotentialToKO.Risky );

        //--Material Information
        var myTeamAlive = _ai.CreateBattleAIUnits_FromPokemon( _ai.GetRemainingAllyPokemon( ourAdapter.PID ) );
        var oppTeamAlive = _ai.CreateBattleAIUnits_FromPokemon( _ai.GetRemainingOpposingPokemon( ourAdapter.PID ) );

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
                if( !mon.IsFainted() && pivotHP > 0.35f )
                {
                    BattleAI_PokemonAdapter monAdapter = new( mon, _ai );
                    var targetThreateningMove = _ai.MoveCommand.GetMove_BestAttack( opponent, monAdapter, "Get Safe Pivot" );
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

        float offensiveWeight = _ai.TeamPieceValues[mon.PID].OffensiveValue / 100f;

        score -= offensiveWeight * 0.4f;

        // Debug.Log( $"Offensive Weight: {offensiveWeight}. Score: {score}" );

        float expendability = Mathf.Clamp01( score );

        // Debug.Log( $"===[{mon.NickName}'s Final clamped Expendability Score: {expendability}]===" );

        return expendability;
    }

    public EstimatedDamageResult Get_EstimatedDamageResult( IBattleAIUnit attacker, IBattleAIUnit target, MoveThreatResult moveThreat )
    {
        const float STAT_SCALAR = 0.29f;
        const float DAMAGE_ROLL = 0.925f;
        float attack = 1f;
        float defense = 1f;
        Stat attackingStat = Stat.Attack;
        Stat defendingStat = Stat.Defense;
        string key = "none";
        var moveSO = moveThreat.Move.MoveSO;
        float movePower = moveThreat.Move.MovePower;
        float modifier = moveThreat.Modifier;
        float brnOrfbt = 1f;

        //--Unique Wallscore Key check
        if( moveThreat.Move != null )
        {
            key = moveThreat.Move.MoveSO.Name;

            if( _unitSim.MovePowerConditions.TryGetValue( key, out var mod ) )
                movePower = mod( attacker, target, moveThreat.Move );
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
        if( _ai.UniqueWallScores.ContainsKey( key ) )
        {
            // Debug.Log( $"[AI Scoring][Get Walling Score] Getting Walling Score! Unique Wall Scores found move {moveThreat.Move.MoveSO.Name} in its dictionary with key: {key}" );
            attackingStat = _ai.UniqueWallScores[key].AttackingStat;
            defendingStat = _ai.UniqueWallScores[key].DefendingStat;
            attack = _ai.GetUnitInferredStat( attacker, attackingStat );
            defense = _ai.GetUnitInferredStat( target, defendingStat );
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
                attack = _ai.GetUnitInferredStat( attacker, attackingStat );
                defense = _ai.GetUnitInferredStat( target, defendingStat );

                if( attacker.SevereStatus == SevereConditionID.BRN && attacker.Ability != AbilityID.Guts )
                    brnOrfbt = 0.5f;
            }
            else if( cat == MoveCategory.Special )
            {
                attackingStat = Stat.SpAttack;
                defendingStat = Stat.SpDefense;
                attack = _ai.GetUnitInferredStat( attacker, attackingStat );
                defense = _ai.GetUnitInferredStat( target, defendingStat );

                if( attacker.SevereStatus == SevereConditionID.FBT )
                    brnOrfbt = 0.5f;
            }
            else
            {
                //--Status move used, we may need to alter this somehow
                attack = 1f;
                defense = 1f;
            }
        }

        float targetMHP = _ai.GetBaseStat( target, Stat.HP );
        float levelFactor = ( 2f * attacker.Level / 5f + 2f );

        float damage = ( ( levelFactor * movePower * ( attack / defense ) / 50 ) + 2 ) * modifier * brnOrfbt * DAMAGE_ROLL;
        float normalizedDamage = ( damage / targetMHP ) * STAT_SCALAR;
        
        float lowRoll = ( ( levelFactor * movePower * ( attack / defense ) / 50 ) + 2 ) * modifier * brnOrfbt * 0.85f;
        float lowRollScaled = ( lowRoll / targetMHP ) * STAT_SCALAR;

        if( !_unitSim.CanActOnTurn( attacker ) )
            normalizedDamage = 0;

        moveThreat.EstimatedDamage = normalizedDamage; //--store damage in MTR for sim use

        // Debug.Log( $"[AI Scoring][Get Walling Score] Getting Walling Score! Target {target.Name}'s Defending Stat: {defendingStat}, {defense}, Base HP: {targetMHP}. Level {attacker.Level} ({levelFactor}) Attacker {attacker.Name}'s Attacking stat {attackingStat}, {attack}. Move: {moveThreat.Move.MoveSO.Name}, Power: {movePower}, Modifier: {modifier}. Final Damage Estimate: {damage}, Normalized: {normalizedDamage}" );
        
        EstimatedDamageResult edr = new()
        {
            // Score = score,
            DamageEstimate = normalizedDamage,
            LowRollEstimate = lowRollScaled,
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

        return new()
        {
            Score = Get_PotentialToKOScoreFromEnum( ptko ),
            PTKO = ptko,
            Modifier = mtr.Modifier,
        };
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

        if( damage <= 0f )              return PotentialToKO.Untouchable;
        else if( damage <= 0.15f )      return PotentialToKO.HardWall;
        else if( damage <= 0.30f )      return PotentialToKO.Sturdy;
        else if( damage <= 0.47f )      return PotentialToKO.Safe;
        else if( damage <= 0.63f )      return PotentialToKO.TwoHKO;
        else if( damage <= 0.80f )      return PotentialToKO.Risky;
        else if( damage <= 0.97f )      return PotentialToKO.Dangerous;
        else if( lowRoll > 0.97f )      return PotentialToKO.OHKO;
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
            BattleAI_PokemonAdapter ourMon = new( ourTeam[i], _ai );

            for( int t = 0; t < theirTeam.Count; t++ )
            {
                BattleAI_PokemonAdapter theirMon = new( theirTeam[t], _ai );

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

    public CurrentPlan EvaluateCurrentPlan( ExchangeEvaluation ee, BoardContext bc, ThreatProfile tp, CurrentPlan prevPlan )
    {
        CurrentPlan nextPlan = new()
        {
            Type = PlanType.None,
            FocusPID = string.Empty,
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

        _ai.CurrentLog.Add( $"[Win Con] Gathered some context. Material Delta: {materialDelta}. I am Stable: {iAmStable}, Opp is Stable : {oppIsStable}" );

        //----------------------------------------
        //--Stabilize
        //----------------------------------------

        if( !iAmStable )
            stabilizeScore += 2.5f;

        stabilizeScore += tp.Urgency >= ThreatUrgency.High ? 2f : 0f;
        stabilizeScore += tp.ThreatensImmediateKO ? 1.5f : 0f;
        stabilizeScore += tp.ForcesSwitch ? 1.0f : 0f;
        _ai.CurrentLog.Add( $"[Win Con] Stabilize Score: {stabilizeScore}" );

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

        preventSweepScore += tp.Urgency >= ThreatUrgency.High ? 1.5f : 0f;
        _ai.CurrentLog.Add( $"[Win Con] Prevent Sweep Score: {preventSweepScore}" );

        //----------------------------------------
        //--Enable Sweep
        //----------------------------------------

        Pokemon bestSweeper = null;
        float bestSweepScore = 0f;

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
            if( tp.Type == ThreatType.BurstDamage || tp.SweepPotential )
                enableSweepScore -= 2.5f;
            else if( ( tp.Type == ThreatType.Tank || tp.Type == ThreatType.Utility ) && iAmStable )
                enableSweepScore += 1.5f;
            else
                enableSweepScore -= 1f;
        }

        if( !iAmStable )
            enableSweepScore -= 1.5f;

        _ai.CurrentLog.Add( $"[Win Con] Enable Sweep Score: {enableSweepScore}" );

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

            if( blocks > blockCount )
            {
                blockCount = blocks;
                worstWall = opp.Pokemon;
            }
        }

        aggressScore += blockCount;
        aggressScore += tp.ConstraintPressure >= 2f ? 1.5f : 0f;

        if( tp.Type == ThreatType.Tank || tp.ConstraintPressure >= 2f )
            aggressScore += 2f;

        _ai.CurrentLog.Add( $"[Win Con] Aggress Score: {aggressScore}" );

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
            float threshold = prev.Confidence + 0.2f;

            if( next.Confidence < threshold && tp.Urgency < ThreatUrgency.High )
                return prev;
        }

        return next;
    }

    public int GetCurrentPlanBias( ActionEvaluation action, ProjectedBoardState pbs, BoardContext bc, CurrentPlan plan )
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

                    if( action.ActorPID == plan.FocusPID )
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

                if( pbs.IAmKONow && action.ActorPID == plan.FocusPID )
                    score -= 25;
                else if( action.ActorPID == plan.FocusPID )
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
            score += 10;
        }
        else if( pbs.OppControlNextTurn )
        {
            score -= 10;
        }

        if( action.Target == null || action.Target.Pokemon == null )
        {
            //--getting odd null errors for action targets, let's leave this as is for now like this as a null check
        }
        else
        {
            if( action.Target.Pokemon == plan.FocusMon )
            {
                score += 10;
            }
        }

        //--if plan aligns with broader win condition from future long term strategy planning function, reward all

        return score;
    }

    public BattlefieldState GetBattlefieldState( SimulatedField field )
    {
        BattlefieldState bfs = new()
        {
            Round = _ai.Round,
            IsEarlyGame = _ai.Round <= 5,
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
            if( _unitSim.PokemonHasWeatherAbility( mon ) )
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

            if( _unitSim.PokemonHasWeatherMove( mon ) )
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
            if( _unitSim.PokemonHasWeatherAbility( mon ) )
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

            if( _unitSim.PokemonHasWeatherMove( mon ) )
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

    //--Role Fulfillment
    public bool AttackerFulfilledRole;
    public bool OpponentFulfilledRole;

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
