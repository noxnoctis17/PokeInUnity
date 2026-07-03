using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum OffensiveStatusType { None, StatusEffect, Disruption, EntryHazard, StatDebuff, Binding, Phaze }
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
                for( int t = 0; t < _ai.TheirActiveBattleAIUnits.Count; t++ )
                {
                    var tar = _ai.GetBattleUnit( _ai.TheirActiveBattleAIUnits[t].Pokemon );
                    targets.Add( tar );
                }
            }
            else if( move.MoveSO.MoveTarget == MoveTarget.AllAdjacent )
            {
                for( int t = 0; t < _ai.TheirActiveBattleAIUnits.Count; t++ )
                {
                    var tar = _ai.GetBattleUnit( _ai.TheirActiveBattleAIUnits[t].Pokemon );
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

        StatusThreatResult best = new()
        {
            Type = ActionResultType.Move,
            ActionType = ActionType.OffensiveStatus,
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
            StatusType = bestType,
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
            TotalValue = finalValue,
        };
    }
}
