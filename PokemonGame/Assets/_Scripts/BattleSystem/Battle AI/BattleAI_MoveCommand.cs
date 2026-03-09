using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleAI_MoveCommand
{
    private BattleAI _ai;
    private BattleAI_UnitSim _unitSim;
    private BattleAI_BattleSim _battleSim;
    private BattleAI_Projection _proj;

    public BattleAI_MoveCommand( BattleAI ai )
    {
        _ai = ai;
        _unitSim = _ai.UnitSim;
        _battleSim = _ai.BattleSim;
        _proj = _ai.Projection;
    }

    public void SubmitMoveCommand( BattleUnit target, ActionEvaluation action )
    {
        _ai.ResetSwitchAmount();
        var attackStyle = ChooseAttackStyle();
        Move move = action.MovePayload;

        switch( attackStyle )
        {
            case AIDecisionType.StrongestMove:
                break;
            
            case AIDecisionType.RandomMove:
                move = GetRandomMove( target );
                break;
        }

        List<BattleUnit> targets = new();
        
        if( move.MoveSO.MoveTarget == MoveTarget.Self )
            targets.Add( _ai.Unit );
        else
            targets.Add( target );

        if( move != null )
        {
            _ai.BattleSystem.SetMoveCommand( _ai.Unit, targets, move, true );
        }
        else
            Debug.LogError( $"{_ai.Unit.Pokemon.NickName} has not chosen a move even though it was supposed to! Battle will now hang!" );
    }

    public int AttackScore( TempoStateResult tempo, ExchangeEvaluation eval, BoardContext context, MoveThreatResult move )
    {
        int score = 0;

        var attackerName = eval.AttackerName;
        var targetName = eval.OpponentName;

        var myPTKO_onTarget = eval.AttackerPTKOR;
        var theirPTKO_onMe = eval.OpponentPTKOR;

        string moveName = "NONE";

        if( move.Move != null )
            moveName = move.Move.MoveSO.Name;
        else
        {
            _ai.CurrentLog.Add( $"({attackerName}) Had no viable attacking move! Tanking Score!" );
            return -999;
        }

        _ai.CurrentLog.Add( $"===[Beginning Attack Scoring for {attackerName} ({moveName}) vs {targetName}. Tempo: {tempo.TempoState}, My PTKO Them: {myPTKO_onTarget.PTKO}, their PTKO on me: {theirPTKO_onMe.PTKO}]===" );

        //--KO Class Advantage
        score += _proj.Get_OffensivePTKOScore( myPTKO_onTarget.Score );
        _ai.CurrentLog.Add( $"My ({attackerName}) PTKO Score {myPTKO_onTarget.Score}. Score: {score}" );

        score += theirPTKO_onMe.Score;
        _ai.CurrentLog.Add( $"Their ({targetName}) PTKO Score {theirPTKO_onMe.Score}. Score: {score}" );

        bool iAmFaster = eval.AttackerMovesFirst;
        bool iThreatenKO = eval.AttackerThreatensKO;
        bool theyThreatenKO = eval.OpponentThreatensKO;

        if( iThreatenKO )
        {
            if( iAmFaster )
                score += 135; //--Commit hard
            else
                score += 75; //--Probably commit
        }

        _ai.CurrentLog.Add( $"I Threaten a KO: {iThreatenKO}. I am faster: {iAmFaster}. Score: {score}" );

        float hp = eval.AttackerHPR;
        if( theyThreatenKO )
        {
            int deathPenalty;

            if( hp > 0.6f )
                deathPenalty = 120;
            else if( hp > 0.3f )
                deathPenalty = 80;
            else
                deathPenalty = 40;

            if( !iAmFaster )
                score -= deathPenalty;
            else
                score -= deathPenalty / 2;
        }
        else
        {
            if( hp > 0.8f && iAmFaster )
                score += 20;
        }

        _ai.CurrentLog.Add( $"They Threaten a KO: {theyThreatenKO}, I am faster: {iAmFaster}. Score: {score}" );

        float myHPRatio = eval.AttackerHPR;

        if( myHPRatio < 0.2f )
            score -= 20;
        else if( myHPRatio < 0.4f )
            score -= 10;
        else if( myHPRatio >= 0.8f && !theyThreatenKO )
            score += 15;

        _ai.CurrentLog.Add( $"HP Ratio Check {myHPRatio}. Score: {score}" );

        score += _ai.TempoAttackModifier( tempo );

        _ai.CurrentLog.Add( $"Tempo check. Score: {score}" );

        if( context.IsForcedTrade )
            score += 45;

        _ai.CurrentLog.Add( $"Forced Trade: {context.IsForcedTrade}. Score: {score}" );

        if( context.IsBehind )
            score += 20;

        _ai.CurrentLog.Add( $"===[Is Behind: {context.IsBehind}. Final Attack Score: {score}]===" );

        //--Attacking Based on Switch Pressure (replaced flat weight bonus simply for attacking)

        if( eval.AttackerForcesSwitch )
        {
            score += 15;
            _ai.CurrentLog.Add( $"Opponent likey forced to switch. Score: {score}" );
        }
        else if( eval.ExchangeState == ExchangeState.Pressure )
        {
            score += 10;
            _ai.CurrentLog.Add( $"The pressure is on! Score: {score}" );
        }

        return score;
    }

    private AIDecisionType ChooseAttackStyle()
    {
        return Random.value < _ai.TrainerSkillModifier ? AIDecisionType.StrongestMove : AIDecisionType.RandomMove;
    }

    private Move GetRandomMove( BattleUnit target )
    {
        // Debug.Log( $"[AI Scoring] Getting Random Move vs {target.Pokemon.NickName}" );
        List<Move> usableMoves = new();

        if( _ai.Unit.Flags[UnitFlags.ChoiceItem].IsActive && _ai.Unit.LastUsedMove != null )
            return _ai.Unit.LastUsedMove;

        foreach( var move in _ai.Unit.Pokemon.ActiveMoves )
        {
            if( move.PP == 0 )
                continue;

            if( !_ai.BattleSystem.MoveSuccess( _ai.Unit, target, move, true ) )
                continue;
            
            usableMoves.Add( move );
        }

        Move randMove = null;
        if( usableMoves != null && usableMoves.Count > 0 )
        {
            int r = Random.Range( 0, usableMoves.Count );
            randMove = usableMoves[r];
            usableMoves.Clear();
        }

        return randMove;
    }

    public MoveThreatResult Get_BestSimulatedAttack( Pokemon attacker, Pokemon target )
    {
        // CustomLogSession moveLog = new();
        int bestScore = int.MinValue;
        float bestModifier = 1f;
        Move bestMove = null;
        TurnOutcomeProjection bestTop = new();
        var materialStatus = _proj.GetMaterialStatus( attacker );

        //--Create Target's PTKO on attacker & target's sim unit once for use in each attacker's move's simulation
        float attHPR                    = _ai.Get_HPRatio( attacker );
        float tarHPR                    = _ai.Get_HPRatio( target );
        var tarMTR                      = _ai.Get_MostThreateningMove( target, attacker ); //--Remember, the order here is attacking unit vs target unit. this is the target's attack on the attacker here.
        var tarMoveMod                  = tarMTR.Modifier;
        var tarWSR                      = _proj.Get_WallingScoreResult( target, attacker, tarMTR );
        PotentialToKOResult tarPTKOR    = _proj.Get_PotentialToKOResult( tarWSR, tarMoveMod, attHPR );
        
        var fieldSim                    = _ai.UnitSim.BuildSimField();
        var targetSimUnit               = _ai.UnitSim.BuildSimUnit( target, tarHPR, tarMTR, fieldSim );

        bool isFaster = _ai.GetUnitContextualSpeed( attacker ) > _ai.GetUnitContextualSpeed( target );

        foreach( var move in attacker.ActiveMoves )
        {
            //--If the move has 0 pp, we can't use it
            if( move.PP == 0 )
                continue;

            //--If the move has 0 power, or is a status move, we skip it. We're looking for damaging moves only!
            if( move.MovePower <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                continue;

            // if( _ai.BattleSimulation.MoveSuccess() ) //--Do this soon!!! --03/06/26
                // continue;

            //--Move type effectiveness
            float effectiveness = TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type2 );

            //--If there a type immunity, skip this move
            if( effectiveness == 0f )
                continue;

            float modifier                  = effectiveness * _ai.UnitSim.Get_MoveModifier( attacker, target, move );
            MoveThreatResult mtr            = new(){ Score = 0, Modifier = modifier, Move = move };
            var attWSR                      = _proj.Get_WallingScoreResult( attacker, target, mtr );
            PotentialToKOResult attPTKOR    = _proj.Get_PotentialToKOResult( attWSR, modifier, tarHPR );

            // bool movesFirst = isFaster || move.MoveSO.MovePriority > MovePriority.Zero;

            targetSimUnit.CurrentHPR        = tarHPR; //--Because we create this sim unit only once, we need to make sure we heal its hp to where it currently is before we run the attack sim!
            Debug.Log( $"[Best Simulated Move] Target's pre-sim HPR: {tarHPR}. Target's Sim Unit HRP: {targetSimUnit.CurrentHPR}" );
            var attackerSimUnit             = _ai.UnitSim.BuildSimUnit( attacker, attHPR, mtr, fieldSim );
            var battleSimContext            = _battleSim.Get_BattleSimContext( attPTKOR.PTKO, tarPTKOR.PTKO, attackerSimUnit, targetSimUnit, fieldSim );
            
            var top                         = _battleSim.SimulateAttackRound( battleSimContext, $"Get Best Simulated Move ({attacker.NickName}, {move.MoveSO.Name})" );

            //--Begin Scoring
            int score = 0;
            if( top.Attacker_DiesBeforeActing )
                score -= 150;

            if( top.Opponent_DiesBeforeActing )
                score += 150;

            if( top.MutualKO )
                score += materialStatus.IsBehind ? 40 : -40;


            score += Mathf.FloorToInt( ( 1f - top.Opponent_EndOfTurnHP ) * 90f );
            score -= Mathf.FloorToInt( ( 1f - top.Attacker_EndOfTurnHP ) * 80f );

            int accuracy = move.MoveSO.Accuracy;
            if( accuracy < 70 )                         score -= 35;
            else if( accuracy < 80 )                    score -= 20;
            else if( accuracy < 90 )                    score -= 10;
            else if( accuracy < 100 )                   score -= 5;

            if( score > bestScore )
            {
                bestScore = score;
                bestModifier = modifier;
                bestMove = move;
                bestTop = top;
            }
        }

        //--Fallback Move Scenario
        if( bestMove == null )
        {
            Move fallbackMove = attacker.GetRandomMove();

            //--Move type effectiveness
            float effectiveness             = TypeChart.GetEffectiveness( fallbackMove.MoveType, target.PokeSO.Type1 ) * TypeChart.GetEffectiveness( fallbackMove.MoveType, target.PokeSO.Type2 );
            float modifier                  = effectiveness * _ai.UnitSim.Get_MoveModifier( attacker, target, fallbackMove );
            MoveThreatResult mtr            = new(){ Score = 0, Modifier = modifier, Move = fallbackMove };
            var attWSR                      = _proj.Get_WallingScoreResult( attacker, target, mtr );
            PotentialToKOResult attPTKOR    = _proj.Get_PotentialToKOResult( attWSR, modifier, tarHPR );

            // bool movesFirst = isFaster || fallbackMove.MoveSO.MovePriority > MovePriority.Zero;

            targetSimUnit.CurrentHPR    = tarHPR; //--Because we create this sim unit only once, we need to make sure we heal its hp to where it currently is before we run the attack sim!
            Debug.Log( $"[Best Simulated Move] Target's pre-sim HPR: {tarHPR}. Target's Sim Unit HRP: {targetSimUnit.CurrentHPR}" );
            var attackerSimUnit         = _ai.UnitSim.BuildSimUnit( attacker, attHPR, mtr, fieldSim );
            var battleSimContext        = _battleSim.Get_BattleSimContext( attPTKOR.PTKO, tarPTKOR.PTKO, attackerSimUnit, targetSimUnit, fieldSim );
            
            var top                     = _battleSim.SimulateAttackRound( battleSimContext, $"Get Best Simulated Move ({attacker.NickName}, {fallbackMove.MoveSO.Name})" );

            bestScore       = 0;
            bestModifier    = modifier;
            bestMove        = fallbackMove;
            bestTop         = top;
        }

        return new()
        {
            Score = bestScore,
            Modifier = bestModifier,
            Move = bestMove,
            Top = bestTop,
        };
    }

    public SetupThreatResult Get_BestSimulatedSetup( Pokemon attacker, Pokemon target )
    {
        var setupMoves = _ai.UnitSim.GetSetupMoves( attacker.ActiveMoves );
        SetupThreatResult best = new();
        int bestValue = int.MinValue;

        var bestAttackBefore = Get_BestSimulatedAttack( attacker, target );

        if( setupMoves.Count <= 0 )
            return best;

        foreach( var move in setupMoves )
        {
            
        }

        return new()
        {
            
        };
    }
}
