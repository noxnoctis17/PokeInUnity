using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SwitchType { Offensive, Defensive, Pivot, }

public class BattleAI_SwitchCommand
{
    private readonly BattleAI _ai;
    private readonly BattleAI_Projection _proj;

    public BattleAI_SwitchCommand( BattleAI ai )
    {
        _ai = ai;
        _proj = _ai.Projection;
    }

    public void SubmitSwitchCommand( Pokemon incomingPokemon )
    {
        _ai.BattleSystem.SetSwitchPokemonCommand( incomingPokemon, _ai.Unit, true );
    }

    public int SwitchScore( TempoStateResult tempo, ExchangeEvaluation eval, SwitchCandidateResult switchCandidate, BoardContext context )
    {
        //--Tank score if unable to switch
        if( _ai.BattleSystem.BattleType == BattleType.WildBattle_1v1 || _ai.Check_IsLastPokemon() )
        {
            _ai.CurrentLog.Add( $"No switch available (wild battle or last pokemon). Tanking Score!" );
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

            if( _ai.TeamPieceValues.TryGetValue( _ai.Unit.Pokemon, out var pieceValue )  )
            {
                int preservationBias = Mathf.FloorToInt( pieceValue.OffensiveValue * 0.25f );
                preservationBias = Mathf.FloorToInt( preservationBias * ( 1 - context.MyExpendability ) ); //--

                score += preservationBias;

                _ai.CurrentLog.Add( $"Piece Value Preservation Bias: {preservationBias}, Score: {score}" );
            }

            if( eval.AttackerHPR <= 0.1f && pieceValue.SpeedScore == 0 )
                score -= 10;
        }

        //--Offensive consideration
        if( losingExchange || context.IsBehind || context.IsForcedTrade )
            score += GetOffensiveSwitchScore( switchCandidate );

        score += _ai.Get_ConsecutiveSwitchPenalty();

        _ai.CurrentLog.Add( $"Consecutive switch penalty: Score: {score}" );

        score += _ai.TempoSwitchModifier( tempo );

        _ai.CurrentLog.Add( $"Tempo Switch Modifier: Score: {score}" );

        if( context.IsForcedTrade && !context.IsTerminal )
            score -= 50;

        _ai.CurrentLog.Add( $"Is Forced Trade: {context.IsForcedTrade}. Score: {score}" );

        //--Switch tax
        score -= 20;

        _ai.CurrentLog.Add( $"===[Final Switch Score after Tax: {score}]===" );
        return score;
    }

    private int GetOffensiveSwitchScore( SwitchCandidateResult switchCandidate )
    {
        int score = 0;
        int offensiveThreshold = 10;
        string switchName = "none";

        if( switchCandidate.Pokemon != null )
            switchName = switchCandidate.Pokemon.NickName;

        _ai.CurrentLog.Add( $"===[Beginning Offensive Switch Scoring for Candidate {switchName}]===" );

        int offensiveDelta = switchCandidate.SwitchOffensePTKOR.Score - switchCandidate.SwitchDefensePTKOR.Score; //--should be offensive ptko score minus defensive ptko score.
        int offensiveBonus = Mathf.Clamp( Mathf.FloorToInt( offensiveDelta * 0.35f ), 0, 25 );

        if( offensiveDelta >= offensiveThreshold )
            score += offensiveBonus;

        _ai.CurrentLog.Add( $"Offensive PTKOR Score: {switchCandidate.SwitchOffensePTKOR.Score}, Defensive PTKOR Score: {switchCandidate.SwitchDefensePTKOR.Score}, Delta: {offensiveDelta}, Bonus: {offensiveBonus}" );

        if( switchCandidate.Pokemon != null && _ai.TeamPieceValues.TryGetValue( switchCandidate.Pokemon, out var pieceValue ) )
        {
            int switchThreatCount = pieceValue.ThreatCount;

            if( switchThreatCount == 2 )
                score += 10;
            else if( switchThreatCount >= 3 )
                score += 20;

            _ai.CurrentLog.Add( $"Threat Count: {switchThreatCount}. Score: {score}" );
        }

        bool switchThreatensKO = switchCandidate.SwitchOffensePTKOR.PTKO >= PotentialToKO.Dangerous;
        bool switchIsThreatenedByKO = switchCandidate.SwitchDefensePTKOR.PTKO >= PotentialToKO.Dangerous;
        bool switchMovesFirst = switchCandidate.MovesFirst;

        if( switchThreatensKO && switchMovesFirst && !switchIsThreatenedByKO )
            score +=  20;
        
        _ai.CurrentLog.Add( $"=[SwitchThreatensKO {switchThreatensKO}, SwitchMovesFirst {switchMovesFirst}, !switchIsThreatenedByKO {!switchIsThreatenedByKO}. Final Offensive Switch Score: {score}]=" );

        return score;
    }

    public SwitchCandidateResult GetSwitch_Defensive( List<BattleUnit> opponents )
    {
        int bestScore = int.MinValue;
        Pokemon bestSwitch = null;
        float bestHPRatio = 0f;
        ThreatResult biggestThreat = _ai.GetThreat_ImmediateDamage( opponents, _ai.Unit.Pokemon ); //--The biggest threat will start as the biggest threat to the current pokemon thinking of switching. It will get overwritten if there's a viable switch candidate. this prevents it from being null in the case there are no more viable switch ins.
        MoveThreatResult threatsScariestMove = new();
        PotentialToKOResult bestSwitch_OffensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        PotentialToKOResult bestSwitch_DefensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        float threatsScariestMoveModifier = 1f;
        bool islegit = true;
        bool isFaster = false;

        var ourParty = _ai.BattleSystem.GetAllyParty( _ai.Unit.Pokemon );
        var ourActiveUnits = _ai.BattleSystem.GetAllyUnits( _ai.Unit );
        var bench = ourParty.Where( p => !ourActiveUnits.Any( u => u.Pokemon == p ) && p.CurrentHP > 0  ).ToList();

        if( bench.Count > 0 )
        {
            foreach( var pokemon in bench )
            {
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Evaluating {pokemon.NickName}. Their current hp is: {pokemon.CurrentHP}" );
                if( pokemon.IsFainted() )
                    continue;

                if( _ai.BattleSystem.IsPokemonSelectedToShift( pokemon ) )
                    continue;

                var threat = _ai.GetThreat_ImmediateDamage( opponents, pokemon );
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] Chosen threat is: {threat.Unit.Pokemon.NickName}" );

                int score = 100;
                int sacrificeWeight = 35;

                float hpRatioRaw = _ai.Get_HPRatio( pokemon );
                float hpRatioAfterHazards = _ai.Get_HPRatio_AfterEntryHazards( pokemon );
                int bulk = pokemon.MaxHP + pokemon.Defense + pokemon.SpDefense;
                float expendability = _ai.GetExpendability( pokemon, hpRatioAfterHazards );

                if( hpRatioAfterHazards <= 0f && !_ai.Check_IsLastPokemon() )
                {
                    var remaining = _ai.GetRemainingAllyPokemon( pokemon );
                    if( remaining.Count > 1 )
                        continue;
                }

                //--Speed check. Not as important in a defensive switch-in.
                bool movesFirst = threat.Unit != null && pokemon.PokeSO.Speed > threat.Unit.Pokemon.PokeSO.Speed;
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
                var targetsHPRatio = _ai.Get_HPRatio( threat.Unit.Pokemon );
                var ourMove = _ai.Get_MostThreateningMove( pokemon, threat.Unit.Pokemon );
                var ourMoveModifier = ourMove.Modifier;
                var ourWSR = _ai.Get_WallingScoreResult( pokemon, threat.Unit.Pokemon, ourMove );

                PotentialToKOResult offensePTKOR = _ai.Get_PotentialToKOResult( ourWSR, ourMoveModifier, targetsHPRatio );

                //--Defensive PTKO Result. This is the opponent's potential to KO this candidate.
                var threatsMove = _ai.Get_MostThreateningMove( threat.Unit.Pokemon, pokemon );
                var threatsMoveModifier = threatsMove.Modifier;
                var threatsWSR = _ai.Get_WallingScoreResult( threat.Unit.Pokemon, pokemon, threatsMove );

                PotentialToKOResult defensePTKOR = _ai.Get_PotentialToKOResult( threatsWSR, threatsMoveModifier, hpRatioAfterHazards );

                score += threatsWSR.Score;

                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] PTKOs Checked. Offensive PTKO: {offensePTKOR.PTKO}. Defensive PTKO: {defensePTKOR.PTKO}. Walling Scores: Our WS {ourWSR.Score}. Threat WS {threatsWSR.Score}. Score: {score}" );

                if( defensePTKOR.PTKO <= PotentialToKO.TwoHKO )
                    score += 15;
                else
                    score += defensePTKOR.Score;

                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] Potential to be KO'd: {defensePTKOR.PTKO}, {defensePTKOR.Score}. Checked vs HP Ratio. Score: {score}" );

                //--Modifier influence. Higher modifiers likely mean super effective damage and switching a mon into a super effective hit is lunacy.
                if( threatsMoveModifier >= 4f )             score -= 60; //--4x damage is almost always certain death. Ideally never pick this candidate.
                else if( threatsMoveModifier >= 2f )        score -= 30; //--Discourage super effective damage.
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

                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] Current threat: {threat.Unit.Pokemon.NickName}, Score: {score}" );

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

                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] Current Biggest Threat is: {biggestThreat.Unit.Pokemon.NickName}" );
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate][{pokemon.NickName}] Current Switch Candidate: {pokemon.NickName}, Score: {score}" );
            }

            Debug.Log( $"[AI Battle][Defensive Switch Candidate] Current unit: {_ai.Unit} is {_ai.Unit.Pokemon.NickName}" );
            Debug.Log( $"[AI Battle][Defensive Switch Candidate] Biggest Threat to switch in unit: {biggestThreat.Unit} is {biggestThreat.Unit.Pokemon.NickName}" );

            float currentMon_HPRatio                = _ai.Get_HPRatio( _ai.Unit.Pokemon );
            var currentMon_ImmediateDamageThreat    = _ai.GetThreat_ImmediateDamage( opponents, _ai.Unit.Pokemon ); //--We re-get the biggest threat vs the current pokemon in the event that it was overwritten by the candidate's biggest threat in a double battle.
            var currentMon_MoveThreat               = _ai.Get_MostThreateningMove( currentMon_ImmediateDamageThreat.Unit.Pokemon, _ai.Unit.Pokemon );
            var currentMon_WSR                      = _ai.Get_WallingScoreResult( currentMon_ImmediateDamageThreat.Unit.Pokemon, _ai.Unit.Pokemon, currentMon_MoveThreat ); //--biggest threat is turning up null toward end of battle

            PotentialToKOResult currentMon_PotentialToKO = _ai.Get_PotentialToKOResult( currentMon_WSR, threatsScariestMoveModifier, currentMon_HPRatio );

            var fieldSim            = _ai.UnitSim.BuildSimField();
            
            var ourBestMove         = _ai.Get_MostThreateningMove( bestSwitch, biggestThreat.Unit.Pokemon );
            var bestSwitchSim       = _ai.UnitSim.BuildSimUnit( bestSwitch, bestHPRatio, ourBestMove, fieldSim );
            float threatHPR         = _ai.Get_HPRatio( biggestThreat.Unit.Pokemon );

            var oppMove             = _ai.Get_MostThreateningMove( biggestThreat.Unit.Pokemon, bestSwitch );
            var oppSim              = _ai.UnitSim.BuildSimUnit( biggestThreat.Unit.Pokemon, threatHPR, oppMove, fieldSim );

            var battleSimCtx        = _proj.Get_BattleSimContext( bestSwitch_OffensePTKOR.PTKO, bestSwitch_DefensePTKOR.PTKO, bestSwitchSim, oppSim, fieldSim, isFaster );
            var top                 = _proj.SimulateRound( battleSimCtx );

            //--Gate switch by KO Class improvement. If the KO Class doesn't improve significantly, don't switch.
            bool improvesKOClass = bestSwitch_DefensePTKOR.PTKO < currentMon_PotentialToKO.PTKO;

            //--Turn Outcome Projection fainting checks.
            bool diesBeforeActing = top.Attacker_DiesBeforeActing;
            bool dieAfterTrade = top.Attacker_EndOfTurnHP <= 0f;
            bool isStillDying = diesBeforeActing || dieAfterTrade;

            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] KO Class Improved: {improvesKOClass}, The Switch will still die: {isStillDying}, IsLegit Switch: {islegit}" );
            if ( isStillDying && !improvesKOClass )
            {
                islegit = false;
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate] KO Class Legitimacy Gate IsLegit: {islegit}" );
            }

            //--HP/Sacrifice Gate
            bool currentPokemonIsLowHP = currentMon_HPRatio < 0.25f;
            bool switchCanSurviveHit = top.Attacker_EndOfTurnHP > 0f;

            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Current Pokemon has low HP: {currentPokemonIsLowHP}, The Switch will still die: {switchCanSurviveHit}, IsLegit Switch: {islegit}" );

            if ( currentPokemonIsLowHP && !switchCanSurviveHit )
            {
                islegit = false;
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Current HP/Sacrifice Legitimacy Gate IsLegit: {islegit}" );
            }

            if( bestSwitch != null )
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Chosen Candidate: {bestSwitch.NickName}, Score: {bestScore}, KO_Class: {bestSwitch_DefensePTKOR.PTKO}" );
            else
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate] No Switch available!" );

            //--Flat penalty for undoing a pivot for probably no reason
            if( bestSwitch != null && _ai.LastSentInPokemon != null && bestSwitch == _ai.LastSentInPokemon )
                bestScore -= 20;
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
        };
    }

    public SwitchCandidateResult GetSwitch_Offensive( List<BattleUnit> opponents )
    {
        int bestScore = int.MinValue;
        Pokemon bestSwitch = null;
        float bestHPRatio = 0f;
        ThreatResult biggestThreat = _ai.GetThreat_ImmediateDamage( opponents, _ai.Unit.Pokemon ); //--The biggest threat will start as the biggest threat to the current pokemon thinking of switching. It will get overwritten if there's a viable switch candidate. this prevents it from being null in the case there are no more viable switch ins.
        MoveThreatResult mostThreateningMove = new();
        PotentialToKOResult bestSwitch_OffensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        PotentialToKOResult bestSwitch_DefensePTKOR = new() { PTKO = PotentialToKO.TwoHKO };
        bool islegit = true;
        bool isFaster = false;

        var ourParty = _ai.BattleSystem.GetAllyParty( _ai.Unit.Pokemon );
        var ourActiveUnits = _ai.BattleSystem.GetAllyUnits( _ai.Unit );
        var bench = ourParty.Where( p => !ourActiveUnits.Any( u => u.Pokemon == p ) && p.CurrentHP > 0  ).ToList();

        foreach( var pokemon in bench )
        {
            Debug.Log( $"[AI Scoring][Offensive Switch Candidate] Evaluating {pokemon.NickName}. Their current hp is: {pokemon.CurrentHP}" );
            if( pokemon.IsFainted() )
                continue;

            if( _ai.BattleSystem.IsPokemonSelectedToShift( pokemon ) )
                continue;

            var threat = _ai.GetThreat_ImmediateDamage( opponents, pokemon );
            Debug.Log( $"[AI Scoring][Offensive Switch Candidate] Chosen threat is: {threat.Unit.Pokemon.NickName}" );

            int score = 100;
            int sacrificeWeight = 35;

            float hpRatioAfterHazards = _ai.Get_HPRatio_AfterEntryHazards( pokemon );

            float expendability = _ai.GetExpendability( pokemon, hpRatioAfterHazards );
            int expendabilityScore = Mathf.FloorToInt( expendability * sacrificeWeight );

            Debug.Log( $"[AI Scoring][Offensive Switch Candidate] {pokemon.NickName}'s HPR: {hpRatioAfterHazards}. Expendability & its Score: {expendability}, {expendabilityScore}." );

            if( hpRatioAfterHazards <= 0f && !_ai.Check_IsLastPokemon() )
            {
                var remaining = _ai.GetRemainingAllyPokemon( pokemon );
                if( remaining.Count > 1 )
                    continue;
            }

            //--Speed check.
            bool movesFirst = threat.Unit != null && pokemon.PokeSO.Speed >= threat.Unit.Pokemon.PokeSO.Speed;

            //--Get PTKOs
            //--Offensive PTKO Result. This is the candidate's potential to KO the current opponent.
            var threatHPR                       = _ai.Get_HPRatio( threat.Unit.Pokemon );
            var candidateMove                   = _ai.Get_MostThreateningMove( pokemon, threat.Unit.Pokemon );
            var candidateMoveModifier           = candidateMove.Modifier;
            var candidateWSR                    = _ai.Get_WallingScoreResult( pokemon, threat.Unit.Pokemon, candidateMove );
            PotentialToKOResult offensePTKOR    = _ai.Get_PotentialToKOResult( candidateWSR, candidateMoveModifier, threatHPR );

            //--Defensive PTKO Result. This is the opponent's potential to KO this candidate.
            var threatsMove                     = _ai.Get_MostThreateningMove( threat.Unit.Pokemon, pokemon );
            var threatsMoveModifier             = threatsMove.Modifier;
            var threatsWSR                      = _ai.Get_WallingScoreResult( threat.Unit.Pokemon, pokemon, threatsMove );
            PotentialToKOResult defensePTKOR    = _ai.Get_PotentialToKOResult( threatsWSR, threatsMoveModifier, hpRatioAfterHazards );

            Debug.Log( $"[AI Scoring][Offensive Switch Candidate] PTKOs Obtained. {pokemon.NickName} PTKO: {offensePTKOR.PTKO}. {threat.Unit.Pokemon.NickName} PTKO: {defensePTKOR.PTKO}" );

            //--Build Simulation Units & Field
            var fieldSim            = _ai.UnitSim.BuildSimField();

            var candidateSim        = _ai.UnitSim.BuildSimUnit( pokemon, hpRatioAfterHazards, candidateMove, fieldSim );
            var threatSim           = _ai.UnitSim.BuildSimUnit( threat.Unit.Pokemon, threatHPR, threatsMove, fieldSim );

            var battleSimCtx        = _proj.Get_BattleSimContext( offensePTKOR.PTKO, defensePTKOR.PTKO, candidateSim, threatSim, fieldSim, movesFirst );
            var top                 = _proj.SimulateRound( battleSimCtx );

            //--Begin Scoring
            Debug.Log( $"[AI Scoring][Offensive Switch Candidate] Beginning Scoring. Base Score: {score}" );

            //--Speed Check. Important.
            if( movesFirst )
                score += 15;
            else
                score -= 10;

            Debug.Log( $"[AI Scoring][Offensive Switch Candidate] Moves First: {movesFirst}. Score: {score}" );

            //--Damage & KO Scoring
            if( top.Opponent_DiesBeforeActing )
            {
                score += 120;
                Debug.Log( $"[AI Scoring][Offensive Switch Candidate] Opponent Dies before acting. Score: {score}" );
            }
            else if( top.Opponent_EndOfTurnHP <= 0f && top.Attacker_EndOfTurnHP > 0f )
            {
                score += 90;
                Debug.Log( $"[AI Scoring][Offensive Switch Candidate] Opponent Dies and we live. Score: {score}" );
            }
            else if( top.Opponent_EndOfTurnHP <= 0f && top.Attacker_EndOfTurnHP <= 0f )
            {
                score += 30;
                score -= expendabilityScore;
                Debug.Log( $"[AI Scoring][Offensive Switch Candidate] We both faint. Score: {score}" );
            }
            else if( top.Attacker_EndOfTurnHP <= 0 && top.Opponent_EndOfTurnHP > 0 )
            {
                score -= 90;
                Debug.Log( $"[AI Scoring][Offensive Switch Candidate] We faint and our opponent does not. Score: {score}" );
            }

            if( top.Attacker_DiesBeforeActing )
            {
                score -= 150;
                Debug.Log( $"[AI Scoring][Offensive Switch Candidate] We Die before acting. Score: {score}" );
            }

            if( top.Attacker_EndOfTurnHP > 0f && top.Opponent_EndOfTurnHP > 0f )
            {
                float damageDealt = 1f - top.Opponent_EndOfTurnHP;
                float damageTaken = 1f - top.Attacker_EndOfTurnHP;

                score += Mathf.FloorToInt( damageDealt * 60f );
                score -= Mathf.FloorToInt( damageTaken * 40f );

                Debug.Log( $"[AI Scoring][Offensive Switch Candidate] Neither faint. Score: {score}" );
            }

            score -= expendabilityScore;

            Debug.Log( $"[AI Scoring][Offensive Switch Candidate] {pokemon.NickName}'s Final Score: {score}" );

            if( score > bestScore )
            {
                bestScore = score;
                bestSwitch = pokemon;
                bestHPRatio = hpRatioAfterHazards;
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

        return new()
        {
            Score = bestScore,
            Pokemon = bestSwitch,
            HPRatio = bestHPRatio,
            SwitchOffensePTKOR = bestSwitch_OffensePTKOR,
            SwitchDefensePTKOR = bestSwitch_DefensePTKOR,
            IsLegitimate = islegit,
            MovesFirst = isFaster,
        };
    }

    public Pokemon GetSwitch_Vacuum()
    {
        int bestScore = int.MinValue;
        Pokemon bestSwitch = null;

        var ourParty = _ai.BattleSystem.GetAllyParty( _ai.Unit.Pokemon );
        var bench = ourParty.Where( p =>  p.CurrentHP > 0  ).ToList();

        foreach( var pokemon in bench )
        {
            int score = 0;

            //--Piece value
            var pieceValue = _ai.TeamPieceValues[pokemon];
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

        return bestSwitch;
    }

}
