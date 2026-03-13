using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
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

    public ProjectedBoardState BuildProjectedBoardState( TurnOutcomeProjection top, int myRemainingPieces, int oppRemainingPieces )
    {
        bool iAmKO = false;
        bool oppIsKO = false;

        bool tempoAdvantage;

        if( top.Attacker_EndOfTurnHP <= 0 )
        {
            myRemainingPieces--;
            iAmKO = true;
            _ai.CurrentLog.Add( $"Attacker Fainted! My remaining pieces reduced from {myRemainingPieces + 1} to {myRemainingPieces}! I am KO is {iAmKO}." );
        }

        if( top.Opponent_EndOfTurnHP <= 0 )
        {
            oppRemainingPieces--;
            oppIsKO = true;
            _ai.CurrentLog.Add( $"Opponent Fainted! Opponent's remaining pieces reduced from {oppRemainingPieces + 1} to {oppRemainingPieces}! I am KO is {oppIsKO}." );
        }

        if( oppIsKO && !iAmKO )
        {
            tempoAdvantage = true;
            _ai.CurrentLog.Add( $"Opponent Fainted! We have tempo advantage: {tempoAdvantage}" );
        }
        else if( iAmKO && oppIsKO )
        {
            tempoAdvantage = false;
            _ai.CurrentLog.Add( $"Attacker Fainted! We have tempo advantage: {tempoAdvantage}" );
        }
        else if( !iAmKO && !oppIsKO )
        {
            tempoAdvantage = top.Attacker.Speed > top.Opponent.Speed;
            _ai.CurrentLog.Add( $"No Faint! We have tempo advantage: {tempoAdvantage}" );
        }
        else
        {
            tempoAdvantage = false;
            _ai.CurrentLog.Add( $"Mutual KO? We have tempo advantage: {tempoAdvantage}" );
        }

        return new()
        {
            MyHP_AfterTurn = top.Attacker_EndOfTurnHP,
            OppHP_AfterTurn = top.Opponent_EndOfTurnHP,
            IAmKO = iAmKO,
            OppIsKO = oppIsKO,
            MutualKO = top.MutualKO,
            IHaveTempoAdvantageNextTurn = tempoAdvantage,
            MyRemainingPieces = myRemainingPieces,
            OppRemainingPieces = oppRemainingPieces,
            MaterialDelta = myRemainingPieces - oppRemainingPieces,
        };
    }

    public int EvaluatePBS( ProjectedBoardState pbs )
    {
        const int MATERIAL_WEIGHT = 100;
        const int TEMPO_WEIGHT = 10;
        const int HP_WEIGHT = 2;

        int score = 0;

        //--Material. Material is currently considered the most important resource.
        score += pbs.MaterialDelta * MATERIAL_WEIGHT;
        _ai.CurrentLog.Add( $"[Evaluate PBS] Material Delta: {pbs.MaterialDelta}. Score: {score}" );

        //--Tempo judges when no ko happens on either side. I have to wonder what happens to scoring when there is a ko.
        if( !pbs.IAmKO && !pbs.OppIsKO )
            score += pbs.IHaveTempoAdvantageNextTurn ? TEMPO_WEIGHT : -TEMPO_WEIGHT;

        _ai.CurrentLog.Add( $"[Evaluate PBS] I am KO: {pbs.IAmKO}. Opp is KO: {pbs.OppIsKO}. Score: {score}" );

        //--Remaining HP judgement.
        int myHPPercent = Mathf.RoundToInt( pbs.MyHP_AfterTurn * 100f );
        int oppHPPercent = Mathf.RoundToInt( pbs.OppHP_AfterTurn * 100f );

        int hpDelta = myHPPercent - oppHPPercent;
        hpDelta = Mathf.Clamp( hpDelta, -50, 50 );
        score += hpDelta * HP_WEIGHT;

        _ai.CurrentLog.Add( $"[Evaluate PBS] My HP Percent: {myHPPercent}. Opp HP Percent: {oppHPPercent}. HP Delta: {hpDelta}. Score: {score}" );

        return score;
    }

    public TempoStateResult GetTempoState( ExchangeEvaluation eval )
    {
        // Debug.Log( $"[AI Scoring][Get Tempo] Starting Tempo State Check for Attacker: {attacker.Pokemon.NickName} vs Target: {target.Pokemon.NickName}" );
        var tempo = ClassifyTempo( eval );

        bool attackerHasPriorityAdvantage = eval.AttackerMovesFirst && !eval.OpponentMovesFirst;
        bool targetHasPriorityAdvantage = eval.OpponentMovesFirst && !eval.AttackerMovesFirst;

        // Debug.Log( $"[AI Scoring][Get Tempo] Final Tempo State: {tempo}, Attacker: {attacker.Pokemon.NickName} vs Target: {target.Pokemon.NickName}" );

        return CreateTempoStateResult( tempo, attackerHasPriorityAdvantage, targetHasPriorityAdvantage );
    }

    public ExchangeEvaluation EvaluateExchange( IBattleAIUnit attacker, IBattleAIUnit target )
    {
        //--Potential to KO
        //--Attacker PTKO Target
        var attackerMTR = _ai.MoveCommand.Get_BestSimulatedAttack( attacker, target, "Evaluate Exchange (attacker vs target)" );
        var targetWSR = Get_WallingScoreResult( attacker, target, attackerMTR );
        float targetHP = _ai.Get_HPRatio( target );

        PotentialToKOResult attackerPTKO_target = Get_PotentialToKOResult( targetWSR, attackerMTR, targetHP );

        //--Target PTKO Attacker
        var targetMTR = _ai.MoveCommand.Get_BestSimulatedAttack( target, attacker, "Evaluate Exchange (target vs attacker)" );
        var attackerWSR = Get_WallingScoreResult( target, attacker, targetMTR );
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
        bool attackerForcesSwitch = _unitSim.PredictForcedSwitch( attackerPTKO_target.PTKO, targetPTKO_attacker.PTKO, attackerMovesFirst );
        bool targetForcesSwitch = _unitSim.PredictForcedSwitch( targetPTKO_attacker.PTKO, attackerPTKO_target.PTKO, targetMovesFirst );

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

            AttackerForcesSwitch = attackerForcesSwitch,
            TargetForcesSwitch = targetForcesSwitch,

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

    private TempoStateResult CreateTempoStateResult( TempoState state, bool attackerHasPriority, bool targetHasPriority )
    {
        return new(){ TempoState = state, AttackerHasPriority = attackerHasPriority, TargetHasPriority = targetHasPriority };
    }

    public BoardContext GetBoardContext( IBattleAIUnit target, ExchangeEvaluation eval )
    {
        BattleAI_PokemonAdapter ourAdapter = new( _ai.Unit.Pokemon, _ai );
        var ( Exists, pivots ) = GetSafePivot( target );
        var materialStatus = GetMaterialStatus( ourAdapter );

        bool lowHP = eval.AttackerHPR < 0.3f;
        bool likelyDying = eval.OpponentPTKOR.PTKO >= PotentialToKO.Dangerous;

        bool isForced = ( likelyDying && !Exists ) || ( lowHP && eval.OpponentPTKOR.PTKO >= PotentialToKO.Risky );

        int myAlive = _ai.GetRemainingAllyPokemon( ourAdapter.PID ).Count;
        int oppAlive = _ai.GetRemainingOpposingPokemon( target.PID ).Count;

        bool isTerminal = myAlive <= 2;

        float hp = _ai.Get_HPRatio( _ai.Unit.Pokemon );
        float expendability = GetExpendability( _ai.ThisUnitAdapter, hp );

        BoardContext context = new()
        {
            IsForcedTrade = isForced,

            HasSafePivot = Exists,
            SafePivots = pivots,

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

    private ( bool Exists, List<Pokemon> pivots ) GetSafePivot( IBattleAIUnit opponent )
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
                    var targetThreateningMove = _ai.MoveCommand.Get_BestSimulatedAttack( opponent, monAdapter, "Get Safe Pivot" );
                    var attackerWSR = Get_WallingScoreResult( opponent, monAdapter, targetThreateningMove );
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

    private float GetRemainingTeamHP( List<Pokemon> team )
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

    public WallingScoreResult Get_WallingScoreResult( IBattleAIUnit attacker, IBattleAIUnit target, MoveThreatResult moveThreat )
    {
        const float STAT_SCALAR = 0.28f;
        const float DAMAGE_ROLL = 0.925f;
        float attack = 1f;
        float defense = 1f;
        Stat attackingStat = Stat.Attack;
        Stat defendingStat = Stat.Defense;
        string key = "none";
        var moveSO = moveThreat.Move.MoveSO;
        float movePower = moveThreat.Move.MovePower;
        float modifier = moveThreat.Modifier;

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
            }
            else if( cat == MoveCategory.Special )
            {
                attackingStat = Stat.SpAttack;
                defendingStat = Stat.SpDefense;
                attack = _ai.GetUnitInferredStat( attacker, attackingStat );
                defense = _ai.GetUnitInferredStat( target, defendingStat );
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

        float damage = ( ( levelFactor * movePower * attack / defense / 50 ) + 2 ) * modifier * DAMAGE_ROLL;
        float normalizedDamage = ( damage / targetMHP ) * STAT_SCALAR;

        Debug.Log( $"[AI Scoring][Get Walling Score] Getting Walling Score! Target {target.Name}'s Defending Stat: {defendingStat}, {defense}, Base HP: {targetMHP}. Level {attacker.Level} ({levelFactor}) Attacker {attacker.Name}'s Attacking stat {attackingStat}, {attack}. Move: {moveThreat.Move.MoveSO.Name}, Power: {movePower}, Modifier: {modifier}. Final Damage Estimate: {damage}, Normalized: {normalizedDamage}" );
        
        WallingScoreResult wsr = new()
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

        return wsr;
    }

    // public WallingScoreResult Get_WallingScoreResult( IBattleAIUnit attacker, IBattleAIUnit target, MoveThreatResult moveThreat )
    // {
    //     const int WALLINGSCORE_NORMALIZATION_OFFSET = 30;
    //     const float WALLINGSCORE_LOGSCALING_FACTOR = 40;
    //     const float MOVE_POWER_BASELINE = 75;
    //     int off = 1;
    //     int def = 1;
    //     Stat offStat = Stat.Attack;
    //     Stat defStat = Stat.Defense;
    //     string key = "none";
    //     var moveSO = moveThreat.Move.MoveSO;
    //     int movePower = moveThreat.Move.MovePower;

    //     //--Unique Wallscore Key check
    //     if( moveThreat.Move != null )
    //     {
    //         key = moveThreat.Move.MoveSO.Name;

    //         if( _unitSim.MovePowerConditions.TryGetValue( key, out var mod ) )
    //             movePower = mod( attacker, target, moveThreat.Move );
    //     }

    //     //--Multi hit move power projection
    //     if( moveSO.HitRange.x >= 2 && moveSO.HitRange.y != 0 )
    //     {
    //         int minHits = moveSO.HitRange.x;
    //         int maxHits = moveSO.HitRange.y;

    //         int expectedHits = Mathf.FloorToInt( ( minHits + maxHits ) * 0.5f );

    //         movePower *= expectedHits;
    //     }
    //     else if( moveSO.HitRange.x >= 2 && moveSO.HitRange.y == 0 )
    //     {
    //         movePower *= moveSO.HitRange.x;
    //     }

    //     //--Get Stats used
    //     if( _ai.UniqueWallScores.ContainsKey( key ) )
    //     {
    //         offStat = _ai.UniqueWallScores[key].AttackingStat;
    //         defStat = _ai.UniqueWallScores[key].DefendingStat;
    //         off = _ai.GetBaseStat( attacker, offStat );
    //         def = _ai.GetBaseStat( target, defStat );
    //     }
    //     else
    //     {
    //         //--Right now MoveThreatResult has scenarios where it isn't returning a move. I need to iron this out asap!!!
    //         MoveCategory cat;
    //         if( moveThreat.Move != null )
    //             cat = moveThreat.Move.MoveSO.MoveCategory;
    //         else
    //             cat = MoveCategory.Status;

    //         if( cat == MoveCategory.Physical )
    //         {
    //             offStat = Stat.Attack;
    //             defStat = Stat.Defense;
    //             off = _ai.GetBaseStat( attacker, offStat );
    //             def = _ai.GetBaseStat( target, defStat );
    //         }
    //         else if( cat == MoveCategory.Special )
    //         {
    //             offStat = Stat.SpAttack;
    //             defStat = Stat.SpDefense;
    //             off = _ai.GetBaseStat( attacker, offStat );
    //             def = _ai.GetBaseStat( target, defStat );
    //         }
    //         else
    //         {
    //             //--Status move used, we may need to alter this somehow
    //             off = 1;
    //             def = 1;
    //         }
    //     }

    //     float statRatio = (float)Mathf.Max( 1f, def ) / (float)Mathf.Max( 1f, off );

    //     float statComponent = MathF.Log( statRatio );
    //     float powerComponent = Mathf.Log( (float)Mathf.Max( 1f, movePower ) / MOVE_POWER_BASELINE );

    //     float rawScore = ( statComponent - powerComponent ) * WALLINGSCORE_LOGSCALING_FACTOR + WALLINGSCORE_NORMALIZATION_OFFSET;

    //     int score = Mathf.RoundToInt( rawScore );

    //     // Debug.Log( $"[AI Scoring][Get Walling Score] Getting Walling Score! Target {target.NickName}'s Defense: {def}, Attacker {attacker.NickName}'s Offense: {off}. Move: {moveThreat.Move.MoveSO.Name}, Power: {movePower}. Stat Component: {statComponent}. Power Component: {powerComponent}. Raw Score: {rawScore}. Final Score: {score}" );

    //     WallingScoreResult wsr = new()
    //     {
    //         Score = score,

    //         AttackingStatStage = attacker.StatStages[offStat],
    //         DefendingStatStage = target.StatStages[defStat],

    //         AttackingDirectModifier = attacker.DirectStatModifiers[offStat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm ),
    //         DefendingDirectModifier = target.DirectStatModifiers[defStat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm ),

    //         Attacker = attacker,
    //         Target = target,
    //     };

    //     return wsr;
    // }

    public PotentialToKOResult Get_PotentialToKOResult( WallingScoreResult wsr, MoveThreatResult mtr, float targetHPR )
    {
        Debug.Log( $"[AI Scoring][Get Walling Score] (Get_PTKOResult) Damage Estimate: {wsr.DamageEstimate}, Target HPR: {targetHPR}" );
        PotentialToKO ptko = GetPTKO_FromDamageEstimate( wsr.DamageEstimate, targetHPR );

        CustomLogSession ptkoLog = new();
        ptkoLog.Add( $"=====[PTKO Plot Bar][{wsr.Attacker.Name}'s {mtr.Move.MoveSO.Name} on {wsr.Target.Name} (Expected Damage: {wsr.DamageEstimate}. PTKO: {ptko})]=====" );
        ptkoLog.Add( $"-60__________-45____________-30________________-12____________________12________________30____________45" );
        ptkoLog.Add( $"OHKO-------Dangerous--------Risky-------------TwoHKO------------------Safe-------------Sturdy-------Hardwall" );
        string bar = $"|====------====|====------====|====---------====|====--------------====|====---------====|====------====|";

        int minScore = -60;
        int maxScore = 60;

        float range = maxScore - minScore;

        int wallIndex = Mathf.RoundToInt( ( ( wsr.DamageEstimate - minScore ) / range ) * ( bar.Length - 1 ) );

        char[] chars = bar.ToCharArray();

        if( wallIndex >= 0 && wallIndex < chars.Length )
            chars[wallIndex] = 'X';

        string result = new( chars );

        ptkoLog.Add( result );
        Debug.Log( ptkoLog.ToString() );
        ptkoLog.Clear();

        return new()
        {
            Score = Get_PotentialToKOScoreFromEnum( ptko ),
            PTKO = ptko,
            Modifier = mtr.Modifier,
        };
    }

    // public PotentialToKOResult Get_PotentialToKOResult( WallingScoreResult wsr, MoveThreatResult mtr, float targetHPRatio )
    // {
    //     PotentialToKO basePotentialKO = Get_PotentialToKOFromWallingScore( wsr.Score );
    //     float moveModifier = mtr.Modifier;
        
    //     //--Move Modifier shift
    //     int moveShift = Get_MoveModifierPTKOShift( moveModifier );

    //     //--HP Ratio shift
    //     int hpShift = Get_HPRatioPTKOShift( basePotentialKO, targetHPRatio );

    //     int tacticalShift = 0;

    //     //--Attacker attacking stat stage and direct modifier shifts
    //     tacticalShift += Get_StatStagePTKOShift( wsr.AttackingStatStage );
    //     tacticalShift += Get_DirectModifierPTKOShift( wsr.AttackingDirectModifier );

    //     //--Target defending stat stage and direct modifier shifts
    //     tacticalShift -= Get_StatStagePTKOShift( wsr.DefendingStatStage );
    //     tacticalShift -= Get_DirectModifierPTKOShift( wsr.DefendingDirectModifier );

    //     int finalShift = moveShift + hpShift + tacticalShift;

    //     bool nearBoundary = PTKOIsNearBoundary( wsr.Score );

    //     if( nearBoundary && finalShift != 0 )
    //     {
    //         int boundaryShift = (int)Mathf.Sign( finalShift ); //--this shifts up one or down one according to boundry math.
    //         Debug.Log( $"[AI Scoring][Shift Potential To KO][Boundary Shift] Walling Score was near a boundary! Before: {finalShift} += {boundaryShift}" );
    //         finalShift += boundaryShift;
    //     }

    //     int finalClassInt = Mathf.Clamp( (int)basePotentialKO + finalShift, (int)PotentialToKO.HardWall, (int)PotentialToKO.OHKO );

    //     //--This checks to see if the target is immune to the selected move (a 0 move modifier means effectiveness was 0). if it is, the ptko is Untouchable. otherwise, we use the appropriate shift.
    //     var finalClass = ( moveModifier == 0 || mtr.Move.MovePower == 0 || mtr.Move.MoveSO.MoveCategory == MoveCategory.Status ) ? PotentialToKO.Untouchable : (PotentialToKO)finalClassInt;

    //     CustomLogSession ptkoLog = new();
    //     ptkoLog.Add( $"=====[PTKO Plot Bar][{wsr.Attacker.Name}'s {mtr.Move.MoveSO.Name} on {wsr.Target.Name} (Base: {basePotentialKO}, Shift: {finalShift}, Final: {finalClass})]=====" );
    //     ptkoLog.Add( $"-60__________-45____________-30________________-12____________________12________________30____________45" );
    //     ptkoLog.Add( $"OHKO-------Dangerous--------Risky-------------TwoHKO------------------Safe-------------Sturdy-------Hardwall" );
    //     string bar = $"|====------====|====------====|====---------====|====--------------====|====---------====|====------====|";

    //     int minScore = -60;
    //     int maxScore = 60;

    //     float range = maxScore - minScore;

    //     int wallIndex = Mathf.RoundToInt( ( ( wsr.Score - minScore ) / range ) * ( bar.Length - 1 ) );

    //     char[] chars = bar.ToCharArray();

    //     if( wallIndex >= 0 && wallIndex < chars.Length )
    //         chars[wallIndex] = 'X';

    //     string result = new( chars );

    //     ptkoLog.Add( result );
    //     Debug.Log( ptkoLog.ToString() );
    //     ptkoLog.Clear();

    //     _ai.BaseWallScores.Add( wsr.Score );
    //     _ai.PTKOShifts.Add( finalClassInt );

    //     return new()
    //     {
    //         Score = Get_PotentialToKOScoreFromEnum( finalClass ),
    //         PTKO = finalClass,
    //         Modifier = moveModifier,
    //     };
    // }

    // private PotentialToKOResult Get_PTKOResultPreview( WallingScoreResult wsr, MoveThreatResult mtr )
    // {
    //     PotentialToKO basePTKO = Get_PotentialToKOFromWallingScore( wsr.Score );
    //     float moveModifier = mtr.Modifier;
    //     int shift = Get_MoveModifierPTKOShift( moveModifier );

    //     int finalClassInt = Mathf.Clamp( (int)basePTKO + shift, (int)PotentialToKO.HardWall, (int)PotentialToKO.OHKO );
        
    //     //--This checks to see if the target is immune to the selected move (a 0 move modifier means effectiveness was 0). if it is, the ptko is Untouchable. otherwise, we use the appropriate shift.
    //     var finalClass = ( moveModifier == 0 || mtr.Move.MovePower == 0 || mtr.Move.MoveSO.MoveCategory == MoveCategory.Status ) ? PotentialToKO.Untouchable : (PotentialToKO)finalClassInt;

    //     return new()
    //     {
    //         Score = Get_PotentialToKOScoreFromEnum( finalClass ),
    //         PTKO = finalClass,
    //         Modifier = moveModifier,
    //     };
    // }

    private PotentialToKOResult Get_PTKOResultPreview( WallingScoreResult wsr, MoveThreatResult mtr )
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

    private PotentialToKO Get_PotentialToKOFromWallingScore( int wallingScore )
    {

        PotentialToKO potentialKO;
        if( wallingScore >= 45 )                potentialKO = PotentialToKO.HardWall;       //--Hard Wall, Shuts down pressure
        else if( wallingScore >= 30 )           potentialKO = PotentialToKO.Sturdy;         //--Sturdy, can take a couple hits
        else if( wallingScore >= 12 )           potentialKO = PotentialToKO.Safe;           //--Safe, can take an extra hit
        else if( wallingScore >= -12 )          potentialKO = PotentialToKO.TwoHKO;         //--Neutral, possible 2HKO
        else if( wallingScore >= -30 )          potentialKO = PotentialToKO.Risky;          //--Getting Risky, almost guaranteed 2HK0
        else if( wallingScore >= -45 )          potentialKO = PotentialToKO.Dangerous;      //--Danger, high damage expected, crit or unexpected damage might OHKO
        else                                    potentialKO = PotentialToKO.OHKO;           //--Fatal, Likely OHKO

        return potentialKO;
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

    private bool PTKOIsNearBoundary( int wallingScore )
    {
        const int OVERLAP = 8;
        int distance;

        if( wallingScore >= 45 )            distance = wallingScore - 45;
        else if( wallingScore >= 30 )       distance = wallingScore - 30;
        else if( wallingScore >= 12 )       distance = wallingScore - 12;
        else if( wallingScore >= -12 )      distance = wallingScore + 12;
        else if( wallingScore >= -30 )      distance = wallingScore + 30;
        else if( wallingScore >= -45 )      distance = wallingScore + 45;
        else distance = 0;

        return Mathf.Abs( distance ) < OVERLAP;
    }

    public int Get_OffensivePTKOScore( int score )
    {
        int off = -score;
        return Mathf.FloorToInt( off * 1.2f ); //--the higher chance of ko, the more incentivized you are because the score increases more due to being a percentage increase.
    }

    // private int Get_MoveModifierPTKOShift( float moveModifier )
    // {
    //     //--A higher modifier shifts positively because the enum starts and 0 and increases. HardWall is 0, while LikelyOHKO is 6
    //     //--A higher modifier means increased damage, therefore the likelyhood of a KO increases.

    //     float log = Mathf.Log( moveModifier, 1.5f );

    //     int shift = Mathf.FloorToInt( log );
    //     // int shift = Mathf.RoundToInt( log );
        
    //     // Debug.Log( $"[AI Scoring][Shift Potential To KO] Move modifier shifting KO Potential by: {shift}" );

    //     return shift;
    // }

    // private int Get_HPRatioPTKOShift( PotentialToKO basePTKO, float targetHPratio )
    // {
    //     int shift = 0;

    //     float expectedDamage = Get_PTKODamagePercent( basePTKO );
    //     float hp = Mathf.Floor( targetHPratio * 1000f ) / 1000f;

    //     Debug.Log( $"[AI Scoring][Shift Potential To KO][HP Shift] Shifting PTKO based on hp (cascading). Base PTKO: {basePTKO}. Expected Damage: {expectedDamage}. Target's Raw HP Ratio: {targetHPratio}. Target's floored HP Ratio: {hp}." );

    //     if( hp <= Get_PTKODamagePercent( PotentialToKO.HardWall ) )       shift += 1;
    //     if( hp <= Get_PTKODamagePercent( PotentialToKO.Sturdy ) )         shift += 1;
    //     if( hp <= Get_PTKODamagePercent( PotentialToKO.Safe ) )           shift += 1;
    //     if( hp <= Get_PTKODamagePercent( PotentialToKO.TwoHKO ) )         shift += 1;
    //     if( hp <= Get_PTKODamagePercent( PotentialToKO.Risky ) )          shift += 1;
    //     if( hp <= Get_PTKODamagePercent( PotentialToKO.Dangerous ) )      shift += 1;

    //     Debug.Log( $"[AI Scoring][Shift Potential To KO][HP Shift] Target's HP Ratio shifting KO Potential by: {shift}" );

    //     return shift;
    // }

    // private int Get_HPRatioPTKOShift( PotentialToKO basePTKO, float targetHPratio )
    // {
    //     int shift = 0;

    //     float expectedDamage = Get_PTKODamagePercent( basePTKO );
    //     float hp = Mathf.Floor( targetHPratio * 1000f ) / 1000f;
    //     float ratio = hp/expectedDamage;

    //     Debug.Log( $"[AI Scoring][Shift Potential To KO][HP Shift] Shifting PTKO based on hp (cascading). Base PTKO: {basePTKO}. Expected Damage: {expectedDamage}. Target's Raw HP Ratio: {targetHPratio}. Target's floored HP Ratio: {hp}." );

    //     shift = Mathf.Clamp( Mathf.RoundToInt( -Mathf.Log( ratio, 2f ) ), -2, 2 );

    //     Debug.Log( $"[AI Scoring][Shift Potential To KO][HP Shift] Target's HP Ratio shifting KO Potential by: {shift}" );


    //     return shift;
    // }

    // private int Get_StatStagePTKOShift( int stage )
    // {
    //     int shift = -0;

    //     if( stage <= -3 )       shift = -2;
    //     else if( stage <= -1 )  shift = -1;
    //     else if( stage <= 0 )   shift = 0;
    //     else if( stage <= 2)    shift = +1;
    //     else if( stage <= 4 )   shift = +2;
    //     else if( stage > 4 )    shift = +2;

    //     // Debug.Log( $"[AI Scoring][Shift Potential To KO] Target's Stat Stage for its defending stat shifting KO Potential by: {shift}" );

    //     return shift;
    // }

    // private int Get_DirectModifierPTKOShift( float totalMod )
    // {
    //     int shift = 0;

    //     if( totalMod <= 0.5f )             shift += -2;
    //     else if( totalMod <= 0.75f )       shift += -1;
    //     else if( totalMod <= 1.1f )        shift += 0;
    //     else if( totalMod <= 1.5f )        shift += 1;
    //     else if( totalMod <= 2f )          shift += 2;
    //     else if( totalMod > 2f )           shift += 3;

    //     // Debug.Log( $"[AI Scoring][Shift Potential To KO] Target's Direct Modifier to its defending stat shifting KO Potential by: {shift}" );

    //     return shift;
    // }

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
        Debug.Log( $"[AI Scoring][Get Walling Score] Damage Estimate: {damageEstimate}, Target HPR: {targetHPR}, Final Damage Done Ratio: {damage}" );

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
        var wsr     = Get_WallingScoreResult( attacker, target, move );
        var result  = Get_PTKOResultPreview( wsr, move );

        return result.PTKO;
    }
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

    public bool MutualKO;
}

public struct ProjectedBoardState
{
    //-Raw Results
    public float MyHP_AfterTurn;
    public float OppHP_AfterTurn;

    public bool IAmKO;
    public bool OppIsKO;
    public bool MutualKO;

    //--Tempo Next Turn
    public bool IHaveTempoAdvantageNextTurn;

    //--Material
    public int MyRemainingPieces;
    public int OppRemainingPieces;

    public int MaterialDelta;
}
