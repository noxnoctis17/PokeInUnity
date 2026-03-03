using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public enum AIDecisionType { Attack, RandomMove, StrongestMove, OffensiveSwitch, DefensiveSwitch, SpeedControl, Weather, FakeOut, Protect, }
public enum PotentialToKO { HardWall, Sturdy, Safe, TwoHKO, Risky, Dangerous, OHKO }
public enum TempoState { WinningHard, Winning, Neutral, Losing, LosingHard }

public class BattleAI : MonoBehaviour
{
    private const int WALLINGSCORE_NORMALIZATION_OFFSET = 30;
    private const float WALLINGSCORE_LOGSCALING_FACTOR = 42;
    private const float MOVE_POWER_BASELINE = 75;
    private int _round;
    private BattleAI_MoveCommand _moveCommand;
    private BattleAI_SwitchCommand _switchCommand;
    public BattleSystem BattleSystem { get; private set; }
    public BattleTrainer Trainer { get; private set; }
    public BattleAI_Projection Projection { get; private set; }
    public BattleAI_UnitSim UnitSim { get; private set; }
    public BattleUnit Unit { get; private set; }
    public Pokemon LastSentInPokemon { get; private set; }
    public float TrainerSkillModifier { get; private set; }
    public int SwitchAmount { get; private set; }
    public Dictionary<string, UniqueWallingScoreMove> UniqueWallScores { get; private set; }
    public Dictionary<Pokemon, PieceValue> TeamPieceValues { get; private set; }
    public CustomLogSession CurrentLog { get; private set; }

    public void InitializeAI( BattleSystem battleSystem, BattleUnit battleUnit )
    {
        BattleSystem = battleSystem;
        Unit = battleUnit;
        Trainer = Unit.Trainer;

        if( battleSystem.BattleType != BattleType.WildBattle_1v1 )
            TrainerSkillModifier = Mathf.Clamp01( battleSystem.TopTrainer1.TrainerSkillLevel / 100f );

        UnitSim = new( this );
        Projection = new( this );
        _moveCommand = new( this );
        _switchCommand = new( this );

        _round = 0;

        InitializeUniqueWallScores();
    }

    public void CleanupAI()
    {
        _moveCommand = null;
        _switchCommand = null;
    }

    public int Get_ConsecutiveSwitchPenalty()
    {
        int penalty = 0;
        for( int i = 0; i < SwitchAmount; i++ )
            penalty -= 30;

        return penalty;
    }

    public void IncreaseSwitchAmount()
    {
        SwitchAmount++;
    }

    public void ResetSwitchAmount()
    {
        SwitchAmount = 0;
    }

    public void SetLastSentInPokemon( Pokemon pokemon )
    {
        LastSentInPokemon = pokemon;
    }

    public List<Pokemon> GetRemainingAllyPokemon( Pokemon pokemon )
    {
        return BattleSystem.GetAllyParty( pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
    }

    public List<Pokemon> GetRemainingOpposingPokemon( Pokemon pokemon )
    {
        return BattleSystem.GetOpposingParty( pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
    }

    public void ChooseCommand()
    {
        CurrentLog = new();

        _round++;

        CurrentLog.Add( $"=====[Choose Command][TURN {_round} - {Unit.Pokemon.NickName}, Offensive Piece Value: {TeamPieceValues[Unit.Pokemon].OffensiveValue}]=====" );

        if( Unit.Pokemon.SevereStatus?.ID == SevereConditionID.FNT || Unit.Pokemon.CurrentHP == 0 )
            return;

        //--Handle Two Turn/Charge/Recharge Moves
        if( Unit.Flags[UnitFlags.Charging].IsActive && Unit.Flags[UnitFlags.Charging].Count > 0 )
        {
            var move = Unit.Flags[UnitFlags.Charging].Move;
            List<BattleUnit> targets = new() { Unit.Flags[UnitFlags.Charging].Target, };
            BattleSystem.SetMoveCommand( Unit, targets, move , true );
            return;
        }

        //--Recharging should simply skip the turn altogether. After ChooseCommand() completes, we increment command count in the AI turn state,
        //--So there shouldn't be any hang ups, at least not in singles. --2/12/26, pre-doubles testing lol
        if( Unit.Flags[UnitFlags.Recharging].IsActive )
        {
            return;
        }

        var opposingUnits = BattleSystem.GetOpposingUnits( Unit );
        var damageThreat = GetThreat_ImmediateDamage( opposingUnits, Unit.Pokemon );
        var exchangeEval = EvaluateExchange( Unit, damageThreat.Unit );
        var tempo = GetTempoState( Unit, damageThreat.Unit, exchangeEval );
        var boardContext = GetBoardContext( damageThreat.Unit, exchangeEval );
        var defensiveSwitchCandidate = _switchCommand.GetSwitch_Defensive( opposingUnits );
        var offensiveSwitchCandidate = _switchCommand.GetSwitch_Offensive( opposingUnits );

        //--Get Attack Score
        int attackScore = _moveCommand.AttackScore( tempo, exchangeEval, boardContext );
        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Attack Score: {attackScore}" );

        //--Get Switch Score
        int switchScore = _switchCommand.SwitchScore( tempo, exchangeEval, defensiveSwitchCandidate, boardContext );
        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Switch Score: {switchScore}" );

        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Final comparison for {Unit.Pokemon.NickName} vs {damageThreat.Unit.Pokemon.NickName}: Attack Score: {attackScore}, Switch Score: {switchScore}" );
        if( switchScore > attackScore )
        {
            CurrentLog.Add( $"FINAL DECISION: {Unit.Pokemon.NickName} is switching with {defensiveSwitchCandidate.Pokemon.NickName}! Switch Amount: {SwitchAmount}. Switch Score: {switchScore}" );
            CurrentLog.Add( "===========================================================" );
            IncreaseSwitchAmount();
            SetLastSentInPokemon( defensiveSwitchCandidate.Pokemon );
            _switchCommand.SubmitSwitchCommand( defensiveSwitchCandidate.Pokemon );
        }
        else
        {
            CurrentLog.Add( $"FINAL DECISION: {Unit.Pokemon.NickName} is attacking! Attack Score: {attackScore}" );
            CurrentLog.Add( "===========================================================" );
            ResetSwitchAmount();
            _moveCommand.SubmitMoveCommand( damageThreat.Unit, boardContext );
        }

        Debug.Log( CurrentLog.ToString() );
        string path = Application.persistentDataPath + "/BattleAI_ChooseCommandLog.txt";
        System.IO.File.AppendAllText( path, CurrentLog.ToString() + "\n" );
    }

    public int TempoAttackModifier( TempoStateResult tempo )
    {
        return tempo.TempoState switch
        {
            TempoState.WinningHard  => +45,
            TempoState.Winning      => +25,
            TempoState.Neutral      => 0,
            TempoState.Losing       => -20,
            TempoState.LosingHard   => -40,
            _ => 0
        };
    }

    public int TempoSwitchModifier( TempoStateResult tempo )
    {
        return tempo.TempoState switch
        {
            TempoState.WinningHard  => -45,
            TempoState.Winning      => -25,
            TempoState.Neutral      => 0,
            TempoState.Losing       => +10,
            TempoState.LosingHard   => +25,
            _ => 0
        };
    }

    public Pokemon RequestedForcedSwitch()
    {
        var opposingUnits = BattleSystem.GetOpposingUnits( Unit );

        if( opposingUnits == null || opposingUnits.Count <= 0 )
            return _switchCommand.GetSwitch_Vacuum();
        else
            return _switchCommand.GetSwitch_Offensive( opposingUnits ).Pokemon;
    }

    public int GetUnitInferredStat( Pokemon pokemon, Stat stat )
    {
        Debug.Log( $"[AI Scoring][Get Walling Score] Getting {pokemon.NickName}'s inferred {stat}" );
        float statValue = GetBaseStat( pokemon, stat );
        Debug.Log( $"[AI Scoring][Get Walling Score] {pokemon.NickName}'s base {stat} value is: {statValue}" );

        int stage = pokemon.StatStages[stat];
        var stageModifier = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f };
        float directModifier = pokemon.DirectStatModifiers[stat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm );

        if( stage >= 0 )
            statValue *= stageModifier[stage];
        else
            statValue /= stageModifier[-stage];

        //--Apply Direct Stat Change (Burn, Paralysis, Ruin Ability, Weather stat change, etc.)
        statValue *= directModifier;

        int final = Mathf.FloorToInt( statValue );

        Debug.Log( $"[AI Scoring][Get Walling Score] {pokemon.NickName}'s Final Inferred {stat} value is: {final}" );

        return final;
    }

    public int GetUnitContextualSpeed( Pokemon pokemon )
    {
        int speed = GetUnitInferredStat( pokemon, Stat.Speed );
        var weather = BattleSystem.Field.Weather;

        if( weather != null )
        {
            if( weather.ID == WeatherConditionID.RAIN && pokemon.AbilityID == AbilityID.SwiftSwim && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;

            if( weather.ID == WeatherConditionID.SUNNY && pokemon.AbilityID == AbilityID.Chlorophyll && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;

            if( weather.ID == WeatherConditionID.SANDSTORM && pokemon.AbilityID == AbilityID.SandRush && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;

            if( weather.ID == WeatherConditionID.SNOW && pokemon.AbilityID == AbilityID.SlushRush && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;
        }

        return speed;
    }

    private int GetBaseStat( Pokemon pokemon, Stat stat )
    {
        return stat switch
        {
            Stat.Attack     => pokemon.PokeSO.Attack,
            Stat.Defense    => pokemon.PokeSO.Defense,
            Stat.SpAttack   => pokemon.PokeSO.SpAttack,
            Stat.SpDefense  => pokemon.PokeSO.SpDefense,
            Stat.Speed      => pokemon.PokeSO.Speed,
            _ => 0
        };
    }

    public TempoStateResult GetTempoState( BattleUnit attacker, BattleUnit target, ExchangeEvaluation eval )
    {
        Debug.Log( $"[AI Scoring][Get Tempo] Starting Tempo State Check for Attacker: {attacker.Pokemon.NickName} vs Target: {target.Pokemon.NickName}" );
        var tempo = ClassifyTempo( eval );

        bool attackerHasPriorityAdvantage = eval.AttackerMovesFirst && !eval.OpponentMovesFirst;
        bool targetHasPriorityAdvantage = eval.OpponentMovesFirst && !eval.AttackerMovesFirst;

        Debug.Log( $"[AI Scoring][Get Tempo] Final Tempo State: {tempo}, Attacker: {attacker.Pokemon.NickName} vs Target: {target.Pokemon.NickName}" );

        return CreateTempoStateResult( tempo, attackerHasPriorityAdvantage, targetHasPriorityAdvantage );
    }

    private ExchangeEvaluation EvaluateExchange( BattleUnit attacker, BattleUnit target )
    {
        //--Speed Check
        int attackerSpeed = GetUnitInferredStat( attacker.Pokemon, Stat.Speed );
        int targetSpeed = GetUnitInferredStat( target.Pokemon, Stat.Speed );
        bool attackerHasPriorityAdvantage = Check_UnitHasPriority( attacker, target ) && !Check_UnitHasPriority( target, attacker );
        bool targetHasPriorityAdvantage = Check_UnitHasPriority( target, attacker ) && !Check_UnitHasPriority( attacker, target );
        bool attackerMovesFirst = attackerSpeed > targetSpeed /*|| attackerHasPriorityAdvantage*/;
        bool targetMovesFirst = targetSpeed > attackerSpeed /*|| targetHasPriorityAdvantage*/;

        Debug.Log( $"[AI Scoring][Get Tempo] Made speed comparisons! Results: Attacker Speed: {attackerSpeed}, Target Speed: {targetSpeed}, Attacker Priority: {attackerHasPriorityAdvantage}, Target Priority: {targetHasPriorityAdvantage}, Attacker Moves First: {attackerMovesFirst}, Target Moves First: {targetMovesFirst}" );

        //--Potential to KO
        //--Attacker PTKO Target
        var attackerThreateningMove = Get_MostThreateningMove( attacker.Pokemon, target.Pokemon );
        var targetWSR = Get_WallingScoreResult( attacker.Pokemon, target.Pokemon, attackerThreateningMove );
        float targetHP = Get_HPRatio( target.Pokemon );

        PotentialToKOResult attackerPTKO_target = Get_PotentialToKOResult( targetWSR, attackerThreateningMove.Modifier, targetHP );

        //--Target PTKO Attacker
        var targetThreatingMove = Get_MostThreateningMove( target.Pokemon, attacker.Pokemon );
        var attackerWSR = Get_WallingScoreResult( target.Pokemon, attacker.Pokemon, targetThreatingMove );
        float attackerHP = Get_HPRatio( attacker.Pokemon );

        PotentialToKOResult targetPTKO_attacker = Get_PotentialToKOResult( attackerWSR, targetThreatingMove.Modifier, attackerHP );

        Debug.Log( $"[AI Scoring][Get Tempo] PTKO's Checked! Results: Attacker PTKO Target: {attackerPTKO_target.PTKO}, Target PTKO Attacker: {targetPTKO_attacker.PTKO}" );

        bool attackerThreatensKO_onTarget       = attackerPTKO_target.PTKO > PotentialToKO.Risky; //--revert back to >= if not good
        bool targetThreatensKO_onAttacker       = targetPTKO_attacker.PTKO > PotentialToKO.Risky; //--revert back to >= if not good
        bool attackerSurvives_targetAttack      = targetPTKO_attacker.PTKO <= PotentialToKO.Risky;
        bool targetSurvives_attackerAttack      = attackerPTKO_target.PTKO <= PotentialToKO.Risky;

        Debug.Log( $"[AI Scoring][Get Tempo] Final Comparisons Made! Results: Attacker Threatens KO: {attackerThreatensKO_onTarget}, Target Threatens KO: {targetThreatensKO_onAttacker}, Attacker Survives: {attackerSurvives_targetAttack}, Target Survives: {targetSurvives_attackerAttack}" );
        
        ExchangeEvaluation eval = new()
        {
            AttackerName = attacker.Pokemon.NickName,
            OpponentName = target.Pokemon.NickName,

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
        };

        return eval;
    }

    private TempoState ClassifyTempo( ExchangeEvaluation eval )
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

    public int GetOutgoingPressure( Pokemon me, BattleUnit target )
    {
        int score = 50;
        float bestMoveThreat = float.MinValue;
        float hpRatio = Get_HPRatio( me );
        float defense;

        if( target.Pokemon.PokeSO.Attack > target.Pokemon.PokeSO.SpAttack )
            defense = me.PokeSO.Defense;
        else
            defense = me.PokeSO.SpDefense;

        Debug.Log( $"[AI Scoring][Outgoing Pressure Check] Starting Outgoing Pressure check on {me.NickName}. Starting Score: {score}" );

        //--Move Threat
        foreach( var move in me.ActiveMoves )
        {
            if( move.MoveSO.MoveCategory == MoveCategory.Status )
                continue;
                
            float effectiveness     = TypeChart.GetEffectiveness( move.MoveType, target.Pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.Pokemon.PokeSO.Type2 );
            float stab              = move.MoveSO.Type == Unit.Pokemon.PokeSO.Type1 || move.MoveSO.Type == Unit.Pokemon.PokeSO.Type2 ? 1.5f : 1f;
            float weather           = BattleSystem.Field.Weather?.OnDamageModify?.Invoke( Unit.Pokemon, target.Pokemon, move ) ?? 1f;
            float terrain           = BattleSystem.Field.Terrain?.OnDamageModify?.Invoke( Unit, target.Pokemon, move ) ?? 1f;
            float item              = Unit.Pokemon.BattleItemEffect?.OnDamageModify?.Invoke( Unit, target.Pokemon, move ) ?? 1f;

            Debug.Log( $"[AI Scoring][Outgoing Pressure Check] Score-ing {me.NickName}'s move {move.MoveSO.Name}. Effectiveness Modifier: {effectiveness}, STAB Modifier: {stab}, Weather Modifier: {weather}" );

            float currentMoveThreat = effectiveness * stab * weather * terrain * item;
            bestMoveThreat = Mathf.Max( bestMoveThreat, currentMoveThreat );

            Debug.Log( $"[AI Scoring][Outgoing Pressure Check] {me.NickName}'s move {move.MoveSO.Name} checked. Move's Score: {bestMoveThreat}" );
        }

             if( bestMoveThreat >= 9f )             score += 90; //--Upper bounds, this move is 4x effective, has STAB, and benefits from weather.
        else if( bestMoveThreat >= 6f )             score += 60; //--This move is 4x effective, and either has STAB OR benefits from weather.
        else if( bestMoveThreat >= 4f )             score += 40; //--This move is 4x effective, or has some combination of 2x effective, stab, and weather.
        else if( bestMoveThreat >= 2f )             score += 20;
        else if( bestMoveThreat >= 1.5f )           score += 15;
        else if( bestMoveThreat >= 1f )             score += 0;
        else if( bestMoveThreat >= 0.5f )           score -= 15;
        else if( bestMoveThreat >= 0.25f )          score -= 25;
        else if( bestMoveThreat == 0f )             score = 0;

        Debug.Log( $"[AI Scoring][Outgoing Pressure Check] {me.NickName}'s Moves have all been checked. Score: {score}" );
        //--Speed comparison
        var meSpeed = GetUnitInferredStat( me, Stat.Speed );
        var targetSpeed = GetUnitInferredStat( target.Pokemon, Stat.Speed );

        if( meSpeed > targetSpeed )
            score += 20;
        else if( meSpeed < targetSpeed )
            score -= 20;

        Debug.Log( $"[AI Scoring][Outgoing Pressure Check] {me.NickName}'s Speed comparison checked. Score: {score}" );

        Debug.Log( $"[AI Scoring][Outgoing Pressure Check] {me.NickName}'s Defense is: {defense}" );

        if ( defense >= 150f )              score += 40;
        else if( defense >= 125f )          score += 25;
        else if( defense >= 100f )          score += 10;
        else if( defense >= 80f )           score += 0;
        else if( defense >= 65f )           score -= 10;
        else if( defense >= 50f )           score -= 25;
        else if( defense < 50f )            score -= 40;

        Debug.Log( $"[AI Scoring][Outgoing Pressure Check] {me.NickName}'s Defense checked. Score: {score}" );

        int bulk = me.PokeSO.MaxHP + me.PokeSO.Defense + me.PokeSO.SpDefense;

        if( bulk >= 400 )           score += 25;
        else if( bulk >= 300 )      score += 10;
        else if( bulk >= 200 )      score += 0;
        else if( bulk >= 150 )      score -= 10;
        else if( bulk <= 100 )      score -= 20;


        Debug.Log( $"[AI Scoring][Outgoing Pressure Check] {me.NickName}'s Overall Bulk checked. Score: {score}" );

        Debug.Log( $"[AI Scoring][Outgoing Pressure Check] {me.NickName}'s HP Ratio is: {hpRatio}" );
        if( hpRatio < 0.25f )           score -= 30;
        else if( hpRatio < 0.5f )       score -= 15;
        ///00

        Debug.Log( $"[AI Scoring][Outgoing Pressure Check] {me.NickName}'s HP checked for low percentage. Score: {score}" );

        Debug.Log( $"[AI Scoring][Outgoing Pressure Check] {me.NickName}'s Final Score: {score}" );
        
        return Mathf.Clamp( score, 0, 250 );
    }

    public BoardContext GetBoardContext( BattleUnit target, ExchangeEvaluation eval )
    {
        var safePivot = GetSafePivot( target );
        var materialStatus = GetMaterialStatus( Unit );

        bool lowHP = eval.AttackerHPR < 0.3f;
        bool likelyDying = eval.OpponentPTKOR.PTKO >= PotentialToKO.Dangerous;

        bool isForced = ( likelyDying && !safePivot.Exists ) || ( lowHP && eval.OpponentPTKOR.PTKO >= PotentialToKO.Risky );

        int myAlive = GetRemainingAllyPokemon( Unit.Pokemon ).Count;
        int oppAlive = GetRemainingOpposingPokemon( target.Pokemon ).Count;

        bool isTerminal = myAlive <= 2;

        float hp = Get_HPRatio( Unit.Pokemon );
        float expendability = GetExpendability( Unit.Pokemon, hp );

        BoardContext context = new()
        {
            IsForcedTrade = isForced,

            HasSafePivot = safePivot.Exists,
            SafePivots = safePivot.pivots,

            IsAhead = materialStatus.ahead,
            IsBehind = materialStatus.behind,

            MyTeamHPPercent = materialStatus.myHP,
            OppTeamHPPercent = materialStatus.oppHP,

            MyAliveCount = myAlive,
            OppAliveCount = oppAlive,
            IsTerminal = isTerminal,

            MyExpendability = expendability,
        };

        return context;
    }

    private ( bool Exists, List<Pokemon> pivots ) GetSafePivot( BattleUnit opponent )
    {
        bool exists;
        List<Pokemon> pivots = new();
        var myTeam = BattleSystem.GetAllyParty( Unit.Pokemon );

        for( int i = 0; i < myTeam.Count; i++ )
        {
            var mon = myTeam[i];
            if( mon != Unit.Pokemon )
            {
                var pivotHP = Get_HPRatio( mon );
                if( !mon.IsFainted() && pivotHP > 0.35f )
                {
                    var targetThreateningMove = Get_MostThreateningMove( opponent.Pokemon, mon );
                    var attackerWSR = Get_WallingScoreResult( opponent.Pokemon, mon, targetThreateningMove );
                    float targetHP = Get_HPRatio( opponent.Pokemon );
                    PotentialToKOResult pivotPTKO_target = Get_PotentialToKOResult( attackerWSR, targetThreateningMove.Modifier, targetHP );

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

    private ( bool ahead, bool behind, float myHP, float oppHP ) GetMaterialStatus( BattleUnit me )
    {
        //--My team & amount of pokemon alive
        var myTeam = BattleSystem.GetAllyParty( me.Pokemon );
        int myAlive = BattleSystem.GetAllyParty( me.Pokemon ).Select( p => p ).Where( p => p.CurrentHP > 0 ).ToList().Count;

        //--Opposing team & amount of their pokemon alive
        var oppTeam = BattleSystem.GetOpposingParty( me.Pokemon );
        int oppAlive = BattleSystem.GetOpposingParty( me.Pokemon ).Select( p => p ).Where( p => p.CurrentHP > 0 ).ToList().Count;

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

        return ( isAhead, isBehind, myTeamHPPercent, oppTeamHPPercent );
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

    public float GetExpendability( Pokemon mon, float hp )
    {
        Debug.Log( $"===[Getting Expendability for {mon.NickName}]===" );

        float score = 0.5f;

        if( hp < 0.4f )     score += 0.2f;
        if( hp < 0.25f )    score += 0.2f;
        if( hp < 0.1f )     score += 0.2f;

        Debug.Log( $"HP Ratio: {hp}, Score: {score}" );

        float offensiveWeight = TeamPieceValues[mon].OffensiveValue / 100f;

        score -= offensiveWeight * 0.4f;

        Debug.Log( $"Offensive Weight: {offensiveWeight}. Score: {score}" );

        float expendability = Mathf.Clamp01( score );

        Debug.Log( $"===[{mon.NickName}'s Final clamped Expendability Score: {expendability}]===" );

        return expendability;
    }

    public float Get_HPRatio( Pokemon pokemon )
    {
        float currentHP = pokemon.CurrentHP;
        float maxHP = pokemon.MaxHP;

        Debug.Log( $"[AI Scoring][Getting HP Ratio] {pokemon.NickName}'s HP Ratio is: {currentHP/maxHP}" );
        return currentHP / maxHP;
    }

    public ThreatResult GetThreat_ImmediateDamage( List<BattleUnit> opponents, Pokemon ourPokemon )
    {
        int highestThreat = int.MinValue;
        BattleUnit highestUnit = null;

        foreach( var threat in opponents )
        {
            int threatScore = 100;
            float moveThreat = float.MinValue;

            Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] Starting threat check on {threat.Pokemon.NickName}. Starting Score: {threatScore}" );

            //--Offensive Pressure
            float offensivePressure;
            if( threat.Pokemon.Attack > threat.Pokemon.SpAttack )
                offensivePressure = threat.Pokemon.Attack;
            else
                offensivePressure = threat.Pokemon.SpAttack;

            Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Offensive Pressure is: {offensivePressure}" );
            
            if( offensivePressure >= 150f )             threatScore += 40;
            else if( offensivePressure >= 125f )        threatScore += 25;
            else if( offensivePressure >= 100f )        threatScore += 10;
            else if( offensivePressure >= 80f )         threatScore += 0;
            else if( offensivePressure >= 65f )         threatScore -= 10;
            else if( offensivePressure >= 50f )         threatScore -= 25;
            else if( offensivePressure < 50f )          threatScore -= 40;

            Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Offensive Pressure checked. Score: {threatScore}" );

            //--Move Threat
            foreach( var move in threat.Pokemon.ActiveMoves )
            {
                if( move.MoveSO.Power <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                    continue;

                if( threat.IsChoiceItemLocked() )
                    if( move != threat.LastUsedMove )
                        continue;

                var field = BattleSystem.Field;

                float effectiveness     = TypeChart.GetEffectiveness( move.MoveType, ourPokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, ourPokemon.PokeSO.Type2 );
                float stab              = threat.Pokemon.CheckTypes( move.MoveType ) ? 1.5f : 1f;
                float weather           = 1f;
                float terrain           = 1f;
                float item              = 1f;

                if( field.Weather != null )
                {
                    if( UnitSim.WeatherDMGModifiers.TryGetValue( field.Weather.ID, out var mod ) )
                        weather = mod( move );
                }

                if( field.Terrain != null )
                {
                    if( UnitSim.TerrainDMGModifiers.TryGetValue( field.Terrain.ID, out var mod ) )
                        terrain = mod( move );
                }

                if( ourPokemon.BattleItemEffect != null )
                {
                    if( UnitSim.ItemDMGModifiers.TryGetValue( ourPokemon.BattleItemEffect.ID, out var mod ) )
                        item = mod( ourPokemon, threat.Pokemon, move );
                }

                Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] Score-ing {threat.Pokemon.NickName}'s move {move.MoveSO.Name}. Effectiveness Modifier: {effectiveness}, STAB Modifier: {stab}, Weather Modifier: {weather}" );

                float currentMoveThreat = effectiveness * stab * weather * terrain * item;
                moveThreat = Mathf.Max( moveThreat, currentMoveThreat );

                Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s move {move.MoveSO.Name} checked. Move's Score: {moveThreat}" );
            }

                 if( moveThreat >= 9f )             threatScore += 90; //--Upper bounds, this move is 4x effective, has STAB, and benefits from weather.
            else if( moveThreat >= 6f )             threatScore += 60; //--This move is 4x effective, and either has STAB OR benefits from weather.
            else if( moveThreat >= 4f )             threatScore += 40; //--This move is 4x effective, or has some combination of 2x effective, stab, and weather.
            else if( moveThreat >= 3 )              threatScore += 30; //--This move is 3x effective. It is likely a 2x effective move with stab.
            else if( moveThreat >= 2f )             threatScore += 20;
            else if( moveThreat >= 1.5f )           threatScore += 15;
            else if( moveThreat >= 1f )             threatScore += 0;
            else if( moveThreat >= 0.5f )           threatScore -= 15;
            else if( moveThreat >= 0.25f )          threatScore -= 25;
            else if( moveThreat == 0f )             threatScore = 0;

            Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Moves have all been checked. Score: {threatScore}" );
            var ourSpeed = GetUnitInferredStat( ourPokemon, Stat.Speed );
            var threatSpeed = GetUnitInferredStat( threat.Pokemon, Stat.Speed );
            //--Higher speed means the target is more threatening
            if( threatSpeed > ourSpeed )
                threatScore += 20;
            else if( threatSpeed < ourSpeed )
                threatScore -= 20;

            Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Speed comparison checked. Score: {threatScore}" );

            //--Current HP Ratio. Lower HP means we're more threatened
            float hpRatio = Get_HPRatio( ourPokemon );

            Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Current HP Ratio is: {hpRatio}" );

            if( hpRatio < 0.25f )           threatScore += 30;
            else if( hpRatio < 0.5f )       threatScore += 15;
            else if( hpRatio < 0.75f )      threatScore += 5;

            Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Current HP Ratio checked. Score: {threatScore}" );

            threatScore = Mathf.Clamp( threatScore, 0, 300 );

            if( threatScore > highestThreat )
            {
                highestThreat = threatScore;
                highestUnit = threat;
            }

            Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] The current most threatening Pokemon is: {highestUnit.Pokemon.NickName}, with a Score of: {highestThreat}" );

        }

        Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] The most threatening Pokemon is: {highestUnit.Pokemon.NickName}, with a Score of: {highestThreat}" );

        return new(){ Score = highestThreat, Unit = highestUnit };
    }

    public bool Check_UnitHasPriority( BattleUnit attacker, BattleUnit target )
    {
        for( int i = 0; i < attacker.Pokemon.ActiveMoves.Count; i++ )
        {
            if( BattleSystem.Field.Terrain != null && BattleSystem.Field.Terrain.ID == TerrainID.Psychic )
                return false;
            else
            {
                if( attacker.Pokemon.ActiveMoves[i].Priority > MovePriority.Zero && attacker.Pokemon.ActiveMoves[i].MoveSO.MoveCategory != MoveCategory.Status )
                {
                    if( attacker.Pokemon.ActiveMoves[i].MoveSO.Name == "Fake Out" )
                        return CanUseFakeOut( attacker, target );
                    else
                        return true;
                }
            }
        }

        return false;
    }

    public bool CanUseFakeOut( BattleUnit attacker, BattleUnit target )
    {
        if( !attacker.Pokemon.CheckHasMove( "Fake Out" ) )
            return false;

        if( attacker.Flags[UnitFlags.TurnsTaken].Count > 0 )
        {
            Debug.Log( $"[AI Scoring] Fake Out user {attacker.Pokemon.NickName}'s Turn Count: {attacker.Flags[UnitFlags.TurnsTaken].Count}" );
            return false;
        }

        if( target.Pokemon.CheckTypes( PokemonType.Ghost ) )
            return false;

        return true;
    }

    public bool Check_IsLastPokemon()
    {
        if( BattleSystem.BattleType == BattleType.WildBattle_1v1 )
            return true;

        var activeEnemyPokemon = BattleSystem.EnemyUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
        var remainingPokemon = BattleSystem.TopTrainer1.GetHealthyPokemon( dontInclude: activeEnemyPokemon );

        return remainingPokemon == null && activeEnemyPokemon.Count > 0;
    }

    public MoveThreatResult Get_MostThreateningMove( Pokemon attacker, Pokemon target, bool preview = false )
    {
        int score = 100;
        int bestMoveScore = 0;
        float modifier = float.MinValue;
        Move bestMove = null;

        //--Move Threat
        foreach( var move in attacker.ActiveMoves )
        {
            if( move.MoveSO.Power <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                continue;

            int currentMoveScore = 0;

            float effectiveness     = TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type2 );
            float stab              = attacker.CheckTypes( move.MoveType ) ? 1.5f : 1f;
            float weather           = 1f;
            float terrain           = 1f;
            float item              = 1f;

            var field = BattleSystem.Field;

            if( field.Weather != null && !preview )
            {
                if( UnitSim.WeatherDMGModifiers.TryGetValue( field.Weather.ID, out var mod ) )
                    weather = mod( move );
            }

            if( field.Terrain != null && !preview )
            {
                if( UnitSim.TerrainDMGModifiers.TryGetValue( field.Terrain.ID, out var mod ) )
                    terrain = mod( move );
            }

            if( attacker.BattleItemEffect != null )
            {
                if( UnitSim.ItemDMGModifiers.TryGetValue( attacker.BattleItemEffect.ID, out var mod ) )
                    item = mod( attacker, target, move );
            }

            if( effectiveness == 0 )
                continue;

            Debug.Log( $"[AI Scoring][Most Threatening Move][{attacker.NickName}][{move.MoveSO.Name}] Effectiveness: {effectiveness}, STAB: {stab}, Weather: {weather}, Terrain: {terrain}, Item: {item}" );

            float currentMoveThreat = effectiveness * stab * weather * terrain * item;

            int movePower = move.MovePower;

            //--Multi hit move power projection
            if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y != 0 )
            {
                int minHits = move.MoveSO.HitRange.x;
                int maxHits = move.MoveSO.HitRange.y;

                int expectedHits = Mathf.FloorToInt( ( minHits + maxHits ) * 0.5f );

                movePower *= expectedHits;
            }
            
            if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y == 0 )
            {
                for( int i = 0; i < move.MoveSO.HitRange.x; i++ )
                {
                    movePower += movePower;
                }
            }

            if( movePower >= 90 )              currentMoveScore += 30;
            else if( movePower >= 60 )         currentMoveScore += 20;
            else if( movePower >= 45 )         currentMoveScore += 15;
            else if( movePower >= 30 )         currentMoveScore += 10;
            else if( movePower >= 15 )         currentMoveScore += 5;

            int accuracy = move.MoveSO.Accuracy;
            if( accuracy < 100 )                    currentMoveScore -= 5;
            else if( accuracy < 90 )                currentMoveScore -= 10;


            if( currentMoveThreat > modifier )
            {
                modifier = currentMoveThreat;
                bestMove = move;
                bestMoveScore = currentMoveScore;
            }

            //--If the attacker is choice-locked, when we get to the move we're locked into we log all of the scores and force-break from the loop
            //--because we cannot use any other move, and should always return this move as the "most threatening" because it is the ONLY threatening move.
            var attUnit = BattleSystem.GetPokemonBattleUnit( attacker );
            if( attUnit != null )
            {
                if( attUnit.Flags[UnitFlags.ChoiceItem].IsActive )
                {
                    if( attUnit.LastUsedMove != null && attUnit.LastUsedMove == move )
                    {
                        modifier = currentMoveThreat;
                        bestMove = move;
                        bestMoveScore = currentMoveScore;
                        break;
                    }
                }
            }

            Debug.Log( $"[AI Scoring][Most Threatening Move][{attacker.NickName}][{move.MoveSO.Name}] Modifier: {currentMoveThreat}" );
        }

        if( modifier >= 9f )                  score += 90; //--Upper bounds, this move is 4x effective, has STAB, and benefits from weather.
        else if( modifier >= 6f )             score += 60; //--This move is 4x effective, and either has STAB OR benefits from weather.
        else if( modifier >= 4f )             score += 40; //--This move is 4x effective, or has some combination of 2x effective, stab, and weather.
        else if( modifier >= 3f )             score += 30; //--This move is 3x effective. It likely has 2x type effectiveness + stab.
        else if( modifier >= 2f )             score += 20;
        else if( modifier >= 1.5f )           score += 15;
        else if( modifier >= 1f )             score += 0;
        else if( modifier >= 0.5f )           score -= 20;
        else if( modifier >= 0.25f )          score -= 40;
        else if( modifier == 0f )             score = 0;

        score += bestMoveScore;

        if( bestMove != null )
            Debug.Log( $"[AI Scoring][Most Threatening Move][{attacker.NickName}] Most Threatening Move is: {bestMove.MoveSO.Name}. Modifier: {modifier}, Score: {score}" );
        else
        {
            Debug.Log( $"[AI Scoring][Most Threatening Move][{attacker.NickName}] No threatening move found! Score should be 0. Score: {score}. Assigning a random move..." );
            bestMove = attacker.GetRandomMove();
        }

        return new(){ Score = score, Modifier = modifier, Move = bestMove };
    }

    public WallingScoreResult Get_WallingScoreResult( Pokemon attacker, Pokemon target, MoveThreatResult moveThreat )
    {
        int off = 1;
        int def = 1;
        Stat offStat = Stat.Attack;
        Stat defStat = Stat.Defense;
        string key = "none";
        float movePower = moveThreat.Move.MovePower;

        if( moveThreat.Move != null )
            key = moveThreat.Move.MoveSO.Name;

        if( UniqueWallScores.ContainsKey( key ) )
        {
            // do stuff
            off = GetBaseStat( attacker, UniqueWallScores[key].AttackingStat );
            def = GetBaseStat( attacker, UniqueWallScores[key].DefendingStat );
        }
        else
        {
            // do everything else
            //--Right now MoveThreatResult has scenarios where it isn't returning a move. I need to iron this out asap!!!
            MoveCategory cat;
            if( moveThreat.Move != null )
                cat = moveThreat.Move.MoveSO.MoveCategory;
            else
                cat = MoveCategory.Status;

            if( cat == MoveCategory.Physical )
            {
                offStat = Stat.Attack;
                defStat = Stat.Defense;
                off = GetBaseStat( attacker, offStat );
                def = GetBaseStat( target, defStat );
            }
            else if( cat == MoveCategory.Special )
            {
                offStat = Stat.SpAttack;
                defStat = Stat.SpDefense;
                off = GetBaseStat( attacker, offStat );
                def = GetBaseStat( target, defStat );
            }
            else
            {
                //--Status move used, we may need to alter this somehow
                off = 1;
                def = 1;
            }
        }

        // int score = ( def - off ) + WALLINGSCORE_NORMALIZATION_OFFSET; //--30 is the normalization offset;
        // int score = Mathf.FloorToInt( Mathf.Log( ( (float)Mathf.Max( 1f, def ) / Mathf.Max( 1f, off ) ) ) * WALLINGSCORE_LOGSCALING_FACTOR + WALLINGSCORE_NORMALIZATION_OFFSET );
        float statRatio = (float)Mathf.Max( 1f, def ) / (float)Mathf.Max( 1f, off );

        float statComponent = MathF.Log( statRatio );
        float powerComponent = Mathf.Log( (float)Mathf.Max( 1f, movePower ) / MOVE_POWER_BASELINE );

        float rawScore = ( statComponent - powerComponent ) * WALLINGSCORE_LOGSCALING_FACTOR + WALLINGSCORE_NORMALIZATION_OFFSET;

        int score = Mathf.FloorToInt( rawScore );

        Debug.Log( $"[AI Scoring][Get Walling Score] Getting Walling Score! Target {target.NickName}'s Defense: {def}, Attacker {attacker.NickName}'s Offense: {off}. Move's Power: {movePower}. Stat Component: {statComponent}. Power Component: {powerComponent}. Raw Score: {rawScore}. Final Score: {score}" );

        WallingScoreResult wsr = new()
        {
            Score = score,

            AttackingStatStage = attacker.StatStages[offStat],
            DefendingStatStage = target.StatStages[defStat],

            AttackingDirectModifier = attacker.DirectStatModifiers[offStat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm ),
            DefendingDirectModifier = target.DirectStatModifiers[defStat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm ),
        };

        return wsr;
    }

    public PotentialToKOResult Get_PotentialToKOResult( WallingScoreResult wsr, float moveModifier, float targetHPRatio )
    {
        PotentialToKO basePotentialKO = Get_PotentialToKOFromWallingScore( wsr.Score );
        
        //--Move Modifier shift
        int moveShift = Get_MoveModifierPTKOShift( moveModifier );

        //--HP Ratio shift
        int hpShift = Get_HPRatioPTKOShift( targetHPRatio );

        int tacticalShift = 0;

        //--Attacker attacking stat stage and direct modifier shifts
        tacticalShift += Get_StatStagePTKOShift( wsr.AttackingStatStage );
        tacticalShift += Get_DirectModifierPTKOShift( wsr.AttackingDirectModifier );

        //--Target defending stat stage and direct modifier shifts
        tacticalShift -= Get_StatStagePTKOShift( wsr.DefendingStatStage );
        tacticalShift -= Get_DirectModifierPTKOShift( wsr.DefendingDirectModifier );

        int finalShift = moveShift + hpShift + tacticalShift;

        int finalClassInt = Mathf.Clamp( (int)basePotentialKO + finalShift, (int)PotentialToKO.HardWall, (int)PotentialToKO.OHKO );

        //--This checks to see if the target is immune to the selected move (a 0 move modifier means effectiveness was 0). if it is, the ptko is a hardwall. otherwise, we use the appropriate shift.
        var finalClass = moveModifier == 0 ? PotentialToKO.HardWall : (PotentialToKO)finalClassInt;

        return new()
        {
            Score = Get_PotentialToKOScoreFromEnum( finalClass ),
            PTKO = finalClass,
            Modifier = moveModifier,
        };
    }

    private PotentialToKOResult Get_PTKOResultPreview( WallingScoreResult wsr, float moveModifier )
    {
        PotentialToKO basePTKO = Get_PotentialToKOFromWallingScore( wsr.Score );
        int shift = Get_MoveModifierPTKOShift( moveModifier );

        int finalClassInt = Mathf.Clamp( (int)basePTKO + shift, (int)PotentialToKO.HardWall, (int)PotentialToKO.OHKO );
        var finalClass = moveModifier == 0 ? PotentialToKO.HardWall : (PotentialToKO)finalClassInt;

        return new()
        {
            Score = Get_PotentialToKOScoreFromEnum( finalClass ),
            PTKO = finalClass,
            Modifier = moveModifier,
        };
    }

    private PotentialToKO Get_PotentialToKOFromWallingScore( int wallingScore )
    {

        PotentialToKO potentialKO;
        if( wallingScore >= 35 )                potentialKO = PotentialToKO.HardWall;       //--Hard Wall, Shuts down pressure
        else if( wallingScore >= 25 )           potentialKO = PotentialToKO.Sturdy;         //--Sturdy, can take a couple hits
        else if( wallingScore >= 10 )           potentialKO = PotentialToKO.Safe;           //--Safe, can take an extra hit
        else if( wallingScore >= -10 )          potentialKO = PotentialToKO.TwoHKO;         //--Neutral, possible 2HKO
        else if( wallingScore >= -25 )          potentialKO = PotentialToKO.Risky;          //--Getting Risky, almost guaranteed 2HK0
        else if( wallingScore >= -35 )          potentialKO = PotentialToKO.Dangerous;      //--Danger, high damage expected, crit or unexpected damage might OHKO
        else                                    potentialKO = PotentialToKO.OHKO;           //--Fatal, Likely OHKO

        return potentialKO;
    }
    
    public int Get_PotentialToKOScoreFromEnum( PotentialToKO koClass )
    {
        //--This is a damn pretty switch, sheesh //--shift safe, sturdy, hardwall scores up a bit, maybe by 5-10, and shift neutral and lower down quite a lot, with bigger negative values for dangerous and ohko than their safe equivalents.
        return koClass switch
        {
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

    private int Get_MoveModifierPTKOShift( float moveModifier )
    {
        //--A higher modifier shifts positively because the enum starts and 0 and increases. HardWall is 0, while LikelyOHKO is 6
        //--A higher modifier means increased damage, therefore the likelyhood of a KO increases.

        int shift = 0;

        // if( moveModifier == 0 )                 shift = -7;
        // else if( moveModifier <= 0.25f )        shift = -4;
        // else if( moveModifier <= 0.5f )         shift = -3;
        // else if( moveModifier <= 0.75f )        shift = -2;
        // else if( moveModifier <= 1.5f )         shift = +0;
        // else if( moveModifier <= 2f )           shift = +1;
        // else if( moveModifier <= 3f )           shift = +2;
        // else if( moveModifier <= 4f )           shift = +3;
        // else if( moveModifier <= 6f )           shift = +5;
        // else if( moveModifier <= 8f )           shift = +7;

        float log = Mathf.Log( moveModifier, 1.5f );

        shift = Mathf.RoundToInt( log ); //--maybe add a small * 1.1 or something here.
        
        Debug.Log( $"[AI Scoring][Shift Potential To KO] Move modifier shifting KO Potential by: {shift}" );

        return shift;
    }

    private int Get_HPRatioPTKOShift( float targetHPratio )
    {
        int shift = 0;

        if( targetHPratio < 0.15f )             shift = +6;
        else if( targetHPratio < 0.25f )        shift = +5;
        else if( targetHPratio < 0.35f )        shift = +4;
        else if( targetHPratio < 0.5f )         shift = +3;
        else if( targetHPratio < 0.75f )        shift = +1;

        Debug.Log( $"[AI Scoring][Shift Potential To KO] Target's HP Ratio shifting KO Potential by: {shift}" );

        return shift;
    }

    private int Get_StatStagePTKOShift( int stage )
    {
        int shift = -0;

        if( stage <= -3 )       shift = -2;
        else if( stage <= -1 )  shift = -1;
        else if( stage <= 0 )   shift = 0;
        else if( stage <= 2)    shift = +1;
        else if( stage <= 4 )   shift = +2;
        else if( stage > 4 )    shift = +2;

        Debug.Log( $"[AI Scoring][Shift Potential To KO] Target's Stat Stage for its defending stat shifting KO Potential by: {shift}" );

        return shift;
    }

    private int Get_DirectModifierPTKOShift( float totalMod )
    {
        int shift = 0;

        if( totalMod <= 0.5f )             shift += -2;
        else if( totalMod <= 0.75f )       shift += -1;
        else if( totalMod <= 1.1f )        shift += 0;
        else if( totalMod <= 1.5f )        shift += 1;
        else if( totalMod <= 2f )          shift += 2;
        else if( totalMod > 2f )           shift += 3;

        Debug.Log( $"[AI Scoring][Shift Potential To KO] Target's Direct Modifier to its defending stat shifting KO Potential by: {shift}" );

        return shift;
    }

    public float Get_PTKODamagePercent( PotentialToKO ptko )
    {
        return ptko switch
        {
            PotentialToKO.HardWall      => 0.075f,
            PotentialToKO.Sturdy        => 0.225f,
            PotentialToKO.Safe          => 0.375f,
            PotentialToKO.TwoHKO        => 0.55f,
            PotentialToKO.Risky         => 0.725f,
            PotentialToKO.Dangerous     => 0.90f,
            PotentialToKO.OHKO          => 1.10f,
            _ => 0f
        };
    }

    private PotentialToKO Get_NeutralPTKO( Pokemon attacker, Pokemon target )
    {
        var move    = Get_MostThreateningMove( attacker, target, true );
        var wsr     = Get_WallingScoreResult( attacker, target, move );
        var result  = Get_PTKOResultPreview( wsr, move.Modifier );

        return result.PTKO;
    }

    private void InitializeUniqueWallScores()
    {
        UniqueWallScores = new()
        {
            { "Body Press", new(){ AttackingStat = Stat.Defense, DefendingStat = Stat.Defense } },
        };
    }

    public void RefreshTeamPieceValues( List<Pokemon> team )
    {
        Debug.Log( $"[AI Scoring][Piece Value] Refreshing Team Piece Values!" );
        TeamPieceValues = new();

        var attackingTiers = PV_GetRankBonuses( team, mon => Mathf.Max( GetUnitInferredStat( mon, Stat.Attack ), GetUnitInferredStat( mon, Stat.SpAttack ) ) );
        var speedTiers = PV_GetRankBonuses( team, mon => GetUnitInferredStat( mon, Stat.Speed ) );

        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];

            ( int offensiveValue, int threatCount, int speedScore ) = PV_GetOffensiveValue( mon, attackingTiers, speedTiers );

            PieceValue value = new()
            {
                OffensiveValue = offensiveValue,
                ThreatCount = threatCount,
                SpeedScore = speedScore,
            };

            TeamPieceValues.Add( mon, value );
            Debug.Log( $"[AI Scoring][Piece Value] {mon.NickName} value assigned! Offensive Value: {value.OffensiveValue}, Speed Score: {value.SpeedScore}" );
        }
    }

    private ( int OffensiveValue, int threatCount, int SpeedScore ) PV_GetOffensiveValue( Pokemon pokemon, Dictionary<Pokemon, int> attackingRanks, Dictionary<Pokemon, int> speedRanks )
    {
        var oppTeam = BattleSystem.GetOpposingParty( pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
        int score = 50;

        score += attackingRanks[pokemon];
        score += speedRanks[pokemon];

        //--PTKO Stuff here
        int threatCount = 0;
        int spreadPressure = 0;
        for( int i = 0; i < oppTeam.Count; i++ )
        {
            var opp = oppTeam[i];
            var ptko = Get_NeutralPTKO( pokemon, opp );
            if( ptko >= PotentialToKO.TwoHKO )
                threatCount++;

            spreadPressure += ptko switch
            {
                PotentialToKO.TwoHKO    => 3,
                PotentialToKO.Risky     => 5,
                PotentialToKO.Dangerous => 10,
                PotentialToKO.OHKO      => 15,
                _ => 0
            };
        }

        if( threatCount > 2 )          score += 5;
        // if( threatCount >= 3 )          score += 15;
        // else if( threatCount == 2 )     score += 10;
        // else if( threatCount == 1 )     score += 5;

        return ( score, threatCount, speedRanks[pokemon] );
    }

    private Dictionary<Pokemon, int> PV_GetRankBonuses( List<Pokemon> team, Func<Pokemon, int> valueSelector )
    {
        List<( Pokemon Mon, int Value )> statList = new();
        Dictionary<Pokemon, int> tiers = new();

        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            int value = valueSelector( mon );
            statList.Add( ( mon, value ) );
        }

        var sorted = statList.OrderByDescending( t => t.Value ).Select( t => t.Mon ).ToList();

        for( int i = 0; i < sorted.Count; i++ )
        {
            int score = 0;

            if( i == 0 )        score = 15;
            else if( i == 1 )   score = 10;
            else if( i == 2 )   score = 5;

            tiers.Add( sorted[i], score );
        }

        return tiers;
    }

    public float Get_HPRatio_AfterEntryHazards( Pokemon pokemon )
    {
        Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Getting HP Ratio for {pokemon.NickName} after taking entry hazard damage!" );
        float hpR = Get_HPRatio( pokemon );
        float damage = Get_EntryHazardDamage( pokemon );

        float finalHPR = Mathf.Max( 0f, hpR - damage );
        Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName}'s Raw HPR: {hpR}, HPR after Hazards: {finalHPR}" );

        return finalHPR;
    }

    public float Get_EntryHazardDamage( Pokemon pokemon )
    {
        float damage = 0;
        var myCourtLoc = BattleSystem.Field.GetPokemonCourtLocationFromTrainer( pokemon );

        Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName} was found in the {myCourtLoc}!" );

        //--Heavy duty boots prevents hazard damage.
        if( pokemon.HeldItem != null && pokemon.BattleItemEffect?.ID == BattleItemEffectID.HeavyDutyBoots )
        {
            Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName} is holding Heavy Duty Boots! No hazard damage should be taken! Damage: {damage}" );
            return damage;
        }

        var court = BattleSystem.Field.ActiveCourts[myCourtLoc];
        if( court.Conditions.ContainsKey( CourtConditionID.StealthRock ) )
        {
            float effectiveness = TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.PokeSO.Type2 );
            damage += ( 1f / 8f ) * effectiveness;
            Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Stealth Rock was found in the {myCourtLoc}! Damage: {damage}" );
        }

        if( court.Conditions.ContainsKey( CourtConditionID.Spikes ) )
        {
            var spikes = court.Conditions[CourtConditionID.Spikes];
            int layers = spikes.Layers;

            if( layers == 1 )
                damage += 1f / 8f;
            else if( layers == 2 )
                damage += 1f / 6f;
            else if( layers >= 3 )
                damage += 1f / 4f;

            Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Spikes ({layers}) were found in the {myCourtLoc}! Damage: {damage}" );
        }

        return damage;
    }
}

public struct ThreatResult
{
    public int Score { get; set; }
    public BattleUnit Unit { get; set; }
}

public struct MoveThreatResult
{
    public float Score { get; set; }
    public float Modifier { get; set; }
    public Move Move { get; set; }
}

public struct SwitchCandidateResult
{
    public int Score { get; set; }
    public Pokemon Pokemon { get; set; }
    public PotentialToKOResult SwitchOffensePTKOR { get; set; }
    public PotentialToKOResult SwitchDefensePTKOR { get; set; }
    public float HPRatio { get; set; }
    public bool IsLegitimate { get; set; }
    public bool MovesFirst { get; set; }
}

public struct WallingScoreResult
{
    public int Score;
    public int AttackingStatStage;
    public int DefendingStatStage;
    public float AttackingDirectModifier;
    public float DefendingDirectModifier;
}

public struct PotentialToKOResult
{
    public int Score { get; set; }
    public PotentialToKO PTKO { get; set; }
    public float Modifier { get; set; }
}

public struct TempoStateResult
{
    public TempoState TempoState { get; set; }
    public bool AttackerHasPriority { get; set; }
    public bool TargetHasPriority { get; set; }
    public string AttackerName { get; set; }
    public string TargetName { get; set; }
}

public struct ExchangeEvaluation
{
    public string AttackerName;
    public string OpponentName;

    public bool AttackerMovesFirst;
    public bool OpponentMovesFirst;

    public bool AttackerThreatensKO;
    public bool OpponentThreatensKO;

    public bool AttackerKillsFirst;
    public bool OpponentKillsFirst;

    public bool AttackerSurvives;
    public bool OpponentSurvives;

    public PotentialToKOResult AttackerPTKOR;
    public PotentialToKOResult OpponentPTKOR;

    public float AttackerHPR;
    public float OpponentHPR;
}

public struct BoardContext
{
    public bool IsForcedTrade;
    public bool HasSafePivot;
    public bool IsAhead;
    public bool IsBehind;
    public float MyTeamHPPercent;
    public float OppTeamHPPercent;
    public List<Pokemon> SafePivots;
    public int MyAliveCount;
    public int OppAliveCount;
    public bool IsTerminal;
    public float MyExpendability;
}

public struct UniqueWallingScoreMove
{
    public Stat AttackingStat;
    public Stat DefendingStat;
}

public struct PieceValue
{
    public int OffensiveValue;
    public int DefensiveValue;
    public int ThreatCount;
    public int SpeedScore;
    public int SetupValue;
    public int SupportValue;
}

public struct SideHazards
{
    
}
