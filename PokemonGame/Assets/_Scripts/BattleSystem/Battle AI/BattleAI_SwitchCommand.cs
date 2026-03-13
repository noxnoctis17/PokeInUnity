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
        _ai.BattleSystem.SetSwitchPokemonCommand( incomingPokemon, _ai.Unit, true );
    }

    public int DefensiveSwitchScore( TempoStateResult tempo, ExchangeEvaluation eval, SwitchCandidateResult switchCandidate, BoardContext context )
    {
        //--Tank score if unable to switch
        if( switchCandidate.Pokemon == null || _ai.BattleSystem.BattleType == BattleType.WildBattle_1v1 || _ai.Check_IsLastPokemon() )
        {
            _ai.CurrentLog.Add( $"No switch available (null, wild battle, or last pokemon). Tanking Score!" );
            return -999;
        }

        int score = 0;

        var attackerName = eval.AttackerName;
        var targetName = eval.OpponentName;
        var switchName = "no switch available!";
        if( switchCandidate.Pokemon != null )
            switchName = switchCandidate.Pokemon.NickName;

        _ai.CurrentLog.Add( $"===[Beginning Switch Scoring for {attackerName} vs {targetName}. Switch Candidate: {switchName}. Tempo: {tempo.TempoState}]===" );


        if( switchCandidate.SwitchDefensePTKOR.PTKO == PotentialToKO.OHKO )
        {
            _ai.CurrentLog.Add( $"Switch candidate's potential to be KO'd is LikelyOHKO! Tanking Score!" );
            return -999;
        }
        
        var currentPTKO = eval.OpponentPTKOR.PTKO;
        var currentScore = eval.OpponentPTKOR.Score;
        var switchPTKOR = switchCandidate.SwitchDefensePTKOR;

        _ai.CurrentLog.Add( $"{targetName}'s Current PTKO me ({attackerName}): {currentPTKO}. {targetName}'s PTKO on Switch Candidate ({switchName}): {switchCandidate.SwitchDefensePTKOR.PTKO}. {switchName}'s PTKO {targetName}: {switchCandidate.SwitchOffensePTKOR.PTKO}" );

        if( context.IsTerminal && context.IsForcedTrade && !switchCandidate.IsLegitimate )
        {
            _ai.CurrentLog.Add( $"Terminal board and no KO class improvement/Switch is illegitimate. Tanking Score!" );
            return -999;
        }

        if( !context.IsTerminal && currentPTKO >= PotentialToKO.Dangerous && switchPTKOR.PTKO >= PotentialToKO.Dangerous )
            score -= 40;

        int improvement = switchPTKOR.Score - currentScore;

        score += improvement;

        _ai.CurrentLog.Add( $"Improvement: {improvement}, Score: {score}" );

        bool iDieBeforeActing = !eval.AttackerMovesFirst && eval.OpponentThreatensKO;

        if( iDieBeforeActing )
            score += 45;

        _ai.CurrentLog.Add( $"Die before Act: {iDieBeforeActing}, Score: {score}" );

        bool losingExchange = eval.OpponentThreatensKO && !eval.AttackerThreatensKO;

        if( losingExchange )
            score += 35;

        _ai.CurrentLog.Add( $"Losing Exchange: {losingExchange}, Score: {score}" );

        if( !switchCandidate.IsLegitimate )
            score -= 70;

        _ai.CurrentLog.Add( $"Legit Switch: {switchCandidate.IsLegitimate}, Score: {score}" );

        bool switchIsThreatenedByKO = switchCandidate.SwitchDefensePTKOR.PTKO >= PotentialToKO.Dangerous;
        bool switchTakesBigDamage = switchCandidate.SwitchDefensePTKOR.PTKO >= PotentialToKO.TwoHKO;

        if( switchIsThreatenedByKO || switchTakesBigDamage )
            score -= 30;

        _ai.CurrentLog.Add( $"Switch is threatened: {switchIsThreatenedByKO}, Switch takes big damage: {switchTakesBigDamage}, Score: {score}" );

        //--Piece Value Modifier
        if( losingExchange && iDieBeforeActing )
        {
            _ai.CurrentLog.Add( $"Trying to get Piece Value for {_ai.Unit.Pokemon.NickName}." );

            if( _ai.TeamPieceValues.TryGetValue( _ai.ThisUnitAdapter.PID, out var pieceValue )  )
            {
                int preservationBias = Mathf.FloorToInt( pieceValue.OffensiveValue * 0.25f );
                preservationBias = Mathf.FloorToInt( preservationBias * ( 1 - context.MyExpendability ) ); //--

                score += preservationBias;

                _ai.CurrentLog.Add( $"Piece Value Preservation Bias: {preservationBias}, Score: {score}" );
            }

            if( eval.AttackerHPR <= 0.1f && pieceValue.SpeedScore == 0 )
                score -= 10;
        }

        score += _ai.Get_ConsecutiveSwitchPenalty();

        _ai.CurrentLog.Add( $"Consecutive switch penalty: Score: {score}" );

        score += _ai.DefensiveSwitch_TempoModifier( tempo );

        _ai.CurrentLog.Add( $"Tempo Switch Modifier: Score: {score}" );

        if( context.IsForcedTrade && !context.IsTerminal )
            score -= 50;

        _ai.CurrentLog.Add( $"Is Forced Trade: {context.IsForcedTrade}. Score: {score}" );

        //--Switch tax
        score -= 20;

        _ai.CurrentLog.Add( $"===[Final Switch Score after Tax: {score}]===" );
        return score;
    }

    public int OffensiveSwitchScore( TempoStateResult tempo, ExchangeEvaluation eval, SwitchCandidateResult switchCandidate, BoardContext context )
    {
        int score = 0;
        string switchName = "none";

        //--Tank score if unable to switch
        if( switchCandidate.Pokemon == null || _ai.BattleSystem.BattleType == BattleType.WildBattle_1v1 || _ai.Check_IsLastPokemon() )
        {
            _ai.CurrentLog.Add( $"No switch available (null, wild battle, or last pokemon). Tanking Score!" );
            return -999;
        }

        switchName = switchCandidate.Pokemon.NickName;

        _ai.CurrentLog.Add( $"===[Beginning Offensive Switch Scoring for Candidate {switchName}]===" );

        int offensiveDelta = switchCandidate.SwitchOffensePTKOR.Score - switchCandidate.SwitchDefensePTKOR.Score; //--should be offensive ptko score minus defensive ptko score.
        score += Mathf.Clamp( Mathf.FloorToInt( offensiveDelta * 0.5f ), 0, 40 );

        _ai.CurrentLog.Add( $"Offensive PTKOR Score: {switchCandidate.SwitchOffensePTKOR.Score}, Defensive PTKOR Score: {switchCandidate.SwitchDefensePTKOR.Score}, Delta: {offensiveDelta}." );

        BattleAI_PokemonAdapter candidateAdapter = new( switchCandidate.Pokemon, _ai );
        if( switchCandidate.Pokemon != null && _ai.TeamPieceValues.TryGetValue( candidateAdapter.PID, out var pieceValue ) )
        {
            int switchThreatCount = pieceValue.ThreatCount;

            if( switchThreatCount == 2 )
                score += 5;
            else if( switchThreatCount >= 3 )
                score += 10;

            _ai.CurrentLog.Add( $"Threat Count: {switchThreatCount}. Score: {score}" );
        }

        bool switchThreatensKO          = switchCandidate.SwitchOffensePTKOR.PTKO >= PotentialToKO.Dangerous;
        bool switchIsThreatenedByKO     = switchCandidate.SwitchDefensePTKOR.PTKO >= PotentialToKO.Dangerous;
        bool switchDoesBigDamage        = switchCandidate.SwitchOffensePTKOR.PTKO >= PotentialToKO.TwoHKO;
        bool switchTakesBigDamage       = switchCandidate.SwitchDefensePTKOR.PTKO >= PotentialToKO.TwoHKO;
        bool switchMovesFirst           = switchCandidate.MovesFirst;

        if( !switchMovesFirst )
            score -= 5;

        if( ( switchThreatensKO || switchDoesBigDamage ) && switchMovesFirst && !switchIsThreatenedByKO )
            score += 60;
        else if( switchIsThreatenedByKO && !switchThreatensKO )
            score -= 40;

        if( !switchThreatensKO && !switchDoesBigDamage && ( switchTakesBigDamage || switchIsThreatenedByKO ) )
            score -= 40;

        _ai.CurrentLog.Add( $"SwitchThreatensKO {switchThreatensKO}, SwitchMovesFirst {switchMovesFirst}, !switchIsThreatenedByKO {!switchIsThreatenedByKO}. Score: {score}" );

        var defensePTKO = switchCandidate.SwitchDefensePTKOR.PTKO;
        float incomingDamage = _proj.Get_PTKODamagePercent( defensePTKO );

        if( incomingDamage >= 0.75 )
            score -= 80;
        else if( incomingDamage >= 0.5 )
            score -= 40;
        else if( incomingDamage >= 0.25 )
            score -= 20;

        _ai.CurrentLog.Add( $"Switch's DefensePTKO (opponent's potential to ko us): {defensePTKO}. Switch's Likely damage taken: {incomingDamage}. Score: {score}]=" );

        //--Forced Switch Pressure
        if( eval.AttackerForcesSwitch )
        {
            score += 25;
            _ai.CurrentLog.Add( $"Opponent likely forced to switch, offensive pivot opportunity. Score: {score}" );
        }
        else if( eval.ExchangeState == ExchangeState.Pressure )
        {
            score -= 5;
            _ai.CurrentLog.Add( $"We're applying pressure. Should this actually encourage offensive switching? Score: {score}" );
        }

        //--Tempo
        score += _ai.OffensiveSwitch_TempoModifier( tempo );

        //--Switch Tax
        score += _ai.Get_ConsecutiveSwitchPenalty();
        score -= 20;

        _ai.CurrentLog.Add( $"=[Final Offensive Switch Score after tempo modifier, consecutive switch pentalty, and switch tax: {score}" );

        return score;
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public SwitchCandidateResult GetSwitch_Defensive( List<IBattleAIUnit> opponents )
    {
        int bestScore = int.MinValue;
        Pokemon bestSwitch = null;
        float bestHPRatio = 0f;
        ThreatResult biggestThreat = _ai.GetThreat_ImmediateDamage( opponents, _ai.ThisUnitAdapter ); //--The biggest threat will start as the biggest threat to the current pokemon thinking of switching. It will get overwritten if there's a viable switch candidate. this prevents it from being null in the case there are no more viable switch ins.
        MoveThreatResult threatsScariestMove = new();
        PotentialToKOResult bestSwitch_OffensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        PotentialToKOResult bestSwitch_DefensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        TurnOutcomeProjection bestCandidateTOP = new();
        float threatsScariestMoveModifier = 1f;
        bool islegit = true;
        bool isFaster = false;

        var ourParty = _ai.BattleSystem.GetAllyParty( _ai.Unit.Pokemon );
        var ourActiveUnits = _ai.BattleSystem.GetAllyUnits( _ai.Unit );
        var bench = ourParty.Where( p => !ourActiveUnits.Any( u => u.Pokemon == p ) && p.CurrentHP > 0  ).ToList();

        Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Getting Defensive Switch for {_ai.ThisUnitAdapter.Name}." );

        if( bench.Count > 0 )
        {
            foreach( var pokemon in bench )
            {
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Evaluating {pokemon.NickName}. Their current hp is: {pokemon.CurrentHP}" );
                if( pokemon.IsFainted() )
                    continue;

                if( _ai.BattleSystem.IsPokemonSelectedToShift( pokemon ) )
                    continue;

                BattleAI_PokemonAdapter pokemonAdapter = new( pokemon, _ai );

                var threat = _ai.GetThreat_ImmediateDamage( opponents, pokemonAdapter );
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] Chosen threat is: {threat.Unit.Name}" );

                int score = 100;
                int sacrificeWeight = 35;

                float hpRatioRaw = _ai.Get_HPRatio( pokemon );
                float hpRatioAfterHazards = _ai.Get_HPRatio_AfterEntryHazards( pokemon );
                int bulk = pokemon.MaxHP + pokemon.Defense + pokemon.SpDefense;
                float expendability = _proj.GetExpendability( pokemonAdapter, hpRatioAfterHazards );

                if( hpRatioAfterHazards <= 0f && !_ai.Check_IsLastPokemon() )
                {
                    var remaining = _ai.GetRemainingAllyPokemon( pokemon );
                    if( remaining.Count > 1 )
                        continue;
                }

                //--Speed check. Not as important in a defensive switch-in.
                bool movesFirst = threat.Unit != null && pokemonAdapter.Speed > threat.Unit.Speed;
                if( movesFirst )
                    score += 5;
                else
                    score -= 10;

                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] Speed Comparison checked. Score: {score}" );

                //--Aggregate bulk based on hp, def, and spdef base stats
                float effectiveBulk = bulk * hpRatioAfterHazards;
                if( effectiveBulk >= 400 )              score += 10;
                else if( effectiveBulk >= 300 )         score += 5;
                else if( effectiveBulk >= 200 )         score += 0;
                else if( effectiveBulk >= 150 )         score -= 5;
                else if( effectiveBulk <= 100 )         score -= 10;
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] Overall Bulk checked. Score: {score}" );

                //--Offensive PTKO Result. This is the candidate's potential to KO the current opponent.
                var targetsHPRatio = _ai.Get_HPRatio( threat.Unit );
                var ourMove = _ai.MoveCommand.Get_BestSimulatedAttack( pokemonAdapter, threat.Unit, "Get Switch Defensive (our move)" );
                var ourMoveModifier = ourMove.Modifier;
                var ourWSR = _proj.Get_WallingScoreResult( pokemonAdapter, threat.Unit, ourMove );

                PotentialToKOResult offensePTKOR = _proj.Get_PotentialToKOResult( ourWSR, ourMove, targetsHPRatio );

                //--Defensive PTKO Result. This is the opponent's potential to KO this candidate.
                var threatsMove = _ai.MoveCommand.Get_BestSimulatedAttack( threat.Unit, pokemonAdapter, "Get Switch Defensive (threat's move)" );
                var threatsMoveModifier = threatsMove.Modifier;
                var threatsWSR = _proj.Get_WallingScoreResult( threat.Unit, pokemonAdapter, threatsMove );

                PotentialToKOResult defensePTKOR = _proj.Get_PotentialToKOResult( threatsWSR, threatsMove, hpRatioAfterHazards );

                score += threatsWSR.Score;

                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] PTKOs Checked. Offensive PTKO: {offensePTKOR.PTKO}. Defensive PTKO: {defensePTKOR.PTKO}. Walling Scores: Our WS {ourWSR.Score}. Threat WS {threatsWSR.Score}. Score: {score}" );

                if( defensePTKOR.PTKO <= PotentialToKO.TwoHKO )
                    score += 15;
                else
                    score += defensePTKOR.Score;

                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] Potential to be KO'd: {defensePTKOR.PTKO}, {defensePTKOR.Score}. Checked vs HP Ratio. Score: {score}" );

                //--Modifier influence. Higher modifiers likely mean super effective damage and switching a mon into a super effective hit is lunacy.
                if( threatsMoveModifier >= 4f )             score -= 65; //--4x damage is almost always certain death. Ideally never pick this candidate.
                else if( threatsMoveModifier >= 2f )        score -= 35; //--Discourage super effective damage.
                else if( threatsMoveModifier >= 1f )        score += 0;
                else if( threatsMoveModifier >= 0.75f )     score += 15; //--Reward Resistances
                else if( threatsMoveModifier >= 0.5f )      score += 25; //--Reward Resistances
                else if( threatsMoveModifier >= 0.25f )     score += 35; //--Reward Resistances

                //--Consider candidate's expendability.
                int expendabilityScore = Mathf.FloorToInt( expendability * sacrificeWeight );
                score -= expendabilityScore;
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] (expendability * sacWeight). Expendability: {expendability}, Expendability Score: {expendabilityScore}" );

                //--Immediate danger override
                bool isInDanger = hpRatioAfterHazards <= 0.4f && defensePTKOR.PTKO > PotentialToKO.TwoHKO || hpRatioAfterHazards <= 0.25f;

                if( isInDanger )
                {
                    Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] In Danger! Reducing Score. Score: {score}" );
                    score -= 50;
                }

                //--Flat penalty for undoing a pivot for probably no reason
                if( _ai.LastSentInPokemon != null && pokemon == _ai.LastSentInPokemon )
                    bestScore -= 15;

                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] Current threat: {threat.Unit.Name}, Score: {score}" );

                if( score > bestScore )
                {
                    bestScore = score;
                    bestSwitch = pokemon;
                    bestHPRatio = hpRatioAfterHazards;
                    bestSwitch_OffensePTKOR = offensePTKOR;
                    bestSwitch_DefensePTKOR = defensePTKOR;
                    biggestThreat = threat;
                    threatsScariestMove = threatsMove;
                    threatsScariestMoveModifier = threatsMoveModifier;
                    isFaster = movesFirst;
                }

                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] Current Biggest Threat is: {biggestThreat.Unit.Name}" );
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] Current Switch Candidate: {pokemon.NickName}, Score: {score}" );
            }

            Debug.Log( $"[AI Battle][Defensive Switch Candidate] Current unit: {_ai.Unit} is {_ai.Unit.Pokemon.NickName}" );
            Debug.Log( $"[AI Battle][Defensive Switch Candidate] Biggest Threat to switch in unit: {biggestThreat.Unit} is {biggestThreat.Unit.Name}" );

            if( bestSwitch == null )
            {
                Debug.LogError( $"[AI Scoring][Defensive Switch Candidate] No Switch available!" );
            }
            else
            {
                BattleAI_PokemonAdapter switchAdapter = new( bestSwitch, _ai );

                float currentMon_HPRatio                = _ai.Get_HPRatio( _ai.Unit.Pokemon );
                var currentMon_ImmediateDamageThreat    = _ai.GetThreat_ImmediateDamage( opponents, _ai.ThisUnitAdapter ); //--We re-get the biggest threat vs the current pokemon in the event that it was overwritten by the candidate's biggest threat in a double battle.
                var currentMon_MoveThreat               = _ai.MoveCommand.Get_BestSimulatedAttack( currentMon_ImmediateDamageThreat.Unit, _ai.ThisUnitAdapter, "Get Switch Defensive (current threat vs current mon)" );
                var currentMon_WSR                      = _proj.Get_WallingScoreResult( currentMon_ImmediateDamageThreat.Unit, _ai.ThisUnitAdapter, currentMon_MoveThreat ); //--biggest threat is turning up null toward end of battle

                PotentialToKOResult currentMon_PotentialToKO = _proj.Get_PotentialToKOResult( currentMon_WSR, threatsScariestMove, currentMon_HPRatio );

                var fieldSim            = _ai.UnitSim.BuildSimField();
                
                var ourBestMove         = _ai.MoveCommand.Get_BestSimulatedAttack( switchAdapter, biggestThreat.Unit, "Get Switch Defensive (current mon vs current threat)" );
                var bestSwitchSim       = _ai.UnitSim.BuildSimUnit( bestSwitch, bestHPRatio, ourBestMove, fieldSim );
                float threatHPR         = _ai.Get_HPRatio( biggestThreat.Unit );

                var oppMove             = _ai.MoveCommand.Get_BestSimulatedAttack( biggestThreat.Unit, switchAdapter, "Get Switch Defensive (current threat vs switch candidate)" );
                var oppSim              = _ai.UnitSim.BuildSimUnit( biggestThreat.Unit, threatHPR, oppMove, fieldSim );

                var battleSimCtx        = _battleSim.Get_BattleSimContext( bestSwitch_OffensePTKOR.PTKO, bestSwitch_DefensePTKOR.PTKO, bestSwitchSim, oppSim, fieldSim );
                bestCandidateTOP        = _battleSim.SimulateSwitchRound( battleSimCtx, true, false );

                //--Gate switch by KO Class improvement. If the KO Class doesn't improve significantly, don't switch.
                bool improvesKOClass = bestSwitch_DefensePTKOR.PTKO < currentMon_PotentialToKO.PTKO;

                //--Turn Outcome Projection fainting checks.
                bool diesBeforeActing = bestCandidateTOP.Attacker_DiesBeforeActing;
                bool dieAfterTrade = bestCandidateTOP.Attacker_EndOfTurnHP <= 0f;
                bool isStillDying = diesBeforeActing || dieAfterTrade;

                Debug.Log( $"[AI Scoring][Defensive Switch Candidate]({bestSwitch.NickName}) KO Class Improved: {improvesKOClass}, The Switch will still die: {isStillDying}, IsLegit Switch: {islegit}" );
                if ( isStillDying && !improvesKOClass )
                {
                    islegit = false;
                    Debug.Log( $"[AI Scoring][Defensive Switch Candidate]({bestSwitch.NickName}) KO Class Legitimacy Gate IsLegit: {islegit}" );
                }

                //--HP/Sacrifice Gate
                bool currentPokemonIsLowHP = currentMon_HPRatio < 0.25f;
                bool switchCanSurviveHit = bestCandidateTOP.Attacker_EndOfTurnHP > 0f;

                Debug.Log( $"[AI Scoring][Defensive Switch Candidate]({bestSwitch.NickName}) Current Pokemon has low HP: {currentPokemonIsLowHP}, The Switch will still die: {switchCanSurviveHit}, IsLegit Switch: {islegit}" );

                if ( currentPokemonIsLowHP && !switchCanSurviveHit )
                {
                    islegit = false;
                    Debug.Log( $"[AI Scoring][Defensive Switch Candidate]({bestSwitch.NickName}) Current HP/Sacrifice Legitimacy Gate IsLegit: {islegit}" );
                }
            }
        }

        return new()
        {
            Score = bestScore,
            Pokemon = bestSwitch,
            HPRatio = bestHPRatio,
            SwitchOffensePTKOR = bestSwitch_OffensePTKOR,
            SwitchDefensePTKOR = bestSwitch_DefensePTKOR,
            IsLegitimate = islegit,
            MovesFirst = isFaster,
            Top = bestCandidateTOP,
        };
    }

/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////
/////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////////

    public SwitchCandidateResult GetSwitch_Offensive(  List<IBattleAIUnit> opponents  )
    {
        int bestScore = int.MinValue;
        Pokemon bestSwitch = null;
        float bestHPRatio = 0f;
        ThreatResult biggestThreat = _ai.GetThreat_ImmediateDamage( opponents, _ai.ThisUnitAdapter ); //--The biggest threat will start as the biggest threat to the current pokemon thinking of switching. It will get overwritten if there's a viable switch candidate. this prevents it from being null in the case there are no more viable switch ins.
        MoveThreatResult mostThreateningMove = new();
        TurnOutcomeProjection bestTop = new();
        PotentialToKOResult bestSwitch_OffensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        PotentialToKOResult bestSwitch_DefensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        bool isFaster = false;

        var ourParty = _ai.BattleSystem.GetAllyParty( _ai.Unit.Pokemon );
        var ourActiveUnits = _ai.BattleSystem.GetAllyUnits( _ai.Unit );
        var bench = ourParty.Where( p => !ourActiveUnits.Any( u => u.Pokemon == p ) && p.CurrentHP > 0  ).ToList();

        foreach( var pokemon in bench )
        {
            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] Evaluating {pokemon.NickName}. Their current hp is: {pokemon.CurrentHP}" );
            if( pokemon.IsFainted() )
                continue;

            if( _ai.BattleSystem.IsPokemonSelectedToShift( pokemon ) )
                continue;

            BattleAI_PokemonAdapter pokemonAdapter = new( pokemon, _ai );

            var threat = _ai.GetThreat_ImmediateDamage( opponents, pokemonAdapter );
            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] Chosen threat is: {threat.Unit.Pokemon.NickName}" );

            int score = 100;
            int sacrificeWeight = 30;

            float hpRatioAfterHazards = _ai.Get_HPRatio_AfterEntryHazards( pokemon );

            float expendability = _proj.GetExpendability( pokemonAdapter, hpRatioAfterHazards );
            int expendabilityScore = Mathf.FloorToInt( expendability * sacrificeWeight );

            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] HPR: {hpRatioAfterHazards}. Expendability & its Score: {expendability}, {expendabilityScore}." );

            if( hpRatioAfterHazards <= 0f && !_ai.Check_IsLastPokemon() )
            {
                var remaining = _ai.GetRemainingAllyPokemon( pokemon );
                if( remaining.Count > 1 )
                    continue;
            }

            //--Speed check.
            bool movesFirst = threat.Unit != null && pokemonAdapter.Speed > threat.Unit.Speed;

            //--Get PTKOs
            //--Offensive PTKO Result. This is the candidate's potential to KO the current opponent.
            var threatHPR                       = _ai.Get_HPRatio( threat.Unit );
            var candidateMove                   = _ai.MoveCommand.Get_BestSimulatedAttack( pokemonAdapter, threat.Unit, "Get Switch Offensive (candidate move vs current threat)" );
            var candidateMoveModifier           = candidateMove.Modifier;
            var candidateWSR                    = _proj.Get_WallingScoreResult( pokemonAdapter, threat.Unit, candidateMove );
            PotentialToKOResult offensePTKOR    = _proj.Get_PotentialToKOResult( candidateWSR, candidateMove, threatHPR );

            //--Defensive PTKO Result. This is the opponent's potential to KO this candidate.
            var threatsMove                     = _ai.MoveCommand.Get_BestSimulatedAttack( threat.Unit, pokemonAdapter, "Get Switch Offensive (current threat vs candidate)" );
            var threatsMoveModifier             = threatsMove.Modifier;
            var threatsWSR                      = _proj.Get_WallingScoreResult( threat.Unit, pokemonAdapter, threatsMove );
            PotentialToKOResult defensePTKOR    = _proj.Get_PotentialToKOResult( threatsWSR, threatsMove, hpRatioAfterHazards );

            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] PTKOs Obtained. {pokemon.NickName} PTKO: {offensePTKOR.PTKO}. {threat.Unit.Pokemon.NickName} PTKO: {defensePTKOR.PTKO}" );

            //--Build Simulation Units & Field
            var fieldSim            = _ai.UnitSim.BuildSimField();
            var threatSim           = _ai.UnitSim.BuildSimUnit( threat.Unit, threatHPR, threatsMove, fieldSim );

            var candidateSim        = _ai.UnitSim.BuildSimUnit( pokemonAdapter, hpRatioAfterHazards, candidateMove, fieldSim );
            var battleSimCtx        = _battleSim.Get_BattleSimContext( offensePTKOR.PTKO, defensePTKOR.PTKO, candidateSim, threatSim, fieldSim );
            var top                 = _battleSim.SimulateSwitchRound( battleSimCtx, true, false );

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
            bool opponentSwitches = _ai.UnitSim.PredictForcedSwitch( offensePTKOR.PTKO, defensePTKOR.PTKO, movesFirst );

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
                > PotentialToKO.TwoHKO      => +10, //--Greater than twohko, meaning safe, sturdy, and hard wall
                _ => 0,
            };

            if( opponentSwitches )
            {
                //--Reduce influence of current PTKO - it doesn't apply if the opponent switches.
                offenseScore = Mathf.FloorToInt( offenseScore * 0.5f );

                //--Use offensive value to influence more generally offensive candidate.
                var pieceValue = _ai.TeamPieceValues[pokemonAdapter.PID];
                score += pieceValue.OffensiveValue / 2; //-- /2 just to reduce severity. we don't want the most offensively valued pokemon to be overvalued in this context.
            }

            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] Defensive PTKO ({defensePTKOR.PTKO}) from opponent {threat.Unit.Pokemon.NickName}. Score: {score}" );

            if( movesFirst )
                score += 20;
            else
                score -= 10;

            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] Moves first: {movesFirst}. Score: {score}" );

            int entryHazardsOnOpposingSide = 0;
            if( threatSim.Court.Conditions.ContainsKey( CourtConditionID.StealthRock ) )
                entryHazardsOnOpposingSide++;
            
            if( threatSim.Court.Conditions.ContainsKey( CourtConditionID.Spikes ) )
                entryHazardsOnOpposingSide++;

            if( threatSim.Court.Conditions.ContainsKey( CourtConditionID.ToxicSpikes ) )
                entryHazardsOnOpposingSide++;

            if( threatSim.CourtSeeded )
                entryHazardsOnOpposingSide++;

            //--Pressure might be enough to force opposing side to switch out. Reward, and if we've set up hazards, reward for forcing them to switch into them.
            if( opponentSwitches )
            {
                score += 35;
                score += entryHazardsOnOpposingSide == 0 ? 0 : 2 * entryHazardsOnOpposingSide;
                // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] We threaten to force a switch! Entry Hazard on opposing side count: {entryHazardsOnOpposingSide}. Score: {score}" );
            }

            score -= expendabilityScore;

            // Debug.Log( $"[AI Scoring][Offensive Switch Candidate][{pokemon.NickName}] Checked Expendability Score: {expendabilityScore}. Final Score: {score}" );

            if( score > bestScore )
            {
                bestScore = score;
                bestSwitch = pokemon;
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
            Debug.LogError( $"[AI Scoring][Offensive Switch Candidate] No Switch available!" );
        }

        return new()
        {
            Score = bestScore,
            Pokemon = bestSwitch,
            HPRatio = bestHPRatio,
            SwitchOffensePTKOR = bestSwitch_OffensePTKOR,
            SwitchDefensePTKOR = bestSwitch_DefensePTKOR,
            MovesFirst = isFaster,
            Top = bestTop,
        };
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
        ThreatResult biggestThreat = _ai.GetThreat_ImmediateDamage( opponents, _ai.ThisUnitAdapter ); //--The biggest threat will start as the biggest threat to the current pokemon thinking of switching. It will get overwritten if there's a viable switch candidate. this prevents it from being null in the case there are no more viable switch ins.
        MoveThreatResult mostThreateningMove = new();
        TurnOutcomeProjection bestTop = new();
        PotentialToKOResult bestSwitch_OffensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        PotentialToKOResult bestSwitch_DefensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        bool islegit = true;
        bool isFaster = false;

        var ourParty = _ai.BattleSystem.GetAllyParty( _ai.Unit.Pokemon );
        var ourActiveUnits = _ai.BattleSystem.GetAllyUnits( _ai.Unit );
        var bench = ourParty.Where( p => !ourActiveUnits.Any( u => u.Pokemon == p ) && p.CurrentHP > 0  ).ToList();

        CustomLogSession log = new();
        log.Add( $"===[Beginning Revenge Switch Candidate Selection]===" );

        foreach( var pokemon in bench )
        {
            log.Add( $"===[AI Scoring][Revenge Switch Candidate] Evaluating {pokemon.NickName}. Their current hp is: {pokemon.CurrentHP}===" );
            if( pokemon.IsFainted() )
                continue;

            if( _ai.BattleSystem.IsPokemonSelectedToShift( pokemon ) )
                continue;

            BattleAI_PokemonAdapter pokemonAdapter = new( pokemon, _ai );

            var threat = _ai.GetThreat_ImmediateDamage( opponents, pokemonAdapter );
            log.Add( $"[AI Scoring][Revenge Switch Candidate] Chosen threat is: {threat.Unit.Name}" );

            int score = 100;
            int sacrificeWeight = 35;

            float hpRatioAfterHazards = _ai.Get_HPRatio_AfterEntryHazards( pokemon );

            float expendability = _proj.GetExpendability( pokemonAdapter, hpRatioAfterHazards );
            int expendabilityScore = Mathf.FloorToInt( expendability * sacrificeWeight );

            log.Add( $"[AI Scoring][Revenge Switch Candidate] {pokemon.NickName}'s HPR: {hpRatioAfterHazards}. Expendability & its Score: {expendability}, {expendabilityScore}." );

            if( hpRatioAfterHazards <= 0f && !_ai.Check_IsLastPokemon() )
            {
                var remaining = _ai.GetRemainingAllyPokemon( pokemon );
                if( remaining.Count > 1 )
                    continue;
            }

            //--Speed check.
            bool movesFirst = threat.Unit != null && pokemonAdapter.Speed >= threat.Unit.Speed;

            //--Get PTKOs
            //--Offensive PTKO Result. This is the candidate's potential to KO the current opponent.
            var threatHPR                       = _ai.Get_HPRatio( threat.Unit );
            var candidateMove                   = _ai.MoveCommand.Get_BestSimulatedAttack( pokemonAdapter, threat.Unit, "Get Switch Revenge (candidate vs current threat)" );
            var candidateMoveModifier           = candidateMove.Modifier;
            var candidateWSR                    = _proj.Get_WallingScoreResult( pokemonAdapter, threat.Unit, candidateMove );
            PotentialToKOResult offensePTKOR    = _proj.Get_PotentialToKOResult( candidateWSR, candidateMove, threatHPR );

            //--Defensive PTKO Result. This is the opponent's potential to KO this candidate.
            var threatsMove                     = _ai.MoveCommand.Get_BestSimulatedAttack( threat.Unit, pokemonAdapter, "Get Switch Revenge (current threat vs candidate)" );
            var threatsMoveModifier             = threatsMove.Modifier;
            var threatsWSR                      = _proj.Get_WallingScoreResult( threat.Unit, pokemonAdapter, threatsMove );
            PotentialToKOResult defensePTKOR    = _proj.Get_PotentialToKOResult( threatsWSR, threatsMove, hpRatioAfterHazards );

            log.Add( $"[AI Scoring][Revenge Switch Candidate] PTKOs Obtained. {pokemon.NickName} PTKO: {offensePTKOR.PTKO}. {threat.Unit.Name} PTKO: {defensePTKOR.PTKO}" );

            //--Build Simulation Units & Field
            var fieldSim            = _ai.UnitSim.BuildSimField();

            var candidateSim        = _ai.UnitSim.BuildSimUnit( pokemonAdapter, hpRatioAfterHazards, candidateMove, fieldSim );
            var threatSim           = _ai.UnitSim.BuildSimUnit( threat.Unit, threatHPR, threatsMove, fieldSim );

            var battleSimCtx        = _battleSim.Get_BattleSimContext( offensePTKOR.PTKO, defensePTKOR.PTKO, candidateSim, threatSim, fieldSim );
            var top                 = _battleSim.SimulateAttackRound( battleSimCtx );

            //--Begin Scoring
            log.Add( $"[AI Scoring][Revenge Switch Candidate] Beginning Scoring. Base Score: {score}" );

            //--Speed Check. Important.
            if( movesFirst )
                score += 25;
            else
                score -= 10;

            log.Add( $"[AI Scoring][Revenge Switch Candidate] Moves First: {movesFirst}. Score: {score}" );

            //--Damage & KO Scoring
            if( top.Opponent_DiesBeforeActing )
            {
                score += 120;
                log.Add( $"[AI Scoring][Revenge Switch Candidate] Opponent Dies before acting. Score: {score}" );
            }
            else if( top.Opponent_EndOfTurnHP <= 0f && top.Attacker_EndOfTurnHP > 0f )
            {
                score += 90;
                log.Add( $"[AI Scoring][Revenge Switch Candidate] Opponent Dies and we live. Score: {score}" );
            }
            else if( top.Opponent_EndOfTurnHP <= 0f && top.Attacker_EndOfTurnHP <= 0f )
            {
                score += 30;
                // score -= expendabilityScore;
                log.Add( $"[AI Scoring][Revenge Switch Candidate] We both faint. Score: {score}" );
            }
            else if( top.Attacker_EndOfTurnHP <= 0 && top.Opponent_EndOfTurnHP > 0 )
            {
                score -= 90;
                log.Add( $"[AI Scoring][Revenge Switch Candidate] We faint and our opponent does not. Score: {score}" );
            }

            if( top.Attacker_DiesBeforeActing )
            {
                score -= 150;
                log.Add( $"[AI Scoring][Revenge Switch Candidate] We Die before acting. Score: {score}" );
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

                log.Add( $"[AI Scoring][Revenge Switch Candidate] {pokemon.NickName} damage done: {damageDealt}. {threat.Unit.Name} damage done: {damageTaken}. Neither faint. Score: {score}" );
            }

            score += Mathf.FloorToInt( defensePTKOR.Score * -0.5f );
            score += Mathf.FloorToInt( offensePTKOR.Score * 0.5f );

            score -= expendabilityScore;

            log.Add( $"[AI Scoring][Revenge Switch Candidate] {pokemon.NickName}'s Final Score: {score}" );
            log.Add( $"================================================================================" );
            log.Add( $"" );

            if( score > bestScore )
            {
                bestScore = score;
                bestSwitch = pokemon;
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
            islegit = false;

        if( bestScore < 0 )
            islegit = false;

        if( bestSwitch == null )
        {
            Debug.LogError( $"[AI Scoring][Revenge Switch Candidate] No Switch available!" );
        }
        else
        {
            log.Add( $"[AI Scoring][Revenge Switch Candidate] Chose {bestSwitch.NickName}! Final Score: {bestScore}" );
        }

        Debug.Log( log.ToString() );
        log.Clear();

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

        var ourParty = _ai.BattleSystem.GetAllyParty( _ai.Unit.Pokemon );
        var bench = ourParty.Where( p =>  p.CurrentHP > 0  ).ToList();

        foreach( var pokemon in bench )
        {
            BattleAI_PokemonAdapter adapter = new( pokemon, _ai );
            int score = 0;

            //--Piece value
            var pieceValue = _ai.TeamPieceValues[adapter.PID];
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
