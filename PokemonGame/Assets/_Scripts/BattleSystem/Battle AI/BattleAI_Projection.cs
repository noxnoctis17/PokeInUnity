using System;
using System.CodeDom;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
using UnityEngine;

public class BattleAI_Projection
{
    private readonly BattleAI _ai;
    private readonly BattleAI_UnitSim _unitSim;
    
    
    public BattleAI_Projection( BattleAI ai )
    {
        _ai = ai;
        _unitSim = _ai.UnitSim;
    }

    public ProjectedBoardState BuildProjectedBoardState( TurnOutcomeProjection top, ExchangeEvaluation eval, TempoStateResult tempo, int myRemainingPieces, int oppRemainingPieces )
    {
        bool iAmKO = false;
        bool oppIsKO = false;

        TempoState tempoState = tempo.TempoState;

        _ai.CurrentLog.Add( $"===[Displaying Simulation Log from Chosen Action for below PBS logs]===" );
        _ai.CurrentLog.Add( top.SimulationLog );

        if( top.Attacker_EndOfTurnHP <= 0 )
        {
            myRemainingPieces--;
            iAmKO = true;
            _ai.CurrentLog.Add( $"Attacker Fainted! My remaining pieces reduced from {myRemainingPieces + 1} to {myRemainingPieces}! Attacker KO is {iAmKO}." );
        }

        if( top.Opponent_EndOfTurnHP <= 0 )
        {
            oppRemainingPieces--;
            oppIsKO = true;
            _ai.CurrentLog.Add( $"Opponent Fainted! Opponent's remaining pieces reduced from {oppRemainingPieces + 1} to {oppRemainingPieces}! Opponent KO is {oppIsKO}." );
        }

        //--Turn Economy
        int myTurnsRemaining = 0;
        int oppTurnsRemaining = 0;

        //--My Turns
        if( eval.AttackerMovesFirst )
            myTurnsRemaining = top.Attacker_EndOfTurnHP > 0 ? 1 : 0;
        else
            myTurnsRemaining = eval.AttackerSurvives ? 1 : 0;

        //--Opponent Turns
        if( eval.OpponentMovesFirst )
            oppTurnsRemaining = top.Opponent_EndOfTurnHP > 0 ? 1 : 0;
        else
            oppTurnsRemaining = eval.OpponentSurvives ? 1 : 0;

        _ai.CurrentLog.Add( $"[Build PBS] My Turns Remaining {myTurnsRemaining}. Opponent Turns Remaining: {oppTurnsRemaining}" );

        //--Post Loss Revenge Quality
        int revengeScore = 0;
        if( iAmKO && !oppIsKO )
        {
            List<IBattleAIUnit> opps = new() { top.Opponent };
            var revengeKiller = _ai.SwitchCommand.GetSwitch_Revenge( opps );
            if( revengeKiller.Top.Opponent_DiesBeforeActing )
            {
                revengeScore += 45;
                _ai.CurrentLog.Add( $"[Build PBS] Revenge Score: {revengeScore}" );
            }
            else if( revengeKiller.Top.AttackerPTKO >= PotentialToKO.Dangerous && revengeKiller.Top.OpponentPTKO <= PotentialToKO.Risky )
            {
                revengeScore += 25;
                _ai.CurrentLog.Add( $"[Build PBS] Revenge Score: {revengeScore}" );
            }
            else if( revengeKiller.Top.AttackerMovedFirst && revengeKiller.Top.AttackerPTKO >= PotentialToKO.Safe )
            {
                revengeScore += 15;
                _ai.CurrentLog.Add( $"[Build PBS] Revenge Score: {revengeScore}" );
            }
        }

        return new()
        {
            MyHP_AfterTurn = top.Attacker_EndOfTurnHP,
            OppHP_AfterTurn = top.Opponent_EndOfTurnHP,

            MyTurnsRemaining = myTurnsRemaining,
            OpponentTurnsRemaining = oppTurnsRemaining,

            IAmKO = iAmKO,
            OppIsKO = oppIsKO,
            MutualKO = top.MutualKO,

            AttackerThreatensKO = eval.AttackerThreatensKO,
            OpponentThreatensKO = eval.OpponentThreatensKO,

            AttackerKillsFirst = eval.AttackerKillsFirst,
            OpponentKillsFirst = eval.OpponentKillsFirst,

            AttackerMovesFirst = eval.AttackerMovesFirst,
            OpponentMovesFirst = eval.OpponentMovesFirst,

            MyRemainingPieces = myRemainingPieces,
            OppRemainingPieces = oppRemainingPieces,
            MaterialDelta = myRemainingPieces - oppRemainingPieces,

            RevengeScore = revengeScore,
            TempoState = tempoState,
        };
    }

    public int EvaluatePBS( ProjectedBoardState pbs )
    {
        const int MATERIAL_WEIGHT = 50;
        const int TEMPO_WEIGHT = 50;
        const int TURN_WEIGHT = 45;
        const int THREAT_WEIGHT = 30;

        int score = 0;

        //--Material. Material is currently considered the most important resource.
        if( pbs.IAmKO || pbs.OppIsKO )
        {
            score += pbs.MaterialDelta * MATERIAL_WEIGHT;
            _ai.CurrentLog.Add( $"[Evaluate PBS] Material Delta: {pbs.MaterialDelta}. Score: {score}" );
        }

        int tempoScore = pbs.TempoState switch
        {
            TempoState.WinningHard  => +60,
            TempoState.Winning      => +35,
            TempoState.Neutral      => 0,
            TempoState.Losing       => -35,
            TempoState.LosingHard   => -60,
            _ => 0
        };

        score += tempoScore;

        _ai.CurrentLog.Add( $"[Evaluate PBS] TempoState: {pbs.TempoState} → {tempoScore}. Score: {score}" );

        int turnDelta = pbs.MyTurnsRemaining - pbs.OpponentTurnsRemaining;
        int turnScore = turnDelta * TURN_WEIGHT;

        score += turnScore;

        _ai.CurrentLog.Add( $"[Evaluate PBS] Turn Delta: {turnDelta} → {turnScore}. Score: {score}" );

        int threatScore = 0;

        //--My threat
        if( !pbs.IAmKO )
        {
            if( pbs.AttackerThreatensKO )
                threatScore += 25;

            if( pbs.AttackerMovesFirst )
                threatScore += 15;

            if( pbs.AttackerKillsFirst )
                threatScore += 40;
        }

        //--Opponent threat. We subtract the score here to give an opposing direction for opponent's threat.
        if( !pbs.OppIsKO )
        {
            if( pbs.OpponentThreatensKO )
                threatScore -= 25;

            if( pbs.OpponentMovesFirst )
                threatScore -= 15;

            if( pbs.OpponentKillsFirst )
                threatScore -= 40;
        }

        score += threatScore;

        _ai.CurrentLog.Add( $"[Evaluate PBS] Threat Score: {threatScore}. Score: {score}" );


        if( pbs.IAmKO && !pbs.OppIsKO )
        {
            score += pbs.RevengeScore;
            _ai.CurrentLog.Add( $"[Evaluate PBS] We're going to faint! Revenge Score: {pbs.RevengeScore}. Score: {score}" );
        }

        if( pbs.IAmKO && pbs.OppIsKO )
        {
            if( pbs.MaterialDelta < 0 )
                score += 10; //--Trade while behind good
            else if( pbs.MaterialDelta > 0 )
                score -= 10; //--Trade while ahead bad
        }

        return score;
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
        float targetHP = _ai.Get_HPRatio( target );

        PotentialToKOResult attackerPTKO_target = Get_PotentialToKOResult( targetWSR, attackerMTR, targetHP );

        //--Target PTKO Attacker
        var targetMTR = _ai.MoveCommand.GetMove_BestAttack( target, attacker, "Evaluate Exchange (target vs attacker)" );
        var attackerWSR = Get_EstimatedDamageResult( target, attacker, targetMTR );
        float attackerHP = _ai.Get_HPRatio( attacker );

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
        if( attackerSpeed > targetSpeed )
        {
            if( attMovePrio > tarMovePrio )
            {
                attackerMovesFirst = true;
                targetMovesFirst = false;
            }
            else if( tarMovePrio > attMovePrio )
            {
                attackerMovesFirst = false;
                targetMovesFirst = true;
            }
            else
            {
                attackerMovesFirst = true;
                targetMovesFirst = false;
            }
        }
        else
        {
            if( attMovePrio > tarMovePrio )
            {
                attackerMovesFirst = true;
                targetMovesFirst = false;

            }
            else if( tarMovePrio > attMovePrio )
            {
                attackerMovesFirst = false;
                targetMovesFirst = true;
            }
            else
            {
                attackerMovesFirst = false;
                targetMovesFirst = true;
            }
        }

        // Debug.Log( $"[AI Scoring][Get Tempo] Made speed comparisons! Results: Attacker Speed: {attackerSpeed}, Target Speed: {targetSpeed}, Attacker Priority: {attackerHasPriorityAdvantage}, Target Priority: {targetHasPriorityAdvantage}, Attacker Moves First: {attackerMovesFirst}, Target Moves First: {targetMovesFirst}" );

        bool attackerThreatensKO_onTarget       = attackerPTKO_target.PTKO > PotentialToKO.Risky; //--revert back to >= if not good
        bool targetThreatensKO_onAttacker       = targetPTKO_attacker.PTKO > PotentialToKO.Risky; //--revert back to >= if not good
        bool attackerSurvives_targetAttack      = targetPTKO_attacker.PTKO <= PotentialToKO.Risky;
        bool targetSurvives_attackerAttack      = attackerPTKO_target.PTKO <= PotentialToKO.Risky;

        // Debug.Log( $"[AI Scoring][Get Tempo] Final Comparisons Made! Results: Attacker Threatens KO: {attackerThreatensKO_onTarget}, Target Threatens KO: {targetThreatensKO_onAttacker}, Attacker Survives: {attackerSurvives_targetAttack}, Target Survives: {targetSurvives_attackerAttack}" );
        
        //--Predict Forced Switch for this turn
        // bool attackerForcesSwitch = _unitSim.PredictForcedSwitch( attackerPTKO_target.PTKO, targetPTKO_attacker.PTKO, attackerMovesFirst );
        // bool targetForcesSwitch = _unitSim.PredictForcedSwitch( targetPTKO_attacker.PTKO, attackerPTKO_target.PTKO, targetMovesFirst );
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

    public ExchangeEvaluation EvaluateExchange( SimulatedUnit attacker, SimulatedUnit opponent, TurnOutcomeProjection top )
    {
        return new()
        {
            AttackerMovesFirst = attacker.Speed > opponent.Speed,
            OpponentMovesFirst = opponent.Speed < attacker.Speed,

            AttackerKillsFirst = top.Opponent_DiesBeforeActing,
            OpponentKillsFirst = top.Attacker_DiesBeforeActing,

            AttackerSurvives = attacker.CurrentHPR > 0,
            OpponentSurvives = opponent.CurrentHPR > 0,
        };
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
        BattleAI_PokemonAdapter ourAdapter = new( _ai.Unit.Pokemon, _ai );
        var safePivot = GetSafePivot( target );
        var materialStatus = GetMaterialStatus( ourAdapter );

        bool lowHP = eval.AttackerHPR < 0.3f;
        bool likelyDying = eval.OpponentPTKOR.PTKO >= PotentialToKO.Dangerous;

        bool isForced = ( likelyDying && !safePivot.Exists ) || ( lowHP && eval.OpponentPTKOR.PTKO >= PotentialToKO.Risky );

        int myAlive = _ai.GetRemainingAllyPokemon( ourAdapter.PID ).Count;
        int oppAlive = _ai.GetRemainingOpposingPokemon( target.PID ).Count;

        bool isTerminal = myAlive <= 2;

        float hp = _ai.Get_HPRatio( _ai.Unit.Pokemon );
        float expendability = GetExpendability( _ai.ThisUnitAdapter, hp );

        BoardContext context = new()
        {
            IsForcedTrade = isForced,

            HasSafePivot = safePivot.Exists,
            SafePivots = safePivot.Pivots,

            IsAhead = materialStatus.IsAhead,
            IsBehind = materialStatus.IsBehind,

            MyTeamHPPercent = materialStatus.MyTeamHPPercent,
            OppTeamHPPercent = materialStatus.OppTeamHPPercent,

            MyAliveCount = myAlive,
            OppAliveCount = oppAlive,
            IsTerminal = isTerminal,

            MyExpendability = expendability,
        };

        return context;
    }

    private ( bool Exists, List<Pokemon> Pivots ) GetSafePivot( IBattleAIUnit opponent )
    {
        bool exists;
        List<Pokemon> pivots = new();
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

                    if( pivotPTKO_target.PTKO < PotentialToKO.Dangerous )
                        pivots.Add( mon );
                    else
                        continue;
                }
            }
        }

        exists = pivots.Count > 0;

        return ( exists, pivots );
    }

    public MaterialStatus GetMaterialStatus( IBattleAIUnit pokemon )
    {
        //--My team & amount of pokemon alive
        var myTeam = _ai.BattleSystem.GetAllyParty( pokemon.PID );
        int myAlive = _ai.GetRemainingAllyPokemon( pokemon.PID ).Count;

        //--Opposing team & amount of their pokemon alive
        var oppTeam = _ai.BattleSystem.GetOpposingParty( pokemon.PID );
        int oppAlive = _ai.GetRemainingOpposingPokemon( pokemon.PID ).Count;

        float myTeamHPPercent = GetRemainingTeamHP( myTeam );
        float oppTeamHPPercent = GetRemainingTeamHP( oppTeam );

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

        return new()
        {
            MyRemainingPieces = myAlive,
            OppRemainingPieces = oppAlive,
            MyTeamHPPercent = myTeamHPPercent,
            OppTeamHPPercent = oppTeamHPPercent,
            IsAhead = isAhead,
            IsBehind = isBehind,
        };
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

    public float GetExpendability( IBattleAIUnit mon, float hp )
    {
        // Debug.Log( $"===[Getting Expendability for {mon.NickName}]===" );

        float score = 0.5f;

        if( hp < 0.4f )     score += 0.2f;
        if( hp < 0.25f )    score += 0.2f;
        if( hp < 0.1f )     score += 0.2f;

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
            Debug.Log( $"[AI Scoring][Get Walling Score] Getting Walling Score! Unique Wall Scores found move {moveThreat.Move.MoveSO.Name} in its dictionary with key: {key}" );
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

        if( !_unitSim.CanActOnTurn( attacker ) )
            damage = 0;

        // Debug.Log( $"[AI Scoring][Get Walling Score] Getting Walling Score! Target {target.Name}'s Defending Stat: {defendingStat}, {defense}, Base HP: {targetMHP}. Level {attacker.Level} ({levelFactor}) Attacker {attacker.Name}'s Attacking stat {attackingStat}, {attack}. Move: {moveThreat.Move.MoveSO.Name}, Power: {movePower}, Modifier: {modifier}. Final Damage Estimate: {damage}, Normalized: {normalizedDamage}" );
        
        EstimatedDamageResult edr = new()
        {
            // Score = score,
            DamageEstimate = normalizedDamage,
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
        PotentialToKO ptko = GetPTKO_FromDamageEstimate( edr.DamageEstimate, targetHPR );

        return new()
        {
            Score = Get_PotentialToKOScoreFromEnum( ptko ),
            PTKO = ptko,
            Modifier = mtr.Modifier,
        };
    }

    private PotentialToKOResult Get_PTKOResultPreview( EstimatedDamageResult wsr, MoveThreatResult mtr )
    {
        PotentialToKO basePTKO = GetPTKO_FromDamageEstimate( wsr.DamageEstimate, 1f );
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

    public PotentialToKO GetPTKO_FromDamageEstimate( float damageEstimate, float targetHPR )
    {
        float damage = damageEstimate / targetHPR;
        // Debug.Log( $"[AI Scoring][Get Walling Score] Damage Estimate: {damageEstimate}, Target HPR: {targetHPR}, Final Damage Done Ratio: {damage}" );

        return damage switch
        {
            <= 0f       => PotentialToKO.Untouchable,
            <= 0.15f    => PotentialToKO.HardWall,
            <= 0.30f    => PotentialToKO.Sturdy,
            <= 0.47f    => PotentialToKO.Safe,
            <= 0.63f    => PotentialToKO.TwoHKO,
            <= 0.80f    => PotentialToKO.Risky,
            <= 0.97f    => PotentialToKO.Dangerous,
            > 0.97f     => PotentialToKO.OHKO,
            _ => PotentialToKO.TwoHKO,
        };
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
            if( theirTeam.Count < i + 1 )
                break;

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

    public string SimulationLog;
}

public struct ProjectedBoardState
{
    //-Raw Results
    public float MyHP_AfterTurn;
    public float OppHP_AfterTurn;

    public int MyTurnsRemaining;
    public int OpponentTurnsRemaining;

    public bool IAmKO;
    public bool OppIsKO;
    public bool MutualKO;

    public bool AttackerThreatensKO;
    public bool OpponentThreatensKO;
    public bool AttackerKillsFirst;
    public bool OpponentKillsFirst;
    public bool AttackerMovesFirst;
    public bool OpponentMovesFirst;

    //--Material
    public int MyRemainingPieces;
    public int OppRemainingPieces;

    public int MaterialDelta;
    public int RevengeScore;

    public TempoState TempoState;
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
