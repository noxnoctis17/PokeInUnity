using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;

public enum AIDecisionType { Attack, RandomMove, StrongestMove, OffensiveSwitch, DefensiveSwitch, SpeedControl, Weather, FakeOut, Protect, }
public enum PotentialToKO { HardWall, Sturdy, Safe, Neutral2HK0, Risky, Dangerous, LikelyOHKO }
public enum TempoState { WinningHard, Winning, Neutral, Losing, LosingHard }

public class BattleAI : MonoBehaviour
{
    private BattleAI_MoveCommand _moveCommand;
    private BattleAI_SwitchCommand _switchCommand;
    public BattleSystem BattleSystem { get; private set; }
    public BattleUnit Unit { get; private set; }
    public Pokemon Pokemon { get; private set; }
    public Pokemon LastSentInPokemon { get; private set; }
    public float TrainerSkillModifier { get; private set; }
    public int SwitchAmount { get; private set; }
    public const int WALLINGSCORE_NORMALIZATION_OFFSET = 30;

    public void SetupAI( BattleSystem battleSystem, BattleUnit battleUnit )
    {
        BattleSystem = battleSystem;
        Unit = battleUnit;
        Pokemon = Unit.Pokemon;

        if( battleSystem.BattleType != BattleType.WildBattle_1v1 )
            TrainerSkillModifier = Mathf.Clamp01( battleSystem.TopTrainer1.TrainerSkillLevel / 100f );

        _moveCommand = new( this );
        _switchCommand = new( this );
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

    public void ChooseCommand()
    {
        Debug.Log( $"[AI Scoring][Choose Command] Choosing a command for {Unit.Pokemon.NickName}" );
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
        var context = GetBoardContext( damageThreat.Unit, exchangeEval );
        int outgoingPressure = GetOutgoingPressure( Unit.Pokemon, damageThreat.Unit );
        var incomingCandidate = _switchCommand.GetSwitch_Defensive( opposingUnits );

        //--Get Attack Score
        int attackScore = _moveCommand.AttackScore( tempo, exchangeEval, context );
        Debug.Log( $"[AI Scoring][Choose Command] {Unit.Pokemon.NickName}'s Attack Score: {attackScore}" );

        //--Get Stay-danger Score
        int switchScore = _switchCommand.SwitchScore( tempo, exchangeEval, incomingCandidate, context );
        Debug.Log( $"[AI Scoring][Choose Command] {Unit.Pokemon.NickName}'s Switch Score: {switchScore}" );

        // bool inDanger = ( !exchangeEval.AttackerMovesFirst && !exchangeEval.TargetThreatensKO ) || ( exchangeEval.TargetThreatensKO && !exchangeEval.AttackerThreatensKO );
        // if( !inDanger )
        //     switchScore = -999;

        Debug.Log( $"[AI Scoring][Choose Command] {Unit.Pokemon.NickName}'s Final comparison for {Unit.Pokemon.NickName} vs {damageThreat.Unit.Pokemon.NickName}: Attack Score: {attackScore}, Switch Score: {switchScore}" );
        if( switchScore > attackScore )
        {
            Debug.Log( $"[AI Scoring][Choose Command] {Unit.Pokemon.NickName} is switching with {incomingCandidate.Pokemon.NickName}! Switch Amount: {SwitchAmount}" );
            IncreaseSwitchAmount();
            SetLastSentInPokemon( incomingCandidate.Pokemon );
            _switchCommand.SubmitSwitchCommand( incomingCandidate.Pokemon );
        }
        else
        {
            Debug.Log( $"[AI Scoring][Choose Command] {Unit.Pokemon.NickName} is attacking!" );
            ResetSwitchAmount();
            _moveCommand.SubmitMoveCommand( damageThreat.Unit );
        }
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
        var oppsingUnits = BattleSystem.GetOpposingUnits( Unit );
        return _switchCommand.GetSwitch_Defensive( oppsingUnits, true ).Pokemon;
    }

    public int GetUnitInferredSpeed( Pokemon pokemon )
    {
        float statValue = pokemon.PokeSO.Speed;

        int stage = pokemon.StatStages[Stat.Speed];
        var stageModifier = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f };
        float directModifier = pokemon.DirectStatModifiers[Stat.Speed].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm );

        if( stage >= 0 )
            statValue *= stageModifier[stage];
        else
            statValue /= stageModifier[-stage];

        //--Apply Direct Stat Change (Burn, Paralysis, Ruin Ability, Weather stat change, etc.)
        statValue *= directModifier;

        int final = Mathf.FloorToInt( statValue );

        return final;
    }

    public TempoStateResult GetTempoState( BattleUnit attacker, BattleUnit target, ExchangeEvaluation eval )
    {
        Debug.Log( $"[AI Scoring][Get Tempo] Starting Tempo State Check for Attacker: {attacker.Pokemon.NickName} vs Target: {target.Pokemon.NickName}" );
        var tempo = ClassifyTempo( eval );

        bool attackerHasPriorityAdvantage = eval.AttackerMovesFirst && !eval.TargetMovesFirst;
        bool targetHasPriorityAdvantage = eval.TargetMovesFirst && !eval.AttackerMovesFirst;

        Debug.Log( $"[AI Scoring][Get Tempo] Final Tempo State: {tempo}, Attacker: {attacker.Pokemon.NickName} vs Target: {target.Pokemon.NickName}" );

        return CreateTempoStateResult( tempo, attackerHasPriorityAdvantage, targetHasPriorityAdvantage );
    }

    private ExchangeEvaluation EvaluateExchange( BattleUnit attacker, BattleUnit target )
    {
        //--Speed Check
        int attackerSpeed = GetUnitInferredSpeed( attacker.Pokemon );
        int targetSpeed = GetUnitInferredSpeed( target.Pokemon );
        bool attackerHasPriorityAdvantage = Check_UnitHasPriority( attacker, target ) && !Check_UnitHasPriority( target, attacker );
        bool targetHasPriorityAdvantage = Check_UnitHasPriority( target, attacker ) && !Check_UnitHasPriority( attacker, target );
        bool attackerMovesFirst = attackerSpeed > targetSpeed || attackerHasPriorityAdvantage;
        bool targetMovesFirst = targetSpeed > attackerSpeed || targetHasPriorityAdvantage;

        Debug.Log( $"[AI Scoring][Get Tempo] Made speed comparisons! Results: Attacker Speed: {attackerSpeed}, Target Speed: {targetSpeed}, Attacker Priority: {attackerHasPriorityAdvantage}, Target Priority: {targetHasPriorityAdvantage}, Attacker Moves First: {attackerMovesFirst}, Target Moves First: {targetMovesFirst}" );

        //--Potential to KO
        //--Attacker PTKO Target
        var attackerThreateningMove = Get_MostThreateningMove( attacker, target.Pokemon );
        int targetWallscore = Get_WallingScore( attacker.Pokemon, target.Pokemon );
        float targetHP = Get_HPRatio( target.Pokemon );

        PotentialToKOResult attackerPTKO_target = Get_PotentialToKOResult( targetWallscore, attackerThreateningMove.Modifier, targetHP );

        //--Target PTKO Attacker
        var targetThreatingMove = Get_MostThreateningMove( target, attacker.Pokemon );
        int attackerWallscore = Get_WallingScore( target.Pokemon, attacker.Pokemon );
        float attackerHP = Get_HPRatio( attacker.Pokemon );

        PotentialToKOResult targetPTKO_attacker = Get_PotentialToKOResult( attackerWallscore, targetThreatingMove.Modifier, attackerHP );

        Debug.Log( $"[AI Scoring][Get Tempo] PTKO's Checked! Results: Attacker PTKO Target: {attackerPTKO_target.PotentialKO}, Target PTKO Attacker: {targetPTKO_attacker.PotentialKO}" );

        bool attackerThreatensKO_onTarget       = attackerPTKO_target.PotentialKO > PotentialToKO.Risky; //--revert back to >= if not good
        bool targetThreatensKO_onAttacker       = targetPTKO_attacker.PotentialKO > PotentialToKO.Risky; //--revert back to >= if not good
        bool attackerSurvives_targetAttack      = targetPTKO_attacker.PotentialKO <= PotentialToKO.Risky;
        bool targetSurvives_attackerAttack      = attackerPTKO_target.PotentialKO <= PotentialToKO.Risky;

        Debug.Log( $"[AI Scoring][Get Tempo] Final Comparisons Made! Results: Attacker Threatens KO: {attackerThreatensKO_onTarget}, Target Threatens KO: {targetThreatensKO_onAttacker}, Attacker Survives: {attackerSurvives_targetAttack}, Target Survives: {targetSurvives_attackerAttack}" );
        
        ExchangeEvaluation eval = new()
        {
            AttackerName = attacker.Pokemon.NickName,
            TargetName = target.Pokemon.NickName,

            AttackerMovesFirst = attackerMovesFirst,
            TargetMovesFirst = targetMovesFirst,

            AttackerThreatensKO = attackerThreatensKO_onTarget,
            TargetThreatensKO = targetThreatensKO_onAttacker,

            AttackerKillsFirst = attackerMovesFirst && attackerThreatensKO_onTarget,
            TargetKillsFirst = targetMovesFirst && targetThreatensKO_onAttacker,

            AttackerSurvives = attackerSurvives_targetAttack,
            TargetSurvives = targetSurvives_attackerAttack,

            AttackerPTKO_onTarget = attackerPTKO_target,
            TargetPTKO_onAttacker = targetPTKO_attacker,

            AttackerHPRatio = attackerHP,
            TargetHPRatio = targetHP,
        };

        Debug.Log( $"[AI Scoring][Get Tempo] Evaluation struct created! Info: {eval}:" );
        Debug.Log( $"[AI Scoring][Get Tempo] AttackerMovesFirst: {eval.AttackerMovesFirst}, TargetMovesFirst: {eval.TargetMovesFirst}" );
        Debug.Log( $"[AI Scoring][Get Tempo] AttackerThreatensKO: {eval.AttackerThreatensKO}, TargetThreatensKO: {eval.TargetThreatensKO}" );
        Debug.Log( $"[AI Scoring][Get Tempo] AttackerKillsFirst: {eval.AttackerKillsFirst}, TargetKillsFirst: {eval.TargetKillsFirst}" );
        Debug.Log( $"[AI Scoring][Get Tempo] AttackerSurvives: {eval.AttackerSurvives}, TargetSurvives: {eval.TargetSurvives}" );

        return eval;
    }

    private TempoState ClassifyTempo( ExchangeEvaluation eval )
    {
        //--Immediate Kill control
        if( eval.AttackerKillsFirst )
            return TempoState.WinningHard;

        if( eval.TargetKillsFirst )
            return TempoState.LosingHard;

        //--Both potentially survive to attack
        if( eval.AttackerSurvives && !eval.TargetSurvives )
            return TempoState.Winning;

        if( eval.TargetSurvives && !eval.AttackerSurvives )
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
            float stab              = move.MoveSO.Type == Pokemon.PokeSO.Type1 || move.MoveSO.Type == Pokemon.PokeSO.Type2 ? 1.5f : 1f;
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
        var meSpeed = GetUnitInferredSpeed( me );
        var targetSpeed = GetUnitInferredSpeed( target.Pokemon );

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

        bool lowHP = eval.AttackerHPRatio < 0.3f;
        bool likelyDying = eval.TargetPTKO_onAttacker.PotentialKO >= PotentialToKO.Dangerous;

        bool isForced = ( likelyDying && !safePivot.Exists ) || ( lowHP && eval.TargetPTKO_onAttacker.PotentialKO >= PotentialToKO.Risky );

        BoardContext context = new()
        {
            IsForcedTrade = isForced,
            HasSafePivot = safePivot.Exists,
            SafePivots = safePivot.pivots,
            IsAhead = materialStatus.ahead,
            IsBehind = materialStatus.behind,
            MyTeamHPPercent = materialStatus.myHP,
            OppTeamHPPercent = materialStatus.oppHP
        };

        return context;
    }

    private ( bool Exists, List<Pokemon> pivots ) GetSafePivot( BattleUnit opponent )
    {
        bool exists;
        List<Pokemon> pivots = new();
        var myTeam = BattleSystem.GetAllyParty( Unit );

        for( int i = 0; i < myTeam.Count; i++ )
        {
            var mon = myTeam[i];
            if( mon != Unit.Pokemon )
            {
                var pivotHP = Get_HPRatio( mon );
                if( !mon.IsFainted() && pivotHP > 0.35f )
                {
                    var targetThreateningMove = Get_MostThreateningMove( opponent, mon );
                    int attackerWallScore = Get_WallingScore( opponent.Pokemon, mon );
                    float targetHP = Get_HPRatio( opponent.Pokemon );
                    PotentialToKOResult pivotPTKO_target = Get_PotentialToKOResult( attackerWallScore, targetThreateningMove.Modifier, targetHP );

                    if( pivotPTKO_target.PotentialKO < PotentialToKO.Dangerous )
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
        var myTeam = BattleSystem.GetAllyParty( me );
        int myAlive = BattleSystem.GetAllyParty( me ).Select( p => p ).Where( p => p.CurrentHP > 0 ).ToList().Count;

        //--Opposing team & amount of their pokemon alive
        var oppTeam = BattleSystem.GetOpposingParty( me );
        int oppAlive = BattleSystem.GetOpposingParty( me ).Select( p => p ).Where( p => p.CurrentHP > 0 ).ToList().Count;

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

    public float Get_HPRatio( Pokemon pokemon )
    {
        float currentHP = pokemon.CurrentHP;
        float maxHP = pokemon.MaxHP;

        Debug.Log( $"[AI Scoring][Getting HP Ratio] {pokemon.NickName}'s HP Ratio is: {currentHP/maxHP}" );
        return currentHP / maxHP;
    }

    public ThreatResult GetThreat_ImmediateDamage( List<BattleUnit> opponents, Pokemon ourUnit )
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

                float effectiveness     = TypeChart.GetEffectiveness( move.MoveType, ourUnit.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, ourUnit.PokeSO.Type2 );
                float stab              = threat.Pokemon.CheckTypes( move.MoveType ) ? 1.5f : 1f;
                float weather           = BattleSystem.Field.Weather?.OnDamageModify?.Invoke( ourUnit, threat.Pokemon, move ) ?? 1f;
                float terrain           = BattleSystem.Field.Terrain?.OnDamageModify?.Invoke( Unit, threat.Pokemon, move ) ?? 1f;
                float item              = Unit.Pokemon.BattleItemEffect?.OnDamageModify?.Invoke( Unit, threat.Pokemon, move ) ?? 1f;

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
            var ourSpeed = GetUnitInferredSpeed( ourUnit );
            var threatSpeed = GetUnitInferredSpeed( threat.Pokemon );
            //--Higher speed means the target is more threatening
            if( threatSpeed > ourSpeed )
                threatScore += 20;
            else if( threatSpeed < ourSpeed )
                threatScore -= 20;

            Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Speed comparison checked. Score: {threatScore}" );

            //--Current HP Ratio. Lower HP means we're more threatened
            float hpRatio = Get_HPRatio( ourUnit );

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
        if( attacker.Pokemon.CheckHasMove( "Fake Out" ) )
            return false;

        if(attacker.Flags[UnitFlags.TurnsTaken].Count > 0 )
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

    public MoveThreatResult Get_MostThreateningMove( BattleUnit attacker, Pokemon target )
    {
        int score = 100;
        int bestMoveScore = 0;
        float modifier = float.MinValue;
        Move bestMove = null;

        //--Move Threat
        foreach( var move in attacker.Pokemon.ActiveMoves )
        {
            if( move.MoveSO.Power <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                continue;

            int currentMoveScore = 0;

            float effectiveness     = TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.PokeSO.Type2 );
            float stab              = attacker.Pokemon.CheckTypes( move.MoveType ) ? 1.5f : 1f;
            float weather           = BattleSystem.Field.Weather?.OnDamageModify?.Invoke( target, attacker.Pokemon, move ) ?? 1f;
            float terrain           = BattleSystem.Field.Terrain?.OnDamageModify?.Invoke( Unit, target, move ) ?? 1f;
            float item              = Unit.Pokemon.BattleItemEffect?.OnDamageModify?.Invoke( Unit, target, move ) ?? 1f;

            if( effectiveness == 0 )
                continue;

            Debug.Log( $"[AI Scoring][Most Threatening Move] Score-ing {attacker.Pokemon.NickName}'s move {move.MoveSO.Name}. Effectiveness Modifier: {effectiveness}, STAB Modifier: {stab}, Weather Modifier: {weather}" );

            float currentMoveThreat = effectiveness * stab * weather * terrain * item;

            if( move.MovePower >= 90 )              currentMoveScore += 30;
            else if( move.MovePower >= 60 )         currentMoveScore += 20;
            else if( move.MovePower >= 45 )         currentMoveScore += 15;
            else if( move.MovePower >= 30 )         currentMoveScore += 10;
            else if( move.MovePower >= 15 )         currentMoveScore += 5;

            if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y != 0 )
            {
                for( int i = 0; i < move.MoveSO.HitRange.y; i++ )
                {
                    currentMoveScore += 5;
                }
            }
            else if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y == 0 )
            {
                for( int i = 0; i < move.MoveSO.HitRange.x; i++ )
                {
                    currentMoveScore += 5;
                }
            }


            if( currentMoveThreat > modifier )
            {
                modifier = currentMoveThreat;
                bestMove = move;
                bestMoveScore = currentMoveScore;
            }

            //--If the attacker is choice-locked, when we get to the move we're locked into we log all of the scores and force-break from the loop
            //--because we cannot use any other move, and should always return this move as the "most threatening" because it is the ONLY threatening move.
            if( attacker.Flags[UnitFlags.ChoiceItem].IsActive )
            {
                if( attacker.LastUsedMove != null && attacker.LastUsedMove == move )
                {
                    modifier = currentMoveThreat;
                    bestMove = move;
                    bestMoveScore = currentMoveScore;
                    break;
                }
            }

            Debug.Log( $"[AI Scoring][Most Threatening Move] {attacker.Pokemon.NickName}'s move {move.MoveSO.Name} checked. Move's Score: {modifier}" );
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
            Debug.Log( $"[AI Scoring][Most Threatening Move] {attacker.Pokemon.NickName}'s Most Threatening Move is: {bestMove.MoveSO.Name}. Modifier: {modifier}, Score: {score}" );
        else
            Debug.Log( $"[AI Scoring][Most Threatening Move] No threatening move found! Score should be 0. Score: {score}" );

        return new(){ Score = score, Modifier = modifier, Move = bestMove };
    }

//--rewrite today to consider chosen move
    public int Get_WallingScore( Pokemon attacker, Pokemon target )
    {
        int off;
        int def;

        if( attacker.PokeSO.Attack > attacker.PokeSO.SpAttack )
        {
            off = attacker.PokeSO.Attack;
            def = target.PokeSO.Defense;
        }
        else
        {
            off = attacker.PokeSO.SpAttack;
            def = target.PokeSO.SpDefense;
        }

        return ( def - off ) + WALLINGSCORE_NORMALIZATION_OFFSET; //--30 is the normalization offset
    }

    public PotentialToKOResult Get_PotentialToKOResult( int wallingScore, float moveModifier, float targetHPRatio )
    {

        PotentialToKO basePotentialKO = Get_PotentialToKOFromWallingScore( wallingScore );
        int shift = ShiftPotentialToKO( basePotentialKO, moveModifier, targetHPRatio );

        int finalClassInt = Mathf.Clamp( (int)basePotentialKO + shift, (int)PotentialToKO.HardWall, (int)PotentialToKO.LikelyOHKO );

        var finalClass = moveModifier == 0 ? PotentialToKO.HardWall : (PotentialToKO)finalClassInt;

        return new()
        {
            Score = Get_PotentialToKOScoreFromEnum( finalClass ),
            PotentialKO = finalClass,
            Modifier = moveModifier,
        };
    }

    private PotentialToKO Get_PotentialToKOFromWallingScore( int wallingScore )
    {

        PotentialToKO potentialKO;
        if( wallingScore >= 35 )                potentialKO = PotentialToKO.HardWall;        //--Hard Wall, Shuts down pressure
        else if( wallingScore >= 25 )           potentialKO = PotentialToKO.Sturdy;          //--Sturdy, can take a couple hits
        else if( wallingScore >= 10 )           potentialKO = PotentialToKO.Safe;            //--Safe, can take an extra hit
        else if( wallingScore >= -10 )          potentialKO = PotentialToKO.Neutral2HK0;     //--Neutral, possible 2HKO
        else if( wallingScore >= -25 )          potentialKO = PotentialToKO.Risky;           //--Getting Risky, almost guaranteed 2HK0
        else if( wallingScore >= -35 )          potentialKO = PotentialToKO.Dangerous;       //--Danger, high damage expected, crit or unexpected damage might OHKO
        else                                    potentialKO = PotentialToKO.LikelyOHKO;      //--Fatal, Likely OHKO

        return potentialKO;
    }
    
    public int Get_PotentialToKOScoreFromEnum( PotentialToKO koClass )
    {
        //--This is a damn pretty switch, sheesh //--shift safe, sturdy, hardwall scores up a bit, maybe by 5-10, and shift neutral and lower down quite a lot, with bigger negative values for dangerous and ohko than their safe equivalents.
        return koClass switch
        {
            PotentialToKO.HardWall       => +70,
            PotentialToKO.Sturdy         => +40,
            PotentialToKO.Safe           => +20,
            PotentialToKO.Neutral2HK0    => 0,
            PotentialToKO.Risky          => -25,
            PotentialToKO.Dangerous      => -65,
            PotentialToKO.LikelyOHKO     => -100,
            _ => 0
        };
    }

    public int Get_OffensivePTKOScore( int score )
    {
        int off = -score;
        return Mathf.FloorToInt( off * 1.2f ); //--the higher chance of ko, the more incentivized you are because the score increases more due to being a percentage increase.
    }

    private int ShiftPotentialToKO( PotentialToKO baseKO, float modifier, float targetHPratio )
    {
        //--A higher modifier shifts positively because the enum starts and 0 and increases. HardWall is 0, while LikelyOHKO is 6
        //--A higher modifier means increased damage, therefore the likelyhood of a KO increases.
        Debug.Log( $"[AI Scoring][Shift Potential To KO] Base KO Potential: {baseKO}, Move Modifier: {modifier}, Target's HP Ratio: {targetHPratio}" );

        int shift = 0;
        int hpShift = 0;

        if( modifier > 4.5f )                   shift = +5;
        else if( modifier > 4f )                shift = +4;
        else if( modifier > 3f )                shift = +3;
        else if( modifier > 2f )                shift = +2;
        else if( modifier > 1.1f )              shift = +1;
        else if( modifier == 0f )               shift = -7;
        else if( modifier <= 0.5f )             shift = -2;
        else if( modifier <= 0.25f )            shift = -4;

        Debug.Log( $"[AI Scoring][Shift Potential To KO] Move modifier shifting KO Potential by: {shift}" );

        if( targetHPratio < 0.15f )            hpShift = +6;
        else if( targetHPratio < 0.25f )       hpShift = +5;
        else if( targetHPratio < 0.35f )       hpShift = +4;
        else if( targetHPratio < 0.5f )        hpShift = +3;
        else if( targetHPratio < 0.6f )        hpShift = +1;

        Debug.Log( $"[AI Scoring][Shift Potential To KO] Target's HP Ratio shifting KO Potential by: {hpShift}" );

        shift += hpShift;

        Debug.Log( $"[AI Scoring][Shift Potential To KO] Total shifting of KO Potential by: {shift}" );

        return shift;
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
    public PotentialToKOResult PTKOResult { get; set; }
    public float HPRatio { get; set; }
    public bool IsLegitimate { get; set; }
}

public struct PotentialToKOResult
{
    public int Score { get; set; }
    public PotentialToKO PotentialKO { get; set; }
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
    public string TargetName;

    public bool AttackerMovesFirst;
    public bool TargetMovesFirst;

    public bool AttackerThreatensKO;
    public bool TargetThreatensKO;

    public bool AttackerKillsFirst;
    public bool TargetKillsFirst;

    public bool AttackerSurvives;
    public bool TargetSurvives;

    public PotentialToKOResult AttackerPTKO_onTarget;
    public PotentialToKOResult TargetPTKO_onAttacker;

    public float AttackerHPRatio;
    public float TargetHPRatio;
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
}
