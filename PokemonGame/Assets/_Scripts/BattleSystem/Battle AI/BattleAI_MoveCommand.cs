using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum OffensiveStatusType { None, StatusEffect, EntryHazard, StatDebuff, Binding }
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
        
        if( attackStyle == AIDecisionType.RandomMove )
        {
            if( move.MoveSO.MoveTarget == MoveTarget.Self || move.MoveSO.MoveTarget == MoveTarget.AllySide )
                targets.Add( _ai.Unit );
            else if( move.MoveSO.MoveTarget == MoveTarget.OpposingSide )
            {
                for( int t = 0; t < _ai.TheirBattleAIUnits.Count; t++ )
                {
                    var tar = _ai.GetBattleUnit( _ai.TheirBattleAIUnits[t].Pokemon );
                    targets.Add( tar );
                }
            }
            else if( move.MoveSO.MoveTarget == MoveTarget.AllAdjacent )
            {
                for( int t = 0; t < _ai.TheirBattleAIUnits.Count; t++ )
                {
                    var tar = _ai.GetBattleUnit( _ai.TheirBattleAIUnits[t].Pokemon );
                    targets.Add( tar );
                }

                var allyUnits = _ai.BattleSystem.GetAllyUnits( _ai.Unit );
                for( int i = 0; i < allyUnits.Count; i++ )
                {
                    if( allyUnits[i] != _ai.Unit )
                        targets.Add( allyUnits[i] );
                    else
                        continue;
                }
            }
            else
                targets.Add( action.Target );
        }
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
        return UnityEngine.Random.value < _ai.TrainerSkillModifier ? AIDecisionType.ChosenMove : AIDecisionType.RandomMove;
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
            int r = UnityEngine.Random.Range( 0, usableMoves.Count );
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

        _ai.CurrentLog.Add( $"===[Beginning Attack Scoring for {attackerName} ({moveName}) vs {targetName}. Tempo: {tempo.TempoState}, My PTKO Them: {myPTKO_onTarget.PTKO}, their PTKO on me: {theirPTKO_onMe.PTKO} ({eval.OpponentMoveName})]===" );

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

        if( context.IsBehind )
            score += 20;

        _ai.CurrentLog.Add( $"===[Is Behind: {context.IsBehind}. Final Attack Score: {score}]===" );

        //--Attacking Based on Switch Pressure
        score += Mathf.FloorToInt( 10f * eval.OpponentSwitchProbability );
        _ai.CurrentLog.Add( $"Opponent likey forced to switch. Score: {score}" );
        
        if( eval.ExchangeState == ExchangeState.Pressure )
        {
            score += 10;
            _ai.CurrentLog.Add( $"The pressure is on! Score: {score}" );
        }

        if( context.IsForcedTrade )
        {
            if( eval.AttackerThreatensKO )
                score += 25;
            else if( eval.AttackerPTKOR.PTKO > PotentialToKO.Safe )
                score += 15;
        }

        _ai.CurrentLog.Add( $"Forced Trade: {context.IsForcedTrade}. Score: {score}" );

        //--Flat KO flag check
        if( eval.AttackerPTKOR.PTKO == PotentialToKO.Dangerous && ( !theyThreatenKO || iAmFaster ) )
            score += 10;
        else if( eval.AttackerPTKOR.PTKO == PotentialToKO.OHKO && ( !theyThreatenKO || iAmFaster ) )
            score += 20;

        score += 10; //--default attack incentive

        return score;
    }

    public int SetupScore( TempoStateResult tempo, ExchangeEvaluation eval, BoardContext context, SetupThreatResult setup )
    {
        int score = 0;

        var attackerName = eval.AttackerName;
        var targetName = eval.OpponentName;

        var myPTKO_AfterSetup = setup.AfterPTKOR;
        var theirPTKO = eval.OpponentPTKOR;

        string moveName = "NONE";

        if( setup.Move != null )
            moveName = setup.Move.MoveSO.Name;
        else
        {
            _ai.CurrentLog.Add( $"({attackerName}) Had no viable setup move! Tanking Score!" );
            return -999;
        }

        //--These are much tighter/more risky because setting up can drastically change an outcome. defensive setup can swing ptko chances, while offensive setup can threaten KOs across entire teams, making your current hp potentially irrelevant
        if( theirPTKO.PTKO >= PotentialToKO.Dangerous && !eval.AttackerMovesFirst )
        {
            _ai.CurrentLog.Add( $"We're likely to die if we setup now! Tanking Score!" );
            return -999;
        }

        if( theirPTKO.PTKO == PotentialToKO.OHKO )
        {
            _ai.CurrentLog.Add( $"We're likely to die if we setup now! Tanking Score!" );
            return -999;
        }

        _ai.CurrentLog.Add( $"===[Beginning Setup Scoring for {attackerName} ({moveName}) vs {targetName}. Tempo: {tempo.TempoState}, My PTKO Them after setup: {myPTKO_AfterSetup.PTKO}, their PTKO on me now: {theirPTKO.PTKO}]===" );

        //--Setup Value base
        score += setup.SetupValue;
        _ai.CurrentLog.Add( $"Added setup value. Score: {score}" );

        //--Discourage setup if we can already KO AND we aren't very tanky vs our current opponent. We DO want to setup if we can take some hits, especially if we're defensively setting up or going for iron defense body press.
        if( eval.AttackerThreatensKO && theirPTKO.PTKO < PotentialToKO.TwoHKO )
        {
            if( eval.AttackerMovesFirst )
                score -= 60;
            else
                score -= 30;
        }

        //--If we are likely to KO next turn
        if( myPTKO_AfterSetup.PTKO >= PotentialToKO.Dangerous && eval.AttackerMovesFirst )
            score += 30;
        else if( myPTKO_AfterSetup.PTKO >= PotentialToKO.Risky )
            score += 20;
        else if( myPTKO_AfterSetup.PTKO <= PotentialToKO.Risky && !eval.AttackerMovesFirst )
            score -= 45;
        else if( myPTKO_AfterSetup.PTKO <= PotentialToKO.Risky )
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
        float switchProb = eval.OpponentSwitchProbability;

        float dangerWeight =
            theirPTKO.PTKO >= PotentialToKO.OHKO ? 1.25f :
            theirPTKO.PTKO >= PotentialToKO.Dangerous ? 1.0f :
            theirPTKO.PTKO >= PotentialToKO.Risky ? 0.75f :
            theirPTKO.PTKO >= PotentialToKO.TwoHKO ? 0.5f : 0.25f;

        if( eval.OpponentMovesFirst )
            dangerWeight *= 1.5f;

        float penalty = 60f * dangerWeight;

        score += Mathf.FloorToInt( switchProb * 25f );
        score -= Mathf.FloorToInt( ( 1f - switchProb ) * penalty );

        _ai.CurrentLog.Add( $"Opponent Switch Probability: {switchProb}. Score: {score}" );

        score += _ai.Setup_TempoModifier( tempo );
        _ai.CurrentLog.Add( $"Checked Tempo Modifier. Score: {score}" );

        //--Multiple setup attempt penalty.
        score -= _ai.SetupAmount * 20;

        return score;
    }

    public int OffensiveStatusScore( TempoStateResult tempo, ExchangeEvaluation eval, BoardContext context, StatusThreatResult status )
    {
        int score = 0;

        var attackerName = eval.AttackerName;
        var targetName = eval.OpponentName;

        var myPTKO_onTarget = status.AttackerPTKOR;
        var theirPTKO_onMe = eval.OpponentPTKOR;

        string moveName = "NONE";

        if( status.Move == null )
        {
            _ai.CurrentLog.Add( $"({attackerName}) Had no viable offensive status move! Tanking Score!" );
            return -999;
        }
        else
            moveName = status.Move.MoveSO.Name;

        //--Survival check
        if( eval.OpponentPTKOR.PTKO >= PotentialToKO.Dangerous )
        {
            _ai.CurrentLog.Add( $"We're likely to die if we use an offensive status move now! Tanking Score!" );
            return -999;
        }
        else if( eval.OpponentPTKOR.PTKO >= PotentialToKO.TwoHKO && !eval.AttackerMovesFirst )
        {
            _ai.CurrentLog.Add( $"We're likely to die if we use an offensive status move now! Tanking Score!" );
            return -999;
        }

        //--Base value
        // score += Mathf.FloorToInt( status.TeamCoverage * 0.3f );
        // score += Mathf.FloorToInt( status.BoardAmbiguity * 0.4f );
        // score += Mathf.FloorToInt( status.Reliability * 0.6f );
        // score += status.ImmediateImpact;

        _ai.CurrentLog.Add( $"===[Beginning Offensive Status Scoring for {attackerName} ({moveName}) vs {targetName}. Tempo: {tempo.TempoState}, My PTKO Them: {myPTKO_onTarget.PTKO}, their PTKO on me: {theirPTKO_onMe.PTKO}]===" );

        if( status.Type == OffensiveStatusType.EntryHazard )
        {
            int strategicValue = status.TeamCoverage - Mathf.FloorToInt( status.ImmediateImpact * 0.5f ) + Mathf.FloorToInt( status.BoardAmbiguity * 0.5f );

            score += strategicValue;
            score += Mathf.FloorToInt( status.Reliability * 0.4f );

            _ai.CurrentLog.Add( $"Entry Hazard detected! Team Coverage: {status.TeamCoverage}, Impact (50%): {Mathf.FloorToInt( status.ImmediateImpact * 0.5f )}, Ambiguity (50%): {status.BoardAmbiguity}. Base Score: {score}" );

            if( _ai.Round == 1 )
                score += 65;
            else if( _ai.Round <= 3 )
                score += 30;
            else if( _ai.Round < 6 )
                score -= 15;
            else if( _ai.Round > 6 )
                score -= 50;

            int remainingOpponents = _ai.GetRemainingOpposingPokemon( _ai.ThisUnitAdapter.PID ).Count;
            score += remainingOpponents * 5;

            _ai.CurrentLog.Add( $"Assessed current round ({_ai.Round}), intent (are we lead? probably if current round is < 3), and remaining opponents ({remainingOpponents}). Score: {score}" );
        }

        if( status.Type == OffensiveStatusType.StatusEffect || status.Type == OffensiveStatusType.StatDebuff )
        {
            score += status.ImmediateImpact - Mathf.FloorToInt( status.TeamCoverage * 0.5f ) + Mathf.FloorToInt( status.Reliability * 0.5f );

            _ai.CurrentLog.Add( $"Status Effect or Statu Debuff detected! Impact: {status.ImmediateImpact}, Coverage (50%) {Mathf.FloorToInt( status.TeamCoverage * 0.5f )}, Reliability (50%): {Mathf.FloorToInt( status.Reliability * 0.5f )}. Base Score: {score}" );

            //--Disruption bonus
            if( !status.Top.OpponentCanAct )
                score += 60;

            _ai.CurrentLog.Add( $"Opponent Can Act: {status.Top.OpponentCanAct}. Score: {score}" );

            int attackEquivalent = eval.AttackerPTKOR.Score - eval.OpponentPTKOR.Score;
            if( status.ImmediateImpact < attackEquivalent )
                score -= 40;

            _ai.CurrentLog.Add( $"Does attack equivalent ({attackEquivalent}) outweigh immediate impact ({status.ImmediateImpact})?. Score: {score}" );

            //--Bonus for punishing a switch with a status effect move like sleep powder or thunder wave
            score += Mathf.FloorToInt( 25f * eval.OpponentSwitchProbability );
        }

        if( eval.ExchangeState == ExchangeState.Pressure )
            score += 15;

        float switchProb = eval.OpponentSwitchProbability;

        float dangerWeight =
            theirPTKO_onMe.PTKO >= PotentialToKO.OHKO ? 1.25f :
            theirPTKO_onMe.PTKO >= PotentialToKO.Dangerous ? 1.0f :
            theirPTKO_onMe.PTKO >= PotentialToKO.Risky ? 0.75f :
            theirPTKO_onMe.PTKO >= PotentialToKO.TwoHKO ? 0.5f : 0.25f;

        if( eval.OpponentMovesFirst )
            dangerWeight *= 1.5f;

        float penalty = 50f * dangerWeight;

        score += Mathf.FloorToInt( switchProb * 75f );
        score -= Mathf.FloorToInt( ( 1f - switchProb ) * penalty );

        _ai.CurrentLog.Add( $"Opponent Switch Probability: {switchProb}. Score: {score}" );

        //--Don’t overuse if attack is better
        if( eval.AttackerThreatensKO )
        {
            if( eval.AttackerMovesFirst )
                score -= 80;
            else
                score -= 40;
        }

        _ai.CurrentLog.Add( $"Checked if attacking may be better. Attacker Threatens KO: {eval.AttackerThreatensKO}. Attacker Moves First: {eval.AttackerMovesFirst} Score: {score}" );

        //--Status for survival incentive
        if( eval.OpponentThreatensKO )
        {
            if( eval.AttackerMovesFirst && status.Type == OffensiveStatusType.StatusEffect )
                score += 25;
            else if( status.Top.OpponentCanAct )
                score -= 150;
        }
        else if( eval.OpponentPTKOR.PTKO >= PotentialToKO.Risky )
            score -= 75;

        _ai.CurrentLog.Add( $"Checked Survival. Opponent Threatens KO: {eval.OpponentThreatensKO}. Opponent acts in simulation: {status.Top.OpponentCanAct} Score: {score}" );

        //--HP context
        float hp = eval.AttackerHPR;

        if( hp <= 0.25f )
            score -= 30;
        else if( hp <= 0.45f && eval.OpponentThreatensKO )
            score -= 20;
        else if( hp >= 0.7f && !eval.OpponentThreatensKO )
            score += 10;

        _ai.CurrentLog.Add( $"HP: {hp}. Score: {score}" );

        //--Tempo
        score += _ai.Setup_TempoModifier( tempo );

        _ai.CurrentLog.Add( $"Checked Tempo. Score: {score}" );

        return score;
    }

    public MoveThreatResult GetMove_BestAttack( IBattleAIUnit attacker, IBattleAIUnit target, bool actionSelect = false, string source = "NO SOURCE", int depth = 0 )
    {
        // CustomLogSession moveLog = new();
        int bestScore = int.MinValue;
        float bestModifier = 1f;
        Move bestMove = null;
        TurnOutcomeProjection bestTop = new();

        //--Create Target's PTKO on attacker & target's sim unit once for use in each attacker's move's simulation
        
        var fieldSim = _ai.UnitSim.BuildSimField();

        // moveLog.Add( $"===[Beginning Scoring for {attacker.Name}'s Best Simulated Attack vs {target.Name}, called from {source}]===" );
        if( attacker.Pokemon == target.Pokemon )
            Debug.LogError( $"===[Beginning Scoring for {attacker.Name}'s Best Simulated Attack vs {target.Name}, called from {source}]===" );

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
            var attackerUnit = _ai.GetBattleUnit( attacker.Pokemon );
            if( attackerUnit != null )
            {
                if( attackerUnit.Flags[UnitFlags.ChoiceItem].IsActive && attackerUnit.LastUsedMove != null && attackerUnit.LastUsedMove != move )
                    continue;
            }

            //--Move type effectiveness
            float effectiveness = _ai.UnitSim.Get_MoveEffectiveness( target, move );

            //--If there a type immunity, skip this move
            if( effectiveness == 0f )
                continue;

            float attHPR                    = _ai.Get_HPRatio( attacker );
            float tarHPR                    = _ai.Get_HPRatio( target );
            var tarMTR                      = depth == 0 ? GetMove_BestAttack( target, attacker, false, "Opponent's best attack (recursion)", depth + 1 ) : _ai.Get_MostThreateningMove( target, attacker ); //--Remember, the order here is attacking unit vs target unit. this is the target's attack on the attacker here.
            var tarEDR                      = _proj.Get_EstimatedDamageResult( target, attacker, tarMTR );
            PotentialToKOResult tarPTKOR    = _proj.Get_PotentialToKOResult( tarEDR, tarMTR, attHPR );

            // moveLog.Add( $"[Best Simulated Move] Getting PTKO for {attacker.Name}'s {move.MoveSO.Name} on {target.Name} (HPR: {tarHPR}" );
            float modifier                  = effectiveness * _ai.UnitSim.Get_MoveModifier( attacker, target, move );
            MoveThreatResult mtr            = new(){ Score = 0, Modifier = modifier, Move = move };
            var attEDR                      = _proj.Get_EstimatedDamageResult( attacker, target, mtr );
            PotentialToKOResult attPTKOR    = _proj.Get_PotentialToKOResult( attEDR, mtr, tarHPR );

            // moveLog.Add( $"[Best Simulated Move] PTKO for {attacker.Name}'s {move.MoveSO.Name} on {target.Name} (HPR: {tarHPR} is: {attPTKOR.PTKO} (Damage Estimate: {attEDR.DamageEstimate})" );

            var targetSimUnit               = _ai.UnitSim.BuildSimUnit( target, tarHPR, tarMTR, fieldSim );
            var attackerSimUnit             = _ai.UnitSim.BuildSimUnit( attacker, attHPR, mtr, fieldSim );
            var battleSimContext            = _battleSim.Get_BattleSimContext( attPTKOR.PTKO, tarPTKOR.PTKO, attackerSimUnit, targetSimUnit, fieldSim );
            
            var top                         = _battleSim.SimulateAttackRound( battleSimContext, $"Get Best Simulated Move ({attacker.Name}, {move.MoveSO.Name})" );

            //--Begin Scoring
            int score = 0;
            if( top.Attacker_DiesBeforeActing )
                score -= 150;

            if( top.Opponent_DiesBeforeActing )
                score += 150;

            int myAliveCount = _ai.GetRemainingAllyPokemon( attacker.PID ).Count;
            int oppAliveCount = _ai.GetRemainingOpposingPokemon( attacker.PID ).Count;

            bool isBehind = myAliveCount < oppAliveCount;

            if( top.MutualKO )
                score += isBehind ? 40 : -40;

            bool opponentThreatensKO = tarPTKOR.PTKO >= PotentialToKO.Risky;
            if( opponentThreatensKO && move.MoveSO.MovePriority > MovePriority.Zero && top.Opponent_DiesBeforeActing )
                score += 25;

            score += Mathf.FloorToInt( ( 1f - top.Opponent_EndOfTurnHP ) * 90f );
            score -= Mathf.FloorToInt( ( 1f - top.Attacker_EndOfTurnHP ) * 80f );

            if( effectiveness >= 2f )
                score += 5;

            if( effectiveness <= 0.75f )
                score -= 5;

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

            int movePowerBonus = Mathf.FloorToInt( movePower * 0.05f );
            int damageBonus = Mathf.FloorToInt( attEDR.DamageEstimate * 5f );
            score += movePowerBonus;
            score += damageBonus;

            // moveLog.Add( $"[Best Simulated Move][{attacker.Name}'s {move.MoveSO.Name}] Move Power: {movePower}. Move Power Bonus: {movePowerBonus}. Damage Bonus: {damageBonus}  Score: {score}." );

            int accuracy = move.MoveSO.Accuracy;
            if( accuracy < 70 )                         score -= 35;
            else if( accuracy < 80 )                    score -= 20;
            else if( accuracy < 90 )                    score -= 10;
            else if( accuracy < 100 )                   score -= 5;

            // moveLog.Add( $"[Best Simulated Move] Final Score for {attacker.Name}'s {move.MoveSO.Name} on {target.Name} (HPR: {tarHPR}. Score: {score}." );

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

            float attHPR                    = _ai.Get_HPRatio( attacker );
            float tarHPR                    = _ai.Get_HPRatio( target );
            var tarMTR                      = depth == 0 ? GetMove_BestAttack( target, attacker, false, source, depth + 1 ) : _ai.Get_MostThreateningMove( target, attacker ); //--Remember, the order here is attacking unit vs target unit. this is the target's attack on the attacker here.
            var tarEDR                      = _proj.Get_EstimatedDamageResult( target, attacker, tarMTR );
            PotentialToKOResult tarPTKOR    = _proj.Get_PotentialToKOResult( tarEDR, tarMTR, attHPR );

            //--Move type effectiveness
            float effectiveness             = _ai.UnitSim.Get_MoveEffectiveness( target, fallbackMove );
            float modifier                  = effectiveness * _ai.UnitSim.Get_MoveModifier( attacker, target, fallbackMove );
            MoveThreatResult mtr            = new(){ Score = 0, Modifier = modifier, Move = fallbackMove };
            var attWSR                      = _proj.Get_EstimatedDamageResult( attacker, target, mtr );
            PotentialToKOResult attPTKOR    = _proj.Get_PotentialToKOResult( attWSR, mtr, tarHPR );

            var targetSimUnit               = _ai.UnitSim.BuildSimUnit( target, tarHPR, tarMTR, fieldSim );
            var attackerSimUnit         = _ai.UnitSim.BuildSimUnit( attacker, attHPR, mtr, fieldSim );
            var battleSimContext        = _battleSim.Get_BattleSimContext( attPTKOR.PTKO, tarPTKOR.PTKO, attackerSimUnit, targetSimUnit, fieldSim );
            
            var top                     = _battleSim.SimulateAttackRound( battleSimContext, $"Get Best Simulated Move ({attacker.Name}, {fallbackMove.MoveSO.Name})" );

            bestScore       = 0;
            bestModifier    = modifier;
            bestMove        = fallbackMove;
            bestTop         = top;
        }

        // moveLog.Add( $"[Best Simulated Move] Final Chosen move & Score for {attacker.Name}'s {bestMove.MoveSO.Name} on {target.Name} Score: {bestScore}." );
        // Debug.Log( $"[Best Simulated Move] Final Chosen move & Score for {attacker.Name}'s {bestMove.MoveSO.Name} on {target.Name} Score: {bestScore}." );

        // Debug.Log( moveLog.ToString() );
        // moveLog.Clear();

        MoveThreatResult finalMtr = new()
        {
            Score = bestScore,
            Modifier = bestModifier,
            Target = target,
            Move = bestMove,
            Top = bestTop,
        };

        if( actionSelect )
        {
            finalMtr.TargetBattleUnit = _ai.GetBattleUnit( target.Pokemon );
        }

        return finalMtr;
    }

    public SetupThreatResult GetMove_Setup( IBattleAIUnit attacker, IBattleAIUnit target, bool actionSelect = false )
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
        var oppTeam = _ai.GetAllyTeamAs_Adapter( target.Pokemon );

        //--Hp Ratios
        float attHPR                            = _ai.Get_HPRatio( attacker );
        float tarHPR                            = _ai.Get_HPRatio( target );

        //--Get the best attack before using a boosting move and its PTKO.
        var attackerMTRbefore                   = GetMove_BestAttack( attacker, target, false, "Best Simulated Setup (Attacker MTR Before)" );
        var attEDRbefore                        = _proj.Get_EstimatedDamageResult( attacker, target, attackerMTRbefore );
        PotentialToKOResult attPTKObefore       = _proj.Get_PotentialToKOResult( attEDRbefore, attackerMTRbefore, tarHPR );

        //--Create Target's PTKO on attacker
        var tarMTRbefore                        = GetMove_BestAttack( target, attacker, false, "Best Simulated Setup (Target MTR Before)", 1 ); //--Remember, the order here is attacking unit vs target unit. this is the target's attack on the attacker here.
        var tarEDRbefore                        = _proj.Get_EstimatedDamageResult( target, attacker, tarMTRbefore );
        PotentialToKOResult tarPTKORbefore      = _proj.Get_PotentialToKOResult( tarEDRbefore, tarMTRbefore, attHPR );
        
        //--Create Sim field
        var fieldSim                            = _ai.UnitSim.BuildSimField();

        bool currentlyFaster = attacker.Speed > target.Speed;

        foreach( var move in setupMoves )
        {
            var stageDelta = _unitSim.BuildStatStageDelta( move );

            //--We need to build this guy to get a new attack for him first, and then we can rebuild him with that improved attack. it's a little goofy, i will try to improve the flow of this later... --03/09/26
            var attackerSetupSim = _unitSim.BuildSimUnit_WithStageDelta( attacker, attHPR, attackerMTRbefore, fieldSim, stageDelta );

            //--Get the best attacks after the attacker uses the current setup move.
            var attackerMTRafter   = GetMove_BestAttack( attackerSetupSim, target, false, "Best Simulated Setup (after)" );
            var targetMTRafter     = GetMove_BestAttack( target, attackerSetupSim, false, "Best Simulated Setup (after)" );

            //--Post Setup Walling Scores
            var attEDRafter = _proj.Get_EstimatedDamageResult( attackerSetupSim, target, attackerMTRafter );
            var tarEDRafter = _proj.Get_EstimatedDamageResult( target, attackerSetupSim, targetMTRafter );

            //--Post Setup PTKOs
            PotentialToKOResult attPTKOafter    = _proj.Get_PotentialToKOResult( attEDRafter, attackerMTRafter, tarHPR );
            PotentialToKOResult tarPTKORafter   = _proj.Get_PotentialToKOResult( tarEDRafter, targetMTRafter, attHPR );

            int offensiveValue = _unitSim.ComputeOffensiveSetupValue( attPTKObefore, attPTKOafter, stageDelta );
            int defensiveValue = _unitSim.ComputeDefensiveSetupValue( tarPTKORafter, tarPTKORbefore, stageDelta ); //--we do after -> before here because we need this to be good for the attacker, not for the defender.

            //--Opposing Team Sweep Comparison
            int sweepValue = 0;
            int sweepCount = 0;
            foreach( var oppAdapter in oppTeam )
            {
                float oppHRP = _ai.Get_HPRatio( oppAdapter );
                var bestVSopp = GetMove_BestAttack( attackerSetupSim, oppAdapter, false, "Best Simulated Setup (best vs target)" );
                var vsOppWSR = _proj.Get_EstimatedDamageResult( attackerSetupSim, oppAdapter, bestVSopp );
                PotentialToKOResult PTKOvsOpp = _proj.Get_PotentialToKOResult( vsOppWSR, bestVSopp, oppHRP );

                bool faster = attackerSetupSim.Speed > oppAdapter.Speed;

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
            foreach( var oppAdapter in oppTeam )
            {
                //--Opp PTKO us Before Setup
                var vsUsMTRbefore = GetMove_BestAttack( oppAdapter, attacker, false, "Best Simulated Setup (Opponent PTKO us before)" );
                var vsUsWSRbefore = _proj.Get_EstimatedDamageResult( oppAdapter, attacker, vsUsMTRbefore );
                PotentialToKOResult OppPTKObefore = _proj.Get_PotentialToKOResult( vsUsWSRbefore, vsUsMTRbefore, attHPR );

                //--Opp PTKO us After Setup
                var vsUsMTRafter = GetMove_BestAttack( oppAdapter, attackerSetupSim, false, "Best Simulated Setup (Opponent PTKO us after)" );
                var vsUsWSRafter = _proj.Get_EstimatedDamageResult( oppAdapter, attackerSetupSim, vsUsMTRafter );
                PotentialToKOResult OppPTKOafter = _proj.Get_PotentialToKOResult( vsUsWSRafter, vsUsMTRafter, attHPR );

                bool faster = attackerSetupSim.Speed > oppAdapter.Speed;

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
        float opponentSwitchProb = _unitSim.PredictSwitchProbability( attPTKObefore.PTKO, tarPTKORbefore.PTKO, currentlyFaster, attHPR, tarHPR, target.Expendability );
        bool opponentSwitches =  UnityEngine.Random.value <= opponentSwitchProb;

        if( opponentSwitches )
            top = _battleSim.SimulatedSetupRound( battleSimContext, false, true, true, false ); //--attacker is switch, opponent is switch, attacker is setup, opponent setup
        else
            top = _battleSim.SimulatedSetupRound( battleSimContext, false, false, true, false ); //--attacker is switch, opponent is switch, attacker is setup, opponent setup

        best = new()
        {
            Move = bestSetup,
            Target = attacker,
            Top = top,
            StageDelta = bestStageDelta,
            BeforePTKOR = bestBeforePTKO,
            AfterPTKOR = bestAfterPTKO,
            SetupValue = bestValue,
            SweepCount = bestSweepCount,
            ImprovedPTKOs = bestImprovedPTKOs,
            OpponentSwitches = opponentSwitches,
        };

        if( actionSelect )
            best.TargetBattleUnit = _ai.GetBattleUnit( target.Pokemon );

        return best;
    }

    
    private struct StatusValue
    {
        public int CandidateScore;
        public int Coverage;
        public int Ambiguity;
        public int Reliability;
        public int Impact;
        public int TotalValue;
    }
    public StatusThreatResult GetMove_OffensiveStatus( IBattleAIUnit attacker, IBattleAIUnit target, bool actionSelect = false )
    {
        var offensiveStatusMoves = _ai.UnitSim.GetOffensiveStatusMoves( attacker.ActiveMoves );

        if( _unitSim.CheckHasMove( attacker, "Curse" ) && _unitSim.CheckCurseIsVolatile( attacker ) )
        {
            Move curse = _unitSim.GetCurseFromActiveMoves( attacker.ActiveMoves );
            if( curse != null )
                offensiveStatusMoves.Add( curse );
        }

        if( offensiveStatusMoves?.Count <= 0 )
            return default;

        int bestScore = int.MinValue;
        StatusValue bestValue = default;
        OffensiveStatusType bestType = OffensiveStatusType.None;

        Move bestMove = null;
        // CustomLogSession log = new();

        //--Pre Status Use Simulation for comparison. "Before".
        //--HP Ratios
        var attackerHPR_Before = _ai.Get_HPRatio( attacker );
        var targetHPR_Before = _ai.Get_HPRatio( target );

        //--Move Threat Result
        var attackerMTR_Before = GetMove_BestAttack( attacker, target );
        var targetMTR_Before = GetMove_BestAttack( target, attacker );

        //--Estimated Damage Results
        var attackerEDR_Before = _proj.Get_EstimatedDamageResult( attacker, target, attackerMTR_Before );
        var targetEDR_Before = _proj.Get_EstimatedDamageResult( target, attacker, attackerMTR_Before );

        //--Potential to KO Results
        var attackerPTKOR_Before = _proj.Get_PotentialToKOResult( attackerEDR_Before, attackerMTR_Before, targetHPR_Before );
        var targetPTKOR_Before = _proj.Get_PotentialToKOResult( targetEDR_Before, targetMTR_Before, attackerHPR_Before );

        //--Attack Round Simulation
        var field_Before = _unitSim.BuildSimField();
        var attackerSim = _unitSim.BuildSimUnit( attacker, attackerHPR_Before, attackerMTR_Before, field_Before );
        var targetSim = _unitSim.BuildSimUnit( target, targetHPR_Before, targetMTR_Before, field_Before );
        var context = _battleSim.Get_BattleSimContext( attackerPTKOR_Before.PTKO, targetPTKOR_Before.PTKO, attackerSim, targetSim, field_Before );

        // log.Add( $"===[[Get Move Offensive Status] Getting Offensive Status Move for {attacker.Name} vs {target.Name}]===" );

        foreach( var move in offensiveStatusMoves )
        {
            StatusValue statusValue = default;
            //--We compare PTKO values for before and after during scoring, along with some other context
            //--to decide whether a status move is worth considering in the current moment.
            //--we then simulate using it after finalizing our move choice, where we then score the results of that during the decision line.
            OffensiveStatusType type;
            bool isCurse    = move.MoveSO.Name == "Curse";
            bool severe     = move.MoveEffects.SevereStatus     != SevereConditionID.None;
            bool vol        = move.MoveEffects.VolatileStatus   != VolatileConditionID.None || isCurse;
            bool trans      = move.MoveEffects.TransientStatus  != TransientConditionID.None;
            // bool bind       = move.MoveEffects.BindingStatus    != BindingConditionID.None; //--Consider having binding moves be part of this decision line later

            bool statusEffect   =  severe || vol  || trans;
            bool hazard         = move.MoveEffects.CourtCondition   != CourtConditionID.None;
            bool debuff         = move.MoveEffects.StatChangeList?.Count > 0 && ( move.MoveSO.MoveEffects.Target == EffectTarget.Enemy || move.MoveSO.MoveEffects.Target == EffectTarget.OpposingSide );

            // log.Add( $"=[Evaluating {move.MoveSO.Name}. Severe: {severe}, Volatile: {vol}, Transient: {trans}, Hazard: {hazard}, Debuff: {debuff}]=" );

            if( statusEffect )
            {
                if( target.SevereStatus != SevereConditionID.None )
                    continue;

                if( target.VolatileStatuses.Contains( move.MoveSO.MoveEffects.VolatileStatus ) || isCurse && target.VolatileStatuses.Contains( VolatileConditionID.Cursed ) )
                    continue;

                type = OffensiveStatusType.StatusEffect;
                // log.Add( $"[{move.MoveSO.Name}] Move is a {type}!" );
            }
            else if( hazard )
            {
                Dictionary<CourtConditionID, int> courtConditions = new();
                if( target.CourtLocation == CourtLocation.TopCourt )
                    courtConditions = field_Before.TopCourtConditions;
                else if( target.CourtLocation == CourtLocation.BottomCourt )
                    courtConditions = field_Before.BottomCourtConditions;

                if( courtConditions.ContainsKey( move.MoveSO.MoveEffects.CourtCondition ) && move.MoveSO.MoveEffects.CourtCondition != CourtConditionID.Spikes && move.MoveSO.MoveEffects.CourtCondition != CourtConditionID.ToxicSpikes )
                    continue;

                type = OffensiveStatusType.EntryHazard;
                // log.Add( $"[{move.MoveSO.Name}] Move is an {type}!" );
            }
            else if( debuff )
            {
                type = OffensiveStatusType.StatDebuff;
                // log.Add( $"[{move.MoveSO.Name}] Move is a {type}!" );
            }
            else
                continue;

            switch( type )
            {
                case OffensiveStatusType.StatusEffect:
                    //--Simulate status application and score results based on before/after minor lookahead
                    statusValue = ScoreOffensiveStatusEffectMove( attackerPTKOR_Before, targetPTKOR_Before, attackerSim, targetSim, move, context/*, log*/ );
                    break;

                case OffensiveStatusType.EntryHazard:
                    statusValue = ScoreOffensiveEntryHazardMove( attackerPTKOR_Before, targetPTKOR_Before, attackerSim, targetSim, move, context/*, log*/ );
                    break;

                case OffensiveStatusType.StatDebuff:
                    statusValue = ScoreStatDebuffMove( attackerPTKOR_Before, targetPTKOR_Before, attackerSim, targetSim, move, context/*, log*/ );
                    break;
            }

            if( statusValue.CandidateScore > bestScore )
            {
                bestScore = statusValue.CandidateScore;
                bestMove = move;
                bestValue = statusValue;
                bestType = type;
            }
        }

        //--Run Offensive Status Use Simulation Here, after picking the move itself.
        if( bestMove == null )
            return default;
        else
        {
            MoveThreatResult statusMove = new(){ Move = bestMove };
            attackerSim.MTR = statusMove;
        }

        float opponentSwitchProb = _unitSim.PredictSwitchProbability( attackerPTKOR_Before.PTKO, targetPTKOR_Before.PTKO, context.AttackerMovesFirst, attackerHPR_Before, targetHPR_Before, target.Expendability );
        bool opponentSwitches = UnityEngine.Random.value <= opponentSwitchProb;

        TurnOutcomeProjection top;
        if( opponentSwitches )
            top = _battleSim.SimulateOffensiveStatusRound( context, true, false, false, true ); //--attacker status, opponent status, attacker switch, opponent switch
        else
            top = _battleSim.SimulateOffensiveStatusRound( context, true, false, false, false ); //--attacker status, opponent status, attacker switch, opponent switch

        // log.Add( top.SimulationLog );
        // Debug.Log( log.ToString() );
        // log.Clear();

        StatusThreatResult best = new()
        {
            Type = bestType,
            Score = bestScore,
            StatusValue = bestValue.TotalValue,
            TeamCoverage = bestValue.Coverage,
            BoardAmbiguity = bestValue.Ambiguity,
            Reliability = bestValue.Reliability,
            ImmediateImpact = bestValue.Impact,

            Move = bestMove,
            Target = target,
            Top = top,

            AttackerPTKOR = attackerPTKOR_Before,
            OpponentPTKOR = targetPTKOR_Before,
            OpponentSwitches = opponentSwitches,
        };

        if( actionSelect )
            best.TargetBattleUnit = _ai.GetBattleUnit( target.Pokemon );

        return best;
    }

    private StatusValue ScoreOffensiveStatusEffectMove( PotentialToKOResult attackerPTKOR_Before, PotentialToKOResult targetPTKOR_Before, IBattleAIUnit attackerSim, IBattleAIUnit targetSim, Move move, BattleSimContext context/*, CustomLogSession log*/ )
    {
        int uniqueScore = 0;
        int coverage = 0;
        int ambiguity = 0;
        int reliability = 0;
        int impact = 0;

        var moveEffects = move.MoveSO.MoveEffects;

        // log.Add( $"[{move.MoveSO.Name}] Beginning Sub Scoring Module for Offensive Status Effect Move..." );

        //--Team Coverage-----------------------------------------
        var oppTeam = _ai.GetRemainingOpposingPokemon( attackerSim.PID );
        var ourTeam = _ai.GetRemainingAllyPokemon( attackerSim.PID );
        var teamAnal = _proj.Get_TeamVSTeamAnalysis( ourTeam, oppTeam );

        int affectedCount = 0;
        int resistCount = 0;
        int statusWeight = 0;

        for( int i = 0; i < oppTeam.Count; i++ )
        {
            var mon = oppTeam[i];
            bool affected = true;
            int weight = 0;
            BattleAI_PokemonAdapter adapter = _ai.GetPokemonAs_Adapter( mon );
            var monMTR = GetMove_BestAttack( adapter, attackerSim );
            float powerScale = monMTR.Move.MovePower / 85f;

            switch( moveEffects.SevereStatus )
            {
                case SevereConditionID.BRN:
                    if( mon.CheckTypes( PokemonType.Fire ) )
                        affected = false;

                    if( monMTR.Move.MoveSO.MoveCategory == MoveCategory.Physical )
                        weight += Mathf.RoundToInt( powerScale * 15f );

                    if( affected )
                        weight += 5;

                    break;

                case SevereConditionID.FBT:
                    if( mon.CheckTypes( PokemonType.Ice ) )
                        affected = false;

                    if( monMTR.Move.MoveSO.MoveCategory == MoveCategory.Special )
                        weight += Mathf.RoundToInt( powerScale * 15f );

                    if( affected )
                        weight += 5;

                    break;

                case SevereConditionID.PSN:
                case SevereConditionID.TOX:
                    if( mon.CheckTypes( PokemonType.Steel ) )
                        affected = false;

                    if( affected )
                        weight += 10;

                    break;

                case SevereConditionID.SLP:
                    if( move.MoveSO.Flags.Contains( MoveFlags.Powder ) && mon.CheckTypes( PokemonType.Grass ) )
                        affected = false;

                    if( affected && monMTR.Top.AttackerPTKO >= PotentialToKO.Risky && !monMTR.Top.AttackerMovedFirst )
                        weight += 25;

                    break;

                case SevereConditionID.PAR:
                    if( mon.CheckTypes( PokemonType.Electric ) )
                        affected = false;

                    if( mon.CheckTypes( PokemonType.Ground ) && move.MoveSO.Name == "Thunder Wave" )
                        affected = false;

                    if( affected && monMTR.Top.AttackerPTKO >= PotentialToKO.Risky && monMTR.Top.AttackerMovedFirst  )
                        weight += 20;

                    break;
            }

            if( move.MoveSO.Name == "Curse" )
            {
                if( attackerSim.CurrentHPR > 0.5f )
                    weight += 15;
                else
                    weight -= 5;
            }

            statusWeight += weight;

            if( affected )
                affectedCount++;
            else
                resistCount++;
        }

        float averageWeight = statusWeight / oppTeam.Count;
        float applicationRatio = affectedCount / oppTeam.Count;
        float switchRatio = teamAnal.Their_LikelySwitches / (float)oppTeam.Count;

        coverage = Mathf.RoundToInt( averageWeight + ( applicationRatio * 20f ) + ( switchRatio * 10f ) - ( resistCount * 2f ) );
        // log.Add( $"[{move.MoveSO.Name}] Status Weight: {statusWeight}, Their Likely Switches {teamAnal.Their_LikelySwitches}, Affected Count: {affectedCount}, Resist Count: {resistCount}. Coverage Score: {coverage}" );

        //--Board Ambiguity---------------------------------------
        float switchAmbiguity = teamAnal.Their_LikelySwitches / (float)oppTeam.Count;
        float threatAmbiguity = teamAnal.Their_ThreatCount / (float)( oppTeam.Count * ourTeam.Count );
        float ptkoSpread = (int)teamAnal.Their_BestPTKO - (int)teamAnal.Their_AveragePTKO;
        float ptkoSpreadAmbiguity = Mathf.Clamp01( 1f - ptkoSpread / 7 );

        //--Final Board Ambiguity Score
        ambiguity = Mathf.FloorToInt( ( switchAmbiguity * 20f ) + ( threatAmbiguity * 10f ) + ( ptkoSpreadAmbiguity * 10f ) );
        // log.Add( $"[{move.MoveSO.Name}] Our Remaining Pokemon: {ourTeam.Count}. Their Likely Switches: {teamAnal.Their_LikelySwitches}, Their Threat Count: {teamAnal.Their_ThreatCount}, Their Best PTKO: {teamAnal.Their_BestPTKO}, Their Average PTKO: {teamAnal.Their_AveragePTKO}" );
        // log.Add( $"[{move.MoveSO.Name}] Switch Ambiguity: {switchAmbiguity}, Threat Ambiguity: {threatAmbiguity}, PTKO Ambiguity: {ptkoSpreadAmbiguity}. Board Ambiguity Value: {ambiguity}" );

        //--Reliability-------------------------------------------
        int acc = move.MoveSO.Accuracy;
        int accuracyScore = 0;
        if( acc < 80 ) accuracyScore -= 10;
        else if( acc < 90 ) accuracyScore -= 5;

        // log.Add( $"[{move.MoveSO.Name}] Accuracy: {acc}. Score: {accuracyScore}" );
        int applicationScore = Mathf.RoundToInt( applicationRatio * 15 );
        // log.Add( $"[{move.MoveSO.Name}] Affected Mons: {affectedCount}/{oppTeam.Count}. Application Score: {applicationScore}" );

        //--Prediction Reliability
        int predictedSwitches = teamAnal.Their_LikelySwitches;
        int predictionScore = Mathf.RoundToInt( switchRatio * 5f );
        // log.Add( $"[{move.MoveSO.Name}] Predicted Switch Pressure: {predictedSwitches}. Prediction Score: {predictionScore}" );

        //--Final Reliability Score
        reliability = accuracyScore + applicationScore + predictionScore;
        // log.Add( $"[{move.MoveSO.Name}] Reliability Value: {reliability}" );

        //--Immediate Impact--------------------------------------
        var targetSim_Statused = _unitSim.BuildSimUnit_WithStatus( targetSim, targetSim.CurrentHPR, targetSim.MTR, context.Field );

        //--MTRs
        var attackerMTR_After = GetMove_BestAttack( attackerSim, targetSim_Statused );
        var targetMTR_After = GetMove_BestAttack( targetSim_Statused, attackerSim );

        //--EDRs
        var attackerEDR_After = _proj.Get_EstimatedDamageResult( attackerSim, targetSim_Statused, attackerSim.MTR );
        var targetEDR_After = _proj.Get_EstimatedDamageResult( targetSim_Statused, attackerSim, targetSim_Statused.MTR );

        //--PTKOs
        var attackerPTKOR_After = _proj.Get_PotentialToKOResult( attackerEDR_After, attackerMTR_After, targetSim_Statused.CurrentHPR );
        var targetPTKOR_After = _proj.Get_PotentialToKOResult( targetEDR_After, targetMTR_After, attackerSim.CurrentHPR );

        //--Score both sets of PTKOs to get overall value - value in a vacuum, and value in using this turn
        impact += ( attackerPTKOR_After.Score - attackerPTKOR_Before.Score ) * 2;
        // log.Add( $"[{move.MoveSO.Name}] Attacker PTKO Score After - Before: {attackerPTKOR_After.Score} - {attackerPTKOR_Before.Score} = {attackerPTKOR_After.Score - attackerPTKOR_Before.Score}. Score: {impact}" );

        impact += ( targetPTKOR_Before.Score - targetPTKOR_After.Score ) * 2;
        // log.Add( $"[{move.MoveSO.Name}] Target PTKO Score Before - After: {targetPTKOR_Before.Score} - {targetPTKOR_After.Score} = {targetPTKOR_Before.Score - targetPTKOR_After.Score}. Score: {impact}" );

        bool deniesTurn = ( targetSim_Statused.SevereStatus == SevereConditionID.PAR || targetSim_Statused.SevereStatus == SevereConditionID.SLP ) && targetSim_Statused.SevereStatusTime > 0;
        bool burnMatters = targetSim_Statused.SevereStatus == SevereConditionID.BRN && targetSim_Statused.MTR.Move.MoveSO.MoveCategory == MoveCategory.Physical;
        bool frostMatters = targetSim_Statused.SevereStatus == SevereConditionID.FBT && targetSim_Statused.MTR.Move.MoveSO.MoveCategory == MoveCategory.Special;
        bool reducesDamage = burnMatters || frostMatters;

        // log.Add( $"[{move.MoveSO.Name}] Denies Turn: {deniesTurn}, Burn Matters: {burnMatters}, Frostbite Matters: {frostMatters}, Reduces Damage: {reducesDamage}." );

        if( deniesTurn )
            impact += 70;

        // log.Add( $"[{move.MoveSO.Name}] Denies Turn: {deniesTurn}. Score: {impact}" );

        if( reducesDamage )
            impact += 40;

        // log.Add( $"[{move.MoveSO.Name}] Reduces Damage: {reducesDamage}. Score: {impact}" );

        int finalValue = coverage + ambiguity + reliability + impact;
        int candidateScore = coverage + ambiguity + reliability + impact + uniqueScore;
        // log.Add( $"[{move.MoveSO.Name}] Coverage: {coverage}, Ambiguity: {ambiguity}, Reliability: {reliability}, Impact: {impact}, Unique Score: {uniqueScore}. Final Value: {finalValue}" );

        return new()
        {
            CandidateScore = candidateScore,
            Coverage = coverage,
            Ambiguity = ambiguity,
            Reliability = reliability,
            Impact = impact,
            TotalValue = finalValue,
        };
    }

    private StatusValue ScoreOffensiveEntryHazardMove( PotentialToKOResult attackerPTKOR_Before, PotentialToKOResult targetPTKOR_Before, IBattleAIUnit attackerSim, IBattleAIUnit targetSim, Move move, BattleSimContext context/*, CustomLogSession log*/ )
    {
        int uniqueScore = 0;
        int coverage = 0;
        int ambiguity = 0;
        int reliability = 0;
        int impact = 0;

        var moveEffects = move.MoveSO.MoveEffects;

        // log.Add( $"[{move.MoveSO.Name}] Beginning Sub Scoring Module for Offensive Entry Hazard Move..." );

        //--Team Coverage----------------------
        //--Remaining Opposing Team
        var oppTeam = _ai.GetRemainingOpposingPokemon( attackerSim.PID );
        int remaining = oppTeam.Count;

        //--Opposing Team HP
        float totalTeamHPR = 0;
        for( int i = 0; i < oppTeam.Count; i++ )
            totalTeamHPR += _ai.Get_HPRatio( oppTeam[i] );
        
        //--Final Coverage Score
        coverage = Mathf.FloorToInt( ( remaining * 5f ) + ( totalTeamHPR * 8f ) );
        // log.Add( $"[{move.MoveSO.Name}] Opponent's Remaining Pokemon: {remaining}. Total Team HPR: {totalTeamHPR}. Coverage Value: {coverage}" );

        //--Board Ambiguity--------------------
        var ourTeam = _ai.GetRemainingAllyPokemon( attackerSim.PID );
        var teamAnal = _proj.Get_TeamVSTeamAnalysis( ourTeam, oppTeam );

        float switchAmbiguity = teamAnal.Their_LikelySwitches / (float)oppTeam.Count;
        float threatAmbiguity = teamAnal.Their_ThreatCount / (float)( oppTeam.Count * ourTeam.Count );
        float ptkoSpread = (int)teamAnal.Their_BestPTKO - (int)teamAnal.Their_AveragePTKO;
        float ptkoSpreadAmbiguity = Mathf.Clamp01( 1f - ptkoSpread / 7 );

        //--Final Board Ambiguity Score
        ambiguity = Mathf.FloorToInt( ( switchAmbiguity * 40f ) + ( threatAmbiguity * 30f ) + ( ptkoSpreadAmbiguity * 30f ) );
        // log.Add( $"[{move.MoveSO.Name}] Our Remaining Pokemon: {ourTeam.Count}. Their Likely Switches: {teamAnal.Their_LikelySwitches}, Their Threat Count: {teamAnal.Their_ThreatCount}, Their Best PTKO: {teamAnal.Their_BestPTKO}, Their Average PTKO: {teamAnal.Their_AveragePTKO}" );
        // log.Add( $"[{move.MoveSO.Name}] Switch Ambiguity: {switchAmbiguity}, Threat Ambiguity: {threatAmbiguity}, PTKO Ambiguity: {ptkoSpreadAmbiguity}. Board Ambiguity Value: {ambiguity}" );

        //--Reliability
        // hazards have good accuracy and provide near-guaranteed team-wide chip and ko pressure 
        int accuracyScore = Mathf.RoundToInt( move.Accuracy * 0.2f );
        // log.Add($"[{move.MoveSO.Name}] Accuracy: {move.Accuracy}. Score: {accuracyScore}");

        //--Affect & Persistence
        int affectedCount = 0;
        int removalThreat = 0;

        for( int i = 0; i < oppTeam.Count; i++ )
        {
            var mon = oppTeam[i];
            bool affected = true;
            bool hazardCanBeRemoved = false;

            switch( moveEffects.CourtCondition )
            {
                case CourtConditionID.StickyWeb:
                    if( mon.CheckTypes( PokemonType.Flying ) || mon.AbilityID == AbilityID.Levitate || mon.BattleItemEffect.ID == BattleItemEffectID.AirBalloon )
                        affected = false;
                    break;

                case CourtConditionID.ToxicSpikes:
                    if( mon.CheckTypes( PokemonType.Poison ) )
                    {
                        affected = false;
                        hazardCanBeRemoved = true;
                    }

                    break;

                case CourtConditionID.LeechSeed:
                    if( mon.CheckTypes( PokemonType.Fire ) )
                    {
                        affected = false;
                        hazardCanBeRemoved = true;
                    }
                    
                    break;
            }

            if( affected )
                affectedCount++;

            if( hazardCanBeRemoved || mon.CheckHasActiveMove( "Defog" ) || mon.CheckHasActiveMove( "Rapid Spin" ) )
                removalThreat++;
        }

        float applicationRatio = affectedCount / (float)oppTeam.Count;
        int applicationScore = Mathf.RoundToInt( applicationRatio * 25 );
        // log.Add( $"[{move.MoveSO.Name}] Affected Mons: {affectedCount}/{oppTeam.Count}. Application Score: {applicationScore}" );

        float removalRatio = removalThreat / (float)oppTeam.Count;
        int persistenceScore = Mathf.RoundToInt( ( 1f - removalRatio ) * 20 );
        // log.Add( $"[{move.MoveSO.Name}] Hazard Removal Threats: {removalThreat}. Persistence Score: {persistenceScore}" );

        //--Reliability--------------------------------------------
        //--Prediction Reliability
        int predictedSwitches = teamAnal.Their_LikelySwitches;
        float switchRatio = predictedSwitches / (float)oppTeam.Count;

        int predictionScore = Mathf.RoundToInt( switchRatio * 5f );
        // log.Add( $"[{move.MoveSO.Name}] Predicted Switch Pressure: {predictedSwitches}. Prediction Score: {predictionScore}" );

        //--Final Reliability Score
        reliability = accuracyScore + applicationScore + persistenceScore + predictionScore;
        // log.Add( $"[{move.MoveSO.Name}] Reliability Value: {reliability}" );

        //--Immediate Impact
        int threatImpact = 0;

        if( targetPTKOR_Before.PTKO >= PotentialToKO.Dangerous )
            threatImpact -= 15;

        bool weMoveFirst = attackerSim.MTR.Move.Priority > targetSim.MTR.Move.Priority || ( attackerSim.MTR.Move.Priority == targetSim.MTR.Move.Priority && attackerSim.Speed > targetSim.Speed );
        if( !weMoveFirst )
            threatImpact -= 10;

        if( attackerPTKOR_Before.PTKO < PotentialToKO.Risky )
            threatImpact += 20;

        int disruptionValue = 5; //--Hazards do not provide any true disruption since they cannot deny turns or reduce damage, but they do provide pressure on switching and team-wide chip, which is disrupting.

        //--Get Tempo
        var exchange = _proj.EvaluateExchange( attackerSim, targetSim );
        var tempo = _proj.ClassifyTempo( exchange );
        int tempoSwing = 0;

        if( tempo == TempoState.LosingHard )
            tempoSwing += Mathf.RoundToInt( disruptionValue * 0.5f );

        //--Hazards do not deny turns.
        //--tempoSwing += turn denial

        impact = threatImpact + disruptionValue + tempoSwing;
        // log.Add( $"[{move.MoveSO.Name}] We Are Faster: {weMoveFirst}. Threat Impact: {threatImpact}, Disruption Value {disruptionValue}, Tempo Swing: {tempoSwing}. Immediate Impact Value: {impact}" );

        //--Layer logic
        Dictionary<CourtConditionID, int> courtConditions = new();
        if( targetSim.CourtLocation == CourtLocation.TopCourt )
            courtConditions = context.Field.TopCourtConditions;
        else if( targetSim.CourtLocation == CourtLocation.BottomCourt )
            courtConditions = context.Field.BottomCourtConditions;

        bool alreadySet = courtConditions.ContainsKey( move.MoveSO.MoveEffects.CourtCondition );

        if( !alreadySet )
            uniqueScore += 25;
        else
            uniqueScore += 10;

        // log.Add( $"[{move.MoveSO.Name}] Hazards Already Set?: {alreadySet}. Score: {uniqueScore}" );

        // 5. Type-specific bonuses
        switch( moveEffects.CourtCondition )
        {
            case CourtConditionID.ToxicSpikes:
                uniqueScore += 30;
                // log.Add( $"[{move.MoveSO.Name}] Toxic Spikes Bonus. Score: {uniqueScore}" );
                break;

            case CourtConditionID.StickyWeb:
                uniqueScore += 35;
                // log.Add( $"[{move.MoveSO.Name}] Sticky Web Bonus. Score: {uniqueScore}" );
                break;
            
            case CourtConditionID.LeechSeed:
                uniqueScore += 35;
                // log.Add( $"[{move.MoveSO.Name}] Leech Seed Bonus. Score: {uniqueScore}" );
                break;
        }

        int finalValue = coverage + ambiguity + reliability + impact;
        int candidateScore = coverage + ambiguity + reliability + impact + uniqueScore;
        // log.Add( $"[{move.MoveSO.Name}] Coverage: {coverage}, Ambiguity: {ambiguity}, Reliability: {reliability}, Impact: {impact}, Unique Score: {uniqueScore}. Final Value: {finalValue}" );

        return new()
        {
            CandidateScore = candidateScore,
            Coverage = coverage,
            Ambiguity = ambiguity,
            Reliability = reliability,
            Impact = impact,
            TotalValue = finalValue,
        };
    }

    private StatusValue ScoreStatDebuffMove( PotentialToKOResult attackerPTKOR_Before, PotentialToKOResult targetPTKOR_Before, IBattleAIUnit attackerSim, IBattleAIUnit targetSim, Move move, BattleSimContext context/*, CustomLogSession log*/ )
    {
        int uniqueScore = 0;
        int coverage = 0;
        int ambiguity = 0;
        int reliability = 0;
        int impact = 0;

        //--Team Anal
        var oppTeam = _ai.GetRemainingOpposingPokemon( attackerSim.PID );
        var ourTeam = _ai.GetRemainingAllyPokemon( attackerSim.PID );
        var teamAnal = _proj.Get_TeamVSTeamAnalysis( ourTeam, oppTeam );

        float switchAmbiguity = teamAnal.Their_LikelySwitches / (float)oppTeam.Count;
        float threatAmbiguity = teamAnal.Their_ThreatCount / (float)( oppTeam.Count * ourTeam.Count );
        float ptkoSpread = (int)teamAnal.Their_BestPTKO - (int)teamAnal.Their_AveragePTKO;
        float ptkoSpreadAmbiguity = Mathf.Clamp01( 1f - ptkoSpread / 7 );
        int theirFavor_ATK = teamAnal.TheirFavorCount_ATK;
        int theirFavor_SpATK = teamAnal.TheirFavorCount_SpATK;

        //--Team Coverage----------------------------------------------------------
        int statCoverageScore = 0;
        int relevantStatTarget = 0;

        for( int i = 0; i < move.MoveSO.MoveEffects.StatChangeList.Count; i++ )
        {
            var stat = move.MoveSO.MoveEffects.StatChangeList[i].Stat;

            switch( stat )
            {
                case Stat.Accuracy:
                    statCoverageScore += 15;
                    break;

                case Stat.Speed:
                    statCoverageScore += 15;
                    relevantStatTarget += teamAnal.Their_Outspeeds;
                    break;

                case Stat.Attack:
                    statCoverageScore += 5;
                    relevantStatTarget += theirFavor_ATK;
                    break;

                case Stat.SpAttack:
                    statCoverageScore += 5;
                    relevantStatTarget += theirFavor_SpATK;
                    break;

                case Stat.Defense:
                    if( attackerSim.MTR.Move.MoveSO.MoveCategory == MoveCategory.Physical )
                        statCoverageScore += 15;
                    else
                        statCoverageScore += 5;
                    break;

                case Stat.SpDefense:
                    if( attackerSim.MTR.Move.MoveSO.MoveCategory == MoveCategory.Special )
                        statCoverageScore += 15;
                    else
                        statCoverageScore += 5;
                    break;
            }
        }

        coverage = Mathf.RoundToInt( ( statCoverageScore * 0.5f ) + ( switchAmbiguity * 15f ) + ( relevantStatTarget * 5f ) );

        //--Board Ambiguity----------------------------------------------------------
        ambiguity = Mathf.RoundToInt( statCoverageScore + ( switchAmbiguity * 25f ) + ( threatAmbiguity * 15f ) + ( ptkoSpreadAmbiguity * 15f ) );

        //--Reliability----------------------------------------------------------
        int acc = move.MoveSO.Accuracy;
        int accuracyScore = 0;
        if( acc < 80 ) accuracyScore -= 10;
        else if( acc < 90 ) accuracyScore -= 5;

        //--Final Reliability Score
        reliability = accuracyScore + 5;

        //--Impact----------------------------------------------------------
        var stageDelta = _unitSim.BuildStatStageDelta( move );
        var targetSim_Debuffed = _unitSim.BuildSimUnit_WithStageDelta( targetSim, targetSim.CurrentHPR, targetSim.MTR, context.Field, stageDelta );

        var attackerMTR_After = GetMove_BestAttack( attackerSim, targetSim_Debuffed );
        var targetMTR_After = GetMove_BestAttack( targetSim_Debuffed, attackerSim );

        var attackerEDR_After = _proj.Get_EstimatedDamageResult( attackerSim, targetSim_Debuffed, attackerMTR_After );
        var targetEDR_After = _proj.Get_EstimatedDamageResult( targetSim_Debuffed, attackerSim, targetMTR_After );

        var attackerPTKOR_After = _proj.Get_PotentialToKOResult( attackerEDR_After, attackerMTR_After, targetSim_Debuffed.CurrentHPR );
        var targetPTKOR_After = _proj.Get_PotentialToKOResult( targetEDR_After, targetMTR_After, attackerSim.CurrentHPR );

        impact += ( attackerPTKOR_After.Score - attackerPTKOR_Before.Score ) * 2;
        impact += ( targetPTKOR_Before.Score - targetPTKOR_After.Score ) * 2;

        foreach ( var statStage in move.MoveEffects.StatChangeList )
        {
            int stages = Mathf.Abs( statStage.Change );

            switch( statStage.Stat )
            {
                case Stat.Attack:
                    if( targetSim.MTR.Move.MoveSO.MoveCategory == MoveCategory.Physical )
                        impact += 20 * stages;

                    break;

                case Stat.SpAttack:
                    if( targetSim.MTR.Move.MoveSO.MoveCategory == MoveCategory.Special )
                        impact += 20 * stages;

                    break;

                case Stat.Speed:
                    bool weBecomeFaster = attackerSim.Speed <= targetSim.Speed && attackerSim.Speed > targetSim_Debuffed.Speed;

                    if( weBecomeFaster )
                        impact += 30;
                    else
                        impact += 5 * stages;

                    break;

                case Stat.Defense:
                case Stat.SpDefense:
                    impact += 10 * stages;

                    break;

                case Stat.Accuracy:
                    impact += 15 * stages;

                    break;
            }
        }

        int finalValue = coverage + ambiguity + reliability + impact;
        int finalScore = finalValue + uniqueScore;

        return new()
        {
            CandidateScore = finalScore,
            Coverage = coverage,
            Ambiguity = ambiguity,
            Reliability = reliability,
            Impact = impact,
            TotalValue = finalValue,
        };
    }
}
