using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum OffensiveStatusType { None, StatusEffect, Disruption, EntryHazard, StatDebuff, Binding, Phaze }
public enum SupportiveStatusType { None, Recovery, ForceMultiplier, BattlefieldControl, AllyProtection }

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
                targets.Add( _ai.CurrentUnitDeciding );
            else if( move.MoveSO.MoveTarget == MoveTarget.OpposingSide )
            {
                for( int t = 0; t < _ai.Blackboard.TheirActiveBattleAIUnits.Count; t++ )
                {
                    var tar = _ai.GetBattleUnit( _ai.Blackboard.TheirActiveBattleAIUnits[t].Pokemon );
                    targets.Add( tar );
                }
            }
            else if( move.MoveSO.MoveTarget == MoveTarget.AllAdjacent )
            {
                for( int t = 0; t < _ai.Blackboard.TheirActiveBattleAIUnits.Count; t++ )
                {
                    var tar = _ai.GetBattleUnit( _ai.Blackboard.TheirActiveBattleAIUnits[t].Pokemon );
                    targets.Add( tar );
                }

                var allyUnits = _ai.BattleSystem.GetAllyUnits( _ai.CurrentUnitDeciding );
                for( int i = 0; i < allyUnits.Count; i++ )
                {
                    if( allyUnits[i] != _ai.CurrentUnitDeciding )
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
            _ai.BattleSystem.SetMoveCommand( _ai.CurrentUnitDeciding, targets, move, true );
        }
        else
            Debug.LogError( $"{_ai.CurrentUnitDeciding.Pokemon.NickName} has not chosen a move even though it was supposed to! Battle will now hang!" );
    }

    private AIDecisionType ChooseAttackStyle()
    {
        return UnityEngine.Random.value < _ai.TrainerSkillModifier ? AIDecisionType.ChosenMove : AIDecisionType.RandomMove;
    }

    private Move GetRandomMove( BattleUnit target )
    {
        // Debug.Log( $"[AI Scoring] Getting Random Move vs {target.Pokemon.NickName}" );
        List<Move> usableMoves = new();

        if( _ai.CurrentUnitDeciding.Flags[UnitFlags.ChoiceItem].IsActive && _ai.CurrentUnitDeciding.LastUsedMove != null )
            return _ai.CurrentUnitDeciding.LastUsedMove;

        foreach( var move in _ai.CurrentUnitDeciding.Pokemon.ActiveMoves )
        {
            if( move.PP == 0 )
                continue;

            if( !_ai.BattleSystem.MoveSuccess( _ai.CurrentUnitDeciding, target, move, true ) )
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

            // if( _ai.BattleSim.MoveSuccess() ) //--Do this soon!!! --03/06/26 --06/22/26, still haven't done it lol. it's going to be important soon with doubles finally around the corner...
                // continue;

            if( move.MoveSO.Name == "Fake Out" && !_ai.CanUseFakeOut( attacker, target ) )
                continue;

            //--Choice lock detection goes here
            if( attacker.VolatileStatuses.Contains( VolatileConditionID.ChoiceLocked ) )
                continue;

            //--Move type effectiveness
            float effectiveness = _ai.UnitSim.Get_MoveEffectiveness( target, move );

            //--If there a type immunity, skip this move
            if( effectiveness == 0f )
                continue;

            float attHPR                    = _ai.Get_HPRatio( attacker );
            float tarHPR                    = _ai.Get_HPRatio( target );

            float modifier                  = effectiveness * _ai.UnitSim.Get_MoveModifier( attacker, target, move );
            MoveThreatResult mtr            = new(){ Score = 0, Modifier = modifier, Move = move };
            var attEDR                      = _proj.Get_EstimatedDamageResult( attacker, target, mtr );
            
            var tarMTR                      = depth == 0 ? GetMove_BestAttack( target, attacker, false, "Opponent's best attack (recursion)", depth + 1 ) : _ai.Get_MostThreateningMove( target, attacker ); //--Remember, the order here is attacking unit vs target unit. this is the target's attack on the attacker here.
            var tarEDR                      = _proj.Get_EstimatedDamageResult( target, attacker, tarMTR );
            
            PotentialToKOResult attPTKOR    = _proj.Get_PotentialToKOResult( attEDR, mtr, tarHPR );
            PotentialToKOResult tarPTKOR    = _proj.Get_PotentialToKOResult( tarEDR, tarMTR, attHPR );

            // moveLog.Add( $"[Best Simulated Move] PTKO for {attacker.Name}'s {move.MoveSO.Name} on {target.Name} (HPR: {tarHPR} is: {attPTKOR.PTKO} (Damage Estimate: {attEDR.DamageEstimate})" );

            var attackerSimUnit             = _ai.UnitSim.BuildSimUnit( attacker, attHPR, mtr, fieldSim );
            var targetSimUnit               = _ai.UnitSim.BuildSimUnit( target, tarHPR, tarMTR, fieldSim );

            SimulationPackage attackerPack  = new(){ SimUnit = attackerSimUnit, ModuleType = SimModuleType.Attack };
            SimulationPackage targetPack    = new(){ SimUnit = targetSimUnit, ModuleType = SimModuleType.Attack };

            var bse                         = _battleSim.BuildBattleSimEvent( attPTKOR.PTKO, tarPTKOR.PTKO, attackerPack, targetPack, fieldSim );            
            var top                         = _battleSim.RunSimulation( bse );

            //--Begin Scoring
            int score = 0;
            if( top.Attacker_DiesBeforeActing )
                score -= 150;

            if( top.Opponent_DiesBeforeActing )
                score += 150;

            if( !top.OpponentCanAct )
            {
                score += 25;
            }

            int myAliveCount = _ai.GetRemainingAllyPokemon( attacker.Pokemon ).Count;
            int oppAliveCount = _ai.GetRemainingOpposingPokemon( attacker.Pokemon ).Count;

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

            float effectiveness             = _ai.UnitSim.Get_MoveEffectiveness( target, fallbackMove );
            float modifier                  = effectiveness * _ai.UnitSim.Get_MoveModifier( attacker, target, fallbackMove );
            MoveThreatResult mtr            = new(){ Score = 0, Modifier = modifier, Move = fallbackMove };
            var attWSR                      = _proj.Get_EstimatedDamageResult( attacker, target, mtr );

            var tarMTR                      = depth == 0 ? GetMove_BestAttack( target, attacker, false, source, depth + 1 ) : _ai.Get_MostThreateningMove( target, attacker ); //--Remember, the order here is attacking unit vs target unit. this is the target's attack on the attacker here.
            var tarEDR                      = _proj.Get_EstimatedDamageResult( target, attacker, tarMTR );

            PotentialToKOResult attPTKOR    = _proj.Get_PotentialToKOResult( attWSR, mtr, tarHPR );
            PotentialToKOResult tarPTKOR    = _proj.Get_PotentialToKOResult( tarEDR, tarMTR, attHPR );

            var attackerSimUnit         = _ai.UnitSim.BuildSimUnit( attacker, attHPR, mtr, fieldSim );
            var targetSimUnit               = _ai.UnitSim.BuildSimUnit( target, tarHPR, tarMTR, fieldSim );

            SimulationPackage attackerPack  = new(){ SimUnit = attackerSimUnit, ModuleType = SimModuleType.Attack };
            SimulationPackage targetPack    = new(){ SimUnit = targetSimUnit, ModuleType = SimModuleType.Attack };

            var bse                         = _battleSim.BuildBattleSimEvent( attPTKOR.PTKO, tarPTKOR.PTKO, attackerPack, targetPack, fieldSim );
            var top                         = _battleSim.RunSimulation( bse );

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

            Type = ActionResultType.Move,
            ActionType = ActionType.Attack,
        };

        if( actionSelect )
        {
            finalMtr.TargetBattleUnit = _ai.GetBattleUnit( target.Pokemon );
        }

        return finalMtr;
    }

    public SetupThreatResult GetMove_Setup( IBattleAIUnit attacker, IBattleAIUnit target, bool actionSelect = false )
    {
        SetupThreatResult best = new()
        {
            Type = ActionResultType.Move,
            ActionType = ActionType.Setup,
        };

        int bestValue = int.MinValue;
        int bestSweepCount = 0;
        int bestImprovedPTKOs = 0;
        
        Move bestSetup = null;

        StatStageDelta bestStageDelta = default;

        PotentialToKOResult bestBeforePTKO = default;
        PotentialToKOResult bestAfterPTKO = default;

        var setupMoves = _ai.UnitSim.GetSetupMoves( attacker.ActiveMoves );
        if( setupMoves.Count <= 0 || attacker.VolatileStatuses.Contains( VolatileConditionID.Taunt ) )
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
        attackerMTRbefore.Move = bestSetup;
        var attackerSim = _unitSim.BuildSimUnit( attacker, attHPR, attackerMTRbefore, fieldSim );
        var opponentSim = _unitSim.BuildSimUnit( target, tarHPR, tarMTRbefore, fieldSim );

        SimulationPackage attackerPack = new(){ SimUnit = attackerSim, ModuleType = SimModuleType.Setup };
        SimulationPackage opponentPack = new(){ SimUnit = opponentSim, ModuleType = SimModuleType.Attack };

        var bse = _battleSim.BuildBattleSimEvent( attPTKObefore.PTKO, tarPTKORbefore.PTKO, attackerPack, opponentPack, fieldSim );

        TurnOutcomeProjection top = _battleSim.RunSimulation( bse );

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

            Type = ActionResultType.Move,
            ActionType = ActionType.Setup,
        };

        if( actionSelect )
            best.TargetBattleUnit = _ai.GetBattleUnit( target.Pokemon );

        return best;
    }

    
    private struct StatusValue
    {
        //--Offensive Status Values
        public int CandidateScore;
        public int Coverage;
        public int Ambiguity;

        //--Supportive Status Values
        public int StrategicReach;
        public int BoardStability;

        //--Universal Values
        public int Reliability;
        public int Impact;
        public int Unique;

        //--Totals
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

        StatusThreatResult best = new()
        {
            Type = ActionResultType.Move,
            ActionType = ActionType.OffensiveStatus,
            SupportiveStatusType = SupportiveStatusType.None,
        };

        if( offensiveStatusMoves?.Count <= 0 || attacker.VolatileStatuses.Contains( VolatileConditionID.Taunt ) )
            return best;

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
            bool bind       = move.MoveEffects.BindingStatus    != BindingConditionID.None; //--Consider having binding moves be part of this decision line later

            bool statusEffect   = severe || vol  || trans;
            bool hazard         = move.MoveEffects.CourtCondition   != CourtConditionID.None;
            bool debuff         = move.MoveEffects.StatChangeList?.Count > 0 && ( move.MoveSO.MoveEffects.Target == EffectTarget.Enemy || move.MoveSO.MoveEffects.Target == EffectTarget.OpposingSide );
            bool disruption     = false;
            bool phazing        = move.MoveEffects.SwitchType == SwitchEffectType.Phaze;

            // log.Add( $"=[Evaluating {move.MoveSO.Name}. Severe: {severe}, Volatile: {vol}, Transient: {trans}, Hazard: {hazard}, Debuff: {debuff}]=" );

            if( statusEffect )
            {
                if( target.SevereStatus != SevereConditionID.None )
                    continue;

                if( target.VolatileStatuses.Contains( move.MoveSO.MoveEffects.VolatileStatus ) || isCurse && target.VolatileStatuses.Contains( VolatileConditionID.Cursed ) )
                    continue;

                bool taunt = false;
                bool encore = false;
                bool healblock = false;
                bool disable = false;
                bool perish = false;

                if( vol )
                {
                    var vs = move.MoveSO.MoveEffects.VolatileStatus;

                    if( vs == VolatileConditionID.Taunt )
                        taunt = true;

                    if( vs == VolatileConditionID.Encore )
                        encore = true;

                    if( vs == VolatileConditionID.HealBlocked )
                        healblock = true;

                    if( vs == VolatileConditionID.Disabled )
                        disable = true;

                    if( vs == VolatileConditionID.Perish )
                        perish = true;

                    disruption = taunt || encore || healblock || disable || perish;

                    type = OffensiveStatusType.Disruption;
                }
                else
                    type = OffensiveStatusType.StatusEffect;
                // log.Add( $"[{move.MoveSO.Name}] Move is a {type} move!" );
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
                // log.Add( $"[{move.MoveSO.Name}] Move is an {type} move!" );
            }
            else if( debuff )
            {
                type = OffensiveStatusType.StatDebuff;
                // log.Add( $"[{move.MoveSO.Name}] Move is a {type} move!" );
            }
            else if( phazing )
            {
                type = OffensiveStatusType.Phaze;
                // log.Add( $"[{move.MoveSO.Name}] Move is a {type} move!" );
            }
            else
                continue;

            switch( type )
            {
                case OffensiveStatusType.StatusEffect:
                    //--Simulate status application and score results based on before/after minor lookahead
                    statusValue = ScoreStatusEffectMove( attackerPTKOR_Before, targetPTKOR_Before, attackerSim, targetSim, move );
                    break;

                case OffensiveStatusType.Disruption:
                    statusValue = ScoreDisruptionMove( attackerPTKOR_Before, targetPTKOR_Before, attackerSim, targetSim, move );
                    break;

                case OffensiveStatusType.EntryHazard:
                    statusValue = ScoreEntryHazardMove( attackerPTKOR_Before, targetPTKOR_Before, attackerSim, targetSim, move );
                    break;

                case OffensiveStatusType.StatDebuff:
                    statusValue = ScoreStatDebuffMove( attackerPTKOR_Before, targetPTKOR_Before, attackerSim, targetSim, move );
                    break;

                case OffensiveStatusType.Phaze:
                    statusValue = ScorePhazeMove( attackerPTKOR_Before, targetPTKOR_Before, attackerSim, targetSim, move );
                    break;

                default: return best;
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

        // float opponentSwitchProb = _unitSim.PredictSwitchProbability( attackerPTKOR_Before.PTKO, targetPTKOR_Before.PTKO, bse.AttackerMovesFirst, attackerHPR_Before, targetHPR_Before, target.Expendability );
        // bool opponentSwitches = UnityEngine.Random.value <= opponentSwitchProb;

        SimulationPackage attackerPack = new(){ SimUnit = attackerSim, ModuleType = SimModuleType.OffensiveStatus };
        SimulationPackage targetPack = new(){ SimUnit = targetSim, ModuleType = SimModuleType.Attack };

        var bse = _battleSim.BuildBattleSimEvent( attackerPTKOR_Before.PTKO, targetPTKOR_Before.PTKO, attackerPack, targetPack, field_Before );

        TurnOutcomeProjection top = _battleSim.RunSimulation( bse );
        // if( opponentSwitches )
        //     top = _battleSim.SimulateOffensiveStatusRound( bse, true, false, false, true ); //--attacker status, opponent status, attacker switch, opponent switch
        // else
        //     top = _battleSim.SimulateOffensiveStatusRound( bse, true, false, false, false ); //--attacker status, opponent status, attacker switch, opponent switch

        // log.Add( top.SimulationLog );
        // Debug.Log( log.ToString() );
        // log.Clear();

        best = new()
        {
            OffensiveStatusType = bestType,
            Score = bestScore,
            StatusValue = bestValue.TotalValue,
            Coverage = bestValue.Coverage,
            Ambiguity = bestValue.Ambiguity,
            Reliability = bestValue.Reliability,
            Impact = bestValue.Impact,

            Move = bestMove,
            Target = target,
            Top = top,

            AttackerPTKOR = attackerPTKOR_Before,
            OpponentPTKOR = targetPTKOR_Before,

            Type = ActionResultType.Move,
            ActionType = ActionType.OffensiveStatus,
        };

        if( actionSelect )
            best.TargetBattleUnit = _ai.GetBattleUnit( target.Pokemon );

        return best;
    }

    private StatusValue ScoreStatusEffectMove( PotentialToKOResult attackerPTKOR_Before, PotentialToKOResult targetPTKOR_Before, IBattleAIUnit attackerSim, IBattleAIUnit targetSim, Move move )
    {
        int uniqueScore = 0;
        int coverage = 0;
        int ambiguity = 0;
        int reliability = 0;
        int impact = 0;

        var moveEffects = move.MoveSO.MoveEffects;
        var field = _unitSim.BuildSimField();

        // log.Add( $"[{move.MoveSO.Name}] Beginning Sub Scoring Module for Offensive Status Effect Move..." );

        //--Team Coverage-----------------------------------------
        var oppTeam = _ai.GetRemainingOpposingPokemon( attackerSim.Pokemon );
        var ourTeam = _ai.GetRemainingAllyPokemon( attackerSim.Pokemon );
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
        var targetSim_Statused = _unitSim.BuildSimUnit_WithStatus( targetSim, targetSim.CurrentHPR, targetSim.MTR, field );

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
            Unique = uniqueScore,
            TotalValue = finalValue,
        };
    }

    private StatusValue ScoreEntryHazardMove( PotentialToKOResult attackerPTKOR_Before, PotentialToKOResult targetPTKOR_Before, IBattleAIUnit attackerSim, IBattleAIUnit targetSim, Move move )
    {
        int uniqueScore = 0;
        int coverage = 0;
        int ambiguity = 0;
        int reliability = 0;
        int impact = 0;

        var moveEffects = move.MoveSO.MoveEffects;
        var field = _unitSim.BuildSimField();

        // log.Add( $"[{move.MoveSO.Name}] Beginning Sub Scoring Module for Offensive Entry Hazard Move..." );

        //--Team Coverage----------------------
        //--Remaining Opposing Team
        var oppTeam = _ai.GetRemainingOpposingPokemon( attackerSim.Pokemon );
        int remaining = oppTeam.Count;

        //--Opposing Team HP
        float totalTeamHPR = 0;
        for( int i = 0; i < oppTeam.Count; i++ )
            totalTeamHPR += _ai.Get_HPRatio( oppTeam[i] );
        
        //--Final Coverage Score
        coverage = Mathf.FloorToInt( ( remaining * 5f ) + ( totalTeamHPR * 8f ) );
        // log.Add( $"[{move.MoveSO.Name}] Opponent's Remaining Pokemon: {remaining}. Total Team HPR: {totalTeamHPR}. Coverage Value: {coverage}" );

        //--Board Ambiguity--------------------
        var ourTeam = _ai.GetRemainingAllyPokemon( attackerSim.Pokemon );
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
        courtConditions = targetSim.CourtLocation == CourtLocation.TopCourt ? field.TopCourtConditions : field.BottomCourtConditions;

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
            Unique = uniqueScore,
            TotalValue = finalValue,
        };
    }

    private StatusValue ScoreStatDebuffMove( PotentialToKOResult attackerPTKOR_Before, PotentialToKOResult targetPTKOR_Before, IBattleAIUnit attackerSim, IBattleAIUnit targetSim, Move move )
    {
        int uniqueScore = 0;
        int coverage = 0;
        int ambiguity = 0;
        int reliability = 0;
        int impact = 0;

        //--Team Anal
        var oppTeam = _ai.GetRemainingOpposingPokemon( attackerSim.Pokemon );
        var ourTeam = _ai.GetRemainingAllyPokemon( attackerSim.Pokemon );
        var teamAnal = _proj.Get_TeamVSTeamAnalysis( ourTeam, oppTeam );
        var field = _unitSim.BuildSimField();

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
        var targetSim_Debuffed = _unitSim.BuildSimUnit_WithStageDelta( targetSim, targetSim.CurrentHPR, targetSim.MTR, field, stageDelta );

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
            Unique = uniqueScore,
            TotalValue = finalValue,
        };
    }

    private StatusValue ScoreDisruptionMove( PotentialToKOResult attackerPTKOR_Before, PotentialToKOResult targetPTKOR_Before, IBattleAIUnit attackerSim, IBattleAIUnit targetSim, Move move )
    {
        int uniqueScore = 0;
        int coverage = 0;
        int ambiguity = 0;
        int reliability = 0;
        int impact = 0;

        //--Our Move Information
        var moveEffects = move.MoveSO.MoveEffects;
        var vs = moveEffects.VolatileStatus;

        //--Target Information
        var targetRP = targetSim.RoleProfile;
        var targetSignals = targetRP.Signals;
        int damagingMoves = targetSignals.PhysicalAttackCount + targetSignals.SpecialAttackCount;
        int statusMoves = targetSignals.StatusMoveCount;
        int setupMoves = targetSignals.SetupMoveCount;

        bool targetIsOffensive = targetRP.PrimaryRole == RoleClass.BulkyAttacker || targetRP.PrimaryRole == RoleClass.RevengeKiller || targetRP.PrimaryRole == RoleClass.SetupSweeper ||
            targetRP.PrimaryRole == RoleClass.Sweeper || targetRP.PrimaryRole == RoleClass.TrickRoomAbuser || targetRP.PrimaryRole == RoleClass.WallBreaker;

        bool targetIsDefensive = targetRP.PrimaryRole == RoleClass.Wall || targetRP.PrimaryRole == RoleClass.DefensiveSetup && targetRP.SecondaryRoles.Contains( RoleClass.Wall );
        bool targetIsUtility = !targetIsOffensive && !targetIsDefensive;

        bool hasHealingMove = targetRP.Traits.Contains( RoleTrait.RecoveryMove );
        bool hasHealingItem = targetRP.Traits.Contains( RoleTrait.RecoveryItem );
        bool lastUsedWasDamaging = false;
        bool lastUsedWasStatus = false;
        bool lastUsedWasSetup = false;
        bool lastUsedWasHealing = false;
        bool lastUsedWasProtect = false;
        bool lastUsedWasFakeOut = false;
        bool lastUsedWasSevere = false;

        var targetBattleUnit = _ai.GetBattleUnit( targetSim.Pokemon );
        bool targetIsActive = false;
        Move lastUsedMove = null;
        if( targetBattleUnit != null && targetBattleUnit.Pokemon != null )
        {
            targetIsActive = true;
            lastUsedMove = targetBattleUnit.LastUsedMove;
            var lcat = lastUsedMove.MoveSO.MoveCategory;
            var lume = lastUsedMove.MoveSO.MoveEffects;

            lastUsedWasDamaging     = lcat == MoveCategory.Physical || lcat == MoveCategory.Special;
            lastUsedWasStatus       = lcat == MoveCategory.Status;
            lastUsedWasSetup        = _unitSim.MoveIsSetup( lastUsedMove );
            lastUsedWasHealing      = _unitSim.MoveIsSelfHeal( lastUsedMove );
            lastUsedWasProtect      = lume.TransientStatus == TransientConditionID.Protect;
            lastUsedWasFakeOut      = lastUsedMove?.MoveSO.Name == "Fake Out";
            lastUsedWasSevere       = lume.SevereStatus != SevereConditionID.None && attackerSim.SevereStatus != SevereConditionID.None;
        }

        //--Team Coverage-----------------------------------------
        var ourTeam = _ai.GetRemainingAllyPokemon( attackerSim.Pokemon );
        var oppTeam = _ai.GetRemainingOpposingPokemon( attackerSim.Pokemon );
        var teamAnal = _proj.Get_TeamVSTeamAnalysis( ourTeam, oppTeam );

        int affectedCount = 0;
        int resistCount = 0;
        int statusWeight = 0;

        //--Effect Categorization
        bool preventsStatus = vs == VolatileConditionID.Taunt || ( vs == VolatileConditionID.Encore && targetIsActive && !lastUsedWasStatus && statusMoves > 0 );
        bool preventsHealing = vs == VolatileConditionID.HealBlocked || vs == VolatileConditionID.Taunt || ( vs == VolatileConditionID.Encore && targetIsActive && !lastUsedWasHealing && hasHealingMove );
        bool forcesRepeat = vs == VolatileConditionID.Encore || ( vs == VolatileConditionID.Taunt && targetIsActive && damagingMoves == 1 );
        bool forcesSwitch = vs == VolatileConditionID.Perish || ( vs == VolatileConditionID.Taunt && damagingMoves <= 0 ) || ( vs == VolatileConditionID.Encore && lastUsedWasSevere ) || ( vs == VolatileConditionID.Encore && lastUsedWasFakeOut ) || ( vs == VolatileConditionID.Encore && lastUsedWasProtect );
        bool preventsLastMove = vs == VolatileConditionID.Disabled || vs == VolatileConditionID.Taunt && lastUsedWasStatus;
        bool disablesLastMove = vs == VolatileConditionID.Disabled || ( vs == VolatileConditionID.Encore && lastUsedWasFakeOut ) || ( vs == VolatileConditionID.Encore && lastUsedWasSevere ) || ( vs == VolatileConditionID.Encore && lastUsedWasProtect );
        bool punishesSetup = preventsStatus || forcesRepeat || forcesSwitch;

        //--Coverage
        foreach( var opp in oppTeam )
        {
            int weight = 0;
            var adapter = _ai.GetPokemonAs_Adapter( opp );
            var rp = adapter.RoleProfile;
            var signals = rp.Signals;

            int oppdamagingMoves = signals.PhysicalAttackCount + signals.SpecialAttackCount;
            int oppStatusMoves = signals.StatusMoveCount;
            int oppSetupMoves = signals.SetupMoveCount;
            bool oppHasHealing = rp.Traits.Contains( RoleTrait.RecoveryMove );

            bool preventsOppStatus = vs == VolatileConditionID.Taunt;
            bool forcesOppRepeat = vs == VolatileConditionID.Encore || vs == VolatileConditionID.Taunt && oppdamagingMoves == 1;
            bool preventsOppHealing = vs == VolatileConditionID.HealBlocked || vs == VolatileConditionID.Taunt;
            bool forcesOppSwitch = vs == VolatileConditionID.Perish;

            if( preventsOppStatus )
            {
                weight += signals.StatusMoveCount * 6;
                weight += signals.SetupMoveCount * 8;
            }

            if( preventsOppHealing && rp.Traits.Contains( RoleTrait.RecoveryMove ) )
            {
                weight += 15;
            }

            if( forcesOppRepeat )
            {
                weight += signals.PhysicalAttackCount * 3;
                weight += signals.SpecialAttackCount * 3;
            }

            if( forcesOppSwitch )
            {
                weight += 10;
            }

            statusWeight += weight;

            if( weight > 0 )
            {
                affectedCount++;
            }
        }

        float averageWeight = statusWeight / oppTeam.Count;
        float applicationRatio = affectedCount / oppTeam.Count;
        float switchRatio = teamAnal.Their_LikelySwitches / (float)oppTeam.Count;

        coverage = Mathf.RoundToInt( averageWeight + ( applicationRatio * 20f ) + ( switchRatio * 10f ) - ( resistCount * 2f ) );

        //--Ambiguity
        float switchAmbiguity = teamAnal.Their_LikelySwitches / (float)oppTeam.Count;
        float threatAmbiguity = teamAnal.Their_ThreatCount / (float)( oppTeam.Count * ourTeam.Count );
        float ptkoSpread = (int)teamAnal.Their_BestPTKO - (int)teamAnal.Their_AveragePTKO;
        float ptkoSpreadAmbiguity = Mathf.Clamp01( 1f - ptkoSpread / 7 );

        //--Final Board Ambiguity Score
        ambiguity = Mathf.FloorToInt( ( switchAmbiguity * 30f ) + ( threatAmbiguity * 10f ) + ( ptkoSpreadAmbiguity * 10f ) );

        //--Reliability
        //--Accuracy
        int acc = move.MoveSO.Accuracy;
        int accuracyScore = 0;
        if( acc < 80 ) accuracyScore -= 10;
        else if( acc < 90 ) accuracyScore -= 5;

        //--Application
        int applicationScore = 0;
        if( preventsStatus )
        {
            applicationScore += statusMoves * 2;
        }

        if( preventsHealing )
        {
            if( hasHealingMove )
            {
                applicationScore += 10;
            }

            if( hasHealingItem )
            {
                applicationScore += 10;
            }
        }

        if( forcesRepeat && targetIsActive )
        {
            applicationScore += 10;
        }

        if( forcesSwitch )
        {
            applicationScore += 20;
        }

        //--Prediction
        int predictedSwitches = teamAnal.Their_LikelySwitches;
        int predictionScore = Mathf.RoundToInt( switchRatio * 5f );

        //--Final Reliability Score
        reliability = accuracyScore + applicationScore + predictionScore;

        //--Impact
        if( preventsStatus )
        {
            impact += statusMoves * 10;
            impact += setupMoves * 12;

            if( targetRP.PrimaryRole == RoleClass.UtilitySupport || targetRP.PrimaryRole == RoleClass.Disrupter || targetRP.PrimaryRole == RoleClass.SetupSweeper )
            {
                impact += 10;
            }
        }

        if( preventsHealing )
        {
            if( ( targetRP.PrimaryRole == RoleClass.Wall && ( hasHealingItem || hasHealingMove ) ) || ( targetRP.Biases.Contains( RoleBias.PassivePressure ) && hasHealingItem ) )
            {
                impact += 25;
            }

            if( hasHealingMove )
            {
                impact += 15;
            }

            if( hasHealingItem )
            {
                impact += 15;
            }
        }

        if( forcesRepeat )
        {
            if( lastUsedWasSetup )
            {
                impact += 60;
            }
            else if( lastUsedWasHealing )
            {
                impact += 45;
            }
            else if( lastUsedWasStatus )
            {
                impact += 35;
            }
            else if( lastUsedWasDamaging )
            {
                impact += 10;
            }
        }

        if( preventsLastMove || disablesLastMove )
        {
            if( lastUsedMove != null )
            {
                impact += 20;

                if( damagingMoves <= 2 )
                {
                    impact += 20;
                }

                if( lastUsedWasHealing )
                {
                    impact += 10;
                }

                if( lastUsedWasSetup )
                {
                    impact += 20;
                }

                if( lastUsedMove.MovePower > 0 )
                {
                    impact += Mathf.RoundToInt( lastUsedMove.MovePower / 5f );
                }
            }
        }

        if( forcesSwitch )
        {
            impact += 30;

            if( targetRP.Biases.Contains( RoleBias.PassivePressure ) )
            {
                impact += 20;
            }

            if( targetPTKOR_Before.PTKO >= PotentialToKO.TwoHKO )
            {
                impact += 15;
            }

            if( targetRP.Traits.Contains( RoleTrait.ShadowTag ) || targetRP.Traits.Contains( RoleTrait.TrappingMove ) )
            {
                impact += 20;
            }
        }

        //--Unique Scores
        if( vs == VolatileConditionID.Taunt && ( targetRP.PrimaryRole == RoleClass.SetupSweeper || targetRP.PrimaryRole == RoleClass.DefensiveSetup || targetIsUtility ) )
        {
            uniqueScore += 25;
        }

        if( vs == VolatileConditionID.Encore && ( lastUsedWasSetup || lastUsedWasFakeOut || lastUsedWasProtect ) )
        {
            uniqueScore += 30;
        }

        if( vs == VolatileConditionID.HealBlocked && targetIsDefensive && hasHealingMove )
        {
            uniqueScore += 20;
        }

        if( vs == VolatileConditionID.Disabled && targetSim.VolatileStatuses.Contains( VolatileConditionID.ChoiceLocked ) )
        {
            uniqueScore += 30;
        }

        if( punishesSetup && ( targetRP.PrimaryRole == RoleClass.SetupSweeper || targetRP.PrimaryRole == RoleClass.DefensiveSetup ) )
        {
            uniqueScore += 15;
        }

        //--Final Tally
        int finalValue = coverage + ambiguity + reliability + impact;
        int finalScore = finalValue + uniqueScore;

        return new()
        {
            CandidateScore = finalScore,
            Coverage = coverage,
            Ambiguity = ambiguity,
            Reliability = reliability,
            Impact = impact,
            Unique = uniqueScore,
            TotalValue = finalValue,
        };
    }

    private StatusValue ScorePhazeMove( PotentialToKOResult attackerPTKOR_Before, PotentialToKOResult targetPTKOR_Before, IBattleAIUnit attackerSim, IBattleAIUnit targetSim, Move move )
    {
        int uniqueScore = 0;
        int coverage = 0;
        int ambiguity = 0;
        int reliability = 0;
        int impact = 0;

        var field = _unitSim.BuildSimField();

        //--Move Information
        var moveEffects = move.MoveSO.MoveEffects;
        var cat = move.MoveSO.MoveCategory;
        var sound = move.MoveSO.Flags.Contains( MoveFlags.Sound );

        //--Their Information
        var targetRP = targetSim.RoleProfile;
        var theirCourt = targetSim.CourtLocation == CourtLocation.TopCourt ? field.TopCourtConditions : field.BottomCourtConditions;
        bool targetIsTypeImmune     = cat != MoveCategory.Status && TypeChart.GetTotalMoveEffectiveness( targetSim.Type, move ) <= 0;
        bool targetIsSoundImmune    = sound && targetSim.RoleProfile.Traits.Contains( RoleTrait.StatusMoveImmune );
        bool targetIsPhazeImmune    = cat != MoveCategory.Status && targetSim.VolatileStatuses.Contains( VolatileConditionID.Substitute ) || targetSim.Ability == AbilityID.SuctionCups;
        bool targetIsImmune         = targetIsTypeImmune || targetIsSoundImmune || targetIsPhazeImmune || _ai.Check_IsLastPokemon( targetSim.Pokemon );
        bool rocksUp                = theirCourt.ContainsKey( CourtConditionID.StealthRock );

        var targetStatStages = targetSim.StatStages;
        int targetRaises = 0;
        int targetLowers = 0;
        foreach( var sc in targetStatStages )
        {
            if( sc.Value > 0 )
                targetRaises += sc.Value;

            if( sc.Value < 0 )
                targetLowers += sc.Value;
        }

        //--Team Analysis
        var ourTeam = _ai.GetRemainingAllyPokemon( attackerSim.Pokemon );
        var oppTeam = _ai.GetRemainingOpposingPokemon( attackerSim.Pokemon );
        var teamAnal = _proj.Get_TeamVSTeamAnalysis( ourTeam, oppTeam );

        //--Coverage
        //--Opposing Team HP
        float totalTeamHPR = 0;
        float totalRocksWeakness = 0;
        int rocksWeakCount = 0;
        int rocksSuperWeakCount = 0;
        bool toxicSpikesAbsorber = false;
        bool leechSeedBurner = false;
        for( int i = 0; i < oppTeam.Count; i++ )
        {
            totalTeamHPR += _ai.Get_HPRatio( oppTeam[i] );
            float rocksEffectiveness = TypeChart.GetTotalEffectiveness( PokemonType.Rock, oppTeam[i].PokeSO.Type1, oppTeam[i].PokeSO.Type2 );
            totalRocksWeakness += rocksEffectiveness;

            if( rocksEffectiveness > 2 )
            {
                rocksSuperWeakCount++;
            }
            else if( rocksEffectiveness > 1 )
            {
                rocksWeakCount++;
            }

            if( oppTeam[i].CheckTypes( PokemonType.Poison ) )
                toxicSpikesAbsorber = true;

            if( oppTeam[i].CheckTypes( PokemonType.Fire ) )
                leechSeedBurner = true;
        }

        int remaining = oppTeam.Count;

        float threatAmbiguity = teamAnal.Their_ThreatCount / (float)( oppTeam.Count * ourTeam.Count );
        float ptkoSpread = (int)teamAnal.Their_BestPTKO - (int)teamAnal.Their_AveragePTKO;

        int hazardValue = 0;

        foreach( var kvp in theirCourt )
        {
            if( kvp.Key == CourtConditionID.LeechSeed )
            {
                hazardValue += 25;

                if( leechSeedBurner )
                    hazardValue -= 10;
            }

            if( kvp.Key == CourtConditionID.Spikes )
            {
                int layers = _ai.BattleSystem.Field.ActiveCourts[targetSim.CourtLocation].Conditions[CourtConditionID.Spikes].Layers;
                hazardValue += 5 * layers;
            }

            if( kvp.Key == CourtConditionID.StealthRock )
            {
                hazardValue += Mathf.RoundToInt( 20f * ( totalRocksWeakness / 6 ) );
            }

            if( kvp.Key == CourtConditionID.StickyWeb )
            {
                hazardValue += 25;
            }

            if( kvp.Key == CourtConditionID.ToxicSpikes )
            {
                int layers = _ai.BattleSystem.Field.ActiveCourts[targetSim.CourtLocation].Conditions[CourtConditionID.ToxicSpikes].Layers;
                int tsValue = 15 * layers;

                if( toxicSpikesAbsorber )
                    tsValue /= 2;

                hazardValue += tsValue;
            }
        }

        coverage = Mathf.FloorToInt( ( remaining * 5f ) + ( totalTeamHPR * 5f ) + hazardValue );

        //--Ambiguity
        float switchAmbiguity = teamAnal.Their_LikelySwitches / (float)oppTeam.Count;
        float ptkoSpreadAmbiguity = Mathf.Clamp01( 1f - ptkoSpread / 7 );

        //--Final Board Ambiguity Score
        ambiguity = Mathf.FloorToInt( ( switchAmbiguity * 40f ) + ( threatAmbiguity * 30f ) + ( ptkoSpreadAmbiguity * 30f ) + hazardValue );

        //--Reliability
        int acc = move.MoveSO.Accuracy;
        int accuracyScore = 0;
        if( acc < 80 ) accuracyScore -= 10;
        else if( acc < 90 ) accuracyScore -= 5;

        // log.Add( $"[{move.MoveSO.Name}] Accuracy: {acc}. Score: {accuracyScore}" );
        int applicationScore = targetIsImmune ? 0 : attackerPTKOR_Before.PTKO == PotentialToKO.OHKO ? 0 : 20;
        // log.Add( $"[{move.MoveSO.Name}] Affected Mons: {affectedCount}/{oppTeam.Count}. Application Score: {applicationScore}" );

        //--Prediction Reliability
        float switchRatio = teamAnal.Their_LikelySwitches / (float)oppTeam.Count;
        int predictionScore = Mathf.RoundToInt( switchRatio * 2f );
        // log.Add( $"[{move.MoveSO.Name}] Predicted Switch Pressure: {predictedSwitches}. Prediction Score: {predictionScore}" );

        //--Final Reliability Score
        reliability = accuracyScore + applicationScore + predictionScore;

        //--Impact
        int statChangeDelta = targetRaises - targetLowers;
        if( statChangeDelta >= 0 && targetRaises > 0 )
        {
            impact += 25;
        }
        else if( statChangeDelta <= 0 && targetLowers > 0 )
        {
            if( attackerPTKOR_Before.PTKO <= PotentialToKO.TwoHKO )
                impact += 10;
            else
                impact -= 20;
        }

        impact += targetRaises * 5;
        impact += targetRP.Signals.SetupPressure;

        if( targetSim.VolatileStatuses.Contains( VolatileConditionID.Substitute ) )
        {
            impact += 25;
        }

        impact += hazardValue;

        if( rocksUp )
        {
            impact += rocksWeakCount * 5;
            impact += rocksSuperWeakCount * 5;
        }

        //--Unique Scores
        if( targetRP.PrimaryRole == RoleClass.SetupSweeper || targetRP.PrimaryRole == RoleClass.DefensiveSetup )
        {
            uniqueScore += 20;

            if( targetRaises > 0 )
            {
                uniqueScore += 10;
            }
        }

        //--Final Tally
        int finalValue = coverage + ambiguity + reliability + impact;
        int finalScore = finalValue + uniqueScore;

        return new()
        {
            CandidateScore = finalScore,
            Coverage = coverage,
            Ambiguity = ambiguity,
            Reliability = reliability,
            Impact = impact,
            Unique = uniqueScore,
            TotalValue = finalValue,
        };
    }

    public StatusThreatResult GetMove_SupportiveStatus( IBattleAIUnit attacker, IBattleAIUnit target, bool actionSelect = false )
    {
        CustomLogSession statusLog = new();
        var supportiveStatusMoves = _ai.UnitSim.GetSupportiveStatusMoves( attacker.ActiveMoves );

        statusLog.Add( $"===[[Get Move Supportive Status] Getting Supportive Status Move for {attacker.Name} vs {target.Name}]===" );

        StatusThreatResult best = new()
        {
            Type = ActionResultType.Move,
            ActionType = ActionType.SupportiveStatus,
            OffensiveStatusType = OffensiveStatusType.None,
        };

        if( supportiveStatusMoves?.Count <= 0 || attacker.VolatileStatuses.Contains( VolatileConditionID.Taunt ) )
        {
            statusLog.Add( $"[Get Move Supportive Status] No Supportive Status Moves found ({supportiveStatusMoves?.Count}) or we are taunted and cannot use them! ({attacker.VolatileStatuses.Contains( VolatileConditionID.Taunt )})" );
            Debug.Log( statusLog.ToString() );
            statusLog.Clear();
            return best;
        }

        int bestScore = int.MinValue;
        StatusValue bestValue = default;
        SupportiveStatusType bestType = SupportiveStatusType.None;
        Move bestMove = null;

        //--Pre Status use simulation for comparisons. "Before".
        //--HP Ratios
        var attackerHPR_Before = attacker.BeginningHPR;
        var targetHPR_Before = target.BeginningHPR;

        //--Move Threat Result
        var attackerMTR_Before = GetMove_BestAttack( attacker, target );
        var targetMTR_Before = GetMove_BestAttack( target, attacker );

        //--Estimated Damage Results
        var attackerEDR_Before = _proj.Get_EstimatedDamageResult( attacker, target, attackerMTR_Before );
        var targetEDR_Before = _proj.Get_EstimatedDamageResult( target, attacker, attackerMTR_Before );

        //--Potential to KO Results
        var attackerPTKOR_Before = _proj.Get_PotentialToKOResult( attackerEDR_Before, attackerMTR_Before, targetHPR_Before );
        var targetPTKOR_Before = _proj.Get_PotentialToKOResult( targetEDR_Before, targetMTR_Before, attackerHPR_Before );

        var field_Before = _unitSim.BuildSimField();
        var courtBefore = attacker.CourtLocation == CourtLocation.TopCourt ? field_Before.TopCourtConditions : field_Before.BottomCourtConditions;

        var attackerSim = _unitSim.BuildSimUnit( attacker, attackerHPR_Before, attackerMTR_Before, field_Before );
        var targetSim = _unitSim.BuildSimUnit( target, targetHPR_Before, targetMTR_Before, field_Before );

        var ally = _ai.GetActiveAllyAs_Adapter( attacker.Pokemon );

        SimulatedUnit allySim = null;

        if( ally != null )
        {
            statusLog.Add( $"[Get Move Supportive Status] Our Ally: {ally.Name}" );
            allySim = _unitSim.CopySimUnit( ally, field_Before );

            allySim.MTR = new()
            {
                Score = 0,
                Modifier = 0,
                CurrentActor = allySim,
                Target = null,
                TargetBattleUnit = null,
                Move = null,
                EstimatedDamage = 0f,
                Top = default,

                Type = ActionResultType.Move,
                ActionType = ActionType.Attack,
                Candidate = null,
            };
        }

        foreach( var move in supportiveStatusMoves )
        {
            StatusValue statusValue = default;
            SupportiveStatusType type = SupportiveStatusType.None;

            var moveTarget = move.MoveSO.MoveTarget;
            var effects = move.MoveSO.MoveEffects;

            bool isSelfHeal = _unitSim.MoveIsSelfHeal( move );
            bool isAllyHeal = move.MoveSO.HealType != HealType.None && moveTarget == MoveTarget.Ally;
            bool isSideHeal = move.MoveSO.HealType != HealType.None && moveTarget == MoveTarget.AllySide;

            bool isAllySetup = _unitSim.MoveIsSetup( move ) && effects.Target == EffectTarget.AllySide;
            bool isHelpingHand = effects.VolatileStatus == VolatileConditionID.HelpingHand;

            bool isWeather = effects.Weather != WeatherConditionID.None;
            bool isTerrain = effects.Terrain != TerrainID.None;
            bool isField = effects.FieldCondition != FieldConditionID.None;

            bool isTailwind = effects.CourtCondition == CourtConditionID.Tailwind;
            
            bool isReflect = effects.CourtCondition == CourtConditionID.Reflect;
            bool isLightScreen = effects.CourtCondition == CourtConditionID.LightScreen;
            bool isAuroraVeil = effects.CourtCondition == CourtConditionID.AuroraVeil;
            bool isScreens = isReflect || isLightScreen || isAuroraVeil;

            bool isSafeguard = effects.CourtCondition == CourtConditionID.SafeGuard;

            bool isRedirection = effects.TransientStatus == TransientConditionID.CenterOfAttention;

            statusLog.Add( $"[Get Move Supportive Status] Evaluating Move: {move.MoveSO.Name}" );
            statusLog.Add( $"Double Battle: {_ai.IsDoubleBattle}. We have an ally: ({ally != null})" );

            //--Move is possible checks
            if( ( isAllySetup || isAllyHeal ) && ( !_ai.IsDoubleBattle || ally == null ) )
            {
                if( moveTarget == MoveTarget.Ally || effects.Target == EffectTarget.Enemy )
                {
                    statusLog.Add( $"[Get Move Supportive Status] Ally setup or ally heal does not work if it is not a double battle ({_ai.IsDoubleBattle}) or we have no ally ({ally == null})!" );
                    Debug.Log( statusLog.ToString() );
                    statusLog.Clear();
                    return best;
                }
            }

            if( isHelpingHand && ( !_ai.IsDoubleBattle || ally == null ) )
            {
                statusLog.Add( $"[Get Move Supportive Status] Helping Hand does not work if it is not a double battle ({_ai.IsDoubleBattle}) or we have no ally ({ally == null})!" );
                Debug.Log( statusLog.ToString() );
                statusLog.Clear();
                return best;
            }

            if( isWeather && field_Before.Weather == effects.Weather )
            {
                statusLog.Add( $"[Get Move Supportive Status] Ally setup or ally heal does not work if it is not a double battle ({_ai.IsDoubleBattle}) or we have no ally ({ally == null})!" );
                Debug.Log( statusLog.ToString() );
                statusLog.Clear();
                return best;
            }

            if( isTerrain && field_Before.Terrain == effects.Terrain )
            {
                statusLog.Add( $"[Get Move Supportive Status] Ally setup or ally heal does not work if it is not a double battle ({_ai.IsDoubleBattle}) or we have no ally ({ally == null})!" );
                Debug.Log( statusLog.ToString() );
                statusLog.Clear();
                return best;
            }

            if( isField && field_Before.FieldConditions.ContainsKey( effects.FieldCondition ) )
            {
                if( effects.FieldCondition == FieldConditionID.TrickRoom && field_Before.FieldConditions.ContainsKey( FieldConditionID.TrickRoom ) )
                {
                    int ourTRS = _unitSim.Get_TrickRoomContextScore( attacker.Pokemon );

                    if( _ai.IsDoubleBattle )
                    {
                        if( ally != null )
                        {
                            int allyTRS = _unitSim.Get_TrickRoomContextScore( ally.Pokemon );

                            if( allyTRS > 0 && ourTRS > 0 )
                            {
                                statusLog.Add( $"Trick Room is already up and we both benefit from it, ignoring!" );
                                Debug.Log( statusLog.ToString() );
                                return best;
                            }

                            if( allyTRS < 0 && ourTRS > 0 )
                            {
                                statusLog.Add( $"Trick Room is already up and we currently benefit from it, ignoring!" );
                                                                statusLog.Clear();
                                return best;
                            }

                            if( allyTRS > 0 && ourTRS < 0 )
                            {
                                statusLog.Add( $"Trick Room is already up and our ally benefits from it, ignoring!" );
                                Debug.Log( statusLog.ToString() );
                                statusLog.Clear();
                                return best;
                            }
                        }
                    }
                    else if( ourTRS > 0 )
                    {
                        statusLog.Add( $"Trick Room is already up and we currently benefit from it, ignoring!" );
                        Debug.Log( statusLog.ToString() );
                        statusLog.Clear();
                        return best;
                    }
                }
            }

            if( isTailwind && courtBefore.ContainsKey( CourtConditionID.Tailwind ) )
            {
                statusLog.Add( $"Tailwind is already up, ignoring!" );
                Debug.Log( statusLog.ToString() );
                statusLog.Clear();
                return best;
            }

            if( isReflect && courtBefore.ContainsKey( CourtConditionID.Reflect ) )
            {
                statusLog.Add( $"Reflect is already up, ignoring!" );
                Debug.Log( statusLog.ToString() );
                statusLog.Clear();
                return best;
            }

            if( isLightScreen && courtBefore.ContainsKey( CourtConditionID.LightScreen ) )
            {
                statusLog.Add( $"Light Screen is already up, ignoring!" );
                Debug.Log( statusLog.ToString() );
                statusLog.Clear();
                return best;
            }

            if( isAuroraVeil && courtBefore.ContainsKey( CourtConditionID.AuroraVeil ) )
            {
                statusLog.Add( $"Aurora Veil is already up, ignoring!" );
                Debug.Log( statusLog.ToString() );
                statusLog.Clear();
                return best;
            }

            if( isRedirection && ( !_ai.IsDoubleBattle || ally == null ) )
            {
                statusLog.Add( $"Redirection doesn't work if it isn't a double battle or we have no ally!" );
                Debug.Log( statusLog.ToString() );
                statusLog.Clear();
                return best;
            }

            if( isSelfHeal )
            {
                type = SupportiveStatusType.Recovery;
            }
            else if( isTailwind || isScreens || isAllySetup || isHelpingHand )
            {
                type = SupportiveStatusType.ForceMultiplier;
            }
            else if( isWeather || isTerrain || isField || isSafeguard )
            {
                type = SupportiveStatusType.BattlefieldControl;
            }
            else if( isAllyHeal || isSideHeal || isRedirection )
            {
                type = SupportiveStatusType.AllyProtection;
            }

            statusLog.Add( $"[Get Move Supportive Status] Move is a {type} move!" );

            switch( type )
            {
                case SupportiveStatusType.Recovery:
                    statusValue = ScoreRecovery( attackerSim, targetSim, move );
                break;

                case SupportiveStatusType.ForceMultiplier:
                    statusValue = ScoreForceMultiplier( attackerSim, targetSim, move );
                break;

                case SupportiveStatusType.BattlefieldControl:
                    statusValue = ScoreBattlefieldControl( attackerSim, targetSim, move );
                break;

                case SupportiveStatusType.AllyProtection:
                    statusValue = ScoreAllyProtection( attackerSim, targetSim, move );
                break;

                default:
                    statusLog.Add( $"[Get Move Supportive Status] Move doesn't have an appropriate Supportive Status Type!" );
                break;
            }

            statusLog.Add( $"[Get Move Supportive Status] Status Values for {move.MoveSO.Name}:" );
            statusLog.Add( $"[Get Move Supportive Status] Strategic Reach: {statusValue.StrategicReach}" );
            statusLog.Add( $"[Get Move Supportive Status] Board Stability: {statusValue.BoardStability}" );
            statusLog.Add( $"[Get Move Supportive Status] Reliability: {statusValue.Reliability}" );
            statusLog.Add( $"[Get Move Supportive Status] Impact: {statusValue.Impact}" );
            statusLog.Add( $"[Get Move Supportive Status] Unique: {statusValue.Unique}" );
            statusLog.Add( $"" );
            statusLog.Add( $"[Get Move Supportive Status] Candidate Score: {statusValue.CandidateScore}" );
            statusLog.Add( $"[Get Move Supportive Status] Total Value: {statusValue.TotalValue}" );
            statusLog.Add( $"" );
            statusLog.Add( $"Candidate Score vs Best Score: {statusValue.CandidateScore} > {bestScore}" );

            if( statusValue.CandidateScore > bestScore )
            {
                bestScore = statusValue.CandidateScore;
                bestMove = move;
                bestValue = statusValue;
                bestType = type;
            }
        }

        statusLog.Add( $"" );
        statusLog.Add( $"[Get Move Supportive Status] Best Move is: {bestMove?.MoveSO.Name}, of type {bestType}. Score: {bestScore}" );

        //--Run Supportive Status Use Simulation Here, after picking the move itself.
        if( bestMove == null )
        {
            statusLog.Add( $"Best move was null!" );
            Debug.Log( statusLog.ToString() );
            statusLog.Clear();
            return default;
        }
        else
        {
            MoveThreatResult statusMove = new(){ Move = bestMove };
            attackerSim.MTR = statusMove;
        }

        IBattleAIUnit actualTarget = null;
        if( bestMove.MoveTarget == MoveTarget.Enemy )
            actualTarget = target;

        if( bestMove.MoveTarget == MoveTarget.Ally )
            actualTarget = allySim ?? attacker;

        if( bestMove.MoveTarget == MoveTarget.Self || bestMove.MoveTarget == MoveTarget.AllySide || bestMove.MoveTarget == MoveTarget.AllField )
            actualTarget = attacker;

        SimulatedUnit actualTargetSim = _unitSim.CopySimUnit( actualTarget, field_Before );
        
        SimulationPackage attackerPack = new(){ SimUnit = attackerSim, ModuleType = SimModuleType.SupportiveStatus };
        SimulationPackage targetPack = new(){ SimUnit = actualTargetSim, ModuleType = SimModuleType.Attack };

        var bse = _battleSim.BuildBattleSimEvent( attackerPTKOR_Before.PTKO, targetPTKOR_Before.PTKO, attackerPack, targetPack, field_Before );

        TurnOutcomeProjection top = _battleSim.RunSimulation( bse );

        best = new()
        {
            SupportiveStatusType = bestType,
            Score = bestScore,

            StatusValue = bestValue.TotalValue,

            StrategicReach = bestValue.StrategicReach,
            Stability = bestValue.BoardStability,
            Reliability = bestValue.Reliability,
            Impact = bestValue.Impact,
            Unique = bestValue.Unique,

            Move = bestMove,
            Target = actualTarget,
            Top = top,

            AttackerPTKOR = attackerPTKOR_Before,
            OpponentPTKOR = targetPTKOR_Before,
        };

        if( actionSelect )
        {
            best.TargetBattleUnit = actualTarget != null ? _ai.GetBattleUnit( actualTarget.Pokemon ) : _ai.GetBattleUnit( attacker.Pokemon );
        }

        if( bestMove != null )
            statusLog.Add( $"[Get Move Supportive Status] Move's Target Flag: {bestMove?.MoveTarget}, Selected Target: {best.TargetBattleUnit.Pokemon?.NickName}" );

        statusLog.Add( $"" );
        statusLog.Add( $"=========================================================================" );
        statusLog.Add( $"" );

        Debug.Log( statusLog.ToString() );
        statusLog.Clear();

        return best;
    }

    private void ApplySupportEffect( IBattleAIUnit unit, Move move, SimulatedField field )
    {
        var moveTarget = move.MoveSO.MoveTarget;
        var effects = move.MoveSO.MoveEffects;
        var court = unit.CourtLocation == CourtLocation.TopCourt ? field.TopCourtConditions : field.BottomCourtConditions;
        var battleField = _ai.BattleSystem.Field;
        var realCourt = battleField.ActiveCourts[unit.CourtLocation];

        bool isAllySetup = _unitSim.MoveIsSetup( move ) && effects.Target == EffectTarget.AllySide;
        bool isHelpingHand = effects.VolatileStatus == VolatileConditionID.HelpingHand;

        bool isWeather = effects.Weather != WeatherConditionID.None;
        bool isTerrain = effects.Terrain != TerrainID.None;
        bool isField = effects.FieldCondition != FieldConditionID.None;

        bool isTailwind = effects.CourtCondition == CourtConditionID.Tailwind;
        bool isScreens = effects.CourtCondition == CourtConditionID.Reflect || effects.CourtCondition == CourtConditionID.LightScreen || effects.CourtCondition == CourtConditionID.AuroraVeil;
        bool isSafeguard = effects.CourtCondition == CourtConditionID.SafeGuard;

        bool isAllyHeal = move.MoveSO.HealType != HealType.None && moveTarget == MoveTarget.Ally;
        bool isSideHeal = move.MoveSO.HealType != HealType.None && moveTarget == MoveTarget.AllySide;

        if( isAllySetup )
        {
            var stages = _unitSim.BuildStatStageDelta( move );
            _unitSim.ApplyStatStages( unit, stages );
        }

        if( isHelpingHand && _ai.IsDoubleBattle )
        {
            unit.VolatileStatuses.Add( VolatileConditionID.HelpingHand );
        }

        if( isWeather )
        {
            field.Weather = effects.Weather;
        }

        if( isTerrain )
        {
            field.Terrain = effects.Terrain;
        }

        if( isField )
        {
            int duration = FieldConditionDB.Conditions[effects.FieldCondition].Duration;
            field.FieldConditions.Add( effects.FieldCondition, duration );
        }

        if( isTailwind || isScreens || isSafeguard )
        {
            int duration = CourtConditionDB.Conditions[effects.CourtCondition].Duration;
            court.Add( effects.CourtCondition, duration );
        }

        if( isAllyHeal || isSideHeal )
        {
            float healAmount = (float)move.MoveSO.HealAmount / 100f;

            unit.BeginningHPR += Mathf.Clamp01( healAmount );
            unit.CurrentHPR += Mathf.Clamp01( healAmount );
        }
    }

    private StatusValue ScoreRecovery( IBattleAIUnit attacker, IBattleAIUnit target, Move move )
    {
        int reach = 0;
        int stability = 0;
        int reliability = 0;
        int impact = 0;
        int unique = 0;

        var field = _unitSim.BuildSimField();

        //--Attacker Information
        var attackerRP = attacker.RoleProfile;
        var attackerPR = attackerRP.PrimaryRole;
        var attTraits = attackerRP.Traits;
        var gp = _ai.Blackboard.GamePlan;

        //--Move Information
        var effects = move.MoveSO.MoveEffects;

        //--HP Information
        float healAmount = move.MoveSO.HealAmount; //--eventually factor weather changing healing percentages here once implemented

        if( move.MoveSO.HealType == HealType.PercentOfMaxHP )
            healAmount /= 100f;

        float incomingDamage = target.MTR.EstimatedDamage;

        float hprAfter;

        //--Exchange Information
        var currentEE = _ai.Projection.EvaluateExchange( attacker, target );

        if( currentEE.AttackerMovesFirst )
            hprAfter = attacker.BeginningHPR + healAmount - incomingDamage;
        else
            hprAfter = attacker.BeginningHPR - incomingDamage <= 0f ? 0f : Mathf.Clamp01( attacker.BeginningHPR - incomingDamage + healAmount );

        var attackerAfter = _unitSim.BuildSimUnit( attacker.Pokemon, hprAfter, attacker.MTR, field );

        var afterEE = _ai.Projection.EvaluateExchange( attackerAfter, target );

        //--Reach
        int hitsBefore = Mathf.CeilToInt( attacker.BeginningHPR / incomingDamage ); //--this should round a percentage into hits. for example, an attacker with 0.7f hpr taking 0.3f damage needs 2.3 hits, which in pokemon means 3 hits.
        int hitsAfter = Mathf.CeilToInt( hprAfter / incomingDamage );

        int hitsGainedValue = hitsAfter * 5;

        int strategicRoleValue = 0;

        if( attackerPR == RoleClass.Wall )
            strategicRoleValue += 15;

        if( gp.OurBlockers.Contains( attacker.Pokemon ) || gp.OurPrimaryWinCon == attacker.Pokemon )
            strategicRoleValue += 15;

        if( attTraits.Contains( RoleTrait.WeatherSetter ) || attTraits.Contains( RoleTrait.TrickRoomSetter ) || attTraits.Contains( RoleTrait.TailwindSetter ) || attTraits.Contains( RoleTrait.HazardRemover ) )
            strategicRoleValue += 15;

        int currentHPValue = Mathf.RoundToInt( 30f * ( 1f - attacker.BeginningHPR ) );
        int recoveryValue = attacker.BeginningHPR <= 0.25f ? 20 : attacker.BeginningHPR <= 0.5f ? 15 : attacker.BeginningHPR <= 0.75f ? 5 : 0;

        if( attacker.SevereStatus == SevereConditionID.TOX )
        {
            recoveryValue += Mathf.RoundToInt( ( (float)attacker.SevereStatusTime * 15f ) * ( 1f - attacker.BeginningHPR ) );
        }

        reach += currentHPValue + recoveryValue + strategicRoleValue;

        //--Stability
        int hitDelta = hitsAfter - hitsBefore;
        int hitImprovementValue = hitDelta * 10;
        int ptkoDelta = (int)currentEE.AttackerPTKOR.PTKO - (int)afterEE.AttackerPTKOR.PTKO;
        int ptkoImprovementValue = ptkoDelta * 10;

        int currentBoardValue = 0;

        if( currentEE.AttackerMovesFirst )
        {
            if( currentEE.AttackerPTKOR.PTKO >= PotentialToKO.Risky )
            {
                if( hitDelta >= 3 )
                    currentBoardValue += 15;
                else if( hitDelta > 1 )
                    currentBoardValue += 10;
                else if( hitDelta < 1 )
                    currentBoardValue -= 20;
            }
        }
        else
        {
            if( currentEE.OpponentPTKOR.PTKO >= PotentialToKO.Dangerous )
            {
                if( hitDelta >= 3 )
                    currentBoardValue += 20;
                else if( hitDelta > 1 )
                    currentBoardValue += 15;
                else
                    currentBoardValue -= 10;
            }
        }

        stability += hitImprovementValue + ptkoImprovementValue + currentBoardValue;

        //--Reliability
        //--Accuracy - all self healing moves bypass accuracy i'm fairly sure. this isn't that useful of a metric.
        int acc = move.MoveSO.Accuracy;
        int accuracyScore = 0;
        if( acc < 80 ) accuracyScore -= 10;
        else if( acc < 90 ) accuracyScore -= 5;

        int switchRead = Mathf.RoundToInt( 20f * currentEE.OpponentSwitchProbability );

        int successScore = 0;
        if( target.RoleProfile.Traits.Contains( RoleTrait.Taunt ) )
            successScore -= 25;
        else
            successScore += 20;

        if( target.RoleProfile.Traits.Contains( RoleTrait.Encore ) && currentEE.OpponentMovesFirst )
            successScore -= 10;
        else
            successScore += 10;

        reliability += accuracyScore + switchRead + successScore;

        //--Impact
        bool targetHasOffensiveBoosts = false;
        foreach( var sc in target.StatStages )
        {
            var stat = sc.Key;
            var change = sc.Value;

            if( change > 0 && ( stat == Stat.Attack || stat == Stat.SpAttack ) )
            {
                targetHasOffensiveBoosts = true;
                break;
            }
        }

        int wallValue = 0;

        if( targetHasOffensiveBoosts )
        {
            wallValue += 10;

            if( hitDelta >= 2 )
                wallValue += 15;
            else if( hitDelta > 0 )
                wallValue += 10;
        }

        wallValue += ptkoDelta * 10;

        int escapeValue = 0;
        if( currentEE.OpponentThreatensKO && hitDelta > 0 )
        {
            escapeValue += 10;

            if( !currentEE.OpponentMovesFirst )
            {
                escapeValue += 10;
            }

            if( attacker.Item == BattleItemEffectID.Leftovers || attacker.Item == BattleItemEffectID.SitrusBerry )
            {
                escapeValue += 10;
            }

            if( attacker.SevereStatus == SevereConditionID.BRN || attacker.SevereStatus == SevereConditionID.FBT || attacker.SevereStatus == SevereConditionID.PSN )
            {
                escapeValue += 10;
            }
        }

        impact += wallValue + escapeValue;

        //--Unique
        var ability = attacker.Ability;
        if( ability == AbilityID.Sturdy && hprAfter >= 1f )
        {
            unique += 10;
        }

        if( ability == AbilityID.Multiscale && hprAfter >= 1f )
        {
            unique += 10;
        }

        if( currentEE.OpponentPTKOR.PTKO >= PotentialToKO.Dangerous && currentEE.OpponentMovesFirst && ability == AbilityID.Regenerator )
        {
            unique -= 20;
        }

        var bu = _ai.GetBattleUnit( attacker.Pokemon );
        if( bu != null && bu.Pokemon != null && bu.Pokemon == attacker.Pokemon )
        {
            if( bu.Flags[UnitFlags.Wish].IsActive && bu.Flags[UnitFlags.Wish].Count == 0 )
            {
                unique += 5;
            }
        }

        if( attackerPR == RoleClass.Wall && attackerRP.Biases.Contains( RoleBias.PassivePressure ) )
        {
            unique += 10;
        }

        //--Healing moves that are changed based on weather are also unique and should be considered here

        //--Weather granting spdef or def boosts (sandstorm and snowscape, respectively) typically mean increased bulk, and therefore healing becomes more valuable.
        if( attacker.DirectStatModifiers.TryGetValue( Stat.Defense, out var def ) )
        {
            if( def.ContainsKey( DirectModifierCause.WeatherDEF ) && _ai.Blackboard.CurrentFieldSnapshot.Weather == WeatherConditionID.SNOW )
            {
                unique += 10;
            }
        }

        if( attacker.DirectStatModifiers.TryGetValue( Stat.SpDefense, out var spdef ) )
        {
            if( spdef.ContainsKey( DirectModifierCause.WeatherSpDEF ) && _ai.Blackboard.CurrentFieldSnapshot.Weather == WeatherConditionID.SANDSTORM )
            {
                unique += 10;
            }
        }

        if( attTraits.Contains( RoleTrait.ToxicPressure ) )
        {
            unique += 15;
        }

        if( attackerRP.Biases.Contains( RoleBias.AttritionFocused ) || attackerRP.Biases.Contains( RoleBias.BulkyOffense ) )
        {
            unique += 15;
        }

        //--Final Tally
        int finalScore = reach + stability + reliability + impact + unique;

        return new()
        {
            CandidateScore = finalScore,
            StrategicReach = reach,
            BoardStability = stability,
            Reliability = reliability,
            Impact = impact,
            Unique = unique,
            TotalValue = finalScore,
        };
    }

    private StatusValue ScoreForceMultiplier( IBattleAIUnit attacker, IBattleAIUnit target, Move move )
    {
        int reach = 0;
        int stability = 0;
        int reliability = 0;
        int impact = 0;
        int unique = 0;

        var fieldBefore = _unitSim.BuildSimField();
        var fieldAfter = _unitSim.BuildSimField();

        //--Attacker Information
        var attackerRP = attacker.RoleProfile;
        var attackerPR = attackerRP.PrimaryRole;
        var attTraits = attackerRP.Traits;
        var gp = _ai.Blackboard.GamePlan;

        //--Target Information
        var targetRP = target.RoleProfile;

        //--Move Information
        var effects = move.MoveSO.MoveEffects;
        bool isAllySetup = _unitSim.MoveIsSetup( move ) && effects.Target == EffectTarget.AllySide;
        bool isHelpingHand = effects.VolatileStatus == VolatileConditionID.HelpingHand;
        bool isTailwind = effects.CourtCondition == CourtConditionID.Tailwind;
        bool isScreens = effects.CourtCondition == CourtConditionID.Reflect || effects.CourtCondition == CourtConditionID.LightScreen || effects.CourtCondition == CourtConditionID.AuroraVeil;

        //--Apply Effect
        IBattleAIUnit attackerAfter = _unitSim.CopySimUnit( attacker, fieldBefore );
        ApplySupportEffect( attackerAfter, move, fieldAfter );

        //--Exchange Information
        var currentEE = _ai.Projection.EvaluateExchange( attacker, target );
        var afterEE = _ai.Projection.EvaluateExchange( attackerAfter, target );

        //--Team Analysis
        var ourTeam = _ai.GetRemainingAllyPokemon( attacker.Pokemon );
        var oppTeam = _ai.GetRemainingOpposingPokemon( attacker.Pokemon );
        var teamAnalBefore = _proj.Get_TeamVSTeamAnalysis( ourTeam, oppTeam );

        var ourRemaining = _ai.GetRemainingPartyAs_IBattleAIUnits( attacker.Pokemon );
        var theirRemaining = _ai.GetRemainingPartyAs_IBattleAIUnits( target.Pokemon );
        float ourRemainingPercentage = ourTeam.Count / (float)_ai.Blackboard.OurTeamPokemon.Count;
        float theirRemainingPercentage = oppTeam.Count / (float)_ai.Blackboard.TheirTeamPokemon.Count;

        List<IBattleAIUnit> ourTeamAfter = new();
        foreach( var mon in ourRemaining )
        {
            var tempField = _unitSim.BuildSimField();
            var sim = _unitSim.GetSimUnit( mon, target, tempField );

            if( isTailwind || isScreens )
                ApplySupportEffect( sim, move, tempField );

            ourTeamAfter.Add( sim );
        }

        var teamAnalAfter = _proj.Get_TeamVSTeamAnalysis( ourTeamAfter, theirRemaining );

        //--Doubles ally check
        IBattleAIUnit ally = null;
        if( _ai.IsDoubleBattle )
            ally = _ai.GetActiveAllyAs_Adapter( attacker.Pokemon );

        ExchangeEvaluation allyBeforeEE = default;
        ExchangeEvaluation allyAfterEE = default;
        if( ally != null )
        {
            allyBeforeEE = _proj.EvaluateExchange( ally, target );

            IBattleAIUnit allyAfter = _unitSim.CopySimUnit( ally, fieldBefore );
            var tempField = _unitSim.BuildSimField();
            ApplySupportEffect( allyAfter, move, tempField );

            allyAfterEE = _proj.EvaluateExchange( allyAfter, target );
        }

        //--Reach -- How much future damage/progress does this effect create?
        int longTerm = 0;
        int shortTerm = 0;

        if( isTailwind || isScreens )
        {
            shortTerm += ourTeam.Count * 3;
            longTerm += ourTeam.Count * 7;
        }

        if( ally != null )
        {
            var allyRP = ally.RoleProfile;
            if( isHelpingHand )
            {
                shortTerm += 30;
            }

            if( isTailwind || isScreens )
            {
                shortTerm += 10;
                longTerm += 5;
            }

            if( isAllySetup )
            {
                if( move.MoveSO.Name == "Acupressure" )
                {
                    shortTerm += 10;
                    longTerm += 10;
                }

                //--Acupressure will not have a pre-set stat stage list of changes, and instead will be handled manually in MoveConditionDB, so it isn't effected by this block.
                foreach( var sc in effects.StatChangeList )
                {
                    var stat = sc.Stat;
                    var change = sc.Change;

                    if( change > 0 )
                    {
                        if( stat == Stat.Attack && allyRP.Biases.Contains( RoleBias.Physical ) )
                        {
                            shortTerm += attacker.Speed > ally.Speed ? 15 : 10;
                            longTerm += 10;
                        }

                        if( stat == Stat.SpAttack && allyRP.Biases.Contains( RoleBias.Special ) )
                        {
                            shortTerm += attacker.Speed > ally.Speed ? 15 : 10;
                            longTerm += 10;
                        }

                        if( stat == Stat.Defense )
                        {
                            shortTerm += attacker.Speed > ally.Speed ? 15 : 10;
                            longTerm += allyRP.Biases.Contains( RoleBias.PhysicallyBulky ) ? 10 : 25;
                        }

                        if( stat == Stat.SpDefense )
                        {
                            shortTerm += attacker.Speed > ally.Speed ? 15 : 10;
                            longTerm += allyRP.Biases.Contains( RoleBias.SpeciallyBulky ) ? 10 : 25;
                        }

                        if( stat == Stat.Speed )
                        {
                            shortTerm += attacker.Speed > ally.Speed ? 25 : 15;
                            longTerm += !allyRP.Traits.Contains( RoleTrait.ParalysisWeak ) ? 20 : 10; //--this just means the ally is likely slow
                        }
                    }
                }
            }
        }

        reach += Mathf.RoundToInt( shortTerm + longTerm + ( ( longTerm * 2f ) * ourRemainingPercentage ) );

        //--Stability
        //--Compare Team vs Team before and after
        int teamImprovement = 0;
        if( teamAnalAfter.Our_AveragePTKO > teamAnalBefore.Our_AveragePTKO )
        {
            teamImprovement += 20;
        }

        if( teamAnalAfter.Our_BestPTKO > teamAnalBefore.Our_BestPTKO )
        {
            teamImprovement += 10;
        }

        if( teamAnalAfter.Their_AveragePTKO < teamAnalBefore.Their_AveragePTKO )
        {
            teamImprovement += 20;
        }

        if( teamAnalAfter.Their_BestPTKO < teamAnalBefore.Their_BestPTKO )
        {
            teamImprovement += 10;
        }

        if( teamAnalAfter.Our_Outspeeds > teamAnalBefore.Our_Outspeeds )
        {
            teamImprovement += 10;
        }

        if( teamAnalBefore.Our_Outspeeds <= teamAnalBefore.Their_Outspeeds && teamAnalAfter.Our_Outspeeds > teamAnalBefore.Their_Outspeeds )
        {
            teamImprovement += 15;
        }

        stability += Mathf.RoundToInt( teamImprovement + ( 25f * ourRemainingPercentage) );
        
        //--Reliability
        //--Accuracy
        int acc = move.MoveSO.Accuracy;
        int accuracyScore = 0;
        if( acc < 80 ) accuracyScore -= 10;
        else if( acc < 90 ) accuracyScore -= 5;

        //--Free turn from switch probability
        int switchRead = Mathf.RoundToInt( 20f * currentEE.OpponentSwitchProbability );

        //--Value from successful execution without prevention or post use punishment
        int successScore = 0;
        if( target.RoleProfile.Traits.Contains( RoleTrait.Taunt ) )
            successScore -= 25;
        else
            successScore += 20;

        if( target.RoleProfile.Traits.Contains( RoleTrait.Encore ) && currentEE.OpponentMovesFirst )
            successScore -= 10;
        else
            successScore += 10;

        //--How well an active ally can use the buff or the current situation actually calls for it
        int allyUseValue = 0;
        if( ourRemainingPercentage >= 0.5f )
            allyUseValue += 10;
        else
            allyUseValue -= 15;

        if( ally != null )
        {
            var allyRP = ally.RoleProfile;
            bool allyIsOffensive = allyRP.PrimaryRole == RoleClass.BulkyAttacker || allyRP.PrimaryRole == RoleClass.RevengeKiller || allyRP.PrimaryRole == RoleClass.SetupSweeper ||
            allyRP.PrimaryRole == RoleClass.Sweeper || allyRP.PrimaryRole == RoleClass.TrickRoomAbuser || allyRP.PrimaryRole == RoleClass.WallBreaker;

            if( effects.CourtCondition == CourtConditionID.Reflect && targetRP.Biases.Contains( RoleBias.Special ) )
            {
                allyUseValue -= 10;
            }

            if( effects.CourtCondition == CourtConditionID.LightScreen && targetRP.Biases.Contains( RoleBias.Physical ) )
            {
                allyUseValue -= 10;
            }

            if( isHelpingHand && allyIsOffensive )
            {
                allyUseValue += 10;
            }
            else
            {
                allyUseValue -= 15;
            }

            if( isTailwind )
            {
                if( attacker.Speed < target.Speed )
                {
                    allyUseValue += 5;
                }

                if( ally.Speed < target.Speed )
                {
                    allyUseValue += 15;

                    if( attacker.Speed > ally.Speed || attacker.Ability == AbilityID.Prankster )
                    {
                        allyUseValue += 10;
                    }
                }
            }
        }

        reliability += accuracyScore + switchRead + successScore + allyUseValue;

        //--Impact
        int currentSituationImprovement = 0;
        int attacker_SurvivalPTKODelta = (int)currentEE.OpponentPTKOR.PTKO - (int)afterEE.OpponentPTKOR.PTKO;
        if( afterEE.AttackerMovesFirst && !currentEE.AttackerMovesFirst )
        {
            currentSituationImprovement += 5;
        }

        if( afterEE.AttackerPTKOR.PTKO > currentEE.AttackerPTKOR.PTKO )
        {
            currentSituationImprovement += 5;

            if( afterEE.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous && currentEE.AttackerPTKOR.PTKO <= PotentialToKO.Risky )
            {
                currentSituationImprovement += 5;

                if( afterEE.AttackerMovesFirst )
                {
                    currentSituationImprovement += 5;
                }
            }
        }

        if( afterEE.OpponentPTKOR.PTKO < currentEE.OpponentPTKOR.PTKO )
        {
            currentSituationImprovement += 5 * attacker_SurvivalPTKODelta;
        }

        if( afterEE.AttackerMovesFirst && !currentEE.AttackerMovesFirst )
        {
            currentSituationImprovement += 5;
        }

        if( ally != null )
        {
            int ally_SurvivalPTKODelta = (int)allyBeforeEE.OpponentPTKOR.PTKO - (int)allyAfterEE.OpponentPTKOR.PTKO;
            if( allyAfterEE.AttackerMovesFirst && !allyBeforeEE.AttackerMovesFirst )
            {
                currentSituationImprovement += 20;
            }

            if( allyAfterEE.AttackerPTKOR.PTKO > allyBeforeEE.AttackerPTKOR.PTKO )
            {
                currentSituationImprovement += 20;

                if( allyAfterEE.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous && allyBeforeEE.AttackerPTKOR.PTKO <= PotentialToKO.Risky )
                {
                    currentSituationImprovement += 10;

                    if( allyAfterEE.AttackerMovesFirst )
                    {
                        currentSituationImprovement += 5;
                    }
                }
            }

            if( allyAfterEE.OpponentPTKOR.PTKO < allyBeforeEE.OpponentPTKOR.PTKO )
            {
                currentSituationImprovement += 10 * ally_SurvivalPTKODelta;
            }

            if( allyAfterEE.AttackerMovesFirst && !allyBeforeEE.AttackerMovesFirst )
            {
                currentSituationImprovement += 20;
            }
        }

        impact += Mathf.RoundToInt( currentSituationImprovement + ( teamImprovement * theirRemainingPercentage ) );
        
        //--Unique
        if( isHelpingHand && ally != null )
        {
            if( allyBeforeEE.AttackerPTKOR.PTKO >= PotentialToKO.Risky && allyBeforeEE.AttackerPTKOR.PTKO < PotentialToKO.OHKO )
            {
                unique += 10;

                if( allyAfterEE.AttackerMovesFirst )
                {
                    unique += 10;
                }
            }
            else if( allyBeforeEE.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous )
            {
                unique -= 15;
            }
        }

        if( isTailwind )
        {
            if( ally != null )
            {
                var allyBiases = ally.RoleProfile.Biases;

                if( allyBiases.Contains( RoleBias.AwkwardSpeed ) || allyBiases.Contains( RoleBias.SlowSpeed ) )
                {
                    unique -= 10;
                }
                else if( allyBiases.Contains( RoleBias.TrickRoomSpeed ) )
                {
                    unique -= 25;
                }
                else
                {
                    unique += 10;
                }
            }
        }

        if( isScreens )
        {
            if( effects.CourtCondition == CourtConditionID.Reflect && targetRP.Biases.Contains( RoleBias.Physical ) )
            {
                unique += 10;
            }

            if( effects.CourtCondition == CourtConditionID.LightScreen && targetRP.Biases.Contains( RoleBias.Special ) )
            {
                unique += 10;
            }

            if( effects.CourtCondition == CourtConditionID.AuroraVeil && ( fieldBefore.Weather == WeatherConditionID.SNOW || fieldAfter.Weather == WeatherConditionID.SNOW ) )
            {
                unique += 25;
            }   
        }

        if( isAllySetup )
        {
            if( move.MoveSO.Name == "Howl" )
            {
                if( attackerRP.Biases.Contains( RoleBias.Physical ) )
                {
                    unique += 10;
                }

                if( ally != null && ally.RoleProfile.Biases.Contains( RoleBias.Physical ) )
                {
                    unique += 20;
                }
            }

            if( move.MoveSO.Name == "Acupressure" )
            {
                if( ally.Ability == AbilityID.Contrary )
                {
                    unique -= 50;
                }
                else if( ally.Ability == AbilityID.Simple )
                {
                    unique += 50;
                }

                if( _unitSim.CheckHasMove( ally, "Stored Power" ) )
                {
                    unique += 25;
                }

                if( _unitSim.CheckHasMove( ally, "Baton Pass" ) )
                {
                    unique += 20;
                }
            }
        }
        
        //--Final Tally
        int finalScore = reach + stability + reliability + impact + unique;

        return new()
        {
            CandidateScore = finalScore,
            StrategicReach = reach,
            BoardStability = stability,
            Reliability = reliability,
            Impact = impact,
            Unique = unique,
            TotalValue = finalScore,
        };
    }

    private StatusValue ScoreBattlefieldControl( IBattleAIUnit attacker, IBattleAIUnit target, Move move )
    {
        int reach = 0;
        int stability = 0;
        int reliability = 0;
        int impact = 0;
        int unique = 0;

        var fieldBefore = _unitSim.BuildSimField();
        var fieldAfter = _unitSim.BuildSimField();

        //--Attacker Information
        var attackerRP = attacker.RoleProfile;
        var attackerPR = attackerRP.PrimaryRole;
        var attTraits = attackerRP.Traits;
        var gp = _ai.Blackboard.GamePlan;

        //--Target Information
        var targetRP = target.RoleProfile;

        //--Move Information
        var effects = move.MoveSO.MoveEffects;
        bool isWeather = effects.Weather != WeatherConditionID.None;
        bool isTerrain = effects.Terrain != TerrainID.None;
        bool isField = effects.FieldCondition != FieldConditionID.None;
        bool isSafeguard = effects.CourtCondition == CourtConditionID.SafeGuard;

        //--Apply Effect
        IBattleAIUnit attackerAfter = _unitSim.CopySimUnit( attacker, fieldBefore );
        ApplySupportEffect( attackerAfter, move, fieldAfter );

        //--Exchange Information
        var attackerVS_Target_Before = _ai.Projection.EvaluateExchange( attacker, target );
        var attackerVS_Target_After = _ai.Projection.EvaluateExchange( attackerAfter, target );

        //--Team Analysis
        var ourTeam = _ai.GetRemainingAllyPokemon( attacker.Pokemon );
        var theirTeam = _ai.GetRemainingOpposingPokemon( attacker.Pokemon );
        var teamAnalBefore = _proj.Get_TeamVSTeamAnalysis( ourTeam, theirTeam );

        var ourRemaining = _ai.GetRemainingPartyAs_IBattleAIUnits( attacker.Pokemon );
        var theirRemaining = _ai.GetRemainingPartyAs_IBattleAIUnits( target.Pokemon );
        float ourRemainingPercentage = ourTeam.Count / (float)_ai.Blackboard.OurTeamPokemon.Count;
        float theirRemainingPercentage = theirTeam.Count / (float)_ai.Blackboard.TheirTeamPokemon.Count;

        List<IBattleAIUnit> ourTeamAfter = new();
        foreach( var mon in ourRemaining )
        {
            var tempField = _unitSim.BuildSimField();
            var sim = _unitSim.GetSimUnit( mon, target, tempField );
            ApplySupportEffect( sim, move, tempField );

            ourTeamAfter.Add( sim );
        }

        var teamAnalAfter = _proj.Get_TeamVSTeamAnalysis( ourTeamAfter, theirRemaining );

        //--Doubles ally check
        IBattleAIUnit ally = null;
        if( _ai.IsDoubleBattle )
            ally = _ai.GetActiveAllyAs_Adapter( attacker.Pokemon );

        ExchangeEvaluation allyVS_Target_Before = default;
        ExchangeEvaluation allyVS_Target_After = default;
        if( ally != null )
        {
            allyVS_Target_Before = _proj.EvaluateExchange( ally, target );

            IBattleAIUnit allyAfter = _unitSim.CopySimUnit( ally, fieldBefore );
            var tempField = _unitSim.BuildSimField();
            ApplySupportEffect( allyAfter, move, tempField );

            allyVS_Target_After = _proj.EvaluateExchange( allyAfter, target );
        }

        IBattleAIUnit theirAlly = null;
        if( _ai.IsDoubleBattle )
            theirAlly = _ai.GetActiveAllyAs_Adapter( target.Pokemon );

        ExchangeEvaluation attackerVS_TheirAlly_Before = default;
        ExchangeEvaluation attackerVS_TheirAlly_After = default;

        ExchangeEvaluation allyVS_TheirAlly_Before = default;
        ExchangeEvaluation allyVS_TheirAlly_After = default;
        if( theirAlly != null )
        {
            attackerVS_TheirAlly_Before = _proj.EvaluateExchange( attacker, theirAlly );

            IBattleAIUnit theirAllyAfter = _unitSim.CopySimUnit( theirAlly, fieldBefore );
            var tempField = _unitSim.BuildSimField();
            ApplySupportEffect( theirAllyAfter, move, tempField );

            attackerVS_TheirAlly_After = _proj.EvaluateExchange( attacker, theirAllyAfter );

            if( ally != null )
            {
                allyVS_TheirAlly_Before = _proj.EvaluateExchange( ally, theirAlly );

                IBattleAIUnit allyAfter = _unitSim.CopySimUnit( ally, fieldBefore );
                var tempField2 = _unitSim.BuildSimField();
                ApplySupportEffect( allyAfter, move, tempField2 );

                allyVS_TheirAlly_After = _proj.EvaluateExchange( allyAfter, theirAlly );
            }
        }

        //--Team Context Scores
        int attackerBattlefieldContext = 0;
        int ourAllyBattlefieldContext = 0;

        int targetBattlefieldContext = 0;
        int theirAllyBattlefieldContext = 0;
        if( isWeather )
        {
            attackerBattlefieldContext += _unitSim.Get_WeatherContextScore( attacker.Pokemon );
            targetBattlefieldContext += _unitSim.Get_WeatherContextScore( target.Pokemon );

            if( ally != null )
                ourAllyBattlefieldContext += _unitSim.Get_WeatherContextScore( ally.Pokemon );

            if( theirAlly != null )
                theirAllyBattlefieldContext += _unitSim.Get_WeatherContextScore( theirAlly.Pokemon );
        }

        if( isTerrain )
        {
            attackerBattlefieldContext += _unitSim.Get_TerrainContextScore( attacker.Pokemon );
            targetBattlefieldContext += _unitSim.Get_TerrainContextScore( target.Pokemon );

            if( ally != null )
                ourAllyBattlefieldContext += _unitSim.Get_TerrainContextScore( ally.Pokemon );

            if( theirAlly != null )
                theirAllyBattlefieldContext += _unitSim.Get_TerrainContextScore( theirAlly.Pokemon );
        }

        if( isField && effects.FieldCondition == FieldConditionID.TrickRoom )
        {
            attackerBattlefieldContext += _unitSim.Get_TrickRoomContextScore( attacker.Pokemon );
            targetBattlefieldContext += _unitSim.Get_TrickRoomContextScore( target.Pokemon );

            if( ally != null )
                ourAllyBattlefieldContext += _unitSim.Get_TrickRoomContextScore( ally.Pokemon );

            if( theirAlly != null )
                theirAllyBattlefieldContext += _unitSim.Get_TrickRoomContextScore( theirAlly.Pokemon );
        }

        int ourTotalTeamContext = 0;
        foreach( var mon in ourTeam )
        {
            if( isWeather )
                ourTotalTeamContext += _unitSim.Get_WeatherContextScore( mon );

            if( isTerrain )
                ourTotalTeamContext += _unitSim.Get_TerrainContextScore( mon );

            if( isField && effects.FieldCondition == FieldConditionID.TrickRoom )
                ourTotalTeamContext += _unitSim.Get_TrickRoomContextScore( mon );
        }

        int theirTotalTeamContext = 0;
        foreach( var mon in theirTeam )
        {
            if( isWeather )
                theirTotalTeamContext += _unitSim.Get_WeatherContextScore( mon );

            if( isTerrain )
                theirTotalTeamContext += _unitSim.Get_TerrainContextScore( mon );

            if( isField && effects.FieldCondition == FieldConditionID.TrickRoom )
                theirTotalTeamContext += _unitSim.Get_TrickRoomContextScore( mon );
        }

        int battlefieldDelta = ourTotalTeamContext - theirTotalTeamContext;

        //--Reach
        reach += Mathf.RoundToInt( ourTotalTeamContext + ( battlefieldDelta * ourRemainingPercentage ) );

        //--Stability
        //--Compare Team vs Team before and after
        int teamImprovement = 0;
        if( teamAnalAfter.Our_AveragePTKO > teamAnalBefore.Our_AveragePTKO )
        {
            teamImprovement += 20;
        }

        if( teamAnalAfter.Our_BestPTKO > teamAnalBefore.Our_BestPTKO )
        {
            teamImprovement += 10;
        }

        if( teamAnalAfter.Their_AveragePTKO < teamAnalBefore.Their_AveragePTKO )
        {
            teamImprovement += 20;
        }

        if( teamAnalAfter.Their_BestPTKO < teamAnalBefore.Their_BestPTKO )
        {
            teamImprovement += 10;
        }

        if( teamAnalAfter.Our_Outspeeds > teamAnalBefore.Our_Outspeeds )
        {
            teamImprovement += 10;
        }

        if( teamAnalBefore.Our_Outspeeds <= teamAnalBefore.Their_Outspeeds && teamAnalAfter.Our_Outspeeds > teamAnalBefore.Their_Outspeeds )
        {
            teamImprovement += 15;
        }

        stability += Mathf.RoundToInt( teamImprovement + ( 25f * ourRemainingPercentage) );

        //--Reliability
        //--Accuracy
        int acc = move.MoveSO.Accuracy;
        int accuracyScore = 0;
        if( acc < 80 ) accuracyScore -= 10;
        else if( acc < 90 ) accuracyScore -= 5;

        //--Free turn from switch probability
        int switchRead = Mathf.RoundToInt( 20f * attackerVS_Target_Before.OpponentSwitchProbability );

        //--Value from successful execution without prevention or post use punishment/removal
        int successScore = 0;
        if( target.RoleProfile.Traits.Contains( RoleTrait.Taunt ) )
            successScore -= 25;
        else
            successScore += 20;

        if( target.RoleProfile.Traits.Contains( RoleTrait.Encore ) && attackerVS_Target_Before.OpponentMovesFirst )
            successScore -= 10;
        else
            successScore += 10;

        bool theyHaveRemover = false;
        foreach( var mon in theirTeam )
        {
            if( isWeather )
            {
                if( _unitSim.GetWeatherFromAbility( mon ) is var weather && weather != WeatherConditionID.None )
                {
                    if( weather != effects.Weather )
                    {
                        theyHaveRemover = true;
                        break;
                    }
                }

                if( mon.AbilityID == AbilityID.CloudNine )
                {
                    theyHaveRemover = true;
                    break;
                }
            }

            if( isTerrain )
            {
                if( _unitSim.GetTerrainFromAbility( mon ) is var terrain && terrain != TerrainID.None )
                {
                    if( terrain != effects.Terrain )
                    {
                        theyHaveRemover = true;
                        break;
                    }
                }
            }

            if( isField && effects.FieldCondition == FieldConditionID.TrickRoom )
            {
                if( mon.CheckHasActiveMove( "Trick Room" ) )
                {
                    theyHaveRemover = true;
                }
            }
        }

        if( theyHaveRemover )
            successScore -= 10;

        //--How well an active ally can use the buff or the current situation actually calls for it
        int allyUseValue = 0;
        if( ourRemainingPercentage >= 0.5f )
            allyUseValue += 10;
        else
            allyUseValue -= 15;

        if( ally != null )
        {
            if( effects.CourtCondition == CourtConditionID.SafeGuard )
            {
                if( targetRP.PrimaryRole == RoleClass.Disrupter || targetRP.Traits.Contains( RoleTrait.StatusSpreader ) || targetRP.Biases.Contains( RoleBias.Disruptive ) || targetRP.Traits.Contains( RoleTrait.Taunt ) )
                {
                    allyUseValue += 15;

                    if( attacker.Speed > target.Speed || attacker.Ability == AbilityID.Prankster )
                    {
                        allyUseValue +=  10;
                    }
                }
            }
        }

        reliability += accuracyScore + switchRead + successScore + allyUseValue;

        //--Impact
        int currentSituationImprovement = 0;
        int attackerVS_Target_SurvivalPTKODelta = (int)attackerVS_Target_Before.OpponentPTKOR.PTKO - (int)attackerVS_Target_After.OpponentPTKOR.PTKO;
        if( attackerVS_Target_After.AttackerMovesFirst && !attackerVS_Target_Before.AttackerMovesFirst )
        {
            currentSituationImprovement += 10;
        }

        if( attackerVS_Target_After.AttackerPTKOR.PTKO > attackerVS_Target_Before.AttackerPTKOR.PTKO )
        {
            currentSituationImprovement += 10;

            if( attackerVS_Target_After.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous && attackerVS_Target_Before.AttackerPTKOR.PTKO <= PotentialToKO.Risky )
            {
                currentSituationImprovement += 10;

                if( attackerVS_Target_After.AttackerMovesFirst )
                {
                    currentSituationImprovement += 10;
                }
            }
        }

        if( attackerVS_Target_After.OpponentPTKOR.PTKO < attackerVS_Target_Before.OpponentPTKOR.PTKO )
        {
            currentSituationImprovement += 5 * attackerVS_Target_SurvivalPTKODelta;
        }

        if( attackerVS_Target_After.AttackerMovesFirst && !attackerVS_Target_Before.AttackerMovesFirst )
        {
            currentSituationImprovement += 10;
        }

        if( theirAlly != null )
        {
            int attackerVS_TheirAlly_SurvivalPTKODelta = (int)attackerVS_TheirAlly_Before.OpponentPTKOR.PTKO - (int)attackerVS_TheirAlly_After.OpponentPTKOR.PTKO;
            if( attackerVS_TheirAlly_After.AttackerMovesFirst && !attackerVS_TheirAlly_Before.AttackerMovesFirst )
            {
                currentSituationImprovement += 20;
            }

            if( attackerVS_TheirAlly_After.AttackerPTKOR.PTKO > attackerVS_TheirAlly_Before.AttackerPTKOR.PTKO )
            {
                currentSituationImprovement += 20;

                if( attackerVS_TheirAlly_After.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous && attackerVS_TheirAlly_Before.AttackerPTKOR.PTKO <= PotentialToKO.Risky )
                {
                    currentSituationImprovement += 10;

                    if( attackerVS_TheirAlly_After.AttackerMovesFirst )
                    {
                        currentSituationImprovement += 5;
                    }
                }
            }

            if( attackerVS_TheirAlly_After.OpponentPTKOR.PTKO < attackerVS_TheirAlly_Before.OpponentPTKOR.PTKO )
            {
                currentSituationImprovement += 10 * attackerVS_TheirAlly_SurvivalPTKODelta;
            }

            if( attackerVS_TheirAlly_After.AttackerMovesFirst && !attackerVS_TheirAlly_Before.AttackerMovesFirst )
            {
                currentSituationImprovement += 20;
            }
        }

        if( ally != null )
        {
            int allyVS_Target_SurvivalPTKODelta = (int)allyVS_Target_Before.OpponentPTKOR.PTKO - (int)allyVS_Target_After.OpponentPTKOR.PTKO;
            if( allyVS_Target_After.AttackerMovesFirst && !allyVS_Target_Before.AttackerMovesFirst )
            {
                currentSituationImprovement += 20;
            }

            if( allyVS_Target_After.AttackerPTKOR.PTKO > allyVS_Target_Before.AttackerPTKOR.PTKO )
            {
                currentSituationImprovement += 20;

                if( allyVS_Target_After.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous && allyVS_Target_Before.AttackerPTKOR.PTKO <= PotentialToKO.Risky )
                {
                    currentSituationImprovement += 10;

                    if( allyVS_Target_After.AttackerMovesFirst )
                    {
                        currentSituationImprovement += 5;
                    }
                }
            }

            if( allyVS_Target_After.OpponentPTKOR.PTKO < allyVS_Target_Before.OpponentPTKOR.PTKO )
            {
                currentSituationImprovement += 10 * allyVS_Target_SurvivalPTKODelta;
            }

            if( allyVS_Target_After.AttackerMovesFirst && !allyVS_Target_Before.AttackerMovesFirst )
            {
                currentSituationImprovement += 20;
            }

            if( theirAlly != null )
            {
                int allyVS_TheirAlly_SurvivalPTKODelta = (int)allyVS_TheirAlly_Before.OpponentPTKOR.PTKO - (int)allyVS_TheirAlly_After.OpponentPTKOR.PTKO;
                if( allyVS_TheirAlly_After.AttackerMovesFirst && !allyVS_TheirAlly_Before.AttackerMovesFirst )
                {
                    currentSituationImprovement += 20;
                }

                if( allyVS_TheirAlly_After.AttackerPTKOR.PTKO > allyVS_TheirAlly_Before.AttackerPTKOR.PTKO )
                {
                    currentSituationImprovement += 20;

                    if( allyVS_TheirAlly_After.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous && allyVS_TheirAlly_Before.AttackerPTKOR.PTKO <= PotentialToKO.Risky )
                    {
                        currentSituationImprovement += 10;

                        if( allyVS_TheirAlly_After.AttackerMovesFirst )
                        {
                            currentSituationImprovement += 5;
                        }
                    }
                }

                if( allyVS_TheirAlly_After.OpponentPTKOR.PTKO < allyVS_TheirAlly_Before.OpponentPTKOR.PTKO )
                {
                    currentSituationImprovement += 10 * allyVS_TheirAlly_SurvivalPTKODelta;
                }

                if( allyVS_TheirAlly_After.AttackerMovesFirst && !allyVS_TheirAlly_Before.AttackerMovesFirst )
                {
                    currentSituationImprovement += 20;
                }
            }
        }

        impact += Mathf.RoundToInt( currentSituationImprovement + ( teamImprovement * theirRemainingPercentage ) );

        //--Unique
        unique += attackerBattlefieldContext + ourAllyBattlefieldContext + battlefieldDelta;

        //--Final Tally
        int finalScore = reach + stability + reliability + impact + unique;

        return new()
        {
            CandidateScore = finalScore,
            StrategicReach = reach,
            BoardStability = stability,
            Reliability = reliability,
            Impact = impact,
            Unique = unique,
            TotalValue = finalScore,
        };
    }

    private StatusValue ScoreAllyProtection( IBattleAIUnit attacker, IBattleAIUnit target, Move move )
    {
        int reach = 0;
        int stability = 0;
        int reliability = 0;
        int impact = 0;
        int unique = 0;

        //--Doubles ally check
        IBattleAIUnit ally = null;
        if( _ai.IsDoubleBattle )
            ally = _ai.GetActiveAllyAs_Adapter( attacker.Pokemon );

        if( ally == null )
            return new();

        var fieldBefore = _unitSim.BuildSimField();
        var fieldAfter = _unitSim.BuildSimField();

        //--Attacker Information
        var attackerRP = attacker.RoleProfile;
        var attackerPR = attackerRP.PrimaryRole;
        var attTraits = attackerRP.Traits;
        var gp = _ai.Blackboard.GamePlan;

        //--Ally Information
        var allyRP = ally.RoleProfile;
        var allyBiases = allyRP.Biases;
        var allyTraits = allyRP.Traits;

        bool allyIsOffensive = allyRP.PrimaryRole == RoleClass.BulkyAttacker || allyRP.PrimaryRole == RoleClass.RevengeKiller || allyRP.PrimaryRole == RoleClass.SetupSweeper ||
            allyRP.PrimaryRole == RoleClass.Sweeper || allyRP.PrimaryRole == RoleClass.TrickRoomAbuser || allyRP.PrimaryRole == RoleClass.WallBreaker;

        //--Target Information
        var targetRP = target.RoleProfile;

        //--Move Information
        var moveTarget = move.MoveSO.MoveTarget;
        var effects = move.MoveSO.MoveEffects;
        bool isAllyHeal = move.MoveSO.HealType != HealType.None && moveTarget == MoveTarget.Ally;
        bool isSideHeal = move.MoveSO.HealType != HealType.None && moveTarget == MoveTarget.AllySide;
        bool isRedirection = effects.TransientStatus == TransientConditionID.CenterOfAttention;

        //--Apply Effect
        IBattleAIUnit attackerAfter = _unitSim.CopySimUnit( attacker, fieldBefore );
        ApplySupportEffect( attackerAfter, move, fieldAfter );

        //--Exchange Information
        var attackerVS_Target_Before = _ai.Projection.EvaluateExchange( attacker, target );
        var attackerVS_Target_After = _ai.Projection.EvaluateExchange( attackerAfter, target );

        ExchangeEvaluation allyVS_Target_Before = default;
        ExchangeEvaluation allyVS_Target_After = default;
        if( ally != null )
        {
            allyVS_Target_Before = _proj.EvaluateExchange( ally, target );

            IBattleAIUnit allyAfter = _unitSim.CopySimUnit( ally, fieldBefore );
            var tempField = _unitSim.BuildSimField();
            ApplySupportEffect( allyAfter, move, tempField );

            allyVS_Target_After = _proj.EvaluateExchange( allyAfter, target );
        }

        IBattleAIUnit theirAlly = null;
        if( _ai.IsDoubleBattle )
            theirAlly = _ai.GetActiveAllyAs_Adapter( target.Pokemon );

        ExchangeEvaluation attackerVS_TheirAlly_Before = default;
        ExchangeEvaluation attackerVS_TheirAlly_After = default;

        ExchangeEvaluation allyVS_TheirAlly_Before = default;
        ExchangeEvaluation allyVS_TheirAlly_After = default;
        if( theirAlly != null )
        {
            attackerVS_TheirAlly_Before = _proj.EvaluateExchange( attacker, theirAlly );

            IBattleAIUnit theirAllyAfter = _unitSim.CopySimUnit( theirAlly, fieldBefore );
            var tempField = _unitSim.BuildSimField();
            ApplySupportEffect( theirAllyAfter, move, tempField );

            attackerVS_TheirAlly_After = _proj.EvaluateExchange( attacker, theirAllyAfter );

            if( ally != null )
            {
                allyVS_TheirAlly_Before = _proj.EvaluateExchange( ally, theirAlly );

                IBattleAIUnit allyAfter = _unitSim.CopySimUnit( ally, fieldBefore );
                var tempField2 = _unitSim.BuildSimField();
                ApplySupportEffect( allyAfter, move, tempField2 );

                allyVS_TheirAlly_After = _proj.EvaluateExchange( allyAfter, theirAlly );
            }
        }

        //--Reach
        bool allyIsValuable = gp.OurPrimaryWinCon == ally.Pokemon || gp.OurBlockers.Contains( ally.Pokemon );
        bool allySetsTeamUp = allyTraits.Contains( RoleTrait.TailwindSetter ) || allyTraits.Contains( RoleTrait.WeatherSetter ) || allyTraits.Contains( RoleTrait.TrickRoomSetter );

        int allyImportance = 0;

        if( allyIsValuable )
        {
            allyImportance += 15;
        }

        if( allySetsTeamUp )
        {
            allyImportance += 15;
        }

        if( allyIsOffensive )
        {
            allyImportance += 10;
        }

        if( ally.StatStages?.Count > 0 )
        {
            foreach( var sc in ally.StatStages )
            {
                if( sc.Value > 0 )
                {
                    allyImportance += sc.Value * 5;
                }
            }
        }

        reach += allyImportance;

        //--Stability
        float ourDamageFromTarget = _proj.Get_PTKODamagePercent( attackerVS_Target_Before.OpponentPTKOR.PTKO );
        float ourDamageFromTheirAlly = _proj.Get_PTKODamagePercent( attackerVS_TheirAlly_Before.OpponentPTKOR.PTKO );
        float ourHPRAfter = ( (float)move.MoveSO.HealAmount / 100f ) + attacker.BeginningHPR;

        int ourHitsFromTargetBefore = Mathf.CeilToInt( attacker.BeginningHPR / ourDamageFromTarget );
        int ourHitsFromTargetAfter = Mathf.CeilToInt( ourHPRAfter / ourDamageFromTarget );

        int ourHitsFromTheirAllyBefore = Mathf.CeilToInt( attacker.BeginningHPR / ourDamageFromTheirAlly );
        int ourHitsFromTheirAllyAfter = Mathf.CeilToInt( ourHPRAfter / ourDamageFromTheirAlly );

        float allyDamageFromTarget = _proj.Get_PTKODamagePercent( allyVS_Target_Before.OpponentPTKOR.PTKO );
        float allyDamageFromTheirAlly = _proj.Get_PTKODamagePercent( allyVS_TheirAlly_Before.OpponentPTKOR.PTKO );
        float allyHPRAfter = ( (float)move.MoveSO.HealAmount / 100f ) + ally.BeginningHPR;

        int allyHitsFromTargetBefore = Mathf.CeilToInt( ally.BeginningHPR / allyDamageFromTarget );
        int allyHitsFromTargetAfter = Mathf.CeilToInt( allyHPRAfter / allyDamageFromTarget );

        int allyHitsFromTheirAllyBefore = Mathf.CeilToInt( ally.BeginningHPR / allyDamageFromTheirAlly );
        int allyHitsFromTheirAllyAfter = Mathf.CeilToInt( allyHPRAfter / allyDamageFromTheirAlly );

        int allyStability = 0;
        int sideStability = 0;

        if( isAllyHeal )
        {
            if( allyHitsFromTargetAfter < allyHitsFromTargetBefore )
            {
                allyStability += 10;
                sideStability += 5;

                if( allyHitsFromTargetBefore <= 0 )
                {
                    allyStability += 10;
                }
            }

            if( theirAlly != null )
            {
                if( allyHitsFromTheirAllyAfter < allyHitsFromTheirAllyBefore )
                {
                    allyStability += 10;
                    sideStability += 5;

                    if( allyHitsFromTheirAllyBefore <= 0 )
                    {
                        allyStability += 10;
                    }
                }
            }
        }

        if( isSideHeal )
        {
            if( allyHitsFromTargetAfter < allyHitsFromTargetBefore )
            {
                allyStability += 10;
                sideStability += 5;

                if( allyHitsFromTargetBefore <= 0 )
                {
                    allyStability += 10;
                }
            }

            if( ourHitsFromTargetAfter < ourHitsFromTargetBefore )
            {
                allyStability += 5;
                sideStability += 10;

                if( ourHitsFromTargetBefore <= 0 )
                {
                    sideStability += 10;
                }
            }

            if( theirAlly != null )
            {
                if( allyHitsFromTheirAllyAfter < allyHitsFromTheirAllyBefore )
                {
                    allyStability += 10;
                    sideStability += 5;

                    if( allyHitsFromTheirAllyBefore <= 0 )
                    {
                        allyStability += 10;
                    }
                }

                if( ourHitsFromTheirAllyAfter < ourHitsFromTheirAllyBefore )
                {
                    allyStability += 5;
                    sideStability += 10;

                    if( ourHitsFromTheirAllyBefore <= 0 )
                    {
                        sideStability += 10;
                    }
                }
            }
        }

        if( isRedirection )
        {
            if( allyHitsFromTargetBefore <= 2 )
            {
                allyStability += 10;

                if( ourHitsFromTargetBefore > 1 )
                {
                    sideStability += 5;
                }
            }

            if( theirAlly != null )
            {
                if( allyHitsFromTheirAllyBefore <= 2 )
                {
                    allyStability += 10;

                    if( ourHitsFromTheirAllyBefore > 1 )
                    {
                        sideStability += 5;
                    }
                }

                if( ourHitsFromTheirAllyBefore > 1 && ourHitsFromTargetBefore > 1 )
                {
                    sideStability += 5;
                }
            }
        }

        stability += allyStability + sideStability;

        //--Reliability
        //--Accuracy
        int acc = move.MoveSO.Accuracy;
        int accuracyScore = 0;
        if( acc < 80 ) accuracyScore -= 10;
        else if( acc < 90 ) accuracyScore -= 5;

        int switchRead = 0;
        switchRead += Mathf.RoundToInt( 10f * attackerVS_Target_Before.OpponentSwitchProbability );

        if( theirAlly != null )
            switchRead += Mathf.RoundToInt( 10f * attackerVS_TheirAlly_Before.OpponentSwitchProbability );

        int successScore = 0;
        if( target.RoleProfile.Traits.Contains( RoleTrait.Taunt ) )
            successScore -= 25;
        else
            successScore += 20;

        if( target.RoleProfile.Traits.Contains( RoleTrait.Encore ) && attackerVS_Target_Before.OpponentMovesFirst )
            successScore -= 10;
        else
            successScore += 10;

        if( theirAlly != null )
        {
            if( theirAlly.RoleProfile.Traits.Contains( RoleTrait.Taunt ) )
                successScore -= 10;
            else
                successScore += 5;

            if( theirAlly.RoleProfile.Traits.Contains( RoleTrait.Encore ) && attackerVS_TheirAlly_Before.OpponentMovesFirst )
                successScore -= 5;
            else
                successScore += 5;
        }

        bool targetUsedSpread = target.MTR?.Move?.MoveSO.MoveTarget == MoveTarget.OpposingSide || target.MTR?.Move?.MoveSO.MoveTarget == MoveTarget.AllAdjacent;

        if( isRedirection && targetUsedSpread )
        {
            successScore -= 20;
        }

        if( isSideHeal && targetUsedSpread )
        {
            successScore += 5;
        }

        int allyValue = 0;
        if( isAllyHeal )
        {
            allyValue += Mathf.RoundToInt( 30f * ( 1f - ally.BeginningHPR ) );
            allyValue += ally.BeginningHPR <= 0.25f ? 20 : ally.BeginningHPR <= 0.5f ? 15 : ally.BeginningHPR <= 0.75f ? 5 : 0;
        }

        if( isSideHeal )
        {
            allyValue += Mathf.RoundToInt( 30f * ( 1f - attacker.BeginningHPR ) );
            allyValue += attacker.BeginningHPR <= 0.25f ? 20 : attacker.BeginningHPR <= 0.5f ? 15 : attacker.BeginningHPR <= 0.75f ? 5 : 0;

            allyValue += Mathf.RoundToInt( 30f * ( 1f - ally.BeginningHPR ) );
            allyValue += ally.BeginningHPR <= 0.25f ? 20 : ally.BeginningHPR <= 0.5f ? 15 : ally.BeginningHPR <= 0.75f ? 5 : 0;
        }
        
        if( isRedirection )
        {
            if( allyVS_Target_Before.OpponentMovesFirst )
            {
                allyValue += 10;
            }

            if( theirAlly != null && allyVS_TheirAlly_Before.OpponentMovesFirst )
            {
                allyValue += 10;
            }
        }

        reliability += accuracyScore + switchRead + successScore + allyValue;

        //--Impact
        //--Current Situation Improvement should directly take into account any healing done. Healing is applied in ApplySupportEffect(), so all "after" exchange evaluations should be done with the healing in tact. this should theoretically change incoming PTKOs.
        int currentSituationImprovement = 0;

        if( isSideHeal || isAllyHeal )
        {
            int attackerVS_Target_SurvivalPTKODelta = (int)attackerVS_Target_Before.OpponentPTKOR.PTKO - (int)attackerVS_Target_After.OpponentPTKOR.PTKO;

            if( attackerVS_Target_After.OpponentPTKOR.PTKO < attackerVS_Target_Before.OpponentPTKOR.PTKO )
            {
                currentSituationImprovement += 5 * attackerVS_Target_SurvivalPTKODelta;
            }

            if( attackerVS_Target_After.AttackerMovesFirst && !attackerVS_Target_Before.AttackerMovesFirst )
            {
                currentSituationImprovement += 10;
            }

            if( theirAlly != null )
            {
                int attackerVS_TheirAlly_SurvivalPTKODelta = (int)attackerVS_TheirAlly_Before.OpponentPTKOR.PTKO - (int)attackerVS_TheirAlly_After.OpponentPTKOR.PTKO;

                if( attackerVS_TheirAlly_After.OpponentPTKOR.PTKO < attackerVS_TheirAlly_Before.OpponentPTKOR.PTKO )
                {
                    currentSituationImprovement += 10 * attackerVS_TheirAlly_SurvivalPTKODelta;
                }

                if( attackerVS_TheirAlly_After.AttackerMovesFirst && !attackerVS_TheirAlly_Before.AttackerMovesFirst )
                {
                    currentSituationImprovement += 20;
                }
            }

            if( ally != null )
            {
                int allyVS_Target_SurvivalPTKODelta = (int)allyVS_Target_Before.OpponentPTKOR.PTKO - (int)allyVS_Target_After.OpponentPTKOR.PTKO;

                if( allyVS_Target_After.OpponentPTKOR.PTKO < allyVS_Target_Before.OpponentPTKOR.PTKO )
                {
                    currentSituationImprovement += 10 * allyVS_Target_SurvivalPTKODelta;
                }

                if( allyVS_Target_After.AttackerMovesFirst && !allyVS_Target_Before.AttackerMovesFirst )
                {
                    currentSituationImprovement += 20;
                }

                if( theirAlly != null )
                {
                    int allyVS_TheirAlly_SurvivalPTKODelta = (int)allyVS_TheirAlly_Before.OpponentPTKOR.PTKO - (int)allyVS_TheirAlly_After.OpponentPTKOR.PTKO;

                    if( allyVS_TheirAlly_After.OpponentPTKOR.PTKO < allyVS_TheirAlly_Before.OpponentPTKOR.PTKO )
                    {
                        currentSituationImprovement += 10 * allyVS_TheirAlly_SurvivalPTKODelta;
                    }

                    if( allyVS_TheirAlly_After.AttackerMovesFirst && !allyVS_TheirAlly_Before.AttackerMovesFirst )
                    {
                        currentSituationImprovement += 20;
                    }
                }
            }
        }

        if( isRedirection )
        {
            var allyPTKO_target = allyVS_Target_Before.AttackerPTKOR.PTKO;
            var targetPTKO_ally = allyVS_Target_Before.OpponentPTKOR.PTKO;
            
            if( targetPTKO_ally >= PotentialToKO.TwoHKO )
            {
                currentSituationImprovement += 10;

                if( targetPTKO_ally >= PotentialToKO.Dangerous )
                {
                    currentSituationImprovement += 10;
                }

                if( allyPTKO_target >= PotentialToKO.Risky )
                {
                    currentSituationImprovement += 10;
                }
            }

            if( theirAlly != null )
            {
                var allyPTKO_theirAlly = allyVS_TheirAlly_Before.AttackerPTKOR.PTKO;
                var theirAllyPTKO_ally = allyVS_TheirAlly_Before.OpponentPTKOR.PTKO;

                if( theirAllyPTKO_ally >= PotentialToKO.TwoHKO )
                {
                    currentSituationImprovement += 10;

                    if( theirAllyPTKO_ally >= PotentialToKO.Dangerous )
                    {
                        currentSituationImprovement += 10;
                    }

                    if( allyPTKO_theirAlly >= PotentialToKO.Risky )
                    {
                        currentSituationImprovement += 10;
                    }
                }
            }
        }

        impact += currentSituationImprovement;

        //--Unique
        if( isAllyHeal || isSideHeal )
        {
            if( ally.Ability == AbilityID.Regenerator )
            {
                unique += 5;
            }

            if( allyRP.PrimaryRole == RoleClass.Wall )
            {
                unique += 5;
            }

            if( isSideHeal )
            {
                //--this function does not run if we do not have an ally, meaning it never runs in singles, therefore it must always be doubles to get here
                if( _ai.IsDoubleBattle )
                    unique += 10;

                if( attackerRP.PrimaryRole == RoleClass.Wall )
                    unique += 5;
            }

            if( allyIsOffensive && ally.BeginningHPR >= 0.5f && ally.Speed < attacker.Speed && ( allyVS_Target_Before.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous || ( theirAlly != null && allyVS_TheirAlly_Before.AttackerPTKOR.PTKO >= PotentialToKO.Dangerous ) ) )
            {
                unique += 10;
            }
        }

        if( isRedirection )
        {
            if( targetUsedSpread )
            {
                unique -= 10;
            }

            if( move.MoveSO.Flags.Contains( MoveFlags.Powder ) )
            {
                if( targetRP.Traits.Contains( RoleTrait.PowderImmune ) )
                {
                    unique -= 20;
                }

                if( theirAlly != null && theirAlly.RoleProfile.Traits.Contains( RoleTrait.PowderImmune ) )
                {
                    unique -= 20;
                }
            }

            if( allyRP.PrimaryRole == RoleClass.SetupSweeper || allyRP.SecondaryRoles.Contains( RoleClass.SetupSweeper ) )
            {
                unique += 15;
            }

            if( allyTraits.Contains( RoleTrait.TailwindSetter ) || allyTraits.Contains( RoleTrait.TrickRoomSetter ) )
            {
                unique += 15;
            }

            if( ally.Item == BattleItemEffectID.FocusSash )
            {
                unique += 10;
            }

            bool allyRedirectsWater = ally.Ability == AbilityID.StormDrain || ally.Ability == AbilityID.WaterAbsorb;
            bool targetUsesWater = target.MTR?.Move?.MoveType == PokemonType.Water;

            bool allyRedirectsElec = ally.Ability == AbilityID.LightningRod || ally.Ability == AbilityID.VoltAbsorb;
            bool targetUsesElec = target.MTR?.Move?.MoveType == PokemonType.Electric;
            if( ally.Ability == AbilityID.WeakArmor || ( allyRedirectsWater && targetUsesWater ) || ( allyRedirectsElec && targetUsesElec ) )
            {
                unique -= 10;
            }
        }

        //--Final Tally
        int finalScore = reach + stability + reliability + impact + unique;

        return new()
        {
            CandidateScore = finalScore,
            StrategicReach = reach,
            BoardStability = stability,
            Reliability = reliability,
            Impact = impact,
            Unique = unique,
            TotalValue = finalScore,
        };
    }
}
