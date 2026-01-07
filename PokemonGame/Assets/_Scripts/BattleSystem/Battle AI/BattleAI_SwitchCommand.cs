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

    public bool ShouldSwitch( ThreatResult damageThreat, int outgoingPressure )
    {
        if( _ai.Check_IsLastPokemon() )
            return false;
        
        var attacker = damageThreat.Unit.Pokemon;
        var target = _ai.Unit.Pokemon;

        float hpRatio = _ai.Get_HPRatio( target );
        int wallingScore = _ai.Get_WallingScore( attacker, target );
        MoveThreatResult moveThreat = _ai.Get_MostThreateningMove( attacker, target );

        PotentialToKOResult potentialToKO = _ai.Get_PotentialToKOResult( wallingScore, moveThreat.Modifier, hpRatio );

        int dangerScore = DangerScore( damageThreat, outgoingPressure, potentialToKO );
        Debug.Log( $"[AI Scoring][Should Switch] Threat and Switch scores for AI {_ai.Unit.Pokemon.NickName}! Incoming Damage: {damageThreat.Score} from {damageThreat.Unit.Pokemon.NickName}, Outgoing Pressure: {outgoingPressure}, Danger Score: {dangerScore}" );

        dangerScore += _ai.Get_ConsecutiveSwitchPenalty();
        Debug.Log( $"[AI Scoring][Should Switch] Applying Consecutive Switching Penalty for {_ai.Unit.Pokemon.NickName}! Danger Score: {dangerScore}" );   

        //--If the Danger Score is higher than the threshold, we return true. We should switch based on score.
        //--IsLegitimate will determine if the scored switch is legitimate, however.
        if( dangerScore >= BattleAI.HARD_SWITCH_THRESHOLD )
            return true;
        else
            return false;
    }

    public int DangerScore( ThreatResult incomingThreat, int outgoingPressure, PotentialToKOResult potentialToKO )
    {
        Debug.Log( $"[AI Scoring][Danger Gap] Beginning Danger Gap Scoring. Threat Score: {incomingThreat}, Outgoing Pressure: {outgoingPressure}, Potential to KO Score: {potentialToKO}" );
        int score = 0;
        int pressureGap = incomingThreat.Score - outgoingPressure;

        score += pressureGap;
        Debug.Log( $"[AI Scoring][Danger Gap] Checked Pressure Gap. Score: {score}" );

        //--KO Class is "signed", a Higher score is a more survivable KO class. Since DangerScore needs to increase with increased danger, we need to invert KO Score.
        int dangerfromKOClass = -potentialToKO.Score;

        score += dangerfromKOClass;
        Debug.Log( $"[AI Scoring][Danger Gap] Checked Inversed Potential to KO Score: {dangerfromKOClass}. Final Score: {score}" );

        return score;
    }

    public SwitchCandidateResult GetSwitch_Defensive( List<BattleUnit> opponents, bool ignoreLegitGates = false )
    {
        int bestScore = int.MinValue;
        Pokemon bestSwitch = null;
        ThreatResult biggestThreat = _ai.GetThreat_ImmediateDamage( opponents, _ai.Unit.Pokemon ); //--The biggest threat will start as the biggest threat to the current pokemon thinking of switching. It will get overwritten if there's a viable switch candidate. this prevents it from being null in the case there are no more viable switch ins.
        PotentialToKO bestSwitchInKOClass = PotentialToKO.Neutral2HK0;
        float mostThreateningMoveModifier = 1f;
        bool islegit = true;

        var ourParty = _ai.BattleSystem.GetAllyParty( _ai.Unit ).Party;
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
                score -= 5;
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] {pokemon.NickName}'s Speed Comparison checked. Score: {score}" );

            //--Tier of the targeted defensive stat's Base Stat
            if ( ourDefense >= 150f )              score += 25;
            else if( ourDefense >= 125f )          score += 15;
            else if( ourDefense >= 100f )          score += 10;
            else if( ourDefense >= 80f )           score += 0;
            else if( ourDefense >= 65f )           score -= 10;
            else if( ourDefense >= 50f )           score -= 15;
            else if( ourDefense < 50f )            score -= 25;
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] {pokemon.NickName}'s Defense checked. Score: {score}" );

            //--Aggregate bulk based on hp, def, and spdef base stats
            if( bulk >= 400 )           score += 15;
            else if( bulk >= 300 )      score += 10;
            else if( bulk >= 200 )      score += 0;
            else if( bulk >= 150 )      score -= 10;
            else if( bulk <= 100 )      score -= 15;
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] {pokemon.NickName}'s Overall Bulk checked. Score: {score}" );

            //--Walling capability vs the threat's highest attacking stat
            int wallingScore = _ai.Get_WallingScore( threat.Unit.Pokemon, pokemon );
            var threatMoveModifier = _ai.Get_MostThreateningMove( threat.Unit.Pokemon, pokemon ).Modifier;

            PotentialToKOResult potentialToKO = _ai.Get_PotentialToKOResult( wallingScore, threatMoveModifier, hpRatio );

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
                bestSwitchInKOClass = potentialToKO.PotentialKO;
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
            bool improvesKOClass = bestSwitchInKOClass < currentMonPotentialToKO.PotentialKO;
            bool isStillDying = bestSwitchInKOClass >= PotentialToKO.Risky;

            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] KO Class Improved: {improvesKOClass}, The Switch will still die: {isStillDying}, IsLegit Switch: {islegit}" );
            if ( isStillDying && !improvesKOClass )
            {
                islegit = false;
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate] KO Class Legitimacy Gate IsLegit: {islegit}" );
            }

            //--HP/Sacrifice Gate
            bool currentPokemonIsLowHP = currentMonHPRatio < 0.25f;
            bool switchCanSurviveHit = bestSwitchInKOClass <= PotentialToKO.Neutral2HK0;

            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Current Pokemon has low HP: {currentPokemonIsLowHP}, The Switch will still die: {switchCanSurviveHit}, IsLegit Switch: {islegit}" );

            if ( currentPokemonIsLowHP && !switchCanSurviveHit )
            {
                islegit = false;
                Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Current HP/Sacrifice Legitimacy Gate IsLegit: {islegit}" );
            }
        }

        if( bestSwitch != null )
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] Chosen Candidate: {bestSwitch.NickName}, Score: {bestScore}, KO_Class: {bestSwitchInKOClass}" );
        else
            Debug.Log( $"[AI Scoring][Defensive Switch Candidate] No Switch available!" );

        return new(){ Score = bestScore, Pokemon = bestSwitch, KOClass = bestSwitchInKOClass, IsLegitimate = islegit };
    }

}
