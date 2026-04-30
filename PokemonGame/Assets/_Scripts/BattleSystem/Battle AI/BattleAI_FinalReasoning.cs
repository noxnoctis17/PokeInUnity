using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleAI_FinalReasoning
{
    private readonly BattleAI _ai;
    private Dictionary<ReasoningRule, Func<ReasoningContext, ReasoningContext>> _reasoningRules;

    public BattleAI_FinalReasoning( BattleAI ai )
    {
        _ai = ai;
        InitReasoningRules();
    }

    public ActionEvaluation ApplyFinalReasoning( List<ActionEvaluation> actions, ExchangeEvaluation ee, BoardContext bc, CurrentPlan plan, ThreatProfile tp )
    {
        ReasoningContext context = new()
        {
            Actions = actions,
            EE = ee,
            BC = bc,
            Plan = plan,
            TP = tp,
            HasDecision = false,
            ChosenAction = null,
        };

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"=====[Applying Final Reasoning. Looking for Rules...]=====" );

        foreach( var rule in _reasoningRules )
        {
            var reasoning = rule.Value.Invoke( context );
            if( reasoning.HasDecision && reasoning.ChosenAction != null )
                return reasoning.ChosenAction;
            else
                continue;
        }

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"=[No rule found or decided on, going with highest scoring action ({actions[0].Type}, {actions[0].Score}).]=" );
        _ai.CurrentLog.Add( $"" );

        return actions[0]; //--index 0 should be the highest scored action.
    }

    private void InitReasoningRules()
    {
        _reasoningRules = new()
        {
            {
                ReasoningRule.Attack_VS_Setup, ( context ) => AttackVSSetup( context )
                    
            },
            {
                ReasoningRule.SwitchPrediction_CoverageMove, ( context ) => SwitchPredictionCoverageMove( context )
            },
            // {
            //     ReasoningRule.StrategicSacrifice, ( context ) => StrategicSacrifice( context )
            // },
            // {
            //     ReasoningRule.Attack_VS_Hazards, ( context ) => AttackVSHazards( context )
            // },
        };
    }

    private ReasoningContext AttackVSSetup( ReasoningContext context )
    {
        _ai.CurrentLog.Add( $"Checking for Attack vs Setup Rule..." );
        context.Rule = ReasoningRule.Attack_VS_Setup;

        ActionEvaluation attack = null;
        ActionEvaluation setup = null;
        context.HasDecision = false;
        context.ChosenAction = null;

        if( context.Actions.Count <= 1 )
            return context;

        if( context.Actions[0].Type != ActionType.Attack && context.Actions[0].Type != ActionType.Setup )
            return context;

        foreach( var action in context.Actions )
        {
            if( action.Type == ActionType.Attack )
            {
                attack = action;
                continue;
            }

            if( action.Type == ActionType.Setup )
            {
                setup = action;
                continue;
            }
        }

        if( attack == null || setup == null )
            return context;

        bool shouldCompare = false;
        bool firstAndSecond = context.Actions[0].Type == ActionType.Attack && context.Actions[1].Type == ActionType.Setup || context.Actions[0].Type == ActionType.Setup && context.Actions[1].Type == ActionType.Attack;

        if( firstAndSecond )
        {
            shouldCompare = true;
        }
        else
        {
            if( setup.Score > attack.Score )
            {
                float threshold = setup.Score * 0.65f;

                if( attack.Score > threshold )
                    shouldCompare = true;
            }
            else
            {
                float threshold = attack.Score * 0.65f;

                if( setup.Score > threshold )
                    shouldCompare = true;
            }
        }

        if( !shouldCompare )
            return context;

        _ai.CurrentLog.Add( $"===[Rule Found: {context.Rule}]===" );
        _ai.CurrentLog.Add( $"Beginning Reasoning checks..." );

        bool koOutcomeEquivalent = false;
        int setupAdvantages = 0;
        int setupRisks = 0;

        //-------------------------------
        //--------Setup Reasoning--------
        //-------------------------------

        //--Setup provides a stat boost. This is an auto-point here.
        // Attacking can also alter stats with some moves (scale shot, close combat, trailblaze),
        // and so this check can also be applied to attack as an actual check, justifying the auto-point here.
        setupAdvantages += 1;

        if( setup.Top2.AttackerHasSweepHorizon )
        {
            setupAdvantages++;
            _ai.CurrentLog.Add( $"Attacker has sweep horizon detected from setup. Advantage." );
        }

        bool weHaveToSwitchNextTurn = _ai.UnitSim.PredictSwitchProbability( setup.Top2.OpponentPTKO, setup.Top2.AttackerPTKO, setup.Top2.AttackerMovedFirst, setup.Top2.Opponent.BeginningHPR, setup.Top2.Attacker.BeginningHPR ) >= 0.8f;
        bool weForceSwitchNextTurn = _ai.UnitSim.PredictSwitchProbability( setup.Top2.AttackerPTKO, setup.Top2.OpponentPTKO, setup.Top2.AttackerMovedFirst, setup.Top2.Attacker.BeginningHPR, setup.Top2.Opponent.BeginningHPR ) >= 0.8f;
        
        if( weForceSwitchNextTurn )
        {
            setupAdvantages++;
            _ai.CurrentLog.Add( $"Setting up causes the opponent to switch next turn. Advantage." );
        }

        if( context.Plan.Type == PlanType.EnableSweep && setup.Top1.Attacker_EndOfTurnHP > 0 && setup.Top2.AttackerMovedFirst )
        {
            setupAdvantages++;
            _ai.CurrentLog.Add( $"The current plan is to sweep and we both survive the turn and move first next turn. Advantage." );
        }

        bool setupFaintThisTurn = setup.Top1.Attacker_EndOfTurnHP <= 0;
        bool setupFaintNextTurn = setup.Top2.Attacker_EndOfTurnHP <= 0;

        if( setupFaintThisTurn || setupFaintNextTurn )
        {
            setupRisks++;
            _ai.CurrentLog.Add( $"We're KO'd either this turn or next turn if we try to setup. Risk." );
        }
        else
        {
            if( attack.Top2.Opponent_EndOfTurnHP <= 0 && setup.Top2.Opponent_EndOfTurnHP <= 0 )
            {
                setupAdvantages++;
                koOutcomeEquivalent = true;
                _ai.CurrentLog.Add( $"Attack gets a KO next turn, and setup gets a KO next turn. Advantage." );
            }
            else if( attack.Top1.Opponent_EndOfTurnHP <= 0 || ( attack.Top2.Opponent_EndOfTurnHP <= 0 && setup.Top2.Opponent_EndOfTurnHP > 0 ) )
            {
                setupRisks++;
                _ai.CurrentLog.Add( $"Attack gets a KO this turn or next turn, but setup does NOT get a KO next turn. Risk." );
            }
        }

        int setupPressureDelta = setup.PBS.PressureScore - attack.PBS.PressureScore;
        if( setupPressureDelta >= 0 )
        {
            setupAdvantages++;
            _ai.CurrentLog.Add( $"Setup Pressure Delta: {setupPressureDelta} >= 0. Rewarding." );
        }

        float attackHPleft = attack.Top2.Attacker_EndOfTurnHP;
        float setupHPleft = setup.Top2.Attacker_EndOfTurnHP;

        if( setupHPleft < attackHPleft )
        {
            setupRisks++;
            _ai.CurrentLog.Add( $"Setup ends with less hp than attack next turn. Risk." );
        }

        if( Mathf.Abs( setupHPleft - attackHPleft ) < 0.05f )
        {
            setupAdvantages++;
            _ai.CurrentLog.Add( "Setup preserves equivalent HP to attack. Advantage." );
        }

        if( !setup.Top2.AttackerCanAct || setup.Top1.Attacker.Pokemon != setup.Top2.Attacker.Pokemon || weHaveToSwitchNextTurn )
        {
            setupRisks++;
            _ai.CurrentLog.Add( $"Setting up this turn causes us to be unable to act next turn, or forces us out via switch or phaze next turn. Risk." );
        }

        //-------------------------------
        //-------------------------------
        //-------------------------------

        if( attack.Score > setup.Score )
        {
            if( koOutcomeEquivalent && setupAdvantages >= 3 && setupRisks <= 1 )
            {
                context.HasDecision = true;
                context.ChosenAction = setup;
                _ai.CurrentLog.Add( $"Setup found to be better than Attack. Selecting Setup." );
            }
            else if( setupAdvantages >= 5 && setupRisks <= 2 )
            {
                context.HasDecision = true;
                context.ChosenAction = setup;
                _ai.CurrentLog.Add( $"Setup found to be better than Attack. Selecting Setup." );
            }

            bool strongSetupCase = koOutcomeEquivalent && Mathf.Abs( setupHPleft - attackHPleft ) < 0.05f;
            if( strongSetupCase && setupRisks == 0 )
            {
                context.HasDecision = true;
                context.ChosenAction = setup;
                _ai.CurrentLog.Add( $"Setup found to be better than Attack. Selecting Setup." );
            }
        }

        if( setup.Score > attack.Score )
        {
            if( !koOutcomeEquivalent && setupAdvantages < 3 && setupRisks > 1 )
            {
                context.HasDecision = true;
                context.ChosenAction = attack;
                _ai.CurrentLog.Add( $"Attack found to be better than Setup. Selecting Attack." );
            }
            else if( setupAdvantages <= 1 && setupRisks > 2 )
            {
                context.HasDecision = true;
                context.ChosenAction = attack;
                _ai.CurrentLog.Add( $"Attack found to be better than Setup. Selecting Attack." );
            }

            bool strongAttackCase = attack.Top1.Opponent_DiesBeforeActing && setupHPleft < attackHPleft;
            if( strongAttackCase && setupRisks > 0 )
            {
                context.HasDecision = true;
                context.ChosenAction = attack;
                _ai.CurrentLog.Add( $"Attack found to be better than Setup. Selecting Attack." );
            }
        }

        return context;
    }

    private ReasoningContext SwitchPredictionCoverageMove( ReasoningContext context )
    {
        _ai.CurrentLog.Add( $"Checking for Switch Prediction Coverage Move Rule..." );
        context.Rule = ReasoningRule.SwitchPrediction_CoverageMove;

        ActionEvaluation attack = null;
        context.HasDecision = false;
        context.ChosenAction = attack;

        if( context.Actions.Count <= 0 )
            return context;

        if( context.Actions[0].Type != ActionType.Attack )
            return context;

        if( context.BC.OppRemainingPieces <= 1 )
            return context;

        attack = context.Actions[0];
        var attackerPTKO = attack.Top1.AttackerPTKO;
        var opponentPTKO = attack.Top1.OpponentPTKO;
        var attackerHPR = attack.Top1.Attacker.BeginningHPR;
        var opponentHPR = attack.Top1.Opponent.BeginningHPR;
        var movesFirst = attack.Top1.AttackerMovedFirst;

        float theySwitchProb = _ai.UnitSim.PredictSwitchProbability( attackerPTKO, opponentPTKO, movesFirst, attackerHPR, opponentHPR );

        if( theySwitchProb < 0.75f )
            return context;

        _ai.CurrentLog.Add( $"" );
        _ai.CurrentLog.Add( $"===[Rule Found: {context.Rule} (Switch Probability: {theySwitchProb} >= 0.75f)]===" );
        _ai.CurrentLog.Add( $"Beginning Reasoning checks..." );

        if( attack.Top1.Opponent_EndOfTurnHP <= 0f && attack.Top1.AttackerMovedFirst && theySwitchProb < 0.9f )
        {
            _ai.CurrentLog.Add( $"We have a guaranteed KO and we're not extra sure they will switch. Going ahead with chosen attack ({attack.MovePayload.MoveSO.Name}) to not throw the game." );
            return context;
        }

        if( theySwitchProb > 0.9f )
            theySwitchProb = 0.9f;

        bool readSwitch = UnityEngine.Random.value <= theySwitchProb;

        if( readSwitch )
        {
            _ai.CurrentLog.Add( $"Reading the switch!" );

            Move bestCoverageMove = null;
            int coverageMovePTKOs = 0;
            var ourActivePokemon = _ai.BattleSystem.GetAllyUnits( _ai.Unit );
            var ourActiveAdapters = _ai.CreateBattleAIUnits_FromBattleUnits( ourActivePokemon );
            var likelyCandidates = _ai.GetLikelyDefensiveSwitches( ourActiveAdapters, attack.Top1.Opponent );
            List<IBattleAIUnit> likelySwitches = new();
            var ourMon = _ai.ThisUnitAdapter;
            int chosenMovePTKOs = 0;

            _ai.CurrentLog.Add( $"Likely Switch Candidates Found: {likelyCandidates.Count}." );
            if( likelyCandidates.Count <= 0 )
            {
                _ai.CurrentLog.Add( $"No switch candidates found, skipping coverage logic." );
                return context;
            }

            //--Convert candidates into IBattleAIUnits
            for( int i = 0; i < likelyCandidates.Count; i++ )
            {
                BattleAI_PokemonAdapter opp = new( likelyCandidates[i], _ai );
                likelySwitches.Add( opp );
                _ai.CurrentLog.Add( $"Adding switch candidate adapter {opp.Name} ({likelyCandidates[i].NickName})." );
            }

            //--Get aggregate PTKO value for move selected by GetMove_BestAttack() vs likely candidates
            _ai.CurrentLog.Add( $"Getting aggregate PTKO value for Chosen Move ({attack.MovePayload.MoveSO.Name}) vs Likely Switch Candidates." );
            for( int i = 0; i < likelySwitches.Count; i++ )
            {
                var opp = likelySwitches[i];
                var move = attack.MovePayload;
                MoveThreatResult ourMTR = new()
                {
                    Move = move,
                    Target = opp,
                };

                float effectiveness = _ai.UnitSim.Get_MoveEffectiveness( opp, move );
                float modifier = _ai.UnitSim.Get_MoveModifier( ourMon, opp, move );
                ourMTR.Modifier = effectiveness * modifier;

                var ourEDR = _ai.Projection.Get_EstimatedDamageResult( ourMon, opp, ourMTR );
                var ourPTKO = _ai.Projection.Get_PotentialToKOResult( ourEDR, ourMTR, opp.CurrentHPR ).PTKO;

                _ai.CurrentLog.Add( $"Move: {move.MoveSO.Name}, Target: {opp.Name}, ourPTKO: {ourPTKO} ({(int)ourPTKO})." );

                chosenMovePTKOs += Mathf.RoundToInt( (int)ourPTKO );
            }

            _ai.CurrentLog.Add( $"Previously chosen move ({attack.MovePayload.MoveSO.Name}) aggregate PTKO score: {chosenMovePTKOs}" );

            //--Get aggregate PTKO value for all moves vs likely candidates
            _ai.CurrentLog.Add( $"Getting aggregate PTKO value for All Active Moves vs Likely Switch Candidates." );
            foreach( var move in ourMon.ActiveMoves )
            {
                if( move.MoveSO.MoveCategory == MoveCategory.Status )
                    continue;

                MoveThreatResult ourMTR = new(){ Move = move };
                int ptkos = 0;

                for( int i = 0; i < likelySwitches.Count; i++ )
                {
                    var opp = likelySwitches[i];

                    ourMTR.Target = opp;
                    float effectiveness = _ai.UnitSim.Get_MoveEffectiveness( opp, move );
                    float modifier = _ai.UnitSim.Get_MoveModifier( ourMon, opp, move );
                    ourMTR.Modifier = effectiveness * modifier;

                    var ourEDR = _ai.Projection.Get_EstimatedDamageResult( ourMon, opp, ourMTR );
                    var ourPTKO = _ai.Projection.Get_PotentialToKOResult( ourEDR, ourMTR, opp.CurrentHPR ).PTKO;

                    _ai.CurrentLog.Add( $"Move: {move.MoveSO.Name}, Target: {opp.Name}, ourPTKO: {ourPTKO} ({(int)ourPTKO})." );

                    ptkos += Mathf.RoundToInt( (int)ourPTKO );
                }

                if( ptkos > coverageMovePTKOs )
                {
                    coverageMovePTKOs = ptkos;
                    bestCoverageMove = move;
                }
            }

            if( bestCoverageMove == null )
                return context;

            _ai.CurrentLog.Add( $"Coverage move ({bestCoverageMove.MoveSO.Name}) aggregate PTKO score: {coverageMovePTKOs}" );

            //--Evaluate current target with bestCoverageMove
            var currentTarget = attack.Top1.Opponent;
            var coverageMTR = new MoveThreatResult { Move = bestCoverageMove, Target = currentTarget };

            float eff = _ai.UnitSim.Get_MoveEffectiveness( currentTarget, bestCoverageMove );
            float mod = _ai.UnitSim.Get_MoveModifier( ourMon, currentTarget, bestCoverageMove );
            coverageMTR.Modifier = eff * mod;

            var coverageEDR = _ai.Projection.Get_EstimatedDamageResult( ourMon, currentTarget, coverageMTR );
            var coveragePTKO = _ai.Projection.Get_PotentialToKOResult( coverageEDR, coverageMTR, currentTarget.CurrentHPR ).PTKO;

            _ai.CurrentLog.Add( $"Coverage move aggregate PTKO score: {coverageMovePTKOs} vs Chosen Move Aggregate PTKO Score: {chosenMovePTKOs}" );

            if( chosenMovePTKOs >= coverageMovePTKOs || bestCoverageMove == attack.MovePayload || coveragePTKO < attackerPTKO - 1 )
            {
                _ai.CurrentLog.Add( $"Decided to not use coverage move, going with our previously selected move {attack.MovePayload.MoveSO.Name}!" );
                return context;
            }
            else
            {
                context.HasDecision = true;
                attack.MovePayload = bestCoverageMove;
                context.ChosenAction = attack;
                _ai.CurrentLog.Add( $"Switching to our chosen coverage move! Best Coverage move is: {bestCoverageMove.MoveSO.Name}" );
                return context;
            }
        }
        else
        {
            _ai.CurrentLog.Add( $"Not going to read the switch, continuing with chosen attack ({attack.MovePayload.MoveSO.Name})" );
            return context;
        }
    }

    private ReasoningContext StrategicSacrifice( ReasoningContext context )
    {
        return context;
    }

    private ReasoningContext AttackVSHazards( ReasoningContext context)
    {
        return context;
    }
}

public enum ReasoningRule
{
    Attack_VS_Setup,
    SwitchPrediction_CoverageMove,
    StrategicSacrifice,
    Attack_VS_Hazards,
}

public class ReasoningContext
{
    public ReasoningRule Rule;
    public List<ActionEvaluation> Actions;
    public ExchangeEvaluation EE;
    public BoardContext BC;
    public CurrentPlan Plan;
    public ThreatProfile TP;

    public bool HasDecision;
    public ActionEvaluation ChosenAction;
}
