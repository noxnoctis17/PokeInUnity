using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

public enum AIDecisionType { Attack, RandomMove, ChosenMove, OffensiveSwitch, DefensiveSwitch, SpeedControl, Weather, FakeOut, Protect, }
public enum PotentialToKO { Untouchable, HardWall, Sturdy, Safe, TwoHKO, Risky, Dangerous, OHKO }
public enum TempoState { WinningHard, Winning, Neutral, Losing, LosingHard }
public enum ExchangeState { Neutral, Pressure, OpponentForcedOut }
public class BattleAI : MonoBehaviour
{
    private int _round;
    private BattleAI_ActionEvaluation _actionEval;
    public BattleSystem BattleSystem { get; private set; }
    public BattleTrainer Trainer { get; private set; }
    // public List<IBattleAIUnit> OurTeamAIUnits { get; private set; }
    public BattleAI_MoveCommand MoveCommand { get; private set; }
    public BattleAI_SwitchCommand SwitchCommand { get; private set; }
    public BattleAI_Projection Projection { get; private set; }
    public BattleAI_BattleSim BattleSim { get; private set; }
    public BattleAI_UnitSim UnitSim { get; private set; }
    public BattleAI_FinalReasoning Final { get; private set; }
    public BattleUnit Unit { get; private set; }
    public BattleAI_PokemonAdapter ThisUnitAdapter { get; private set; }
    public Pokemon LastSentInPokemon { get; private set; }
    public List<IBattleAIUnit> LastOpposingPokemon { get; private set; }
    public float TrainerSkillModifier { get; private set; }
    public int SwitchAmount { get; private set; }
    public int SetupAmount { get; private set; }
    public Dictionary<string, UniqueWallingScoreMove> UniqueWallScores { get; private set; }
    public Dictionary<string, PieceValue> TeamPieceValues { get; private set; }
    public CustomLogSession CurrentLog { get; private set; }
    public List<IBattleAIUnit> OpposingUnits { get; private set; }
    public int Round => _round;
    public CurrentPlan CurrentPlan { get; private set; }

    public void InitializeAI( BattleSystem battleSystem, BattleUnit battleUnit )
    {
        BattleSystem = battleSystem;
        Unit = battleUnit;
        Trainer = Unit.Trainer;

        if( battleSystem.BattleType != BattleType.WildBattle_1v1 )
            TrainerSkillModifier = Mathf.Clamp01( battleSystem.TopTrainer1.TrainerSkillLevel / 100f );

        UnitSim         = new( this );
        Projection      = new( this );
        BattleSim       = new( this );
        MoveCommand     = new( this );
        SwitchCommand   = new( this );
        _actionEval     = new( this );
        Final           = new( this );

        _round = 0;
        SetupAmount = 0;

        InitializeUniqueWallScores();
    }

    public void CleanupAI()
    {
        MoveCommand = null;
        SwitchCommand = null;
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

    public void IncreaseSetupAmount()
    {
        SetupAmount++;
    }

    public void ResetSetupAmount()
    {
        SetupAmount = 0;
    }

    public void SetLastSentInPokemon( Pokemon pokemon )
    {
        LastSentInPokemon = pokemon;
    }

    public void SetLastOpposingPokemon( List<IBattleAIUnit> opponents )
    {
        LastOpposingPokemon = opponents;
    }

    public List<Pokemon> GetRemainingAllyPokemon( Pokemon pokemon )
    {
        return BattleSystem.GetAllyParty( pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
    }

    public List<Pokemon> GetRemainingOpposingPokemon( Pokemon pokemon )
    {
        return BattleSystem.GetOpposingParty( pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
    }

    public List<Pokemon> GetRemainingAllyPokemon( string pid )
    {
        return BattleSystem.GetAllyParty( pid ).Where( p => p.CurrentHP > 0 ).ToList();
    }

    public List<Pokemon> GetRemainingOpposingPokemon( string pid )
    {
        return BattleSystem.GetOpposingParty( pid ).Where( p => p.CurrentHP > 0 ).ToList();
    }

    public List<IBattleAIUnit> GetPartyAsIBattleAIUnits( string pid )
    {
        var allyParty = GetRemainingAllyPokemon( pid );
        var aiParty = CreateBattleAIUnits_FromPokemon( allyParty );
        return aiParty;
    }

    public List<BattleUnit> GetOpposingUnits( string pid )
    {
        List<BattleUnit> oppUnits = new();

        for( int i = 0; i < BattleSystem.PlayerUnits.Count; i++ )
        {
            var unit = BattleSystem.PlayerUnits[i];
            if( unit.Pokemon?.PID == pid )
            {
                oppUnits = BattleSystem.GetOpposingUnits( unit );
            }
            else
                continue;
        }

        for( int i = 0; i < BattleSystem.EnemyUnits.Count; i++ )
        {
            var unit = BattleSystem.EnemyUnits[i];
            if( unit.Pokemon?.PID == pid )
            {
                oppUnits = BattleSystem.GetOpposingUnits( unit );
            }
            else
                continue;
        }

        return oppUnits;
    }

    public BattleUnit GetBattleUnit( string pid )
    {
        for( int i = 0; i < BattleSystem.PlayerUnits.Count; i++ )
        {
            var unit = BattleSystem.PlayerUnits[i];
            if( unit.Pokemon?.PID == pid )
            {
                return unit;
            }
            else
                continue;
        }

        for( int i = 0; i < BattleSystem.EnemyUnits.Count; i++ )
        {
            var unit = BattleSystem.EnemyUnits[i];
            if( unit.Pokemon?.PID == pid )
            {
                return unit;
            }
            else
                continue;
        }

        return null;
    }

    public List<IBattleAIUnit> CreateBattleAIUnits_FromBattleUnits( List<BattleUnit> units )
    {
        List<IBattleAIUnit> aiUnits = new();

        for( int i = 0; i < units.Count; i++ )
        {
            BattleAI_PokemonAdapter monAdapter = new( units[i].Pokemon, this );
            aiUnits.Add( monAdapter );
        }

        return aiUnits;
    }

    public List<IBattleAIUnit> CreateBattleAIUnits_FromPokemon( List<Pokemon> party )
    {
        List<IBattleAIUnit> aiUnits = new();

        for( int i = 0; i < party.Count; i++ )
        {
            BattleAI_PokemonAdapter monAdapter = new( party[i], this );
            aiUnits.Add( monAdapter );
        }

        return aiUnits;
    }

    public Pokemon GetPokemonFromPID( string pid )
    {
        var myTeam = BattleSystem.GetAllyParty( Unit.Pokemon );
        var oppTeam = BattleSystem.GetOpposingParty( Unit.Pokemon );

        for( int i = 0; i < myTeam.Count; i++ )
        {
            var mon = myTeam[i];
            if( mon?.PID == pid )
            {
                return mon;
            }
            else
                continue;
        }

        for( int i = 0; i < oppTeam.Count; i++ )
        {
            var mon = oppTeam[i];
            if( mon?.PID == pid )
            {
                return mon;
            }
            else
                continue;
        }

        return null;
    }

    public void ChooseCommand()
    {
        CurrentLog = new();

        _round++;

        //--Set Unit Adapter
        ThisUnitAdapter = new( Unit.Pokemon, this );

        CurrentLog.Add( $"=====[Choose Command][TURN {_round} - {ThisUnitAdapter.Name}, Offensive Piece Value: {TeamPieceValues[ThisUnitAdapter?.PID].OffensiveValue}]=====" );

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
            return;

        //--Opposing Threats
        OpposingUnits = CreateBattleAIUnits_FromBattleUnits( BattleSystem.GetOpposingUnits( Unit ) );
        var damageThreat = GetThreat_ImmediateDamage( OpposingUnits, ThisUnitAdapter );
        
        //--Get Best Action based on high level heuristics, turn outcome simulation, flat board analysis, and simulaiton result adjustments.
        var bestAction = GetBestAction( damageThreat, OpposingUnits );

        CurrentLog.Add( $"===[FINAL DECISION: {Unit.Pokemon.NickName} chose the {bestAction.Type} Action! Final Score: {bestAction.Score}]===" );
        Debug.Log( CurrentLog.ToString() );
        string path = Application.persistentDataPath + "/BattleAI_ChooseCommandLog.txt";
        System.IO.File.AppendAllText( path, CurrentLog.ToString() + "\n" );
        CurrentLog.Clear();

        switch( bestAction.Type )
        {
            case ActionType.Attack: MoveCommand.SubmitMoveCommand( bestAction );
                break;

            case ActionType.DefensiveSwitch: SwitchCommand.SubmitSwitchCommand( bestAction.SwitchPayload );
                break;

            case ActionType.OffensiveSwitch: SwitchCommand.SubmitSwitchCommand( bestAction.SwitchPayload );
                break;

            case ActionType.Setup: MoveCommand.SubmitMoveCommand( bestAction );
                IncreaseSetupAmount();
                break;

            case ActionType.OffensiveStatus: MoveCommand.SubmitMoveCommand( bestAction );
                break;
        }
    }

    private ActionEvaluation GetBestAction( ThreatResult damageThreat, List<IBattleAIUnit> opposingUnits )
    {
        //--Brain Layer Evaluations
        var exchangeEval    = Projection.EvaluateExchange( ThisUnitAdapter, damageThreat.Unit );
        var tempo           = Projection.GetTempoState( exchangeEval );
        var boardContext    = Projection.GetBoardContext( damageThreat.Unit, exchangeEval );
        var threatProfile   = GetThreatProfile( exchangeEval, boardContext, damageThreat.Unit );
        var currentPlan     = Projection.EvaluateCurrentPlan( exchangeEval, boardContext, threatProfile, CurrentPlan );
        CurrentPlan         = currentPlan;

        //--Action Candidates + TOP
        var bestAttack              = MoveCommand.GetMove_BestAttack( ThisUnitAdapter, damageThreat.Unit, "Get Best Action" );
        var defensiveSwitch         = SwitchCommand.GetSwitch_Defensive( opposingUnits );
        var offensiveSwitch         = SwitchCommand.GetSwitch_Offensive( opposingUnits );
        var bestSetup               = MoveCommand.GetMove_Setup( ThisUnitAdapter, damageThreat.Unit );
        var bestOffensiveStatus     = MoveCommand.GetMove_OffensiveStatus( ThisUnitAdapter, damageThreat.Unit );

        List<ActionEvaluation> actions = new();

        //--Attack. This is the only thing that should never actually be null. Eventually, this will return Struggle in the event there is no available attack at all due to taunt/encore/choice lock or lack of PP
        ActionEvaluation attackActionEval = default;
        if( bestAttack.Move != null )
        {
            attackActionEval = Get_AttackAction( tempo, exchangeEval, boardContext, bestAttack );
            actions.Add( attackActionEval );
        }

        //--Defensive Switch
        ActionEvaluation defSwitchActionEval = default;
        if( defensiveSwitch.Pokemon != null )
        {
            defSwitchActionEval = Get_DefensiveSwitchAction( tempo, exchangeEval, boardContext, defensiveSwitch );
            actions.Add( defSwitchActionEval );
        }

        //--Offensive Switch
        ActionEvaluation offSwitchActionEval = default;
        if( offensiveSwitch.Pokemon != null )
        {
            offSwitchActionEval = Get_OffensiveSwitchAction( tempo, exchangeEval, boardContext, offensiveSwitch );
            actions.Add( offSwitchActionEval );
        }

        //--Setup. swords dance, iron defense, dragon dance
        ActionEvaluation setupActionEval = default;
        if( bestSetup.Move != null )
        {
            setupActionEval = Get_SetupAction( tempo, exchangeEval, boardContext, bestSetup );
            actions.Add( setupActionEval );
        }

        //--Offensive Status. Thunder Wave, Toxic, Stealth Rocks, Sleep Powder, Growl
        ActionEvaluation offensiveStatusActionEval = default;
        if( bestOffensiveStatus.Move != null )
        {
            offensiveStatusActionEval = Get_OffensiveStatusAction( tempo, exchangeEval, boardContext, bestOffensiveStatus );
            actions.Add( offensiveStatusActionEval );
        }

        //--Support Status
        //--screens, manual weather, redirection, trick room, tailwind, howl

        var doomedOutcome = CheckIfDoomedTurn( actions, exchangeEval );

        if( doomedOutcome.DoomedTurn )
        {
            //--Sacrifice Evaluation of all actions
            Debug.Log( $"[Doomed!] TURN {_round} is doomed! It's all doomed! beginning Sacrifice Line Evaluations." );
            CurrentLog.Add( $"[Doomed!] TURN {_round} is doomed! It's all doomed! beginning Sacrifice Line Evaluations." );
            //--Standard Evaluation of all actions
            for( int i = 0; i < actions.Count; i++ )
            {
                actions[i] = _actionEval.EvaluateSacrificeLine( actions[i], doomedOutcome );
            }
        }
        else
        {
            //--Standard Evaluation of all actions
            for( int i = 0; i < actions.Count; i++ )
            {
                actions[i] = _actionEval.EvaluateAction( actions[i] );

                if( threatProfile.Exists )
                    actions[i].Score += _actionEval.EvaluateThreatResponse( actions[i], threatProfile, doomedOutcome, boardContext );
                
                //--PBS
                var pbs = Projection.BuildPBS( actions[i].Top1, actions[i].Top2, actions[i].ExchangeEvaluation, boardContext.MyRemainingPieces, boardContext.OppRemainingPieces );
                int futureScore = Projection.EvaluatePBS( pbs );
                CurrentLog.Add( $"Action: {actions[i].Type}. Future Score from EvaluatePBS: {futureScore}" );
                actions[i].PBS = pbs;

                int winConBias = Projection.GetCurrentPlanBias( actions[i], pbs, boardContext, CurrentPlan );
                CurrentLog.Add( $"Action: {actions[i].Type}. Current Plan is: {CurrentPlan.Type}. Bias: {winConBias}" );
                CurrentLog.Add( $"" );

                actions[i].Score += futureScore + winConBias;
            }
        }

        string attackActionText             = bestAttack.Move != null ?             $"Attack ({bestAttack.Move?.MoveSO.Name}): {attackActionEval.Score}"                                    : $"Attack not found!";
        string defensiveSwitchActionText    = defensiveSwitch.Pokemon != null ?     $"Defensive Switch ({defensiveSwitch.Pokemon?.NickName}): {defSwitchActionEval.Score}"                  : $"Defensive Switch not found!";
        string offensiveSwitchActionText    = offensiveSwitch.Pokemon != null ?     $"Offensive Switch ({offensiveSwitch.Pokemon?.NickName}): {offSwitchActionEval.Score}"                  : $"Offensive Switch not found!";
        string setupActionText              = bestSetup.Move != null ?              $"Setup Move ({bestSetup.Move?.MoveSO.Name}): {setupActionEval.Score}"                                  : $"Setup move not found!";
        string offensiveStatusActionText    = bestOffensiveStatus.Move != null ?    $"Offensive Status Move ({bestOffensiveStatus.Move?.MoveSO.Name}): {offensiveStatusActionEval.Score}"   : $"Offensive Status move not found!";

        CurrentLog.Add( $"===[Final Option Scores]===" );
        CurrentLog.Add( attackActionText );
        CurrentLog.Add( defensiveSwitchActionText );
        CurrentLog.Add( offensiveSwitchActionText );
        CurrentLog.Add( setupActionText );
        CurrentLog.Add( offensiveStatusActionText );
        CurrentLog.Add( $"" );

        actions = actions.OrderByDescending( a => a.Score ).ToList();
        ActionEvaluation bestAction;

        if( !doomedOutcome.DoomedTurn )
            bestAction = Final.ApplyFinalReasoning( actions, exchangeEval, boardContext, CurrentPlan, threatProfile );
        else
            bestAction = actions.FirstOrDefault();

        //--Select highest scored ActionEvaluation
        return bestAction;
    }

    private DoomedOutcome CheckIfDoomedTurn( List<ActionEvaluation> actions, ExchangeEvaluation exchangeEval )
    {
        //--Guaranteed Piece Loss
        int pieceLossCount = 0;
        for( int i = 0; i < actions.Count; i++ )
        {
            var action = actions[i];
            if( action.Top1.Attacker_EndOfTurnHP <= 0f )
                pieceLossCount++;
        }

        bool nearGuaranteedPieceLoss = pieceLossCount == actions.Count - 1;
        bool alwaysLoseAPiece = pieceLossCount == actions.Count;

        //--Attacker Cannot Act
        bool opponentThreatensKO        = exchangeEval.OpponentThreatensKO;
        bool attackerMovesFirst         = exchangeEval.AttackerMovesFirst;

        bool attackerCannotAct = opponentThreatensKO && !attackerMovesFirst;

        //--No Viable Switches
        int switchActionCount = 0;
        int unviableSwitches = 0;
        for( int i = 0; i < actions.Count; i++ )
        {
            var action = actions[i];
            if( action.Type == ActionType.OffensiveSwitch || action.Type == ActionType.DefensiveSwitch )
            {
                switchActionCount++;
                var switchLookAhead = MoveCommand.GetMove_BestAttack( action.Top1.Attacker, action.Top1.Opponent ).Top;

                //--We use the look ahead PTKOs because those are the PTKOs that would be in effect for the following round. we use the "current" switch simulation HP Ratios because those would be the values we start the following round with.
                bool forceSwitchNextRound = UnitSim.PredictSwitchProbability( switchLookAhead.AttackerPTKO, switchLookAhead.OpponentPTKO, switchLookAhead.AttackerMovedFirst, action.Top1.Attacker.CurrentHPR, action.Top1.Opponent.CurrentHPR ) >= 0.9f;

                //--Does this line enable a revenge kill?
                bool canKO = switchLookAhead.AttackerPTKO >= PotentialToKO.Dangerous;
                bool enablesRevenge = canKO && ( switchLookAhead.OpponentPTKO <= PotentialToKO.Risky || switchLookAhead.AttackerMovedFirst );

                bool diesNextTurn = switchLookAhead.Attacker_DiesBeforeActing || switchLookAhead.Attacker_EndOfTurnHP <= 0f;

                bool unstablePosition = diesNextTurn;
                bool badFollowUp = switchLookAhead.AttackerPTKO <= PotentialToKO.Safe && !switchLookAhead.AttackerMovedFirst;

                if( enablesRevenge )
                    continue;

                //--This checks to see if the incoming damage when we switch in was the TwoHKO damage range (0.55f damage on incoming) or more, and then checks the look ahead attack round for how threatening we are the following turn.
                if( unstablePosition || ( badFollowUp && forceSwitchNextRound ) )
                    unviableSwitches++;
            }
            else
                continue;
        }

        int viableSwitches = switchActionCount - unviableSwitches;
        bool allSwitchesDoomed = viableSwitches == 0;

        //--Opponent Sweep Check
        List<Pokemon> ourTeamToBeSwept = null;
        int fasterThan = 0;
        int threatCount = 0;
        bool theyKO;
        bool sweepBeginning;
        bool sweepIncoming;
        for( int i = 0; i < actions.Count; i++ )
        {
            var action = actions[i];
            
            BattleAI_PokemonAdapter revengeCandidate = null;
            if( action.Top1.Attacker_DiesBeforeActing || action.Top1.Attacker_EndOfTurnHP <= 0 )
            {
                var switchCandidate = SwitchCommand.GetSwitch_Revenge( OpposingUnits ).Pokemon;
                if( switchCandidate != null )
                    revengeCandidate = new( switchCandidate, this  );
            }
            else if( UnitSim.PredictSwitchProbability( action.Top1.OpponentPTKO, action.Top1.AttackerPTKO, action.Top1.AttackerMovedFirst, action.Top1.Opponent.CurrentHPR, action.Top1.Attacker.CurrentHPR ) >= 0.8f )
            {
                var switchCandidate = SwitchCommand.GetSwitch_Defensive( OpposingUnits ).Pokemon;
                if( switchCandidate != null )
                    revengeCandidate = new( switchCandidate, this  );
            }

            IBattleAIUnit nextPokemon;
            if( revengeCandidate != null )
                nextPokemon = revengeCandidate;
            else
                nextPokemon = action.Top1.Attacker;

            //--Keep in mind, this simulation is from the perspective of the opponent attacking us. Therefore, inside this TOP, WE are the opponent.
            var opponentSweepTOP = MoveCommand.GetMove_BestAttack( action.Top1.Opponent, nextPokemon ).Top;
            
            ourTeamToBeSwept = GetRemainingAllyPokemon( nextPokemon.PID );
            bool movesFirst = opponentSweepTOP.Attacker.Speed > opponentSweepTOP.Opponent.Speed;
            bool theyForceSwitch = UnitSim.PredictSwitchProbability( opponentSweepTOP.AttackerPTKO, opponentSweepTOP.OpponentPTKO, movesFirst, opponentSweepTOP.Attacker.CurrentHPR, opponentSweepTOP.Opponent.CurrentHPR ) >= 0.8f;

            theyKO = opponentSweepTOP.Opponent_DiesBeforeActing || opponentSweepTOP.Opponent_EndOfTurnHP <= 0f;
            sweepBeginning = theyKO || theyForceSwitch;

            if( sweepBeginning )
            {
                foreach( var ally in ourTeamToBeSwept )
                {
                    int allySpeed = GetUnitContextualSpeed( ally );

                    if( opponentSweepTOP.Attacker.Speed > allySpeed )
                        fasterThan++;

                    BattleAI_PokemonAdapter us = new( ally, this );
                    var ptko = Projection.Get_NeutralPTKO( opponentSweepTOP.Attacker, us );
                    if( ptko >= PotentialToKO.TwoHKO && opponentSweepTOP.Attacker.Speed > allySpeed || ptko >= PotentialToKO.Risky )
                        threatCount++;
                }
            }
        }

        if( fasterThan >= ourTeamToBeSwept.Count - 1 && ( threatCount > 3 || threatCount >= ourTeamToBeSwept.Count - 1 ) )
            sweepIncoming = true;
        else
            sweepIncoming = false;

        //--No Tempo Recovery Line Exists
        int tempoRecoveryScore = 0;
        TurnOutcomeProjection tempoCreatedTOP = default;
        for( int i = 0; i < actions.Count; i++ )
        {
            var action = actions[i];

            BattleAI_PokemonAdapter revengeCandidate = null;
            if( action.Top1.Attacker_DiesBeforeActing || action.Top1.Attacker_EndOfTurnHP <= 0 )
            {
                var switchCandidate = SwitchCommand.GetSwitch_Revenge( OpposingUnits ).Pokemon;
                if( switchCandidate != null )
                    revengeCandidate = new( switchCandidate, this  );
            }
            else if( UnitSim.PredictSwitchProbability( action.Top1.AttackerPTKO, action.Top1.OpponentPTKO, action.Top1.AttackerMovedFirst, action.Top1.Attacker.CurrentHPR, action.Top1.Opponent.CurrentHPR ) >= 0.8 )
            {
                var switchCandidate = SwitchCommand.GetSwitch_Revenge( OpposingUnits ).Pokemon;
                if( switchCandidate != null )
                    revengeCandidate = new( switchCandidate, this  );
            }

            IBattleAIUnit nextPokemon;
            if( revengeCandidate != null )
                nextPokemon = revengeCandidate;
            else
                nextPokemon = action.Top1.Attacker;

            var followUp = MoveCommand.GetMove_BestAttack( nextPokemon, action.Top1.Opponent ).Top;

            bool revengeKill = followUp.Opponent_DiesBeforeActing || followUp.Opponent_EndOfTurnHP <= 0 || ( followUp.OpponentPTKO >= PotentialToKO.TwoHKO && followUp.AttackerMovedFirst );

            float switchProb = UnitSim.PredictSwitchProbability( followUp.AttackerPTKO, followUp.OpponentPTKO, followUp.AttackerMovedFirst, nextPokemon.CurrentHPR, action.Top1.Opponent.CurrentHPR );
            bool forcesSwitch = switchProb >= 0.8f;

            bool favorableTrade = action.Top1.Opponent_EndOfTurnHP <= 0f || action.Top1.MutualKO;

            bool stabilizesNextTurn = followUp.Attacker_EndOfTurnHP > 0f && followUp.Attacker_EndOfTurnHP > 0.35f && followUp.OpponentPTKO <= PotentialToKO.TwoHKO;

            if( revengeKill )           tempoRecoveryScore += 2;
            if( forcesSwitch )          tempoRecoveryScore += 2;
            if( favorableTrade )        tempoRecoveryScore += 1;
            if( stabilizesNextTurn )    tempoRecoveryScore += 1;
        }

        bool noTempoRecoveryLine = tempoRecoveryScore == 0;
        bool weakTempoRecovery   = tempoRecoveryScore <= 2;

        //--Final Safe Line Check
        bool safeLineExists = false;
        for( int i = 0; i < actions.Count; i++ )
        {
            var action = actions[i];

            bool survives = action.Top1.Attacker_EndOfTurnHP > 0f && !action.Top1.Attacker_DiesBeforeActing;
            bool stabilizes = action.Top1.OpponentPTKO <= PotentialToKO.TwoHKO || action.Top1.Attacker_EndOfTurnHP >= 0.4f;

            if( survives && stabilizes )
            {
                safeLineExists = true;
                break;
            }
            else
                continue;
        }

        //--Overall Pressure check
        float pressure = 0;
        
        if( nearGuaranteedPieceLoss )       pressure += 1.0f;
        if( alwaysLoseAPiece )              pressure += 2.0f;

        if( allSwitchesDoomed )             pressure += 2.0f;
        else if( viableSwitches == 1 )      pressure += 1.0f;

        if( sweepIncoming )                 pressure += 2.5f;

        if( noTempoRecoveryLine )           pressure += 2.5f;
        else if( weakTempoRecovery )        pressure += 1.0f;

        if( attackerCannotAct )             pressure += 1.5f;

        if( safeLineExists )                pressure -= 2.5f;

        bool doomedTurn = pressure >= 5f;

        if( doomedTurn && safeLineExists && !sweepIncoming )
            doomedTurn = false;

        return new()
        {
            NearGuaranteedPieceLoss = nearGuaranteedPieceLoss,
            AlwaysLoseAPiece = alwaysLoseAPiece,
            OpponentThreatensKO = opponentThreatensKO,
            AttackerMovesFirst = attackerMovesFirst,
            AttackerCannotAct = attackerCannotAct,
            ViableSwitches = viableSwitches,
            AllSwitchesDoomed = allSwitchesDoomed,
            SweepIncoming = sweepIncoming,
            NoTempoRecoveryLine = noTempoRecoveryLine,
            TempoRecoveredTOP = tempoCreatedTOP,

            PressureScore = pressure,
            DoomedTurn = doomedTurn,
        };
    }

    public ThreatProfile GetThreatProfile( ExchangeEvaluation exchangeEval, BoardContext boardContext, IBattleAIUnit opponent )
    {
        ThreatProfile profile = new()
        {
            ThreatUnit = opponent,
            ThreatensImmediateKO = exchangeEval.OpponentThreatensKO,
            OutspeedsCurrent = exchangeEval.OpponentMovesFirst,
            ThreatPTKO = exchangeEval.OpponentPTKOR.PTKO,
        };

        CurrentLog.Add( $"" );
        CurrentLog.Add( $"===================================" );
        CurrentLog.Add( $"=====[Building Threat Profile]=====" );
        CurrentLog.Add( $"===================================" );

        //--Check opponent current sweep potential
        int threatened = 0;
        int faster = 0;

        var allies = boardContext.MyTeamAlive;

        foreach( var ally in allies )
        {
            int allySpeed = GetUnitContextualSpeed( ally );

            if( opponent.Speed > allySpeed )
                faster++;

            var ptko = Projection.Get_NeutralPTKO( opponent, ally );
            if( ( ptko >= PotentialToKO.TwoHKO && opponent.Speed > allySpeed ) || ptko > PotentialToKO.Risky )
                threatened++;
        }

        profile.ThreatenedAlliesCount = threatened;
        profile.OutspeedsAlliesCount = faster;

        profile.SweepPotential = faster >= allies.Count - 1 && ( threatened >= allies.Count - 1 || threatened > 3 );
        CurrentLog.Add( $"Threatened Allies: {threatened}. Outsped Allies: {faster}. Sweep Potential: {profile.SweepPotential}" );

        //--Are we forced to switch
        profile.ForcesSwitch = exchangeEval.AttackerSwitches;
        CurrentLog.Add( $"Exchange Evaluation predicted the opponent might force us to switch this turn: {profile.ForcesSwitch}" );

        //--Constraint Pressure. How many of our mons struggle against the opponent?
        int struggleCount = 0;

        foreach( var ally in allies )
        {
            var ex = Projection.EvaluateExchange( ally, opponent );

            bool weStruggle = ex.AttackerPTKOR.PTKO < PotentialToKO.Risky;
            bool theyThreaten = ex.OpponentPTKOR.PTKO > PotentialToKO.Risky;

            if( weStruggle && theyThreaten )
                struggleCount++;
        }

        float constraintPressure = struggleCount * 0.6f;

        if( profile.ForcesSwitch )
            constraintPressure += 1.5f;

        profile.ConstraintPressure = constraintPressure;

        //--Threat Type
        float offensivePressure = 0f;
        float defensiveBulk = 0f;

        //--Offensive Pressure
        if( profile.ThreatPTKO >= PotentialToKO.Dangerous )
            offensivePressure += 2f;

        if( profile.ThreatensImmediateKO )
            offensivePressure += 2f;

        if( profile.OutspeedsCurrent )
            offensivePressure += 2f;

        //--Defensive Bulk
        defensiveBulk += profile.ThreatenedAlliesCount * 0.3f;

        //--Classify Threat Type
        if( profile.SweepPotential )
            profile.Type = ThreatType.BurstDamage;
        else if( offensivePressure >= 3 && profile.OutspeedsCurrent )
            profile.Type = ThreatType.BurstDamage;
        else if( constraintPressure >= 2.5f && offensivePressure < 2f )
            profile.Type = ThreatType.Tank;
        else if( constraintPressure >= 2f )
            profile.Type = ThreatType.Utility;
        else if( offensivePressure >= 2f )
            profile.Type = ThreatType.Pressure;
        else
            profile.Type = ThreatType.None;

        //--Pressure
        profile.PressureScore = CalculateThreatPressure( profile );
        profile.OffensivePressure = offensivePressure;
        profile.DefensiveBulk = defensiveBulk;

        //--Urgency
        profile.Urgency = GetThreatUrgency( profile.PressureScore );

        profile.Exists = profile.Urgency >= ThreatUrgency.Medium;

        CurrentLog.Add( $"Pressure Score: {profile.PressureScore}. Urgency: {profile.Urgency}. Threat Exists: {profile.Exists}. Threat Type: {profile.Type}" );
        CurrentLog.Add( $"===================================" );
        CurrentLog.Add( $"" );

        return profile;
    }

    private float CalculateThreatPressure( ThreatProfile profile )
    {
        float pressure = 0;
        pressure += profile.ConstraintPressure * 0.8f;

        if( profile.ThreatensImmediateKO )      pressure += 1.5f;
        if( profile.OutspeedsCurrent )          pressure += 1.0f;

        pressure += profile.ThreatenedAlliesCount * 0.5f;

        if( profile.SweepPotential )            pressure += 2.0f;
        if( profile.ForcesSwitch )              pressure += 1.0f;

        return pressure;
    }

    private ThreatUrgency GetThreatUrgency( float pressure )
    {
        ThreatUrgency urgency;

        if( pressure >= 4.5f )          urgency = ThreatUrgency.Critical;
        else if( pressure >= 3f )       urgency = ThreatUrgency.High;
        else if( pressure >= 1.5f )     urgency = ThreatUrgency.Medium;
        else if( pressure > 0f )        urgency = ThreatUrgency.Low;
        else                            urgency = ThreatUrgency.None;

        return urgency;
    }

    private ActionEvaluation Get_AttackAction( TempoStateResult tempo, ExchangeEvaluation exchangeEval, BoardContext boardContext, MoveThreatResult bestAttack )
    {
        int attackScore = MoveCommand.AttackScore( tempo, exchangeEval, boardContext, bestAttack );
        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Attack Score: {attackScore}" );
        CurrentLog.Add( $"" );
        var attackActionEval = _actionEval.BuildActionEvaluation( ActionType.Attack, attackScore, bestAttack.Target, bestAttack.Move, bestAttack.Top, exchangeEval );
        CurrentLog.Add( $"" );
        attackActionEval.Score += _actionEval.EvaluateBattlefieldState( attackActionEval, boardContext );

        return attackActionEval;
    }

    private ActionEvaluation Get_DefensiveSwitchAction( TempoStateResult tempo, ExchangeEvaluation exchangeEval, BoardContext boardContext, SwitchCandidateResult defensiveSwitch )
    {
        int defSwitchScore = SwitchCommand.DefensiveSwitchScore( tempo, exchangeEval, defensiveSwitch, boardContext );
        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Defensive Switch Score: {defSwitchScore} via Candidate: {defensiveSwitch.Pokemon?.NickName}" );
        CurrentLog.Add( $"" );
        var defSwitchActionEval = _actionEval.BuildActionEvaluation( ActionType.DefensiveSwitch, defSwitchScore, null, defensiveSwitch.Pokemon, defensiveSwitch.Top, exchangeEval );
        CurrentLog.Add( $"" );
        defSwitchActionEval.Score += _actionEval.EvaluateBattlefieldState( defSwitchActionEval, boardContext );

        return defSwitchActionEval;
    }

    private ActionEvaluation Get_OffensiveSwitchAction( TempoStateResult tempo, ExchangeEvaluation exchangeEval, BoardContext boardContext, SwitchCandidateResult offensiveSwitch )
    {
        int offSwitchScore = SwitchCommand.OffensiveSwitchScore( tempo, exchangeEval, offensiveSwitch, boardContext );
        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Offensive Switch Score: {offSwitchScore} via Candidate: {offensiveSwitch.Pokemon?.NickName}" );
        CurrentLog.Add( $"" );
        var offSwitchActionEval = _actionEval.BuildActionEvaluation( ActionType.OffensiveSwitch, offSwitchScore, null, offensiveSwitch.Pokemon, offensiveSwitch.Top, exchangeEval );
        CurrentLog.Add( $"" );
        offSwitchActionEval.Score += _actionEval.EvaluateBattlefieldState( offSwitchActionEval, boardContext );

        return offSwitchActionEval;
    }

    private ActionEvaluation Get_SetupAction( TempoStateResult tempo, ExchangeEvaluation exchangeEval, BoardContext boardContext, SetupThreatResult bestSetup )
    {
        int setupScore = MoveCommand.SetupScore( tempo, exchangeEval, boardContext, bestSetup );
        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Setup Score: {setupScore}" );
        CurrentLog.Add( $"" );
        var setupActionEval = _actionEval.BuildActionEvaluation( ActionType.Setup, setupScore, bestSetup.Target, bestSetup.Move, bestSetup.Top, exchangeEval );
        CurrentLog.Add( $"" );
        setupActionEval.Score += _actionEval.EvaluateBattlefieldState( setupActionEval, boardContext );

        return setupActionEval;
    }

    private ActionEvaluation Get_OffensiveStatusAction( TempoStateResult tempo, ExchangeEvaluation exchangeEval, BoardContext boardContext, StatusThreatResult bestOffensiveStatus )
    {
        int statusScore = MoveCommand.OffensiveStatusScore( tempo, exchangeEval, boardContext, bestOffensiveStatus );
        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Offensive Status Score: {statusScore}" );
        CurrentLog.Add( $"" );
        var statusActionEval = _actionEval.BuildActionEvaluation( ActionType.OffensiveStatus, statusScore, bestOffensiveStatus.Target, bestOffensiveStatus.Move, bestOffensiveStatus.Top, exchangeEval );
        CurrentLog.Add( $"" );
        statusActionEval.Score += _actionEval.EvaluateBattlefieldState( statusActionEval, boardContext );

        return statusActionEval;
    }

    public Pokemon RequestedForcedSwitch()
    {
        var opposingUnits = BattleSystem.GetOpposingUnits( Unit );
        int oppPokemon = 0;

        for( int i = 0; i < opposingUnits.Count; i++ )
        {
            var opp = opposingUnits[i];
            if( opp.Pokemon != null )
                oppPokemon++;
            else
                continue;
        }

        if( oppPokemon <= 0 )
        {
            Debug.Log( $"[AI Scoring][Request Forced Switch] Chose to get a Vacuum Switch!" );
            return SwitchCommand.GetSwitch_Vacuum();
        }
        else
        {
            Debug.Log( $"[AI Scoring][Request Forced Switch] Chose to get a Revenge Switch!" );
            var opps = CreateBattleAIUnits_FromBattleUnits( opposingUnits );
            return SwitchCommand.GetSwitch_Revenge( opps ).Pokemon;
        }
    }

    public Pokemon RequestRandomSwitch()
    {
        var ourParty = BattleSystem.GetAllyParty( Unit.Pokemon );
        var ourActiveUnits = BattleSystem.GetAllyUnits( Unit );
        var bench = ourParty.Where( p => !ourActiveUnits.Any( u => u.Pokemon == p ) && p.CurrentHP > 0  ).ToList();

        int r = UnityEngine.Random.Range( 0, bench.Count );

        return bench[r];
    }

    public Pokemon RequestLead()
    {
        Debug.Log( $"[AI] Lead pokemon requested using GetSwitch_Vacuum!" );
        return SwitchCommand.GetSwitch_Vacuum();
    }

    public int GetUnitInferredStat( Pokemon pokemon, Stat stat )
    {
        // Debug.Log( $"[AI Scoring][Get Walling Score] Getting {pokemon.NickName}'s inferred {stat}" );
        float statValue = GetBaseStat( pokemon, stat );
        // Debug.Log( $"[AI Scoring][Get Walling Score] {pokemon.NickName}'s base {stat} value is: {statValue}" );

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

        // Debug.Log( $"[AI Scoring][Get Walling Score] {pokemon.NickName}'s Final Inferred {stat} value is: {final}" );

        return final;
    }

    public int GetUnitInferredStat( IBattleAIUnit pokemon, Stat stat )
    {
        // Debug.Log( $"[AI Scoring][Get Walling Score] Getting {pokemon.Name}'s inferred {stat}" );
        float statValue = GetBaseStat( pokemon, stat );

        int stage = pokemon.StatStages[stat];
        var stageModifier = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f };
        float directModifier = pokemon.DirectStatModifiers[stat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm );

        stage = Mathf.Clamp( stage, -6, 6 );

        if( stage >= 0 )
            statValue *= stageModifier[stage];
        else
            statValue /= stageModifier[-stage];

        //--Apply Direct Stat Change (Burn, Paralysis, Ruin Ability, Weather stat change, etc.)
        statValue *= directModifier;

        int final = Mathf.FloorToInt( statValue );

        // Debug.Log( $"[AI Scoring][Get Walling Score] {pokemon.Name}'s base {stat} value is: {statValue} with a stage of {stage} and a direct modifier total of {directModifier}" );
        // Debug.Log( $"[AI Scoring][Get Walling Score] {pokemon.Name}'s Final Inferred {stat} value is: {final}" );

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

    public int GetUnitContextualSpeed( IBattleAIUnit pokemon )
    {
        int speed = GetUnitInferredStat( pokemon, Stat.Speed );
        var weather = BattleSystem.Field.Weather;

        if( weather != null )
        {
            if( weather.ID == WeatherConditionID.RAIN && pokemon.Ability == AbilityID.SwiftSwim && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;

            if( weather.ID == WeatherConditionID.SUNNY && pokemon.Ability == AbilityID.Chlorophyll && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;

            if( weather.ID == WeatherConditionID.SANDSTORM && pokemon.Ability == AbilityID.SandRush && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;

            if( weather.ID == WeatherConditionID.SNOW && pokemon.Ability == AbilityID.SlushRush && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;
        }

        return speed;
    }

    public int GetBaseStat( Pokemon pokemon, Stat stat )
    {
        return stat switch
        {
            Stat.HP         => pokemon.PokeSO.MaxHP,
            Stat.Attack     => pokemon.PokeSO.Attack,
            Stat.Defense    => pokemon.PokeSO.Defense,
            Stat.SpAttack   => pokemon.PokeSO.SpAttack,
            Stat.SpDefense  => pokemon.PokeSO.SpDefense,
            Stat.Speed      => pokemon.PokeSO.Speed,
            _ => 0
        };
    }

    public int GetBaseStat( IBattleAIUnit pokemon, Stat stat )
    {
        return stat switch
        {
            Stat.HP         => pokemon.HPBaseStat,
            Stat.Attack     => pokemon.Attack,
            Stat.Defense    => pokemon.Defense,
            Stat.SpAttack   => pokemon.SpAttack,
            Stat.SpDefense  => pokemon.SpDefense,
            Stat.Speed      => pokemon.Speed,
            _ => 0
        };
    }

    public int Attack_TempoModifier( TempoStateResult tempo )
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

    public int DefensiveSwitch_TempoModifier( TempoStateResult tempo )
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

    public int OffensiveSwitch_TempoModifier( TempoStateResult tempo )
    {
        return tempo.TempoState switch
        {
            TempoState.WinningHard  => -30,
            TempoState.Winning      => -15,
            TempoState.Neutral      => +0,
            TempoState.Losing       => -15,
            TempoState.LosingHard   => -35,
            _ => 0
        };
    }

    public int Setup_TempoModifier( TempoStateResult tempo )
    {
        return tempo.TempoState switch
        {
            TempoState.WinningHard  => -35,
            TempoState.Winning      => -15,
            TempoState.Neutral      => +0,
            TempoState.Losing       => +20,
            TempoState.LosingHard   => +10,
            _ => 0
        };
    }

    public ThreatResult GetThreat_ImmediateDamage( List<IBattleAIUnit> opponents, IBattleAIUnit ourPokemon )
    {
        int highestThreat = int.MinValue;
        IBattleAIUnit highestUnit = null;

        foreach( var threat in opponents )
        {
            int threatScore = 100;
            float moveThreat = float.MinValue;

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] Starting threat check on {threat.Pokemon.NickName}. Starting Score: {threatScore}" );

            //--Offensive Pressure
            int atk = GetUnitInferredStat( threat, Stat.Attack );
            int spatk = GetUnitInferredStat( threat, Stat.SpAttack );

            float offensivePressure;

            if( atk > spatk )
                offensivePressure = atk;
            else
                offensivePressure = spatk;

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Offensive Pressure is: {offensivePressure}" );
            
            if( offensivePressure >= 150f )             threatScore += 40;
            else if( offensivePressure >= 125f )        threatScore += 25;
            else if( offensivePressure >= 100f )        threatScore += 10;
            else if( offensivePressure >= 80f )         threatScore += 0;
            else if( offensivePressure >= 65f )         threatScore -= 10;
            else if( offensivePressure >= 50f )         threatScore -= 25;
            else if( offensivePressure < 50f )          threatScore -= 40;

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Offensive Pressure checked. Score: {threatScore}" );

            //--Move Threat
            foreach( var move in threat.ActiveMoves )
            {
                if( move.MoveSO.Power <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                    continue;

                if( threat.VolatileStatuses.Contains( VolatileConditionID.ChoiceLocked ) )
                {
                    var unit = GetBattleUnit( threat.PID );
                    if( unit != null && move != unit.LastUsedMove )
                        continue;
                }

                var field = BattleSystem.Field;

                float effectiveness     = TypeChart.GetEffectiveness( move.MoveType, ourPokemon.Type.One ) * TypeChart.GetEffectiveness( move.MoveType, ourPokemon.Type.Two );
                float stab              = UnitSim.CheckTypes( move.MoveType, threat ) ? 1.5f : 1f;
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

                if( ourPokemon.Item != BattleItemEffectID.None )
                {
                    if( UnitSim.ItemDMGModifiers.TryGetValue( ourPokemon.Item, out var mod ) )
                        item = mod( ourPokemon, threat, move );
                }

                // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] Score-ing {threat.Pokemon.NickName}'s move {move.MoveSO.Name}. Effectiveness Modifier: {effectiveness}, STAB Modifier: {stab}, Weather Modifier: {weather}" );

                float currentMoveThreat = effectiveness * stab * weather * terrain * item;
                moveThreat = Mathf.Max( moveThreat, currentMoveThreat );

                // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s move {move.MoveSO.Name} checked. Move's Score: {moveThreat}" );
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

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Moves have all been checked. Score: {threatScore}" );
            var ourSpeed = GetUnitContextualSpeed( ourPokemon );
            var threatSpeed = GetUnitContextualSpeed( threat );
            //--Higher speed means the target is more threatening
            if( threatSpeed > ourSpeed )
                threatScore += 20;
            else if( threatSpeed < ourSpeed )
                threatScore -= 20;

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Speed comparison checked. Score: {threatScore}" );

            //--Current HP Ratio. Lower HP means we're more threatened
            float hpRatio = Get_HPRatio( ourPokemon );

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Current HP Ratio is: {hpRatio}" );

            if( hpRatio < 0.25f )           threatScore += 30;
            else if( hpRatio < 0.5f )       threatScore += 15;
            else if( hpRatio < 0.75f )      threatScore += 5;

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Current HP Ratio checked. Score: {threatScore}" );

            threatScore = Mathf.Clamp( threatScore, 0, 300 );

            if( threatScore > highestThreat )
            {
                highestThreat = threatScore;
                highestUnit = threat;
            }

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] The current most threatening Pokemon is: {highestUnit.Pokemon.NickName}, with a Score of: {highestThreat}" );

        }

        // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] The most threatening Pokemon is: {highestUnit.Pokemon.NickName}, with a Score of: {highestThreat}" );

        return new(){ Score = highestThreat, Unit = highestUnit };
    }

    public bool Check_UnitHasPriority( IBattleAIUnit attacker, IBattleAIUnit target )
    {
        for( int i = 0; i < attacker.ActiveMoves.Count; i++ )
        {
            if( BattleSystem.Field.Terrain != null && BattleSystem.Field.Terrain.ID == TerrainID.Psychic )
                return false;
            else
            {
                if( attacker.ActiveMoves[i].Priority > MovePriority.Zero && attacker.ActiveMoves[i].MoveSO.MoveCategory != MoveCategory.Status )
                {
                    if( attacker.ActiveMoves[i].MoveSO.Name == "Fake Out" )
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
            return false;

        if( target.Pokemon.CheckTypes( PokemonType.Ghost ) )
            return false;

        return true;
    }

    public bool CanUseFakeOut( IBattleAIUnit attacker, IBattleAIUnit target )
    {
        var attackerUnit = GetBattleUnit( attacker.PID );

        if( attackerUnit == null )
            return false;

        if( !attackerUnit.Pokemon.CheckHasMove( "Fake Out" ) )
            return false;

        if( attackerUnit.Flags[UnitFlags.TurnsTaken].Count > 0 )
            return false;

        if( UnitSim.CheckTypes( PokemonType.Ghost, target ) )
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

    public MoveThreatResult Get_MostThreateningMove( IBattleAIUnit attacker, IBattleAIUnit target, bool preview = false )
    {
        int bestScore = int.MinValue;
        float bestModifier = float.MinValue;
        Move bestMove = null;

        // if( preview )
            // Debug.Log( $"[Setup Action Evaluation Stat Stage Check] preview is true" );

        //--Move Threat
        foreach( var move in attacker.ActiveMoves )
        {
            if( move.MoveSO.Power <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                continue;

            int score = 0;

            float effectiveness     = TypeChart.GetEffectiveness( move.MoveType, target.Type.One ) * TypeChart.GetEffectiveness( move.MoveType, target.Type.Two );

            if( effectiveness == 0 )
                continue;

            // float stab              = UnitSim.CheckTypes( move.MoveType, attacker ) ? 1.5f : 1f;
            // float weather           = 1f;
            // float terrain           = 1f;
            // float item              = 1f;

            // var field = BattleSystem.Field;

            // if( field.Weather != null && !preview )
            // {
            //     if( UnitSim.WeatherDMGModifiers.TryGetValue( field.Weather.ID, out var mod ) )
            //         weather = mod( move );
            // }

            // if( field.Terrain != null && !preview )
            // {
            //     if( UnitSim.TerrainDMGModifiers.TryGetValue( field.Terrain.ID, out var mod ) )
            //         terrain = mod( move );
            // }

            // if( attacker.Item != BattleItemEffectID.None )
            // {
            //     if( UnitSim.ItemDMGModifiers.TryGetValue( attacker.Item, out var mod ) )
            //         item = mod( attacker, target, move );
            // }

            // Debug.Log( $"[AI Scoring][Most Threatening Move][{attacker.NickName}][{move.MoveSO.Name}] Effectiveness: {effectiveness}, STAB: {stab}, Weather: {weather}, Terrain: {terrain}, Item: {item}" );

            float modifier = effectiveness * UnitSim.Get_MoveModifier( attacker, target, move );
            // float modifier = effectiveness * stab * weather * terrain * item;

            int movePower = move.MovePower;

            //--Multi hit move power projection
            if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y != 0 )
            {
                int minHits = move.MoveSO.HitRange.x;
                int maxHits = move.MoveSO.HitRange.y;

                int expectedHits = Mathf.FloorToInt( ( minHits + maxHits ) * 0.5f );

                movePower *= expectedHits;
            }
            else if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y == 0 )
            {
                movePower *= move.MoveSO.HitRange.x;
            }

            if( movePower >= 90 )                       score += 30;
            else if( movePower >= 60 )                  score += 20;
            else if( movePower >= 45 )                  score += 15;
            else if( movePower >= 30 )                  score += 10;
            else if( movePower >= 15 )                  score += 5;

            if( modifier >= 9f )                 score += 90; //--Upper bounds, this move is 4x effective, has STAB, and benefits from weather.
            else if( modifier >= 6f )            score += 60; //--This move is 4x effective, and either has STAB OR benefits from weather.
            else if( modifier >= 4f )            score += 40; //--This move is 4x effective, or has some combination of 2x effective, stab, and weather.
            else if( modifier >= 3f )            score += 30; //--This move is 3x effective. It likely has 2x type effectiveness + stab.
            else if( modifier >= 2f )            score += 20;
            else if( modifier >= 1.5f )          score += 15;
            else if( modifier >= 1f )            score += 0;
            else if( modifier >= 0.5f )          score -= 20;
            else if( modifier >= 0.25f )         score -= 40;
            else if( modifier == 0f )            score = 0;

            int accuracy = move.MoveSO.Accuracy;
            if( accuracy < 70 )                         score -= 35;
            else if( accuracy < 80 )                    score -= 20;
            else if( accuracy < 90 )                    score -= 10;
            else if( accuracy < 100 )                   score -= 5;

            float tarHPR                    = Get_HPRatio( target );
            MoveThreatResult mtr            = new(){ Score = 0, Modifier = modifier, Move = move };
            var attEDR                      = Projection.Get_EstimatedDamageResult( attacker, target, mtr );
            PotentialToKOResult attPTKOR    = Projection.Get_PotentialToKOResult( attEDR, mtr, tarHPR );

            score += Mathf.FloorToInt( attEDR.DamageEstimate * 150 );

            int targetSpeed = GetUnitContextualSpeed( target );
            int attackerSpeed = GetUnitContextualSpeed( attacker );

            if( attPTKOR.PTKO > PotentialToKO.Risky )
                score += 20;

            if( targetSpeed > attackerSpeed && move.Priority > MovePriority.Zero && attPTKOR.PTKO > PotentialToKO.Risky )
                score += 50;
            else if( targetSpeed > attackerSpeed && move.Priority > MovePriority.Zero )
                score += 20;

            if( score > bestScore )
            {
                bestModifier = modifier;
                bestMove = move;
                bestScore = score;
            }

            //--If the attacker is choice-locked, when we get to the move we're locked into we log all of the scores and force-break from the loop
            //--because we cannot use any other move, and should always return this move as the "most threatening" because it is the ONLY threatening move.
            var attUnit = GetBattleUnit( attacker.PID );
            if( attUnit != null )
            {
                if( attUnit.Flags[UnitFlags.ChoiceItem].IsActive )
                {
                    if( attUnit.LastUsedMove != null && attUnit.LastUsedMove == move )
                    {
                        bestModifier = modifier;
                        bestMove = move;
                        bestScore = score;
                        break;
                    }
                }
            }

            // Debug.Log( $"[AI Scoring][Most Threatening Move][{attacker.NickName}][{move.MoveSO.Name}] Modifier: {currentModifier}" );
        }

        bestMove ??= UnitSim.GetRandomMove( attacker );

        return new(){ Score = bestScore, Modifier = bestModifier, Move = bestMove };
    }

    public List<(int PTKO, Pokemon Mon )> GetTopThreats( List<Pokemon> team, Pokemon me )
    {
        List<( int ptko, Pokemon mon )> threats = new();
        BattleAI_PokemonAdapter ourMon = new( me, this );

        for( int i = 0; i < team.Count; i ++ )
        {
            BattleAI_PokemonAdapter theirMon = new( team[i], this );

            //--MTRs
            var ourMTR = MoveCommand.GetMove_BestAttack( ourMon, theirMon );
            var theirMTR = MoveCommand.GetMove_BestAttack( theirMon, ourMon );

            //--EDRs
            var ourEDR = Projection.Get_EstimatedDamageResult( ourMon, theirMon, ourMTR );
            var theirEDR = Projection.Get_EstimatedDamageResult( theirMon, ourMon, theirMTR );

            //--PTKOs
            var ourPTKO = Projection.Get_PotentialToKOResult( ourEDR, ourMTR, theirMon.CurrentHPR ).PTKO;
            var theirPTKO = Projection.Get_PotentialToKOResult( theirEDR, theirMTR, ourMon.CurrentHPR ).PTKO;

            if( theirPTKO - 1 > ourPTKO || theirPTKO > PotentialToKO.Risky && theirMTR.Top.AttackerMovedFirst )
                threats.Add( ( (int)theirPTKO, team[i] ) );
        }

        threats.Sort( ( a, b ) => a.CompareTo( ( a.ptko, a.mon ) ) );

        return threats;
    }

    public void RefreshTeamPieceValues( List<Pokemon> team )
    {
        List<IBattleAIUnit> teamAIUnits = new();
        
        for( int i =0; i < team.Count; i++ )
        {
            BattleAI_PokemonAdapter mon = new( team[i], this );
            teamAIUnits.Add( mon );

            // if( team[i] == Unit.Pokemon ) //--This hack sucks...
                // ThisUnitAdapter = mon;
        }

        TeamPieceValues = GetTeamPieceValues( teamAIUnits );
    }

    public Dictionary<string, PieceValue> GetTeamPieceValues( List<IBattleAIUnit> team )
    {
        // Debug.Log( $"[AI Scoring][Piece Value] Refreshing Team Piece Values!" );
        Dictionary<string, PieceValue> teamPieceValues = new();

        var attackingTiers = PV_GetRankBonuses( team, mon => Mathf.Max( GetUnitInferredStat( mon, Stat.Attack ), GetUnitInferredStat( mon, Stat.SpAttack ) ) );
        var speedTiers = PV_GetRankBonuses( team, mon => GetUnitContextualSpeed( mon ) );

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

            teamPieceValues.Add( mon.PID, value );
            // Debug.Log( $"[AI Scoring][Piece Value] {mon.Name} value assigned! Offensive Value: {value.OffensiveValue}, Speed Score: {value.SpeedScore}" );
        }

        return teamPieceValues;
    }

    private ( int OffensiveValue, int threatCount, int SpeedScore ) PV_GetOffensiveValue( IBattleAIUnit pokemon, Dictionary<IBattleAIUnit, int> attackingRanks, Dictionary<IBattleAIUnit, int> speedRanks )
    {
        var oppTeam = BattleSystem.GetOpposingParty( pokemon.PID ).Where( p => p.CurrentHP > 0 ).ToList();
        int score = 50;

        score += attackingRanks[pokemon];
        score += speedRanks[pokemon];

        //--PTKO Stuff here
        int threatCount = 0;
        int spreadPressure = 0;
        for( int i = 0; i < oppTeam.Count; i++ )
        {
            BattleAI_PokemonAdapter opp = new( oppTeam[i], this );
            var ptko = Projection.Get_NeutralPTKO( pokemon, opp );
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

        return ( score, threatCount, speedRanks[pokemon] );
    }

    private Dictionary<IBattleAIUnit, int> PV_GetRankBonuses( List<IBattleAIUnit> team, Func<IBattleAIUnit, int> valueSelector )
    {
        List<( IBattleAIUnit Mon, int Value )> statList = new();
        Dictionary<IBattleAIUnit, int> tiers = new();

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

    public float Get_HPRatio( Pokemon pokemon )
    {
        float currentHP = pokemon.CurrentHP;
        float maxHP = pokemon.MaxHP;

        // Debug.Log( $"[AI Scoring][Getting HP Ratio] {pokemon.NickName}'s HP Ratio is: {currentHP/maxHP}" );
        return currentHP / maxHP;
    }

    public float Get_HPRatio( IBattleAIUnit pokemon )
    {
        return pokemon.CurrentHPR;
    }

    public float Get_HPRatio_AfterEntryHazards( Pokemon pokemon )
    {
        // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Getting HP Ratio for {pokemon.NickName} after taking entry hazard damage!" );
        float hpR = Get_HPRatio( pokemon );
        float damage = Get_EntryHazardDamage( pokemon );

        float finalHPR = Mathf.Max( 0f, hpR - damage );
        // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName}'s Raw HPR: {hpR}, HPR after Hazards: {finalHPR}" );

        return finalHPR;
    }

    public float Get_HPRatio_AfterEntryHazards( IBattleAIUnit pokemon )
    {
        // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Getting HP Ratio for {pokemon.NickName} after taking entry hazard damage!" );
        float hpR = Get_HPRatio( pokemon );
        float damage = Get_EntryHazardDamage( pokemon );

        float finalHPR = Mathf.Max( 0f, hpR - damage );
        // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName}'s Raw HPR: {hpR}, HPR after Hazards: {finalHPR}" );

        return finalHPR;
    }

    public float Get_EntryHazardDamage( Pokemon pokemon )
    {
        float damage = 0;
        var myCourtLoc = BattleSystem.Field.GetPokemonCourtLocationFromTrainer( pokemon );

        // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName} was found in the {myCourtLoc}!" );

        //--Heavy duty boots prevents hazard damage.
        if( pokemon.HeldItem != null && pokemon.BattleItemEffect?.ID == BattleItemEffectID.HeavyDutyBoots )
        {
            // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName} is holding Heavy Duty Boots! No hazard damage should be taken! Damage: {damage}" );
            return damage;
        }

        var court = BattleSystem.Field.ActiveCourts[myCourtLoc];
        if( court.Conditions.ContainsKey( CourtConditionID.StealthRock ) )
        {
            float effectiveness = TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.PokeSO.Type2 );
            damage += ( 1f / 8f ) * effectiveness;
            // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Stealth Rock was found in the {myCourtLoc}! Damage: {damage}" );
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

            // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Spikes ({layers}) were found in the {myCourtLoc}! Damage: {damage}" );
        }

        return damage;
    }

    public float Get_EntryHazardDamage( IBattleAIUnit pokemon )
    {
        float damage = 0;
        var myCourtLoc = BattleSystem.Field.GetPokemonCourtLocationFromTrainer( pokemon.PID );

        // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName} was found in the {myCourtLoc}!" );

        //--Heavy duty boots prevents hazard damage.
        if( pokemon.Item == BattleItemEffectID.HeavyDutyBoots )
        {
            // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName} is holding Heavy Duty Boots! No hazard damage should be taken! Damage: {damage}" );
            return damage;
        }

        var court = BattleSystem.Field.ActiveCourts[myCourtLoc];
        if( court.Conditions.ContainsKey( CourtConditionID.StealthRock ) )
        {
            float effectiveness = TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.Type.One ) * TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.Type.Two );
            damage += ( 1f / 8f ) * effectiveness;
            // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Stealth Rock was found in the {myCourtLoc}! Damage: {damage}" );
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

            // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Spikes ({layers}) were found in the {myCourtLoc}! Damage: {damage}" );
        }

        return damage;
    }

    public float Get_EntryHazardDamage( IBattleAIUnit pokemon, CourtConditionID hazard, int layers = 1 )
    {
        float damage = 0;

        //--Heavy duty boots prevents hazard damage.
        if( pokemon.Item == BattleItemEffectID.HeavyDutyBoots )
            return damage;

        if( hazard == CourtConditionID.StealthRock )
        {
            float effectiveness = TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.Type.One ) * TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.Type.Two );
            damage += ( 1f / 8f ) * effectiveness;
        }

        if( hazard == CourtConditionID.Spikes )
        {
            if( layers == 1 )
                damage += 1f / 8f;
            else if( layers == 2 )
                damage += 1f / 6f;
            else if( layers >= 3 )
                damage += 1f / 4f;
        }

        return damage;
    }

    private void InitializeUniqueWallScores()
    {
        UniqueWallScores = new()
        {
            { "Body Press", new(){ AttackingStat = Stat.Defense, DefendingStat = Stat.Defense } },
        };
    }

    public List<Pokemon> GetLikelyDefensiveSwitches( List<IBattleAIUnit> ourActiveMons, IBattleAIUnit theirActiveMon )
    {
        List<Pokemon> likelySwitches = new();

        var scr = SwitchCommand.GetSwitch_Defensive( ourActiveMons, true, theirActiveMon, true );
        List<( Pokemon Pokemon, int Score )> allCandidates = new();

        CurrentLog.Add( $"" );
        CurrentLog.Add( $"=[Getting Likely Switches]=" );

        if( scr.ReturnAllList == null || scr.ReturnAllList.Count <= 0 )
        {
            Debug.LogError( "GetSwitch_Defensive() returned an empty Return All List when it shouldn't have. Falling back on entire team." );
            return GetRemainingAllyPokemon( theirActiveMon.PID );
        }
        else
            allCandidates = scr.ReturnAllList.ToList();

        CurrentLog.Add( $"All Candidates Count: {allCandidates.Count}." );
        foreach( var cand in allCandidates )
            CurrentLog.Add( $"{cand.Pokemon.NickName}, {cand.Score}" );
            
        allCandidates = allCandidates.OrderByDescending( c => c.Score ).ToList();

        int bestCandidateScore = allCandidates[0].Score;
        CurrentLog.Add( $"Best Candidate Score: {allCandidates[0].Score} ({bestCandidateScore})." );
        for( int i = 0; i < allCandidates.Count; i++ )
        {
            int threshold = bestCandidateScore - 30;
            CurrentLog.Add( $"Threshold: {threshold}." );
            CurrentLog.Add( $"Checking candidate {allCandidates[i].Pokemon.NickName}. Score: {allCandidates[i].Score}." );

            if( allCandidates[i].Score >= threshold )
                likelySwitches.Add( allCandidates[i].Pokemon );

            if( i > 2 ) //--Caps at top 4 candidates
                break;
        }

        CurrentLog.Add( $"Likely Switches Count: {likelySwitches.Count}." );
        CurrentLog.Add( $"" );

        return likelySwitches;
    }
    
}

public struct ThreatResult
{
    public int Score { get; set; }
    public IBattleAIUnit Unit { get; set; }
}

public class MoveThreatResult
{
    public float Score { get; set; }
    public float Modifier { get; set; }
    public IBattleAIUnit Target { get; set; }
    public Move Move { get; set; }
    public float EstimatedDamage { get; set; }
    public TurnOutcomeProjection Top { get; set; }
}

public struct SetupThreatResult
{
    public Move Move;
    public IBattleAIUnit Target;
    public TurnOutcomeProjection Top;

    public StatStageDelta StageDelta;

    public PotentialToKOResult BeforePTKOR;
    public PotentialToKOResult AfterPTKOR;

    public int SetupValue;
    public int SweepCount;
    public int ImprovedPTKOs;

    public bool OpponentSwitches;
}

public struct StatusThreatResult
{
    public OffensiveStatusType Type;
    public int Score;
    public int StatusValue;
    public Move Move;
    public IBattleAIUnit Target;
    public TurnOutcomeProjection Top;

    public int TeamCoverage;
    public int BoardAmbiguity;
    public int Reliability;
    public int ImmediateImpact;

    public PotentialToKOResult AttackerPTKOR;
    public PotentialToKOResult OpponentPTKOR;
    public bool OpponentSwitches;
}

//--This stores the stage changes for setup moves.
public struct StatStageDelta
{
    public int HP;
    public int Attack;
    public int Defense;
    public int SpAttack;
    public int SpDefense;
    public int Speed;

    public int Accuracy;
    public int Evasion;

    public float CritRatio;
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
    public TurnOutcomeProjection Top { get; set; }

    public List<( Pokemon Pokemon, int Score )> ReturnAllList;
}

public struct EstimatedDamageResult
{
    public int Score;
    public float DamageEstimate;
    public float LowRollEstimate;
    public int AttackingStatStage;
    public int DefendingStatStage;
    public float AttackingDirectModifier;
    public float DefendingDirectModifier;
    public IBattleAIUnit Attacker;
    public IBattleAIUnit Target;
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

    public bool AttackerHasPriorityMove;
    public bool OpponentHasPriorityMove;

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

    public bool OpponentSwitches;
    public bool AttackerSwitches;

    public string AttackerMoveName;
    public string OpponentMoveName;

    public ExchangeState ExchangeState;
}

public struct BoardContext
{
    public bool IsForcedTrade;
    public bool HasSafePivot;

    public bool IsAhead;
    public bool IsBehind;

    public float MyTeamHPPercent;
    public float OppTeamHPPercent;

    public int MyRemainingPieces;
    public int OppRemainingPieces;

    public bool IsTerminal;
    public float MyExpendability;

    public List<IBattleAIUnit> MyTeamAlive;
    public List<IBattleAIUnit> OppTeamAlive;

    public BattlefieldState BattlefieldState;
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

public struct UniqueWallingScoreMove
{
    public Stat AttackingStat;
    public Stat DefendingStat;
}

public class ActionEvaluation
{
    public ActionType Type;
    public int Score;
    public string ActorPID;
    public BattleUnit Target;
    public Move MovePayload;
    public Pokemon SwitchPayload;
    public TurnOutcomeProjection Top1;
    public TurnOutcomeProjection Top2;
    public ProjectedBoardState PBS;

    public ExchangeEvaluation ExchangeEvaluation;

    public bool NextTurn_WeAreForcedOut;
    public bool NextTurn_TheyAreForcedOut;
}

public struct MaterialStatus
{
    public int MyRemainingPieces;
    public int OppRemainingPieces;

    public float MyTeamHPPercent;
    public float OppTeamHPPercent;

    public bool IsAhead;
    public bool IsBehind;
}

public enum PlanType { None, Stabilize, Trade, Aggress, EnableSweep, PreventSweep }
public class CurrentPlan
{
    public PlanType Type;
    public string FocusPID;
    public Pokemon FocusMon;
    public float Confidence;
    public bool AllowSacrifice;

    public int TurnsActive;
}

public enum ThreatUrgency { None, Low, Medium, High, Critical }
public enum ThreatType
{
    None,
    BurstDamage,    //--Fast KO Threat (glass cannons, speedy sweeper)
    Pressure,       //--Consistent chip (offensive pivot-ish)
    Tank,           //--Bulky, low-medium damage, hard to get rid of. may have sustain
    Setup,          //--A mon that is okay now but will become exceedingly dangerous if allowed to click a setup move
    Utility,        //--Hazard/Disruptive support type
}

public struct ThreatProfile
{
    public bool Exists;

    public ThreatType Type;
    public float OffensivePressure; //--Damage-based pressure
    public float DefensiveBulk; //--Survivability-based pressure

    public IBattleAIUnit ThreatUnit;
    public Pokemon ThreatPokemon;

    //--Main Signals
    public bool ThreatensImmediateKO;
    public bool OutspeedsCurrent;
    public PotentialToKO ThreatPTKO;

    //--Team-wide Pressure
    public int ThreatenedAlliesCount; //--How many of our remaining mons it pressures
    public int OutspeedsAlliesCount; //--How many of our remaining mons it outspeeds

    //--Behavior Flags
    public bool ForcesSwitch;
    public bool SweepPotential;

    //--Urgency
    public float PressureScore;
    public float ConstraintPressure;
    public ThreatUrgency Urgency;
}

public struct BattlefieldState
{
    public int Round;
    public bool IsEarlyGame;

    public WeatherConditionID Weather;
    public int WeatherDuration;
    public TerrainID Terrain;
    public int TerrainDuration;

    public int EntryHazardsOn_MySide;
    public int EntryHazardsOn_TheirSide;

    public bool WeHave_Tailwind;
    public bool TheyHave_Tailwind;
    public bool WeHave_TailwindSetter;
    public bool TheyHave_TailwindSetter;

    public int OurTailwindDuration;
    public int TheirTailwindDuration;

    public bool TrickRoomActive;
    public bool WeHave_TrickRoomAdvantage;
    public bool TheyHave_TrickRoomAdvantage;
    public bool WeHave_TrickRoomSetter;
    public bool TheyHave_TrickRoomSetter;
    public int TrickRoomDuration;

    public bool WeHave_WeatherControl;
    public bool TheyHave_WeatherControl;
    public bool WeHave_WeatherSetter_Ability;
    public bool TheyHave_WeatherSetter_Ability;
    public bool WeHave_WeatherSetter_Move;
    public bool TheyHave_WeatherSetter_Move;

    public bool WeHave_TerrainSetter_Ability;
    public bool TheyHave_TerrainSetter_Ability;
    public bool WeHave_TerrainSetter_Move;
    public bool TheyHave_TerrainSetter_Move;
    public bool WeHave_TerrainControl;
    public bool TheyHave_TerrainControl;

    public bool WeHave_Reflect;
    public bool WeHave_LightScreen;
    public bool WeHave_AuroraVeil;
    public bool WeHave_ReflectSetter;
    public bool WeHave_LightScreenSetter;
    public bool WeHave_AuroraSetter;
    public int OurReflectDuration;
    public int OurLightScreenDuration;
    public int OurAuroraVeilDuration;

    public bool TheyHave_Reflect;
    public bool TheyHave_LightScreen;
    public bool TheyHave_AuroraVeil;
    public bool TheyHave_ReflectSetter;
    public bool TheyHave_LightScreenSetter;
    public bool TheyHave_AuroraSetter;
    public int TheirReflectDuration;
    public int TheirLightScreenDuration;
    public int TheirAuroraVeilDuration;

    public bool WeHave_FieldControl;
    public bool TheyHave_FieldControl;
    public int FieldControlDelta;
}
