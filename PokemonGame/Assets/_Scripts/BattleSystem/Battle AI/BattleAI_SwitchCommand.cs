using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SwitchType { Offensive, Defensive, Pivot, }

public class BattleAI_SwitchCommand
{
    private readonly BattleAI _ai;
    public SwitchType LastSwitchType { get; private set; }

    public BattleAI_SwitchCommand( BattleAI ai )
    {
        _ai = ai;
    }

    public void SubmitSwitchCommand( Pokemon incomingPokemon )
    {
        // _ai.BattleSystem.SetAIHardSwitch( true );
        _ai.BattleSystem.SetSwitchPokemonCommand( incomingPokemon, _ai.Unit, true );
    }

    public int SwitchScore( TempoStateResult tempo, ExchangeEvaluation eval, SwitchCandidateResult switchCandidate, BoardContext context )
    {
        //--Tank score if unable to switch
        if( _ai.BattleSystem.BattleType == BattleType.WildBattle_1v1 || _ai.Check_IsLastPokemon() )
            return -999;

        if( switchCandidate.PTKOResult.PotentialKO == PotentialToKO.LikelyOHKO )
            return -999;

        int score = 0;

        var attackerName = eval.AttackerName;
        var targetName = eval.TargetName;
        var switchName = "no switch available!";

        if( switchCandidate.Pokemon != null )
            switchName = switchCandidate.Pokemon.NickName;

        Debug.Log( $"[AI Scoring][Choose Command][Switch Score] Beginning Switch Scoring for {attackerName} vs {targetName}. Switch Candidate: {switchName}. Tempo: {tempo.TempoState}" );
        
        var currentPTKO = eval.TargetPTKO_onAttacker.PotentialKO;
        var currentScore = eval.TargetPTKO_onAttacker.Score;
        var switchPTKO = switchCandidate.PTKOResult;

        Debug.Log( $"[AI Scoring][Choose Command][Switch Score] {targetName}'s Current PTKO {currentPTKO} on me ({attackerName}) and their PTKO Score: {currentScore}. {targetName}'s PTKO on Switch in ({switchName}): {switchCandidate.PTKOResult.PotentialKO}" );

        int improvement = currentScore - switchPTKO.Score;
        score += improvement;

        Debug.Log( $"[AI Scoring][Choose Command][Switch Score] Improvement: {improvement}, Score: {score}" );

        bool iDieBeforeActing = !eval.AttackerMovesFirst && eval.TargetThreatensKO;

        if( iDieBeforeActing )
            score += 50;

        Debug.Log( $"[AI Scoring][Choose Command][Switch Score] Die before Act: {iDieBeforeActing}, Score: {score}" );

        bool switchThreatensKO = switchCandidate.PTKOResult.PotentialKO >= PotentialToKO.Dangerous;
        bool switchThreatensBigDamage = switchCandidate.PTKOResult.PotentialKO > PotentialToKO.Neutral2HK0;

        if( !switchThreatensKO && !switchThreatensBigDamage )
            score -= 30;

        bool losingExchange = eval.TargetThreatensKO && !eval.AttackerThreatensKO;

        if( losingExchange )
            score += 35;

        Debug.Log( $"[AI Scoring][Choose Command][Switch Score] Losing Exchange: {losingExchange}, Score: {score}" );

        if( !switchCandidate.IsLegitimate )
            score -= 70;

        Debug.Log( $"[AI Scoring][Choose Command][Switch Score] Legit Switch: {switchCandidate.IsLegitimate}, Score: {score}" );

        score += _ai.Get_ConsecutiveSwitchPenalty();

        Debug.Log( $"[AI Scoring][Choose Command][Switch Score] Consecutive switch penalty: Score: {score}" );

        score += _ai.TempoSwitchModifier( tempo );

        Debug.Log( $"[AI Scoring][Choose Command][Switch Score] Tempo Switch Modifier: Score: {score}" );

        if( context.IsForcedTrade )
            score -= 50;

        //--Switch tax
        score -= 15;

        return score;
    }

    public SwitchCandidateResult GetSwitch_Defensive( List<BattleUnit> opponents, bool ignoreLegitGates = false )
    {
        int bestScore = int.MinValue;
        Pokemon bestSwitch = null;
        float bestHPRatio = 0f;
        ThreatResult biggestThreat = _ai.GetThreat_ImmediateDamage( opponents, _ai.Unit.Pokemon ); //--The biggest threat will start as the biggest threat to the current pokemon thinking of switching. It will get overwritten if there's a viable switch candidate. this prevents it from being null in the case there are no more viable switch ins.
        PotentialToKOResult bestSwitchInPTKO = new() { PotentialKO = PotentialToKO.Neutral2HK0 };
        float mostThreateningMoveModifier = 1f;
        bool islegit = true;

        var ourParty = _ai.BattleSystem.GetAllyParty( _ai.Unit );
        var ourActiveUnits = _ai.BattleSystem.GetAllyUnits( _ai.Unit );
        var bench = ourParty.Where( p => !ourActiveUnits.Any( u => u.Pokemon == p ) && p.CurrentHP > 0  ).ToList();

        foreach( var pokemon in bench )
        {
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Evaluating {pokemon.NickName}. Their current hp is: {pokemon.CurrentHP}" );
            if( pokemon.IsFainted() )
                continue;

            if( _ai.BattleSystem.IsPokemonSelectedToShift( pokemon ) )
                continue;

            var threat = _ai.GetThreat_ImmediateDamage( opponents, pokemon );
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Chosen threat is: {threat.Unit.Pokemon.NickName}" );

            int score = 100;

            float hpRatio = _ai.Get_HPRatio( pokemon );
            int threatsOffense;
            int ourDefense;
            int bulk = pokemon.PokeSO.MaxHP + pokemon.PokeSO.Defense + pokemon.PokeSO.SpDefense;

            if( threat.Unit.Pokemon.PokeSO.Attack > threat.Unit.Pokemon.PokeSO.SpAttack )
            {
                ourDefense = pokemon.PokeSO.Defense;
                threatsOffense = threat.Unit.Pokemon.PokeSO.Attack;
            }
            else
            {
                ourDefense = pokemon.PokeSO.SpDefense;
                threatsOffense = threat.Unit.Pokemon.PokeSO.SpAttack;
            }

            bool isInDanger = hpRatio <= 0.4f && ourDefense <= 80f || hpRatio <= 0.25f;

            //--Speed check. Not as important in a defensive switch-in.
            if( threat.Unit != null && pokemon.PokeSO.Speed < threat.Unit.Pokemon.PokeSO.Speed )
                score -= 15;
            else
                score += 5;

            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] {pokemon.NickName}'s Speed Comparison checked. Score: {score}" );

            //--Tier of the targeted defensive stat's Base Stat
            if ( ourDefense >= 150f )              score += 25;
            else if( ourDefense >= 125f )          score += 15;
            else if( ourDefense >= 100f )          score += 10;
            else if( ourDefense >= 80f )           score += 0;
            else if( ourDefense >= 65f )           score -= 5;
            else if( ourDefense >= 50f )           score -= 10;
            else if( ourDefense < 50f )            score -= 15;
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] {pokemon.NickName}'s Defense checked. Score: {score}" );

            //--Aggregate bulk based on hp, def, and spdef base stats
            if( bulk >= 400 )           score += 15;
            else if( bulk >= 300 )      score += 10;
            else if( bulk >= 200 )      score += 0;
            else if( bulk >= 150 )      score -= 5;
            else if( bulk <= 100 )      score -= 10;
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] {pokemon.NickName}'s Overall Bulk checked. Score: {score}" );

            //--Walling capability vs the threat's highest attacking stat
            int wallingScore = _ai.Get_WallingScore( threat.Unit.Pokemon, pokemon );
            var threatMoveModifier = _ai.Get_MostThreateningMove( threat.Unit, pokemon ).Modifier;

            //--Modifier influence. Higher modifiers likely mean super effective damage and switching a mon into a super effective hit is lunacy.
            if( threatMoveModifier >= 4f )          score -= 50; //--4x damage is almost always certain death. Ideally never pick this candidate.
            else if( threatMoveModifier >= 2f )     score -= 25; //--Discourage super effective damage.
            else if( threatMoveModifier <= 0.5f )   score += 10; //--reward resist, but not too much.


            PotentialToKOResult potentialToKO = _ai.Get_PotentialToKOResult( wallingScore, threatMoveModifier, hpRatio );

            if( potentialToKO.PotentialKO <= PotentialToKO.Neutral2HK0 )
                score += 15;
            else
                score += potentialToKO.Score;
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] {pokemon.NickName}'s Potential to be KO'd: {potentialToKO.PotentialKO}, {potentialToKO.Score}. Checked vs HP Ratio. Score: {score}" );

            //--Immediate danger override
            if( isInDanger )
            {
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate] {pokemon.NickName}'s In Danger!" );
                score -= 50;
            }

            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Current threat: {threat.Unit.Pokemon.NickName}, Score: {score}" );

            if( score > bestScore )
            {
                bestScore = score;
                bestSwitch = pokemon;
                bestHPRatio = hpRatio;
                bestSwitchInPTKO = potentialToKO;
                biggestThreat = threat;
                mostThreateningMoveModifier = threatMoveModifier;
            }

            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Current Biggest Threat is: {biggestThreat.Unit.Pokemon.NickName}" );
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Current Switch Candidate: {pokemon.NickName}, Score: {score}" );
        }

        Debug.Log( $"[AI Scoring][Defensive Switch Candidate] The Biggest Threat is: {biggestThreat.Unit.Pokemon.NickName}" );

        if( !ignoreLegitGates )
        {
            Debug.Log( $"[AI Battle][Defensive Switch Candidate] Current unit: {_ai.Unit} is {_ai.Unit.Pokemon.NickName}" );
            Debug.Log( $"[AI Battle][Defensive Switch Candidate] Biggest Threat unit: {biggestThreat.Unit} is {biggestThreat.Unit.Pokemon.NickName}" );
            float currentMonHPRatio = _ai.Get_HPRatio( _ai.Unit.Pokemon );
            int currentMonWallingScore = _ai.Get_WallingScore( biggestThreat.Unit.Pokemon, _ai.Unit.Pokemon ); //--biggest threat is turning up null toward end of battle

            PotentialToKOResult currentMonPotentialToKO = _ai.Get_PotentialToKOResult( currentMonWallingScore, mostThreateningMoveModifier, currentMonHPRatio );

            //--Gate switch by KO Class improvement. If the KO Class doesn't improve significantly, don't switch.
            bool improvesKOClass = bestSwitchInPTKO.PotentialKO < currentMonPotentialToKO.PotentialKO;
            bool isStillDying = bestSwitchInPTKO.PotentialKO > PotentialToKO.Risky;

            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] KO Class Improved: {improvesKOClass}, The Switch will still die: {isStillDying}, IsLegit Switch: {islegit}" );
            if ( isStillDying && !improvesKOClass )
            {
                islegit = false;
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate] KO Class Legitimacy Gate IsLegit: {islegit}" );
            }

            //--HP/Sacrifice Gate
            bool currentPokemonIsLowHP = currentMonHPRatio < 0.25f;
            bool switchCanSurviveHit = bestSwitchInPTKO.PotentialKO <= PotentialToKO.Neutral2HK0;

            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Current Pokemon has low HP: {currentPokemonIsLowHP}, The Switch will still die: {switchCanSurviveHit}, IsLegit Switch: {islegit}" );

            if ( currentPokemonIsLowHP && !switchCanSurviveHit )
            {
                islegit = false;
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Current HP/Sacrifice Legitimacy Gate IsLegit: {islegit}" );
            }
        }

        if( bestSwitch != null )
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Chosen Candidate: {bestSwitch.NickName}, Score: {bestScore}, KO_Class: {bestSwitchInPTKO}" );
        else
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] No Switch available!" );

        //--Flat penalty for undoing a pivot for probably no reason
        if( bestSwitch != null && _ai.LastSentInPokemon != null && bestSwitch == _ai.LastSentInPokemon )
            bestScore -= 20;

        return new(){ Score = bestScore, Pokemon = bestSwitch, HPRatio = bestHPRatio, PTKOResult = bestSwitchInPTKO, IsLegitimate = islegit };
    }

}
