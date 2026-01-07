using UnityEngine;
using System.Collections.Generic;
using System.Linq;

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
    public float TrainerSkillModifier { get; private set; }
    public int SwitchAmount { get; private set; }
    public const int HARD_SWITCH_THRESHOLD = 75;
    public const int WALLINGSCORE_NORMALIZATION_OFFSET = 30;

    public void SetupAI( BattleSystem battleSystem, BattleUnit battleUnit ){
        BattleSystem = battleSystem;
        Unit = battleUnit;
        Pokemon = Unit.Pokemon;

        if( battleSystem.BattleType != BattleType.WildBattle_1v1 )
            TrainerSkillModifier = Mathf.Clamp01( battleSystem.TopTrainerParty.GetComponent<Trainer>().TrainerSkillLevel / 100f );

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

    public void ChooseCommand()
    {
        Debug.Log( $"[AI Scoring] {Unit.Pokemon.NickName} ChooseCommand()" );
        if( Unit.Pokemon.SevereStatus?.ID == StatusConditionID.FNT || Unit.Pokemon.CurrentHP == 0 )
            return;

            var opposingUnits = BattleSystem.GetOpposingUnits( Unit );
            var damageThreat = GetThreat_ImmediateDamage( opposingUnits, Unit.Pokemon );
            int outgoingPressure = GetOutgoingPressure( Unit.Pokemon, damageThreat.Unit );

            if( BattleSystem.BattleType != BattleType.WildBattle_1v1 && _switchCommand.ShouldSwitch( damageThreat, outgoingPressure ) )
            {
                Debug.Log( $"[AI Scoring] {Unit.Pokemon.NickName} is thinking about switching out!" );
                var incomingCandidate = _switchCommand.GetSwitch_Defensive( opposingUnits );
                if( incomingCandidate.Pokemon != null && incomingCandidate.IsLegitimate )
                {
                    if( _moveCommand.ShouldAttackInstead( damageThreat ) )
                    {
                        Debug.Log( $"[AI Scoring] {Unit.Pokemon.NickName} chose to attack instead of switch!" );
                        ResetSwitchAmount();
                        _moveCommand.SubmitMoveCommand( damageThreat.Unit );
                    }
                    else
                    {
                        Debug.Log( $"[AI Scoring] {Unit.Pokemon.NickName} is switching with {incomingCandidate.Pokemon.NickName}! Switch Amount: {SwitchAmount}" );
                        IncreaseSwitchAmount();
                        Debug.Log( $"[AI Scoring] New Switch Amount: {SwitchAmount}" );
                        _switchCommand.SubmitSwitchCommand( incomingCandidate.Pokemon );
                    }
                }
                else
                {
                    ResetSwitchAmount();
                    _moveCommand.SubmitMoveCommand( damageThreat.Unit );
                }
            }
            else
            {
                Debug.Log( $"[AI Scoring] {Unit.Pokemon.NickName} chose to attack target: {damageThreat.Unit.Pokemon.NickName}" );
                ResetSwitchAmount();
                _moveCommand.SubmitMoveCommand( damageThreat.Unit );
            }
    }

    public Pokemon RequestedForcedSwitch()
    {
        var oppsingUnits = BattleSystem.GetOpposingUnits( Unit );
        return _switchCommand.GetSwitch_Defensive( oppsingUnits, true ).Pokemon;
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

            //--Assign a bonus for effectiveness
            float effectiveness = TypeChart.GetEffectiveness( move.MoveType, target.Pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( move.MoveType, target.Pokemon.PokeSO.Type2 );

            //--Assign a bonus for stab
            float stab = move.MoveSO.Type == Pokemon.PokeSO.Type1 || move.MoveSO.Type == Pokemon.PokeSO.Type2 ? 1.5f : 1f;

            //--Assign a bonus for a weather damage boost
            float weather = BattleSystem.Field.Weather?.OnDamageModify?.Invoke( Unit.Pokemon, target.Pokemon, move ) ?? 1f;

            Debug.Log( $"[AI Scoring][Outgoing Pressure Check] Score-ing {me.NickName}'s move {move.MoveSO.Name}. Effectiveness Modifier: {effectiveness}, STAB Modifier: {stab}, Weather Modifier: {weather}" );

            float currentMoveThreat = effectiveness * stab * weather;
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
        if( me.Speed > target.Pokemon.Speed )
            score += 20;
        else if( me.Speed < target.Pokemon.Speed )
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

        Debug.Log( $"[AI Scoring][Outgoing Pressure Check] {me.NickName}'s HP checked for low percentage. Score: {score}" );

        Debug.Log( $"[AI Scoring][Outgoing Pressure Check] {me.NickName}'s Final Score: {score}" );
        
        return Mathf.Clamp( score, 0, 250 );
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

                Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] Score-ing {threat.Pokemon.NickName}'s move {move.MoveSO.Name}. Effectiveness Modifier: {effectiveness}, STAB Modifier: {stab}, Weather Modifier: {weather}" );

                float currentMoveThreat = effectiveness * stab * weather;
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

            //--Higher speed means the target is more threatening
            if( threat.Pokemon.Speed > ourUnit.Speed )
                threatScore += 20;
            else if( threat.Pokemon.Speed < ourUnit.Speed )
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

    public bool Check_UnitHasPriority( Pokemon pokemon )
    {
        for( int i = 0; i < pokemon.ActiveMoves.Count; i++ )
        {
            if( pokemon.ActiveMoves[i].MoveSO.MovePriority <= MovePriority.One && pokemon.ActiveMoves[i].MoveSO.Name != "Protect" )
                return true;
        }

        return false;
    }

    public bool Check_IsLastPokemon()
    {
        if( BattleSystem.BattleType == BattleType.WildBattle_1v1 )
            return true;

        var activeEnemyPokemon = BattleSystem.EnemyUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
        var remainingPokemon = BattleSystem.TopTrainerParty.GetHealthyPokemon( dontInclude: activeEnemyPokemon );

        return remainingPokemon == null && activeEnemyPokemon.Count > 0;
    }

    public MoveThreatResult Get_MostThreateningMove( Pokemon attacker, Pokemon target )
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
            float weather           = BattleSystem.Field.Weather?.OnDamageModify?.Invoke( target, attacker, move ) ?? 1f;

            Debug.Log( $"[AI Scoring][Most Threatening Move] Score-ing {attacker.NickName}'s move {move.MoveSO.Name}. Effectiveness Modifier: {effectiveness}, STAB Modifier: {stab}, Weather Modifier: {weather}" );

            float currentMoveThreat = effectiveness * stab * weather;

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

            Debug.Log( $"[AI Scoring][Most Threatening Move] {attacker.NickName}'s move {move.MoveSO.Name} checked. Move's Score: {modifier}" );
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

        Debug.Log( $"[AI Scoring][Most Threatening Move] {attacker.NickName}'s Most Threatening Move is: {bestMove.MoveSO.Name}. Modifier: {modifier}, Score: {score}" );

        return new(){ Score = score, Modifier = modifier, Move = bestMove };
    }

    public PotentialToKOResult Get_PotentialToKOResult( int wallingScore, float moveModifier, float targetHPRatio )
    {

        PotentialToKO basePotentialKO = Get_PotentialToKOFromWallingScore( wallingScore );
        int shift = ShiftPotentialToKO( basePotentialKO, moveModifier, targetHPRatio );

        int finalClassInt = Mathf.Clamp( (int)basePotentialKO + shift, (int)PotentialToKO.HardWall, (int)PotentialToKO.LikelyOHKO );

        var finalClass = (PotentialToKO)finalClassInt;

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
        if( wallingScore >= 45 )                potentialKO = PotentialToKO.HardWall;        //--Hard Wall, Shuts down pressure
        else if( wallingScore >= 25 )           potentialKO = PotentialToKO.Sturdy;          //--Sturdy, can take a couple hits
        else if( wallingScore >= 10 )           potentialKO = PotentialToKO.Safe;            //--Safe, can take an extra hit
        else if( wallingScore >= -9 )           potentialKO = PotentialToKO.Neutral2HK0;     //--Neutral, possible 2HKO
        else if( wallingScore >= -29 )          potentialKO = PotentialToKO.Risky;           //--Getting Risky, almost guaranteed 2HK0
        else if( wallingScore >= -59 )          potentialKO = PotentialToKO.Dangerous;       //--Danger, high damage expected, crit or unexpected damage might OHKO
        else                                    potentialKO = PotentialToKO.LikelyOHKO;      //--Fatal, Likely OHKO

        return potentialKO;
    }
    
    private int Get_PotentialToKOScoreFromEnum( PotentialToKO koClass )
    {
        //--This is a damn pretty switch, sheesh
        return koClass switch
        {
            PotentialToKO.HardWall       => +60,
            PotentialToKO.Sturdy         => +35,
            PotentialToKO.Safe           => +15,
            PotentialToKO.Neutral2HK0    => 0,
            PotentialToKO.Risky          => -15,
            PotentialToKO.Dangerous      => -35,
            PotentialToKO.LikelyOHKO     => -60,
            _ => 0
        };
    }

    private int ShiftPotentialToKO( PotentialToKO baseKO, float modifier, float targetHPratio )
    {
        //--A higher modifier shifts positively because the enum starts and 0 and increases. HardWall is 0, while LikelyOHKO is 6
        //--A higher modifier means increased damage, therefore the likelyhood of a KO increases.
        Debug.Log( $"[AI Scoring][Shift Potential To KO] Base KO Potential: {baseKO}, Move Modifier: {modifier}, Target's HP Ratio: {targetHPratio}" );

        int shift = 0;
        int hpShift = 0;

        if( modifier > 4f )                     shift = +2;
        else if( modifier > 2f )                shift = +1;
        else if( modifier == 0f )               shift = -2;
        else if( modifier <= 0.5f )             shift = -1;

        Debug.Log( $"[AI Scoring][Shift Potential To KO] Move modifier shifting KO Potential by: {shift}" );

        if( targetHPratio <= 0.25f )            hpShift = +2;
        else if( targetHPratio <= 0.5f )        hpShift = +1;

        Debug.Log( $"[AI Scoring][Shift Potential To KO] Target's HP Ratio shifting KO Potential by: {hpShift}" );

        shift += hpShift;

        Debug.Log( $"[AI Scoring][Shift Potential To KO] Total shifting of KO Potential by: {shift}" );

        return shift;
    }
}

public class ThreatResult
{
    public int Score { get; set; }
    public BattleUnit Unit { get; set; }
}

public class MoveThreatResult
{
    public float Score { get; set; }
    public float Modifier { get; set; }
    public Move Move { get; set; }
}

public class SwitchCandidateResult
{
    public int Score { get; set; }
    public Pokemon Pokemon { get; set; }
    public PotentialToKO KOClass { get; set; }
    public bool IsLegitimate { get; set; }
}

public class PotentialToKOResult
{
    public int Score { get; set; }
    public PotentialToKO PotentialKO { get; set; }
    public float Modifier { get; set; }
}
