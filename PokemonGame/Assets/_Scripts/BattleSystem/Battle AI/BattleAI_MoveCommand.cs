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

    public void SubmitMoveCommand( ActionEvaluation action )
    {
        _ai.ResetSwitchAmount();
        var attackStyle = ChooseAttackStyle();
        Move move = action.MovePayload;

        switch( attackStyle )
        {
            case AIDecisionType.ChosenMove:
                break;
            
            case AIDecisionType.RandomMove:
                move = GetRandomMove( action.Target );
                break;
        }

        List<BattleUnit> targets = new();
        
        if( move.MoveSO.MoveTarget == MoveTarget.Self )
            targets.Add( _ai.Unit );
        else
            targets.Add( action.Target );

        if( move != null )
        {
            _ai.BattleSystem.SetMoveCommand( _ai.Unit, targets, move, true );
        }
        else
            Debug.LogError( $"{_ai.Unit.Pokemon.NickName} has not chosen a move even though it was supposed to! Battle will now hang!" );
    }

    private AIDecisionType ChooseAttackStyle()
    {
        return Random.value < _ai.TrainerSkillModifier ? AIDecisionType.ChosenMove : AIDecisionType.RandomMove;
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

        score += _ai.Attack_TempoModifier( tempo );

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

    public int SetupScore( TempoStateResult tempo, ExchangeEvaluation eval, BoardContext context, SetupThreatResult setup )
    {
        int score = 0;

        var attackerName = eval.AttackerName;
        var targetName = eval.OpponentName;

        var myPTKO_onTarget = setup.AfterPTKO;
        var theirPTKO_onMe = eval.OpponentPTKOR;

        string moveName = "NONE";

        if( setup.Move != null )
            moveName = setup.Move.MoveSO.Name;
        else
        {
            _ai.CurrentLog.Add( $"({attackerName}) Had no viable setup move! Tanking Score!" );
            return -999;
        }

        if( theirPTKO_onMe.PTKO >= PotentialToKO.Risky && !eval.AttackerMovesFirst )
        {
            _ai.CurrentLog.Add( $"We're likely to die if we setup now! Tanking Score!" );
            return -999;
        }

        if( theirPTKO_onMe.PTKO >= PotentialToKO.Dangerous )
        {
            _ai.CurrentLog.Add( $"We're likely to die if we setup now! Tanking Score!" );
            return -999;
        }

        _ai.CurrentLog.Add( $"===[Beginning Setup Scoring for {attackerName} ({moveName}) vs {targetName}. Tempo: {tempo.TempoState}, My PTKO Them after setup: {myPTKO_onTarget.PTKO}, their PTKO on me now: {theirPTKO_onMe.PTKO}]===" );

        //--Setup Value base
        score += setup.SetupValue;
        _ai.CurrentLog.Add( $"Added setup value. Score: {score}" );

        //--Discourage setup if we can already KO AND we aren't very tanky vs our current opponent. We DO want to setup if we can take some hits, especially if we're defensively setting up or going for iron defense body press.
        if( eval.AttackerThreatensKO && theirPTKO_onMe.PTKO < PotentialToKO.TwoHKO )
        {
            if( eval.AttackerMovesFirst )
                score -= 60;
            else
                score -= 30;
        }

        //--If we are likely to KO next turn
        if( myPTKO_onTarget.PTKO >= PotentialToKO.Dangerous && eval.AttackerMovesFirst )
            score += 30;
        else if( myPTKO_onTarget.PTKO >= PotentialToKO.Risky )
            score += 20;
        else if( myPTKO_onTarget.PTKO <= PotentialToKO.Risky && !eval.AttackerMovesFirst )
            score -= 45;
        else if( myPTKO_onTarget.PTKO <= PotentialToKO.Risky )
            score -= 35;

        _ai.CurrentLog.Add( $"Checked current PTKO. Score: {score}" );

        //--Sweep Count
        if( setup.SweepCount > 3 )
            score += 40;
        else if( setup.SweepCount > 0 )
            score += setup.SweepCount * 10;

        _ai.CurrentLog.Add( $"Checked Sweep Count. Score: {score}" );

        //--Improved survivability across opponent's entire remaining pieces
        score += setup.ImprovedPTKOs * 10;

        _ai.CurrentLog.Add( $"Checked Sweep Count. Score: {score}" );

        //--If the opponent is likely to switch, we should consider setting up. If they don't, maybe it's not the best idea even if we survive.
        if( setup.OpponentSwitches )
            score += 25;
        else if( !setup.OpponentSwitches && theirPTKO_onMe.PTKO >= PotentialToKO.TwoHKO )
            score -= 60;
        else if( !setup.OpponentSwitches )
            score -= 15;

        score += _ai.Setup_TempoModifier( tempo );
        _ai.CurrentLog.Add( $"Checked Tempo Modifier. Score: {score}" );

        //--Multiple setup attempt penalty.
        score -= _ai.SetupAmount * 25;

        return score;
    }

    public MoveThreatResult Get_BestSimulatedAttack( IBattleAIUnit attacker, IBattleAIUnit target, string source = "NO SOURCE" )
    {
        CustomLogSession moveLog = new();
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
        PotentialToKOResult tarPTKOR    = _proj.Get_PotentialToKOResult( tarWSR, tarMTR, attHPR );
        
        var fieldSim                    = _ai.UnitSim.BuildSimField();
        var targetSimUnit               = _ai.UnitSim.BuildSimUnit( target, tarHPR, tarMTR, fieldSim );

        bool isFaster = _ai.GetUnitContextualSpeed( attacker ) > _ai.GetUnitContextualSpeed( target );

        moveLog.Add( $"===[Beginning Scoring for {attacker.Name}'s Best Simulated Attack vs {target.Name}, called from {source}]===" );

        foreach( var move in attacker.ActiveMoves )
        {
            //--If the move has 0 pp, we can't use it
            if( move.PP == 0 )
                continue;

            //--If the move has 0 power, or is a status move, we skip it. We're looking for damaging moves only!
            if( move.MovePower <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                continue;

            // if( _ai.BattleSim.MoveSuccess() ) //--Do this soon!!! --03/06/26
                // continue;

            if( move.MoveSO.Name == "Fake Out" && !_ai.CanUseFakeOut( attacker, target ) )
                continue;

            //--choice lock detection goes here
            var attackerUnit = _ai.GetBattleUnit( attacker.PID );
            if( attackerUnit != null )
            {
                if( attackerUnit.Flags[UnitFlags.ChoiceItem].IsActive && attackerUnit.LastUsedMove != null && attackerUnit.LastUsedMove != move )
                    continue;
            }

            //--Move type effectiveness
            float effectiveness = TypeChart.GetEffectiveness( move.MoveType, target.Type.One ) * TypeChart.GetEffectiveness( move.MoveType, target.Type.Two );

            //--If there a type immunity, skip this move
            if( effectiveness == 0f )
                continue;

            moveLog.Add( $"[Best Simulated Move] Getting PTKO for {attacker.Name}'s {move.MoveSO.Name} on {target.Name} (HPR: {tarHPR}" );
            float modifier                  = effectiveness * _ai.UnitSim.Get_MoveModifier( attacker, target, move );
            MoveThreatResult mtr            = new(){ Score = 0, Modifier = modifier, Move = move };
            var attWSR                      = _proj.Get_WallingScoreResult( attacker, target, mtr );
            PotentialToKOResult attPTKOR    = _proj.Get_PotentialToKOResult( attWSR, mtr, tarHPR );

            moveLog.Add( $"[Best Simulated Move] PTKO for {attacker.Name}'s {move.MoveSO.Name} on {target.Name} (HPR: {tarHPR} is: {attPTKOR.PTKO} (Damage Estimate: {attWSR.DamageEstimate})" );

            // bool movesFirst = isFaster || move.MoveSO.MovePriority > MovePriority.Zero;

            targetSimUnit.CurrentHPR        = tarHPR; //--Because we create this sim unit only once, we need to make sure we heal its hp to where it currently is before we run the attack sim!
            moveLog.Add( $"[Best Simulated Move] Target's pre-sim HPR: {tarHPR}. Target's Sim Unit HRP: {targetSimUnit.CurrentHPR}" );
            var attackerSimUnit             = _ai.UnitSim.BuildSimUnit( attacker, attHPR, mtr, fieldSim );
            var battleSimContext            = _battleSim.Get_BattleSimContext( attPTKOR.PTKO, tarPTKOR.PTKO, attackerSimUnit, targetSimUnit, fieldSim );
            
            var top                         = _battleSim.SimulateAttackRound( battleSimContext, $"Get Best Simulated Move ({attacker.Name}, {move.MoveSO.Name})" );

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

            // if( effectiveness >= 2f )
                // score += 5;

            int movePower = move.MovePower;
            var moveSO = move.MoveSO;

            if( _unitSim.MovePowerConditions.TryGetValue( move.MoveSO.Name, out var mod ) )
            {
                movePower = mod( attacker, target, move );
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

            // score += Mathf.FloorToInt( move.MovePower * 0.05f );

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
            Move fallbackMove = _unitSim.GetRandomMove( attacker );

            //--Move type effectiveness
            float effectiveness             = TypeChart.GetEffectiveness( fallbackMove.MoveType, target.Type.One ) * TypeChart.GetEffectiveness( fallbackMove.MoveType, target.Type.Two );
            float modifier                  = effectiveness * _ai.UnitSim.Get_MoveModifier( attacker, target, fallbackMove );
            MoveThreatResult mtr            = new(){ Score = 0, Modifier = modifier, Move = fallbackMove };
            var attWSR                      = _proj.Get_WallingScoreResult( attacker, target, mtr );
            PotentialToKOResult attPTKOR    = _proj.Get_PotentialToKOResult( attWSR, mtr, tarHPR );

            // bool movesFirst = isFaster || fallbackMove.MoveSO.MovePriority > MovePriority.Zero;

            targetSimUnit.CurrentHPR    = tarHPR; //--Because we create this sim unit only once, we need to make sure we heal its hp to where it currently is before we run the attack sim!
            moveLog.Add( $"[Best Simulated Move] Target's pre-sim HPR: {tarHPR}. Target's Sim Unit HRP: {targetSimUnit.CurrentHPR}" );
            var attackerSimUnit         = _ai.UnitSim.BuildSimUnit( attacker, attHPR, mtr, fieldSim );
            var battleSimContext        = _battleSim.Get_BattleSimContext( attPTKOR.PTKO, tarPTKOR.PTKO, attackerSimUnit, targetSimUnit, fieldSim );
            
            var top                     = _battleSim.SimulateAttackRound( battleSimContext, $"Get Best Simulated Move ({attacker.Name}, {fallbackMove.MoveSO.Name})" );

            bestScore       = 0;
            bestModifier    = modifier;
            bestMove        = fallbackMove;
            bestTop         = top;
        }

        Debug.Log( moveLog.ToString() );
        moveLog.Clear();

        return new()
        {
            Score = bestScore,
            Modifier = bestModifier,
            Target = target,
            Move = bestMove,
            Top = bestTop,
        };
    }

    public SetupThreatResult Get_BestSimulatedSetup( IBattleAIUnit attacker, IBattleAIUnit target )
    {
        SetupThreatResult best = new();

        int bestValue = int.MinValue;
        int bestSweepCount = 0;
        int bestImprovedPTKOs = 0;
        
        Move bestSetup = null;

        StatStageDelta bestStageDelta = default;

        PotentialToKOResult bestBeforePTKO = default;
        PotentialToKOResult bestAfterPTKO = default;

        var setupMoves = _ai.UnitSim.GetSetupMoves( attacker.ActiveMoves );
        if( setupMoves.Count <= 0 )
            return best;

        //--Get opposing team's remaining pokemon.
        var oppTeam = _ai.GetRemainingOpposingPokemon( attacker.PID );

        //--Hp Ratios
        float attHPR                            = _ai.Get_HPRatio( attacker );
        float tarHPR                            = _ai.Get_HPRatio( target );

        //--Get the best attack before using a boosting move and its PTKO.
        var attackerMTRbefore                   = Get_BestSimulatedAttack( attacker, target, "Best Simulated Setup (before)" );
        var attWSRbefore                        = _proj.Get_WallingScoreResult( attacker, target, attackerMTRbefore );
        PotentialToKOResult attPTKObefore       = _proj.Get_PotentialToKOResult( attWSRbefore, attackerMTRbefore, tarHPR );

        //--Create Target's PTKO on attacker
        var tarMTRbefore                        = _ai.Get_MostThreateningMove( target, attacker ); //--Remember, the order here is attacking unit vs target unit. this is the target's attack on the attacker here.
        var tarWSRbefore                        = _proj.Get_WallingScoreResult( target, attacker, tarMTRbefore );
        PotentialToKOResult tarPTKORbefore      = _proj.Get_PotentialToKOResult( tarWSRbefore, tarMTRbefore, attHPR );
        
        //--Create Sim field
        var fieldSim                            = _ai.UnitSim.BuildSimField();

        bool currentlyFaster = _ai.GetUnitContextualSpeed( attacker ) > _ai.GetUnitContextualSpeed( target );

        foreach( var move in setupMoves )
        {
            var stageDelta = _unitSim.BuildStatStageDelta( move );

            //--We need to build this guy to get a new attack for him first, and then we can rebuild him with that improved attack. it's a little goofy, i will try to improve the flow of this later... --03/09/26
            var attackerSetupSim = _unitSim.BuildSimUnit( attacker, attHPR, attackerMTRbefore, fieldSim, stageDelta );

            //--Get the best attacks after the attacker uses the current setup move.
            var attackerMTRafter   = Get_BestSimulatedAttack( attackerSetupSim, target, "Best Simulated Setup (after)" );
            var targetMTRafter     = Get_BestSimulatedAttack( target, attackerSetupSim, "Best Simulated Setup (after)" );

            //--Post Setup Walling Scores
            var attWSRafter = _proj.Get_WallingScoreResult( attackerSetupSim, target, attackerMTRafter );
            var tarWSRafter = _proj.Get_WallingScoreResult( target, attackerSetupSim, targetMTRafter );

            //--Post Setup PTKOs
            PotentialToKOResult attPTKOafter    = _proj.Get_PotentialToKOResult( attWSRafter, attackerMTRafter, tarHPR );
            PotentialToKOResult tarPTKORafter   = _proj.Get_PotentialToKOResult( tarWSRafter, targetMTRafter, attHPR );

            int offensiveValue = _unitSim.ComputeOffensiveSetupValue( attPTKObefore, attPTKOafter, stageDelta );
            int defensiveValue = _unitSim.ComputeDefensiveSetupValue( tarPTKORafter, tarPTKORbefore, stageDelta ); //--we do after -> before here because we need this to be good for the attacker, not for the defender.

            //--Opposing Team Sweep Comparison
            int sweepValue = 0;
            int sweepCount = 0;
            foreach( var opp in oppTeam )
            {
                BattleAI_PokemonAdapter oppAdapter = new( opp, _ai );

                float oppHRP = _ai.Get_HPRatio( oppAdapter );
                var bestVSopp = Get_BestSimulatedAttack( attackerSetupSim, oppAdapter, "Best Simulated Setup (best vs target)" );
                var vsOppWSR = _proj.Get_WallingScoreResult( attackerSetupSim, oppAdapter, bestVSopp );
                PotentialToKOResult PTKOvsOpp = _proj.Get_PotentialToKOResult( vsOppWSR, bestVSopp, oppHRP );

                bool faster = _ai.GetUnitContextualSpeed( attackerSetupSim ) > _ai.GetUnitContextualSpeed( oppAdapter );

                if( PTKOvsOpp.PTKO >= PotentialToKO.Dangerous && faster )
                {
                    sweepValue += 10;
                    sweepCount += 1;
                }
                else if( PTKOvsOpp.PTKO >= PotentialToKO.Dangerous )
                {
                    sweepValue += 5;
                    sweepCount += 1;
                }
            }

            if( sweepCount >= 3 )
                sweepValue += 5;

            //--Opposing Team's offensive threat reduction comparison
            int wallValue = 0;
            int improvedPTKOs = 0;
            foreach( var opp in oppTeam )
            {
                BattleAI_PokemonAdapter oppAdapter = new( opp, _ai );

                //--Opp PTKO us Before Setup
                var vsUsMTRbefore = Get_BestSimulatedAttack( oppAdapter, attacker, "Best Simulated Setup (before)" );
                var vsUsWSRbefore = _proj.Get_WallingScoreResult( oppAdapter, attacker, vsUsMTRbefore );
                PotentialToKOResult OppPTKObefore = _proj.Get_PotentialToKOResult( vsUsWSRbefore, vsUsMTRbefore, attHPR );

                //--Opp PTKO us After Setup
                var vsUsMTRafter = Get_BestSimulatedAttack( oppAdapter, attackerSetupSim, "Best Simulated Setup (after)" );
                var vsUsWSRafter = _proj.Get_WallingScoreResult( oppAdapter, attackerSetupSim, vsUsMTRafter );
                PotentialToKOResult OppPTKOafter = _proj.Get_PotentialToKOResult( vsUsWSRafter, vsUsMTRafter, attHPR );

                bool faster = _ai.GetUnitContextualSpeed( attackerSetupSim ) > _ai.GetUnitContextualSpeed( oppAdapter );

                //--Compare before after defensive ptko from the entire team vs us here
                if( OppPTKOafter.PTKO < OppPTKObefore.PTKO )
                {
                    wallValue += 10;
                    improvedPTKOs += 1;
                }

                if( (int)OppPTKOafter.PTKO < Mathf.Max( 0, (int)OppPTKObefore.PTKO - 1 ) )
                {
                    wallValue += 10;
                }
            }

            int totalValue = offensiveValue + defensiveValue + sweepValue + wallValue;

            if( totalValue > bestValue )
            {
                bestSetup = move;
                bestStageDelta = stageDelta;
                bestBeforePTKO = attPTKObefore;
                bestAfterPTKO = attPTKOafter;
                bestValue = totalValue;
                bestSweepCount = sweepCount;
                bestImprovedPTKOs = improvedPTKOs;
            }
        }

        //--Build sim units before setup round
        attackerMTRbefore.Move = bestSetup; //--We need to replace the move here with the setup move so that the stage delta can be properly extracted from it during simulation.
        var attackerSim = _unitSim.BuildSimUnit( attacker, attHPR, attackerMTRbefore, fieldSim );
        var opponentSim = _unitSim.BuildSimUnit( target, tarHPR, tarMTRbefore, fieldSim );
        var battleSimContext = _battleSim.Get_BattleSimContext( attPTKObefore.PTKO, tarPTKORbefore.PTKO, attackerSim, opponentSim, fieldSim );

        TurnOutcomeProjection top;
        bool opponentSwitches = _unitSim.PredictSwitchProbability( attPTKObefore.PTKO, tarPTKORbefore.PTKO, currentlyFaster, attHPR, tarHPR ) >= 0.8f;

        if( opponentSwitches )
            top = _battleSim.SimulatedSetupRound( battleSimContext, false, true, true, false );
        else
            top = _battleSim.SimulatedSetupRound( battleSimContext, false, false, true, false );

        best = new()
        {
            Move = bestSetup,
            Target = attacker,
            Top = top,
            StageDelta = bestStageDelta,
            BeforePTKO = bestBeforePTKO,
            AfterPTKO = bestAfterPTKO,
            SetupValue = bestValue,
            SweepCount = bestSweepCount,
            ImprovedPTKOs = bestImprovedPTKOs,
            OpponentSwitches = opponentSwitches,
        };

        return best;
    }
}
