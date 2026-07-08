using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SwitchType { Offensive, Defensive, Pivot, }

public class BattleAI_SwitchCommand
{
    private readonly BattleAI _ai;
    private readonly BattleAI_Projection _proj;
    private readonly BattleAI_BattleSim _battleSim;

    public BattleAI_SwitchCommand( BattleAI ai )
    {
        _ai = ai;
        _proj = _ai.Projection;
        _battleSim = _ai.BattleSim;
    }

    public void SubmitSwitchCommand( Pokemon incomingPokemon )
    {
        _ai.IncreaseSwitchAmount();
        _ai.SetLastSentInPokemon( incomingPokemon );
        _ai.SetLastOpposingPokemon( _ai.Blackboard.TheirActiveBattleAIUnits.ToList() );
        _ai.BattleSystem.SetSwitchPokemonCommand( incomingPokemon, _ai.CurrentUnitDeciding, true );
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public SwitchCandidateResult GetSwitch_Defensive( IBattleAIUnit returnPokemon, bool returnAll = false )
    {
        // CustomLogSession defensiveSwitchLog = new();

        int bestScore = int.MinValue;
        Pokemon bestSwitch = null;
        float bestHPRatio = 0f;
        IBattleAIUnit threat;
        MoveThreatResult incomingMove;
        PotentialToKOResult bestSwitch_OffensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        PotentialToKOResult bestSwitch_DefensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        TurnOutcomeProjection bestCandidateTOP = new();
        float threatsScariestMoveModifier = 1f;
        bool islegit = true;
        bool isFaster = false;

        List<IBattleAIUnit> bench = new();
        Dictionary<Pokemon, SwitchCandidateResult> returnAllList = new();

        //--Convert switching functions to only take in the pokemon that wants to switch. using that pokemon, we will gain access to
        //--that pokemon's opposing units and their ally bench through helpers in BattleAI. This will make these functions much more
        //--generic and reusable without the weird readOpponent hacks and potentialy wrong-team comparison errors

        List<IBattleAIUnit> allyUnits = new();
        List<IBattleAIUnit> allyActiveUnits = new();
        List<IBattleAIUnit> opponentActiveUnits = new();
        // defensiveSwitchLog.Add( $"===[Defensive Switch Candidate] Pokemon returning to the bench is: {returnPokemon.Pokemon.NickName}]===" );
        allyUnits = _ai.GetTeamAs_IBattleAIUnit( returnPokemon.Pokemon );
        allyActiveUnits = _ai.GetActiveAllyUnits_AsBattleAIUnits( returnPokemon.Pokemon );
        opponentActiveUnits = _ai.GetActiveOpposingUnits_AsBattleAIUnits( returnPokemon.Pokemon );

        if( allyUnits.Count > 6 )
            Debug.LogError( $"how this mf have more than 6 pokemon on his team? {returnPokemon.Name}" );

        bench = allyUnits.Where( p => !allyActiveUnits.Any( u => u.Pokemon == p.Pokemon ) && p.Pokemon.CurrentHP > 0  ).ToList();
        
        if( bench.Count > 5 )
            Debug.LogError( $"how this mf have more than 5 pokemon on his bench? {returnPokemon.Name}" );

        // defensiveSwitchLog.Add( $"===[Defensive Switch Candidate] Ally Units Count: {allyUnits.Count}, Bench Count: {bench.Count}]===" );

        threat = _ai.GetThreat_ImmediateDamage( opponentActiveUnits, returnPokemon ).Unit;
        incomingMove = _ai.MoveCommand.GetMove_BestAttack( threat, returnPokemon, false, "GetSwitch_Defensive(), incoming move" );
        // defensiveSwitchLog.Add( $"===[Defensive Switch Candidate] Ally Active Unit[0]: {allyActiveUnits[0].Name}, Opponent Active Unit[0]: {opponentActiveUnits[0].Name}]===" );
        // defensiveSwitchLog.Add( $"===[Defensive Switch Candidate] Threat: {threat.Pokemon.NickName}, incoming move: {incomingMove.Move.MoveSO.Name}]===" );

        var threatsEDR_onCurrentMon = _proj.Get_EstimatedDamageResult( threat, returnPokemon, incomingMove );
        PotentialToKOResult threatPTKOR_onCurrentMon = _proj.Get_PotentialToKOResult( threatsEDR_onCurrentMon, incomingMove, _ai.Get_HPRatio( returnPokemon ) );

        // defensiveSwitchLog.Add( $"===[Defensive Switch Candidate] Getting Defensive Switch for {returnPokemon.Name}. Opponent PTKO on us: {threatPTKOR_onCurrentMon.PTKO} with move: {incomingMove.Move.MoveSO.Name}]===" );

        if( bench.Count > 0 )
        {
            foreach( var candidateAdapter in bench )
            {
                if( candidateAdapter.Pokemon.IsFainted )
                    continue;

                if( !returnAll && _ai.BattleSystem.IsPokemonSelectedToShift( candidateAdapter.Pokemon ) )
                    continue;

                // defensiveSwitchLog.Add( $"=[Defensive Switch Candidate][{candidateAdapter.Name}] Beginning evaluation for {candidateAdapter.Name}. Their current hp is: {candidateAdapter.Pokemon.CurrentHP} ({candidateAdapter.CurrentHPR})]=" );

                int score = 100;

                float candidateHPRafterHazards = _ai.Get_HPRatio_AfterEntryHazards( candidateAdapter );

                if( candidateHPRafterHazards <= 0f && !_ai.Check_IsLastPokemon( candidateAdapter.Pokemon ) )
                {
                    var remaining = _ai.GetRemainingAllyPokemon( candidateAdapter.Pokemon );
                    if( remaining.Count > 1 )
                        continue;
                }

                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] HPR after Hazards is: {candidateHPRafterHazards}" );

                //--Rebuild incoming move's MTR.
                float effectiveness = TypeChart.GetTotalMoveEffectiveness( candidateAdapter.Type, incomingMove.Move );
                MoveThreatResult incomingMTR_vsCandidate = new()
                {
                    Modifier = effectiveness * _ai.UnitSim.Get_MoveModifier( candidateAdapter, threat, incomingMove.Move ),
                    Move = incomingMove.Move,
                };

                //--Offensive PTKO Result. This is the candidate's potential to KO the current opponent.
                var threatHPR = _ai.Get_HPRatio( threat );
                var candidateMTR = _ai.MoveCommand.GetMove_BestAttack( candidateAdapter, threat, false, "Get Switch Defensive (our move)" );
                var candidateEDR = _proj.Get_EstimatedDamageResult( candidateAdapter, threat, candidateMTR );

                //--Defensive PTKO Result. This is the opponent's potential to KO this candidate.
                var threatsEDR = _proj.Get_EstimatedDamageResult( threat, candidateAdapter, incomingMTR_vsCandidate );

                PotentialToKOResult candidatePTKOR = _proj.Get_PotentialToKOResult( candidateEDR, candidateMTR, threatHPR );
                PotentialToKOResult threatPTKOR = _proj.Get_PotentialToKOResult( threatsEDR, incomingMTR_vsCandidate, candidateHPRafterHazards );

                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] Our PTKO them: {candidatePTKOR.PTKO}. Their PTKO us: {threatPTKOR.PTKO}" );

                //--Build Simulation Units & Field
                var fieldSim = _ai.UnitSim.BuildSimField();

                var candidateSim = _ai.UnitSim.BuildSimUnit( candidateAdapter, candidateHPRafterHazards, candidateMTR, fieldSim );
                var threatSim = _ai.UnitSim.BuildSimUnit( threat, threatHPR, incomingMTR_vsCandidate, fieldSim );

                SimulationPackage candidatePack = new(){ SimUnit = candidateSim, ModuleType = SimModuleType.Switch };
                SimulationPackage threatPack = new(){ SimUnit = threatSim, ModuleType = SimModuleType.Attack };

                var bse = _battleSim.BuildBattleSimEvent( candidatePTKOR.PTKO, threatPTKOR.PTKO, candidatePack, threatPack, fieldSim );
                var switchTOP = _battleSim.RunSimulation( bse ); //--Attacker is switch, opponent is switch
                // var switchTOP = _battleSim.SimulateSwitchRound( battleSimContext, true, false ); //--Attacker is switch, opponent is switch

                // defensiveSwitchLog.Add( $"==[Defensive Switch Candidate][{candidateAdapter.Name}] Logging TOP]===" );
                // defensiveSwitchLog.Add( $"{switchTOP.SimulationLog}" );
                // defensiveSwitchLog.Add( $"" );

                //--Survive switch-in hard-gate
                if( switchTOP.Attacker_DiesBeforeActing )
                    score -= 125;
                else if( switchTOP.Attacker_EndOfTurnHP <= 0f )
                    score -= 100;

                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] Attacker Dies before Acting: {switchTOP.Attacker_DiesBeforeActing}. Attacker end of turn HPR: {switchTOP.Attacker_EndOfTurnHP}. Score: {score}" );

                //--Damage taken on switch in factor
                float damageTaken = candidateHPRafterHazards - switchTOP.Attacker_EndOfTurnHP;
                score += Mathf.FloorToInt( ( 1f - damageTaken )  * 35f );

                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] Damage Taken: {damageTaken}. Score: {score}" );

                //--Modifier influence. Higher modifiers likely mean super effective damage and switching a mon into a super effective hit is lunacy.
                if( effectiveness >= 4f )             score -= 35; //--4x damage is almost always certain death. Ideally never pick this candidate.
                else if( effectiveness >= 2f )        score -= 20; //--Discourage super effective damage.
                else if( effectiveness >= 1f )        score += 0;
                else if( effectiveness >= 0.75f )     score += 10; //--Reward Resistances
                else if( effectiveness >= 0.5f )      score += 15; //--Reward Resistances
                else if( effectiveness >= 0.25f )     score += 20; //--Reward Resistances
                else if( effectiveness == 0f )        score += 30; //--Reward Immunity

                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] Modifier: {effectiveness}. Score: {score}" );

                //--Consider candidate's expendability.
                float expendability = _proj.GetExpendability( candidateAdapter, candidateHPRafterHazards );
                int sacrificeWeight = 35;
                int expendabilityScore = Mathf.FloorToInt( expendability * sacrificeWeight );
                score -= expendabilityScore;
                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] Expendability Score: {expendabilityScore}. Score: {score}" );

                //--Role Preservation
                if( _ai.Blackboard.OurTeamPieceValues.TryGetValue( returnPokemon.Pokemon, out var currentPieceValue ) )
                {
                    int preserveOffensivePieceBonus = Mathf.FloorToInt( currentPieceValue.OffensiveValue * 0.5f );
                    score += preserveOffensivePieceBonus;
                    // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] Preserve Offensive Piece Bonus: {preserveOffensivePieceBonus}. Score: {score}" );
                }

                if( _ai.Blackboard.OurTeamPieceValues.TryGetValue( candidateAdapter.Pokemon, out var candidatePieceValue ) )
                {
                    int deathValuePenalty = Mathf.FloorToInt( candidatePieceValue.OffensiveValue * 0.5f );
                    score -= deathValuePenalty;
                    // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] Death Value Penalty: {deathValuePenalty}. Score: {score}" );
                }

                //--Penalty for likely undoing a pivot
                if( _ai.LastSentInPokemon != null )
                {
                    if( candidateAdapter == _ai.LastSentInPokemon )
                    {
                        score -= 30;
                    
                        bool lastOpponentStillOnField = false;
                        for( int i = 0; i < _ai.LastOpposingPokemon.Count; i++ )
                        {
                            var lastOpp = _ai.LastOpposingPokemon[i];
                            if( lastOpp.PID == threat.PID )
                            {
                                lastOpponentStillOnField = true;
                                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] This Pokemon's Last Opponent is still on the field! Skipping!");
                                break;
                            }
                            else
                                continue;
                        }

                        if( lastOpponentStillOnField )
                            score -= 50;
                    }
                }

                //--Legitimacy Checks
                bool isStillDying = switchTOP.Attacker_DiesBeforeActing || switchTOP.Attacker_EndOfTurnHP <= 0f;;
                bool improvesKOClass = threatPTKOR.PTKO < threatPTKOR_onCurrentMon.PTKO;
                bool legitSwitch = true;
                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] KO Class Improved: {improvesKOClass}, The Switch will still die: {isStillDying}, IsLegit Switch: {islegit}" );
                
                if ( isStillDying && !improvesKOClass )
                {
                    legitSwitch = false;
                    // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] KO Class Legitimacy Gate IsLegit: {islegit}" );
                }

                //--Minor lookahead, with minor bonuses
                var next = _ai.MoveCommand.GetMove_BestAttack( switchTOP.Attacker, switchTOP.Opponent ).Top;
                // defensiveSwitchLog.Add( $"===[Defensive Switch Candidate][{candidateAdapter.NickName}] Logging Look ahead ]===" );
                // defensiveSwitchLog.Add( $"{next.SimulationLog}" );
                // defensiveSwitchLog.Add( $"" );

                //--are WE forced to switch next turn?
                float weSwitchNextProb = _ai.UnitSim.PredictSwitchProbability( next.Attacker.Pokemon, next.OpponentPTKO, next.AttackerPTKO, next.AttackerMovedFirst, switchTOP.Opponent_EndOfTurnHP, switchTOP.Attacker_EndOfTurnHP, next.Attacker.Expendability );
                score -= Mathf.FloorToInt( 30f * weSwitchNextProb );

                //--Do we die immediately next turn?
                if( next.Attacker_DiesBeforeActing )
                    score -= 50;
                else if( next.Attacker_EndOfTurnHP <= 0 )
                    score -= 30;

                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] Next - Attacker Dies before acting: {next.Attacker_DiesBeforeActing}. Attacker end of turn hpr: {next.Attacker_EndOfTurnHP}. Are we forced to switch: {areWeForcedToSwitch}. Score: {score}" );

                //--Do we pressure and force a switch? Do we KO?
                float theySwitchNextProb = _ai.UnitSim.PredictSwitchProbability( next.Opponent.Pokemon, next.AttackerPTKO, next.OpponentPTKO, next.AttackerMovedFirst, switchTOP.Attacker_EndOfTurnHP, switchTOP.Opponent_EndOfTurnHP, next.Opponent.Expendability );
                score += Mathf.FloorToInt( 20f * theySwitchNextProb );

                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] Next - Candidate PTKO: {next.AttackerPTKO}. Are they forced to switch: {doWeForceThemToSwitch}. Score: {score}" );

                if( next.AttackerMovedFirst )
                    score += 5;
                else
                    score -= 5;

                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] Attacker moved first: {next.AttackerMovedFirst}. Score: {score}" );

                if( returnAll )
                {
                    SwitchCandidateResult src = new()
                    {
                        Score = score,
                        Pokemon = candidateAdapter.Pokemon,
                        HPRatio = candidateHPRafterHazards,
                        SwitchOffensePTKOR = candidatePTKOR,
                        SwitchDefensePTKOR = threatPTKOR,
                        IsLegitimate = islegit,
                        Top = switchTOP,

                        Type = ActionResultType.Switch,
                        ActionType = ActionType.DefensiveSwitch,
                    };

                    returnAllList.Add( candidateAdapter.Pokemon, src );
                }

                if( score > bestScore )
                {
                    bestScore = score;
                    bestSwitch = candidateAdapter.Pokemon;
                    bestHPRatio = candidateHPRafterHazards;
                    bestSwitch_OffensePTKOR = candidatePTKOR;
                    bestSwitch_DefensePTKOR = threatPTKOR;
                    threatsScariestMoveModifier = incomingMTR_vsCandidate.Modifier;
                    bestCandidateTOP = switchTOP;
                    islegit = legitSwitch;
                    isFaster = next.AttackerMovedFirst;
                }

                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate][{candidateAdapter.Name}] Final Score: {score}" );
                // defensiveSwitchLog.Add( $"" );
                // defensiveSwitchLog.Add( $"===================================================================================" );
                // defensiveSwitchLog.Add( $"" );
            }

            if( bestSwitch == null )
            {
                Debug.Log( $"[Defensive Switch Candidate] No Switch available!" );
            }
            // else
                // defensiveSwitchLog.Add( $"[Defensive Switch Candidate] Best Defensive Switch: {bestSwitch?.NickName}, Final Score: {bestScore}" );
        }

        // Debug.Log( defensiveSwitchLog.ToString() );
        // defensiveSwitchLog.Clear();

        SwitchCandidateResult scr = new()
        {
            Score = bestScore,
            Pokemon = bestSwitch,
            HPRatio = bestHPRatio,
            SwitchOffensePTKOR = bestSwitch_OffensePTKOR,
            SwitchDefensePTKOR = bestSwitch_DefensePTKOR,
            IsLegitimate = islegit,
            Top = bestCandidateTOP,

            Type = ActionResultType.Switch,
            ActionType = ActionType.DefensiveSwitch,
        };

        if( returnAll )
        {
            scr.ReturnAllList = new();
            scr.ReturnAllList = returnAllList.ToDictionary( kvp => kvp.Key, kvp => kvp.Value );
        }

        return scr;
        
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public SwitchCandidateResult GetSwitch_Offensive( IBattleAIUnit returnPokemon, bool returnAll = false )
    {
        int bestScore = int.MinValue;
        Pokemon bestSwitch = null;
        float bestHPRatio = 0f;
        ThreatResult biggestThreat;
        MoveThreatResult mostThreateningMove = new();
        TurnOutcomeProjection bestTop = new();
        PotentialToKOResult bestSwitch_OffensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        PotentialToKOResult bestSwitch_DefensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        bool isFaster = false;

        List<IBattleAIUnit> bench = new();
        Dictionary<Pokemon, SwitchCandidateResult> returnAllList = new();

        List<IBattleAIUnit> allyUnits = new();
        List<IBattleAIUnit> allyActiveUnits = new();
        List<IBattleAIUnit> opponentActiveUnits = new();

        //--Convert switching functions to only take in the pokemon that wants to switch. using that pokemon, we will gain access to
        //--that pokemon's opposing units and their ally bench through helpers in BattleAI. This will make these functions much more
        //--generic and reusable without the weird readOpponent hacks and potentialy wrong-team comparison errors

        allyUnits = _ai.GetTeamAs_IBattleAIUnit( returnPokemon.Pokemon );
        allyActiveUnits = _ai.GetActiveAllyUnits_AsBattleAIUnits( returnPokemon.Pokemon );
        opponentActiveUnits = _ai.GetActiveOpposingUnits_AsBattleAIUnits( returnPokemon.Pokemon );

        if( allyUnits.Count > 6 )
            Debug.LogError( $"how this mf have more than 6 pokemon on his team? {returnPokemon.Name}" );

        bench = allyUnits.Where( p => !allyActiveUnits.Any( u => u.Pokemon == p.Pokemon ) && p.Pokemon.CurrentHP > 0  ).ToList();
        
        if( bench.Count > 5 )
            Debug.LogError( $"how this mf have more than 5 pokemon on his bench? {returnPokemon.Name}" );

        foreach( var candidate in bench )
        {
            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] Evaluating {pokemon.NickName}. Their current hp is: {pokemon.CurrentHP}" );
            if( candidate.Pokemon.IsFainted )
                continue;

            if( !returnAll && _ai.BattleSystem.IsPokemonSelectedToShift( candidate.Pokemon ) )
                continue;

            var threat = _ai.GetThreat_ImmediateDamage( opponentActiveUnits, candidate );
            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] Chosen threat is: {threat.Unit.Pokemon.NickName}" );

            int score = 100;
            int sacrificeWeight = 30;

            float hpRatioAfterHazards = _ai.Get_HPRatio_AfterEntryHazards( candidate );

            float expendability = _proj.GetExpendability( candidate, hpRatioAfterHazards );
            int expendabilityScore = Mathf.FloorToInt( expendability * sacrificeWeight );
            score -= expendabilityScore;

                // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] HPR: {hpRatioAfterHazards}. Expendability & its Score: {expendability}, {expendabilityScore}." );

            if( hpRatioAfterHazards <= 0f && !_ai.Check_IsLastPokemon( candidate.Pokemon ) )
            {
                var remaining = _ai.GetRemainingAllyPokemon( candidate.Pokemon );
                if( remaining.Count > 1 )
                    continue;
            }

            //--Get PTKOs
            //--Offensive PTKO Result. This is the candidate's potential to KO the current opponent.
            var threatHPR                       = _ai.Get_HPRatio( threat.Unit );
            var candidateMove                   = _ai.MoveCommand.GetMove_BestAttack( candidate, threat.Unit, false, "Get Switch Offensive (candidate move vs current threat)" );
            var candidateMoveModifier           = candidateMove.Modifier;
            var candidateWSR                    = _proj.Get_EstimatedDamageResult( candidate, threat.Unit, candidateMove );
            PotentialToKOResult offensePTKOR    = _proj.Get_PotentialToKOResult( candidateWSR, candidateMove, threatHPR );

            //--Defensive PTKO Result. This is the opponent's potential to KO this candidate.
            var threatsMove                     = _ai.MoveCommand.GetMove_BestAttack( threat.Unit, candidate, false, "Get Switch Offensive (current threat vs candidate)" );
            var threatsMoveModifier             = threatsMove.Modifier;
            var threatsWSR                      = _proj.Get_EstimatedDamageResult( threat.Unit, candidate, threatsMove );
            PotentialToKOResult defensePTKOR    = _proj.Get_PotentialToKOResult( threatsWSR, threatsMove, hpRatioAfterHazards );

            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] PTKOs Obtained. {pokemon.NickName} PTKO: {offensePTKOR.PTKO}. {threat.Unit.Pokemon.NickName} PTKO: {defensePTKOR.PTKO}" );

            //--Build Simulation Units & Field
            var fieldSim                        = _ai.UnitSim.BuildSimField();
            var threatSim                       = _ai.UnitSim.BuildSimUnit( threat.Unit, threatHPR, threatsMove, fieldSim );

            var candidateSim                    = _ai.UnitSim.BuildSimUnit( candidate, hpRatioAfterHazards, candidateMove, fieldSim );

            SimulationPackage candidatePack     = new(){ SimUnit = candidateSim, ModuleType = SimModuleType.Switch };
            SimulationPackage threatPack        = new(){ SimUnit = threatSim, ModuleType = SimModuleType.Attack };

            var bse                             = _battleSim.BuildBattleSimEvent( offensePTKOR.PTKO, defensePTKOR.PTKO, candidatePack, threatPack, fieldSim );
            var top                             = _battleSim.RunSimulation( bse );
            // var top                             = _battleSim.SimulateSwitchRound( bse, true, false );

            //--Speed check.
            bool movesFirst = false;

            var attMovePrio = candidateSim.MTR.Move.Priority;
            var oppMovePrio = threatSim.MTR.Move.Priority;

            if( attMovePrio != oppMovePrio )
                movesFirst = attMovePrio > oppMovePrio;
            else
                movesFirst = candidateSim.Speed > threatSim.Speed;

            //--Begin Scoring
            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] Beginning Scoring. Base Score: {score}" );

            //--Immediately penalize if it faints on entry. I should make sure this bool gets set correctly from Simulate Switch Round. This should probably be harsher.
            if( top.Attacker_DiesBeforeActing )
                score -= 125;

            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] dies before acting {top.Attacker_DiesBeforeActing}. Score: {score}" );

            //--General HP Score?
            score += Mathf.FloorToInt( hpRatioAfterHazards * 40 );
            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] HPR Bonus. End of turn HPR: {top.Attacker_EndOfTurnHP}. Score: {score}" );

            //--Predict Opponent Switches
            float opponentSwitchProb = _ai.UnitSim.PredictSwitchProbability( threatSim.Pokemon, offensePTKOR.PTKO, defensePTKOR.PTKO, movesFirst, 1f, top.Opponent.BeginningHPR, top.Opponent.Expendability );

            //--PTKO Scoring
            int offenseScore = offensePTKOR.PTKO switch
            {
                PotentialToKO.TwoHKO        => 10,
                PotentialToKO.Risky         => 30,
                PotentialToKO.Dangerous     => 50,
                PotentialToKO.OHKO          => 70,
                _ => 0,
            };

            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] Offensive PTKO ({offensePTKOR.PTKO}) on opponent {threat.Unit.Pokemon.NickName}. Score: {score}" );

            score += defensePTKOR.PTKO switch
            {
                PotentialToKO.OHKO          => -60,
                PotentialToKO.Dangerous     => -40,
                PotentialToKO.Risky         => -20,
                >= PotentialToKO.TwoHKO      => +10, //--Greater than or equal to twohko, meaning also safe, sturdy, and hard wall
                _ => 0,
            };

            //--Reduce influence of current PTKO - it doesn't apply if the opponent switches.
            offenseScore = Mathf.FloorToInt( offenseScore * 0.5f * opponentSwitchProb );

            //--Use offensive value to influence more generally offensive candidate.
            var pieceValue = _ai.Blackboard.GetPokemon_PieceValue( candidate.Pokemon );
            score += pieceValue.OffensiveValue / 2; //-- /2 just to reduce severity. we don't want the most offensively valued pokemon to be overvalued in this context.

            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] Defensive PTKO ({defensePTKOR.PTKO}) from opponent {threat.Unit.Pokemon.NickName}. Score: {score}" );

            if( movesFirst )
                score += 20;
            else
                score -= 10;

            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] Moves first: {movesFirst}. Score: {score}" );
            Dictionary<CourtConditionID, int> courtConditions = new();
            if( threatSim.CourtLocation == CourtLocation.TopCourt )
                courtConditions = fieldSim.TopCourtConditions;
            else if( threatSim.CourtLocation == CourtLocation.BottomCourt )
                courtConditions = fieldSim.BottomCourtConditions;

            int entryHazardsOnOpposingSide = 0;
            if( courtConditions.ContainsKey( CourtConditionID.StealthRock ) )
                entryHazardsOnOpposingSide++;
            
            if( courtConditions.ContainsKey( CourtConditionID.Spikes ) )
                entryHazardsOnOpposingSide++;

            if( courtConditions.ContainsKey( CourtConditionID.ToxicSpikes ) )
                entryHazardsOnOpposingSide++;

            if( courtConditions.ContainsKey( CourtConditionID.LeechSeed ) )
                entryHazardsOnOpposingSide++;

            //--Pressure might be enough to force opposing side to switch out. Reward, and if we've set up hazards, reward for forcing them to switch into them.
            int hazardReward = entryHazardsOnOpposingSide == 0 ? 0 : 2 * entryHazardsOnOpposingSide;
            score += Mathf.FloorToInt( 40f * opponentSwitchProb ) * hazardReward;
            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] We threaten to force a switch! Entry Hazard on opposing side count: {entryHazardsOnOpposingSide}. Score: {score}" );

            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] Checked Expendability Score: {expendabilityScore}. Final Score: {score}" );

            if( returnAll )
            {
                SwitchCandidateResult src = new()
                {
                    Score = score,
                    Pokemon = candidate.Pokemon,
                    HPRatio = hpRatioAfterHazards,
                    SwitchOffensePTKOR = offensePTKOR,
                    SwitchDefensePTKOR = defensePTKOR,
                    Top = top,

                    Type = ActionResultType.Switch,
                    ActionType = ActionType.OffensiveSwitch,
                };

                returnAllList.Add( candidate.Pokemon, src );
            }

            if( score > bestScore )
            {
                bestScore = score;
                bestSwitch = candidate.Pokemon;
                bestHPRatio = hpRatioAfterHazards;
                bestTop = top;
                bestSwitch_OffensePTKOR = offensePTKOR;
                bestSwitch_DefensePTKOR = defensePTKOR;
                biggestThreat = threat;
                mostThreateningMove = candidateMove;
                isFaster = movesFirst;
            }
        }

        if( bestSwitch == null )
        {
            Debug.Log( $"[AI Scoring][Offensive Switch Candidate] No Switch available!" );
        }

        SwitchCandidateResult scr = new()
        {
            Score = bestScore,
            Pokemon = bestSwitch,
            HPRatio = bestHPRatio,
            SwitchOffensePTKOR = bestSwitch_OffensePTKOR,
            SwitchDefensePTKOR = bestSwitch_DefensePTKOR,
            MovesFirst = isFaster,
            Top = bestTop,

            Type = ActionResultType.Switch,
            ActionType = ActionType.OffensiveSwitch,
        };

        if( returnAll )
        {
            scr.ReturnAllList = new();
            scr.ReturnAllList = returnAllList.ToDictionary( kvp => kvp.Key, kvp => kvp.Value );
        }

        return scr;
    }

////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public SwitchCandidateResult GetSwitch_Revenge( List<IBattleAIUnit> opponents )
    {
        int bestScore = int.MinValue;
        Pokemon bestSwitch = null;
        float bestHPRatio = 0f;
        ThreatResult biggestThreat = _ai.GetThreat_ImmediateDamage( opponents, _ai.CurrentUnitAdapter ); //--The biggest threat will start as the biggest threat to the current pokemon thinking of switching. It will get overwritten if there's a viable switch candidate. this prevents it from being null in the case there are no more viable switch ins.
        MoveThreatResult mostThreateningMove = new();
        TurnOutcomeProjection bestTop = new();
        PotentialToKOResult bestSwitch_OffensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        PotentialToKOResult bestSwitch_DefensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        bool islegit = true;
        bool isFaster = false;

        List<IBattleAIUnit> bench = new();

        List<IBattleAIUnit> allyTeam = new();
        List<BattleUnit> allyActiveBattleUnits = new();
        List<IBattleAIUnit> allyActiveUnits = new();
        List<IBattleAIUnit> opponentActiveUnits = new();

        allyTeam = _ai.GetOpposingTeamAs_IBattleAIUnit( opponents[0].Pokemon );
        allyActiveBattleUnits = _ai.BattleSystem.GetAllyUnits( allyTeam[0].Pokemon ); //--We get the active units directly from the battle system to avoid issues with unrefreshed team adapters & active ibattleaiunit tracking.
        allyActiveUnits = _ai.GetActiveAllyUnits_AsBattleAIUnits( allyActiveBattleUnits[0].Pokemon );

        // if( allyTeam.Count > 6 )
            // Debug.LogError( $"how this mf have more than 6 pokemon on his team?" );

        bench = allyTeam.Where( p => !allyActiveUnits.Any( u => u.Pokemon == p.Pokemon ) && p.Pokemon.CurrentHP > 0  ).ToList();
        int remaining = allyTeam.Where( p => p.Pokemon.CurrentHP > 0 ).ToList().Count;
        
        // if( bench.Count > 5 )
            // Debug.LogError( $"how this mf have more than 5 pokemon on his bench?" );

        // if( bench.Count <= 0 && remaining > 0 )
            // Debug.LogError( $"Ally Count: {remaining}. Somehow we have no mons on the bench but mons remaining on the team. How did a pokemon end up being considered active when it wasn't on the field? possibly in adapter updates..." );

        // CustomLogSession log = new();
        // log.Add( $"===[Beginning Revenge Switch Candidate Selection]===" );

        foreach( var candidate in bench )
        {
            // log.Add( $"===[AI Scoring][Revenge Switch Candidate] Evaluating {candidate.Pokemon.NickName}. Their current hp is: {candidate.Pokemon.CurrentHP}===" );
            if( candidate.Pokemon.IsFainted )
                continue;

            if( _ai.BattleSystem.IsPokemonSelectedToShift( candidate.Pokemon ) )
                continue;

            var threat = _ai.GetThreat_ImmediateDamage( opponents, candidate );
            // log.Add( $"[AI Scoring][Revenge Switch Candidate] Chosen threat is: {threat.Unit.Name}" );

            int score = 100;

            float hpRatioAfterHazards = _ai.Get_HPRatio_AfterEntryHazards( candidate );
            if( hpRatioAfterHazards <= 0f && !_ai.Check_IsLastPokemon( candidate.Pokemon ) )
            {
                if( remaining > 1 )
                    continue;
            }

            //--Get PTKOs
            //--Offensive PTKO Result. This is the candidate's potential to KO the current opponent.
            var threatHPR                       = _ai.Get_HPRatio( threat.Unit );
            var candidateMove                   = _ai.MoveCommand.GetMove_BestAttack( candidate, threat.Unit, false, "Get Switch Revenge (candidate vs current threat)" );
            var candidateMoveModifier           = candidateMove.Modifier;
            var candidateWSR                    = _proj.Get_EstimatedDamageResult( candidate, threat.Unit, candidateMove );
            PotentialToKOResult offensePTKOR    = _proj.Get_PotentialToKOResult( candidateWSR, candidateMove, threatHPR );

            //--Defensive PTKO Result. This is the opponent's potential to KO this candidate.
            var threatsMove                     = _ai.MoveCommand.GetMove_BestAttack( threat.Unit, candidate, false, "Get Switch Revenge (current threat vs candidate)" );
            var threatsMoveModifier             = threatsMove.Modifier;
            var threatsWSR                      = _proj.Get_EstimatedDamageResult( threat.Unit, candidate, threatsMove );
            PotentialToKOResult defensePTKOR    = _proj.Get_PotentialToKOResult( threatsWSR, threatsMove, hpRatioAfterHazards );

            // log.Add( $"[AI Scoring][Revenge Switch Candidate] PTKOs Obtained. {candidate.Name} PTKO: {offensePTKOR.PTKO}. {threat.Unit.Name} PTKO: {defensePTKOR.PTKO}" );

            //--Build Simulation Units & Field
            var fieldSim                        = _ai.UnitSim.BuildSimField();

            var candidateSim                    = _ai.UnitSim.BuildSimUnit( candidate, hpRatioAfterHazards, candidateMove, fieldSim );
            var threatSim                       = _ai.UnitSim.BuildSimUnit( threat.Unit, threatHPR, threatsMove, fieldSim );

            SimulationPackage candidatePack     = new(){ SimUnit = candidateSim, ModuleType = SimModuleType.Switch };
            SimulationPackage threatPack        = new(){ SimUnit = threatSim, ModuleType = SimModuleType.Attack };

            var bse                             = _battleSim.BuildBattleSimEvent( offensePTKOR.PTKO, defensePTKOR.PTKO, candidatePack, threatPack, fieldSim );
            var top                             = _battleSim.RunSimulation( bse );
            // var top                             = _battleSim.SimulateAttackRound( bse );

            //--Speed check.
            bool movesFirst = top.AttackerMovedFirst;

            //--Utility check
            bool hasUtility = _ai.UnitSim.GetOffensiveStatusMoves( candidate.ActiveMoves ).Count > 0 || _ai.UnitSim.GetSetupMoves( candidate.ActiveMoves ).Count > 0;

            //--Begin Scoring
            // log.Add( $"[AI Scoring][Revenge Switch Candidate] Beginning Scoring. Base Score: {score}" );

            //--Speed Check. Important.
            if( movesFirst )
                score += 25;
            else
                score -= 10;

            if( hasUtility && top.AttackerPTKO <= PotentialToKO.TwoHKO )
                score += 10;
            else if( hasUtility )
                score += 5;
            else if( top.AttackerPTKO <= PotentialToKO.Safe && top.OpponentPTKO >= PotentialToKO.TwoHKO )
                score -= 10;

            // log.Add( $"[AI Scoring][Revenge Switch Candidate] Moves First: {movesFirst}. Score: {score}" );

            //--Damage & KO Scoring
            if( top.Opponent_DiesBeforeActing )
            {
                score += 120;
                // log.Add( $"[AI Scoring][Revenge Switch Candidate] Opponent Dies before acting. Score: {score}" );
            }
            else if( top.Opponent_EndOfTurnHP <= 0f && top.Attacker_EndOfTurnHP > 0f )
            {
                score += 90;
                // log.Add( $"[AI Scoring][Revenge Switch Candidate] Opponent Dies and we live. Score: {score}" );
            }
            else if( top.Opponent_EndOfTurnHP <= 0f && top.Attacker_EndOfTurnHP <= 0f )
            {
                score += 30;
                // score -= expendabilityScore;
                // log.Add( $"[AI Scoring][Revenge Switch Candidate] We both faint. Score: {score}" );
            }
            else if( top.Attacker_EndOfTurnHP <= 0 && top.Opponent_EndOfTurnHP > 0 )
            {
                score -= 90;
                // log.Add( $"[AI Scoring][Revenge Switch Candidate] We faint and our opponent does not. Score: {score}" );
            }

            if( top.Attacker_DiesBeforeActing )
            {
                score -= 150;
                // log.Add( $"[AI Scoring][Revenge Switch Candidate] We Die before acting. Score: {score}" );
            }

            if( top.Attacker_EndOfTurnHP > 0f )
                score += 25;
            else
                score -= 25;

            if( top.Attacker_EndOfTurnHP > 0f && top.Opponent_EndOfTurnHP > 0f )
            {
                float damageDealt = 1f - top.Opponent_EndOfTurnHP;
                float damageTaken = 1f - top.Attacker_EndOfTurnHP;

                if( offensePTKOR.PTKO == PotentialToKO.Untouchable )
                    damageDealt = 0f;

                score += Mathf.FloorToInt( damageDealt * 60f );
                score -= Mathf.FloorToInt( damageTaken * 40f );

                // log.Add( $"[AI Scoring][Revenge Switch Candidate] {candidate.Name} damage done: {damageDealt}. {threat.Unit.Name} damage done: {damageTaken}. Neither faint. Score: {score}" );
            }

            score += Mathf.FloorToInt( defensePTKOR.Score * -0.5f );
            score += Mathf.FloorToInt( offensePTKOR.Score * 0.5f );

            //--Predict if the opponent is likely to switch at the start of the immediate revenge round. Bonus if we force a switch.
            float opponentSwitchProb = _ai.UnitSim.PredictSwitchProbability( top.Opponent.Pokemon, top.AttackerPTKO, top.OpponentPTKO, movesFirst, top.Attacker.BeginningHPR, top.Opponent.BeginningHPR, top.Opponent.Expendability );
            score += Mathf.FloorToInt( 45f * opponentSwitchProb );

            //--Predict if this candidate is likely to switch at the start of the immediate revenge round. if so, penalize. revenge candidates should attack or create offense/pressure with status, not make a defensive switch as their first immediate action.
            float candidateImmediateSwitchProbability = _ai.UnitSim.PredictSwitchProbability( top.Attacker.Pokemon, top.OpponentPTKO, top.AttackerPTKO, movesFirst, top.Opponent.BeginningHPR, top.Attacker.BeginningHPR, top.Attacker.Expendability );
            score -= Mathf.FloorToInt( 75f * candidateImmediateSwitchProbability );

            bool opponentSwitches = UnityEngine.Random.value <= opponentSwitchProb;

            //--Look ahead at the round after the immediate revenge round. Do we succeed at revenging? Do we create pressure? Do we fail? Are we forced to switch immediately?
            if( top.Attacker_EndOfTurnHP > 0 )
            {
                    var ourActivePokemon = _ai.BattleSystem.GetAllyUnits( _ai.CurrentUnitDeciding );
                    var ourActiveAdapters = _ai.CreateBattleAIUnits_FromBattleUnits( ourActivePokemon );
                    
                    var offensiveSwitch = GetSwitch_Offensive( top.Opponent ).Pokemon;
                    var defensiveSwitch = GetSwitch_Defensive( top.Opponent ).Top.Attacker;

                    SimulatedUnit nextOpponent;
                    MoveThreatResult nextOpponentMTR;

                    if( top.Opponent_EndOfTurnHP <= 0f && offensiveSwitch != null )
                    {
                        BattleAI_PokemonAdapter opponentOffensiveSwitchAdapter = _ai.GetPokemonAs_Adapter( offensiveSwitch );
                        nextOpponentMTR = _ai.MoveCommand.GetMove_BestAttack( opponentOffensiveSwitchAdapter, top.Attacker );
                        nextOpponent = _ai.UnitSim.BuildSimUnit( opponentOffensiveSwitchAdapter, opponentOffensiveSwitchAdapter.BeginningHPR, nextOpponentMTR, fieldSim );
                    }
                    else if( opponentSwitches && defensiveSwitch != null )
                    {
                        SimulatedUnit opponentDefensiveSwitchAdapter = defensiveSwitch;
                        nextOpponentMTR = _ai.MoveCommand.GetMove_BestAttack( opponentDefensiveSwitchAdapter, top.Attacker );
                        nextOpponent = _ai.UnitSim.BuildSimUnit( opponentDefensiveSwitchAdapter, opponentDefensiveSwitchAdapter.CurrentHPR, nextOpponentMTR, fieldSim );
                    }
                    else
                    {
                        nextOpponentMTR = _ai.MoveCommand.GetMove_BestAttack( top.Opponent, top.Attacker );
                        nextOpponent = _ai.UnitSim.BuildSimUnit( top.Opponent, top.Opponent_EndOfTurnHP, nextOpponentMTR, fieldSim );
                    }

                    if( nextOpponent != null )
                    {
                        var candidateMTR_FollowUp                   = _ai.MoveCommand.GetMove_BestAttack( top.Attacker, nextOpponent );

                        //--Follow up EDRs
                        var candidateEDR_FollowUp                   = _proj.Get_EstimatedDamageResult( top.Attacker, nextOpponent, candidateMTR_FollowUp );
                        var nextOpponentEDR_FollowUp                = _proj.Get_EstimatedDamageResult( nextOpponent, top.Attacker, nextOpponentMTR );

                        //--Follow up PTKORs
                        var candidatePTKOR_FollowUp                 = _proj.Get_PotentialToKOResult( candidateEDR_FollowUp, candidateMTR_FollowUp, top.Attacker_EndOfTurnHP );
                        var nextOpponentPTKOR_FollowUp              = _proj.Get_PotentialToKOResult( nextOpponentEDR_FollowUp, nextOpponentMTR, nextOpponent.BeginningHPR );

                        var candidateSim_FollowUp                   = _ai.UnitSim.BuildSimUnit( top.Attacker, top.Attacker_EndOfTurnHP, candidateMTR_FollowUp, fieldSim );
                        var threatSim_FollowUp                      = _ai.UnitSim.BuildSimUnit( nextOpponent, nextOpponent.BeginningHPR, nextOpponent.MTR, fieldSim );

                        SimulationPackage candidatePack_FollowUp    = new(){ SimUnit = candidateSim_FollowUp, ModuleType = SimModuleType.Attack };
                        SimulationPackage threatPack_FollowUp       = new(){ SimUnit = threatSim_FollowUp, ModuleType = SimModuleType.Attack };

                        var bse_FollowUp                            = _battleSim.BuildBattleSimEvent( candidatePTKOR_FollowUp.PTKO, nextOpponentPTKOR_FollowUp.PTKO, candidatePack_FollowUp, threatPack_FollowUp, fieldSim );
                        var followUp                                = _battleSim.RunSimulation( bse_FollowUp );
                        // var followUp                             = _battleSim.SimulateAttackRound( battleSimCtx_FollowUp );
                        
                        float candidateForcesSwitch_FollowUp = _ai.UnitSim.PredictSwitchProbability( followUp.Opponent.Pokemon, followUp.AttackerPTKO, followUp.OpponentPTKO, followUp.AttackerMovedFirst, followUp.Attacker.BeginningHPR, followUp.Opponent.BeginningHPR, followUp.Opponent.Expendability );
                        float opponentForcesSwitch_FollowUp = _ai.UnitSim.PredictSwitchProbability( followUp.Attacker.Pokemon, followUp.OpponentPTKO, followUp.AttackerPTKO, followUp.AttackerMovedFirst, followUp.Opponent.BeginningHPR, followUp.Attacker.BeginningHPR, followUp.Attacker.Expendability );

                        bool unstableNextTurn = followUp.Attacker_DiesBeforeActing || followUp.Attacker_EndOfTurnHP <= 0f;
                        bool canUseUtility = hasUtility && !unstableNextTurn;
                        bool lowPressure = followUp.AttackerPTKO <= PotentialToKO.Safe && !canUseUtility;

                        if( unstableNextTurn )
                            score -= 60;
                        else if( lowPressure )
                            score -= 30;

                        bool stabilizes = followUp.Attacker_EndOfTurnHP > 0.4f && followUp.OpponentPTKO < PotentialToKO.TwoHKO;
                        if( stabilizes && canUseUtility )
                            score += 45;
                        else if( stabilizes )
                            score += 35;

                        if( followUp.Opponent_DiesBeforeActing )
                            score += 20;
                        else if( followUp.Opponent_EndOfTurnHP <= 0 )
                            score += 15;

                        bool badDeath = followUp.Attacker_EndOfTurnHP <= 0f && followUp.Opponent_EndOfTurnHP > 0.4f;
                        if( badDeath )
                            score -= 30;

                        score += Mathf.FloorToInt( 40f * candidateForcesSwitch_FollowUp );
                        score -= Mathf.FloorToInt( 80f * opponentForcesSwitch_FollowUp );
                    }
            }

            // log.Add( $"[AI Scoring][Revenge Switch Candidate] {candidate.Name}'s Final Score: {score}" );
            // log.Add( $"================================================================================" );
            // log.Add( $"" );

            if( score > bestScore )
            {
                bestScore = score;
                bestSwitch = candidate.Pokemon;
                bestHPRatio = hpRatioAfterHazards;
                bestTop = top;
                bestSwitch_OffensePTKOR = offensePTKOR;
                bestSwitch_DefensePTKOR = defensePTKOR;
                biggestThreat = threat;
                mostThreateningMove = candidateMove;
                isFaster = movesFirst;
            }
        }

        if( bestSwitch == null )
        {
            if( bench.Count > 0 )
                bestSwitch = bench[Random.Range( 0, bench.Count )].Pokemon;
            else if( remaining > 0 )
                bestSwitch = allyTeam[Random.Range( 0, bench.Count )].Pokemon; //--this will likely permanently fix the error, but it really shouldn't be happening in the first place. i need to fix the active/bench discrepancy
            else
                Debug.LogError( $"[AI Scoring][Revenge Switch Candidate] No Switch available!" );
        }
        else
        {
            // log.Add( $"[AI Scoring][Revenge Switch Candidate] Chose {bestSwitch.NickName}! Final Score: {bestScore}" );
        }

        // Debug.Log( log.ToString() );
        // log.Clear();

        return new()
        {
            Score = bestScore,
            Pokemon = bestSwitch,
            HPRatio = bestHPRatio,
            SwitchOffensePTKOR = bestSwitch_OffensePTKOR,
            SwitchDefensePTKOR = bestSwitch_DefensePTKOR,
            IsLegitimate = islegit,
            MovesFirst = isFaster,
            Top = bestTop,

            Type = ActionResultType.Switch,
            ActionType = ActionType.OffensiveSwitch,
        };
    }

//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
//////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public Pokemon GetSwitch_Vacuum()
    {
        int bestScore = int.MinValue;
        Pokemon bestSwitch = null;

        var ourParty = _ai.BattleSystem.GetAllyParty( _ai.CurrentUnitDeciding.Pokemon );
        var bench = ourParty.Where( p =>  p.CurrentHP > 0  ).ToList();

        foreach( var pokemon in bench )
        {
            BattleAI_PokemonAdapter adapter = _ai.GetPokemonAs_Adapter( pokemon );
            int score = 0;

            //--Piece value
            var pieceValue = _ai.Blackboard.OurTeamPieceValues[adapter.Pokemon];
            score += pieceValue.OffensiveValue;

            //--Weather Context
            score += _ai.UnitSim.Get_WeatherContextScore( pokemon );

            //--Terrain Context
            score += _ai.UnitSim.Get_TerrainContextScore( pokemon );

            //--Room Context
            score += _ai.UnitSim.Get_TrickRoomContextScore( pokemon );
            
            //--HP Context
            float hpr = _ai.Get_HPRatio_AfterEntryHazards( pokemon );
            if( hpr <= 0.25f )          score -= 6;
            else if( hpr <= 0.5f )      score -= 4;
            else if( hpr <= 0.75f )     score -= 2;

            bool trickRoomActive = _ai.BattleSystem.BattleFlags[BattleFlag.TrickRoom];
            //--Speed Identity bonus
            if( !trickRoomActive )
                score += pieceValue.SpeedScore;
            else
                score -= pieceValue.SpeedScore;

            if( score > bestScore )
            {
                bestScore = score;
                bestSwitch = pokemon;
            }
        }

        if( bestSwitch == null )
        {
            Debug.LogError( $"[AI Scoring][Vacuum Switch Candidate] No Switch available!" );
        }

        return bestSwitch;
    }

}
