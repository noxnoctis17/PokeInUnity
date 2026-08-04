using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing.Design;
using System.Linq;
using UnityEngine;

public enum SimModuleType { Attack, Switch, Setup, OffensiveStatus, SupportiveStatus, Protect }
public class BattleAI_BattleSim
{
    private BattleAI _ai;
    private BattleAI_UnitSim _unitSim;
    private BattleAI_Projection _proj;
    private List<Action<SimulatedUnit, List<SimulatedUnit>, SimulatedField, bool /*phase tick*/>> _roundEndPhases;
    private int _rounds;
    private const float HP_EPSILON = 0.0009f;
    public Dictionary<string, Func<IBattleAIUnit, IBattleAIUnit, Move, bool>> MoveSuccess { get; private set; }

    public BattleAI_BattleSim( BattleAI ai )
    {
        _ai = ai;
        _unitSim = _ai.UnitSim;
        _proj = _ai.Projection;
        MoveSuccessDicInit();
        BuildRoundEndPhaseList();
        _rounds = 0;
    }

    public SimulationModule BuildSimModule( SimModuleType type, int priority, SimulatedUnit attacker, List<SimulatedUnit> targets )
    {
        Action<SimulatedUnit, SimulatedUnit, SimulatedField> module = type switch
        {
            SimModuleType.Attack => RunAttackModule,
            SimModuleType.Switch => RunSwitchModule,
            SimModuleType.Setup => RunSetupModule,
            SimModuleType.OffensiveStatus => RunOffensiveStatusModule,
            SimModuleType.SupportiveStatus => RunSupportiveStatusModule,
            _ => RunAttackModule,
        };

        SimulationModule sm = new( type, priority, attacker, targets, module );

        return sm;
    }

    public SimulationPackage BuildSimPackage( SimulatedUnit unit, List<SimulatedUnit> targets, SimModuleType moduleType )
    {
        SimulationPackage sp = new()
        {
            Unit = unit,
            Targets = targets.ToList(),
            ModuleType = moduleType,
            Exists = true,
        };

        return sp;
    }

    public RoundPackage BuildRoundPackage( SimulationPackage attackerPack, SimulationPackage attackerAllyPack, SimulationPackage opponentPack, SimulationPackage opponentAllyPack )
    {
        RoundPackage rp = new()
        {
            AttackerPack = attackerPack,
            OpponentPack = opponentPack,
        };

        if( attackerAllyPack.Exists )
            rp.AttackerAllyPack = attackerAllyPack;

        if( opponentAllyPack.Exists )
            rp.OpponentAllyPack = opponentAllyPack;

        return rp;
    }

    public BattleSimEvent BuildBattleSimEvent( RoundPackage roundPack, SimulatedField field, int depth = 1 )
    {
        const int priority_offset = (int)MovePriority.Zero;
        
        //--We need to establish all participating units and their relationships. it may be best to simply include fields for attackerAllyPack and opponentAllyPack, or restructure SimulationPackage to contain
        //--everything necessary. Or, rather, yet another struct called RoundPackage, that simply has 4 SimulationPackages in it. We build RoundPackage and feed it into BBSE, and use a some bools
        //--to make sure targeting is correct. bool targetIsOpponent, bool targetIsAlly, bool attackerSpreadMove, bool opponentSpreadMove, bool allySpreadMove, bool opponentAllySpreadMove
        //--i don't know if i want intentTOP to simulate a full exchange between all 4 pokemon, or simply the results of the current pokemon vs its chosen target + any side effects, such as the results of
        //--being hit when using a support move on its side or on its ally, or the results of it or its target using spread moves. i don't think it's necessary to produce a full round and get intended actions
        //--from all 4 pokemon, we can use ExchangePack to check both side's ally PTKOs and get estimated damage from that where and when necessary.

        SimulationPackage attackerPack = roundPack.AttackerPack;
        SimulationPackage opponentPack = roundPack.OpponentPack;

        SimulationPackage allyPack = default;
        SimulationPackage opponentAllyPack = default;

        SimulatedUnit attacker = roundPack.AttackerPack.Unit;
        SimulatedUnit opponent = roundPack.OpponentPack.Unit;

        SimulatedUnit attackerAlly = null;
        SimulatedUnit opponentAlly = null;

        _unitSim.TurnSimLog.Add( $"===[Building Battle Simulation Event ({attacker.Name}'s {roundPack.AttackerPack.ModuleType} vs {opponent.Name}'s {opponentPack.ModuleType})]===" );

        _unitSim.TurnSimLog.Add( $"RoundPack.AttackerAllyPack Exists: {roundPack.AttackerAllyPack.Exists}" );
        if( roundPack.AttackerAllyPack.Exists )
        {
            allyPack = roundPack.AttackerAllyPack;
            attackerAlly = roundPack.AttackerAllyPack.Unit;
            _unitSim.TurnSimLog.Add( $"RoundPack.AttackerAllyPack exists, setting allyPack. Unit: {allyPack.Unit.Name}, Module: {allyPack.ModuleType}, Exists: {allyPack.Exists}" );
        }

        _unitSim.TurnSimLog.Add( $"RoundPack.OpponentAllyPack Exists: {roundPack.OpponentAllyPack.Exists}" );
        if( roundPack.OpponentAllyPack.Exists )
        {
            opponentAllyPack = roundPack.OpponentAllyPack;
            opponentAlly = roundPack.OpponentAllyPack.Unit;
            _unitSim.TurnSimLog.Add( $"OpponentAlly Pack exists, setting opponentAllyPack. Unit: {opponentAllyPack.Unit.Name}, Module: {opponentAllyPack.ModuleType}, Exists: {opponentAllyPack.Exists}" );
        }

        //--All units list for round end effect processing
        var units = new List<SimulatedUnit> { attacker, opponent };

        if( attackerAlly != null )
        {
            units.Add( attackerAlly );
            _unitSim.TurnSimLog.Add( $"BSE {attacker.Name}'s ally is: {attackerAlly?.Name}" );
        }

        if( opponentAlly != null )
        {
            units.Add( opponentAlly );
            _unitSim.TurnSimLog.Add( $"BSE {opponent.Name}'s ally is: {opponentAlly?.Name}" );
        }

        units.Sort( ( a, b ) => b.Speed.CompareTo( a.Speed ) );

        //--Module Command/Move Priority assignment block
        int attackerPriority = attackerPack.ModuleType == SimModuleType.Switch ? 99 : attacker.MTR?.Move != null ? ( (int)attacker.MTR.Move.Priority - priority_offset ) : ( (int)MovePriority.Zero - priority_offset );
        int opponentPriority = opponentPack.ModuleType == SimModuleType.Switch ? 99 : opponent.MTR?.Move != null ? ( (int)opponent.MTR.Move.Priority - priority_offset ) : ( (int)MovePriority.Zero - priority_offset );
        int attackerAllyPriority = allyPack.Exists ? allyPack.ModuleType == SimModuleType.Switch ? 99 : attackerAlly.MTR?.Move != null ? ( (int)attackerAlly.MTR.Move.Priority - priority_offset ) : ( (int)MovePriority.Zero - priority_offset ) : -99;
        int opponentAllyPriority = opponentAllyPack.Exists ? opponentAllyPack.ModuleType == SimModuleType.Switch ? 99 : opponentAlly.MTR?.Move != null ? ( (int)opponentAlly.MTR.Move.Priority - priority_offset ) : ( (int)MovePriority.Zero - priority_offset ) : -99;

        //--Build Sim Module
        SimulationModule attackerModule = BuildSimModule( attackerPack.ModuleType, attackerPriority, attacker, attackerPack.Targets );
        SimulationModule opponentModule = BuildSimModule( opponentPack.ModuleType, opponentPriority, opponent, opponentPack.Targets );
        
        List<SimulationModule> modules = new() { attackerModule, opponentModule, };

        if( attackerAlly != null )
        {
            SimulationModule allyModule = BuildSimModule( allyPack.ModuleType, attackerAllyPriority, attackerAlly, allyPack.Targets );
            modules.Add( allyModule );
        }

        if( opponentAlly != null )
        {
            SimulationModule allyModule = BuildSimModule( opponentAllyPack.ModuleType, opponentAllyPriority, opponentAlly, opponentAllyPack.Targets );
            modules.Add( allyModule );
        }

        //--Sort modules in appropriate priority -> speed orders (which also considers trick room now)
        ReOrderModules( ref modules, field );
        bool attMovesFirst = modules[0].Attacker.Pokemon == attacker.Pokemon;

        _unitSim.TurnSimLog.Add( $"[Turn Simulation] Attacker ({attacker.Name}) Speed: {attacker.Speed}. Opponent ({opponent.Name}) Speed: {opponent.Speed}." );
        _unitSim.TurnSimLog.Add( $"[Turn Simulation] Attacker ({attacker.Name}) Move Priority: {attackerPriority}. Opponent ({opponent.Name}) Move Priority {opponentPriority}." );
        _unitSim.TurnSimLog.Add( $"[Turn Simulation] Attacker ({attacker.Name}) Moves First: {attMovesFirst}." );
        _unitSim.TurnSimLog.Add( $"" );

        //--Build BSE
        BattleSimEvent bse = new()
        {
            Depth = depth,

            Attacker = attacker,
            Opponent = opponent,
            AttackerAlly = attackerAlly,
            OpponentAlly = opponentAlly,

            ActiveUnits = units,
            SimModules = modules,

            Field = field,

            AttackerMovesFirst = modules[0].Attacker.Pokemon == attacker.Pokemon,
            OpponentMovedFirst = modules[0].Attacker.Pokemon == opponent.Pokemon,
            AttackerAllyMovedFirst = attackerAlly != null && modules[0].Attacker.Pokemon == attackerAlly.Pokemon,
            OpponentAllyMovedFirst = opponentAlly != null && modules[0].Attacker.Pokemon == opponentAlly.Pokemon,

            Attacker_CanAct = _unitSim.CanActOnTurn( attacker ),
            Opponent_CanAct = _unitSim.CanActOnTurn( opponent ),
            AttackerAlly_CanAct = attackerAlly != null && _unitSim.CanActOnTurn( attackerAlly ),
            OpponentAlly_CanAct = opponentAlly != null && _unitSim.CanActOnTurn( opponentAlly ),
        };

        _unitSim.TurnSimLog.Add( $"Attacker {bse.Attacker.Name} (HPR: {bse.Attacker.BeginningHPR})" );
        _unitSim.TurnSimLog.Add( $"Opponent {bse.Opponent.Name} (HPR: {bse.Opponent.BeginningHPR})" );
        _unitSim.TurnSimLog.Add( $"Attacker Ally {bse.AttackerAlly?.Name} (HPR: {bse.AttackerAlly?.BeginningHPR}) ({attackerAlly?.Name}, {attackerAlly?.BeginningHPR})" );
        _unitSim.TurnSimLog.Add( $"Opponent Ally {bse.OpponentAlly?.Name} (HPR: {bse.OpponentAlly?.BeginningHPR}) ({opponentAlly?.Name}, {opponentAlly?.BeginningHPR})" );
        _unitSim.TurnSimLog.Add( $"Total Units: {bse.ActiveUnits.Count} ({units.Count}), Total Modules: {bse.SimModules.Count} ({modules.Count})" );
        _unitSim.TurnSimLog.Add( $"" );

        return bse;
    }

    public List<SimulatedUnit> GetTOPTargets( SimulatedUnit attacker, SimulatedUnit opponent, SimulatedUnit attackerAlly, SimulatedUnit opponentAlly, MoveThreatResult mtr )
    {
        List<SimulatedUnit> targets = new();

        if( IsSimTarget( attacker, mtr.Targets ) )
            targets.Add( attacker );

        if( IsSimTarget( attackerAlly, mtr.Targets ) )
            targets.Add( attackerAlly );

        if( IsSimTarget( opponent, mtr.Targets ) )
            targets.Add( opponent );

        if( IsSimTarget( opponentAlly, mtr.Targets ) )
            targets.Add( opponentAlly );

        return targets;
    }

    private bool IsSimTarget( SimulatedUnit unit, List<IBattleAIUnit> targets )
    {
        if( targets == null || targets?.Count <= 0 )
            return false;

        foreach( var target in targets )
        {
            if( target?.Pokemon == unit?.Pokemon )
            {
                return true;
            }
        }

        return false;
    }

    private TurnOutcomeProjection BuildTOP( BattleSimEvent bse, bool log = false )
    {
        TurnOutcomeProjection top = new()
        {
            Field = bse.Field, //--We currently do not make any increments to field. this feature should be expanded on to account for duration tics and such.

            ReplacedUnits = new(),

            Depth = bse.Depth,

            Attacker = bse.Attacker,
            Opponent = bse.Opponent,
            AttackerAlly = bse.AttackerAlly,
            OpponentAlly = bse.OpponentAlly,

            AttackerPTKO = bse.Attacker.MTR != null ? bse.Attacker.MTR.PTKO : default,
            OpponentPTKO = bse.Opponent.MTR != null ? bse.Opponent.MTR.PTKO : default,
            AttackerAllyPTKO = bse.AttackerAlly != null ? bse.Attacker.MTR.PTKO : default,
            OpponentAllyPTKO = bse.OpponentAlly != null ? bse.Opponent.MTR.PTKO : default,

            Attacker_EndOfTurnHP = bse.Attacker.EndHPR,
            Opponent_EndOfTurnHP = bse.Opponent.EndHPR,

            Attacker_DiesBeforeActing = bse.Attacker_DiesBeforeActing,
            Opponent_DiesBeforeActing = bse.Opponent_DiesBeforeActing,

            AttackerCanAct = bse.Attacker_CanAct,
            OpponentCanAct = bse.Opponent_CanAct,

            MutualKO = bse.Attacker.EndHPR <= 0f && bse.Opponent.EndHPR <= 0f,

            AttackerMovedFirst = bse.AttackerMovesFirst,
            OpponentMovedFirst = bse.OpponentMovedFirst,
            AttackerAllyMovedFirst = bse.AttackerAllyMovedFirst,
            OpponentAllyMovedFirst = bse.OpponentAllyMovedFirst,
        };

        _unitSim.LogTop( top );
        // top.SimulationLog = _unitSim.TurnSimLog.ToString();

        if( log )
            Debug.Log( _unitSim.TurnSimLog.ToString() );

        _unitSim.TurnSimLog.Clear();

        _rounds = 0;

        return top;
    }

    public TurnOutcomeProjection BuildIntentTOP( ActionType action, IActionResult ourResult, ThreatIntentResult tir )
    {
        MoveThreatResult ourMTR = null;
        MoveThreatResult theirMTR = null;

        IBattleAIUnit attacker = _ai.GetPokemonAs_IBattleAIUnit( ourResult.Top.Attacker.Pokemon );
        IBattleAIUnit opponent = _ai.GetPokemonAs_IBattleAIUnit( tir.PrimaryIntent.IntentResult.Top.Attacker.Pokemon );

        SimModuleType attackerModule = SimModuleType.Attack;
        SimModuleType opponentModule = SimModuleType.Attack;

        CustomLogSession intentLog = new();

        intentLog.Add( $"===============================" );
        intentLog.Add( $"=====[Building Intent TOP]=====" );
        intentLog.Add( $"===============================" );
        intentLog.Add( $"" );

        //----------------------------------------------------------------------------
        //--[Our Action]--------------------------------------------------------------
        //----------------------------------------------------------------------------
        intentLog.Add( $"Our Action: {action}" );
        switch( action )
        {
            case ActionType.Attack:

                var attack = (MoveThreatResult)ourResult;
                ourMTR = attack;
                attackerModule = SimModuleType.Attack;

                intentLog.Add( $"Attacker {attacker.Name} ({attacker.BeginningHPR}/{attacker.EndHPR}) with move {ourMTR.Move.MoveSO.Name}" );

            break;

            case ActionType.DefensiveSwitch:

                var defSwitch = (SwitchCandidateResult)ourResult;
                // attacker = defSwitch.Top.Attacker;
                attackerModule = SimModuleType.Switch;

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Targets = new() { attacker },
                    TargetBattleUnits = null,
                    Move = null,
                    EstimatedDamage = 0,
                };

                intentLog.Add( $"Defensive Switch Candidate {attacker.Name} ({attacker.BeginningHPR}/{attacker.EndHPR})." );

            break;

            case ActionType.OffensiveSwitch:

                var offSwitch = (SwitchCandidateResult)ourResult;
                attackerModule = SimModuleType.Switch;

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Targets = new() { attacker },
                    TargetBattleUnits = null,
                    Move = null,
                    EstimatedDamage = 0,
                };

                intentLog.Add( $"Offensive Switch Candidate {attacker.Name} ({attacker.BeginningHPR}/{attacker.EndHPR})." );

            break;

            case ActionType.Setup:

                var setup = (SetupThreatResult)ourResult;
                attackerModule = SimModuleType.Setup;

                _unitSim.UndoStageDelta( attacker, setup.StageDelta );

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Targets = setup.Targets,
                    TargetBattleUnits = setup.TargetBattleUnits,
                    Move = setup.Move,
                    EstimatedDamage = 0f,
                };

                intentLog.Add( $"Attacker {attacker.Name} ({attacker.BeginningHPR}/{attacker.EndHPR}) with move {ourMTR.Move.MoveSO.Name}" );

            break;

            case ActionType.OffensiveStatus:

                var offStatus = (StatusThreatResult)ourResult;
                attackerModule = SimModuleType.OffensiveStatus;

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Targets = offStatus.Targets,
                    TargetBattleUnits = offStatus.TargetBattleUnits,
                    Move = offStatus.Move,
                    EstimatedDamage = 0f,
                };

                intentLog.Add( $"Attacker {attacker.Name} ({attacker.BeginningHPR}/{attacker.EndHPR}) with move {ourMTR.Move.MoveSO.Name}" );

            break;

            case ActionType.SupportiveStatus:

                var suppStatus = (StatusThreatResult)ourResult;
                attackerModule = SimModuleType.SupportiveStatus;

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Targets = suppStatus.Targets,
                    TargetBattleUnits = suppStatus.TargetBattleUnits,
                    Move = suppStatus.Move,
                    EstimatedDamage = 0f,
                };

                intentLog.Add( $"Attacker {attacker?.Name} ({attacker?.BeginningHPR}/{attacker?.EndHPR}) with move {ourMTR?.Move?.MoveSO.Name}" );

            break;
        }

        //----------------------------------------------------------------------------
        //--[Their Action]------------------------------------------------------------
        //----------------------------------------------------------------------------
        intentLog.Add( $"" );
        intentLog.Add( $"Their Action: {tir.PrimaryIntent} (Confidence: {tir.Confidence}, Evidence: {tir.PrimaryIntent.Evidence})" );
        switch( tir.PrimaryIntent.IntentType )
        {
            case IntentType.Attack:

                var attack = (MoveThreatResult)tir.PrimaryIntent.IntentResult;
                // opponent = attack.Top.Attacker;
                opponentModule = SimModuleType.Attack;
                theirMTR = attack;

                intentLog.Add( $"Attacker {opponent.Name} ({opponent.BeginningHPR}/{opponent.EndHPR}) with move {theirMTR.Move.MoveSO.Name}" );

            break;

            case IntentType.DefensiveSwitch:

                var defSwitch = (SwitchCandidateResult)tir.PrimaryIntent.IntentResult;
                opponentModule = SimModuleType.Switch;

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Targets = new() { opponent },
                    TargetBattleUnits = null,
                    Move = null,
                    EstimatedDamage = 0,
                };

                intentLog.Add( $"Defensive Switch Candidate {opponent.Name} ({opponent.BeginningHPR}/{opponent.EndHPR})" );

            break;

            case IntentType.OffensiveSwitch:

                var offSwitch = (SwitchCandidateResult)tir.PrimaryIntent.IntentResult;
                opponentModule = SimModuleType.Switch;

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Targets = new() { opponent },
                    TargetBattleUnits = null,
                    Move = null,
                    EstimatedDamage = 0,
                };

                intentLog.Add( $"Offensive Switch Candidate {opponent.Name} ({opponent.BeginningHPR}/{opponent.EndHPR})" );

            break;

            case IntentType.Setup:

                var setup = (SetupThreatResult)tir.PrimaryIntent.IntentResult;
                // opponent = setup.Top.Attacker;
                opponentModule = SimModuleType.Setup;

                _unitSim.UndoStageDelta( opponent, setup.StageDelta );

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Targets = setup.Targets,
                    TargetBattleUnits = setup.TargetBattleUnits,
                    Move = setup.Move,
                    EstimatedDamage = 0f,
                };

                intentLog.Add( $"Attacker {opponent.Name} ({opponent.BeginningHPR}/{opponent.EndHPR}) with move {theirMTR.Move.MoveSO.Name}" );

            break;

            case IntentType.OffensiveStatus:

                var offStatus = (StatusThreatResult)tir.PrimaryIntent.IntentResult;
                // opponent = offStatus.Top.Attacker;
                opponentModule = SimModuleType.OffensiveStatus;

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Targets = offStatus.Targets,
                    TargetBattleUnits = offStatus.TargetBattleUnits,
                    Move = offStatus.Move,
                    EstimatedDamage = 0f,
                };

                intentLog.Add( $"Attacker {opponent.Name} ({opponent.BeginningHPR}/{opponent.EndHPR}) with move {theirMTR.Move.MoveSO.Name}" );

            break;

            case IntentType.SupportiveStatus:

                var suppStatus = (StatusThreatResult)tir.PrimaryIntent.IntentResult;
                // opponent = suppStatus.Top.Attacker;
                opponentModule = SimModuleType.SupportiveStatus;

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Targets = suppStatus.Targets,
                    TargetBattleUnits = suppStatus.TargetBattleUnits,
                    Move = suppStatus.Move,
                    EstimatedDamage = 0f,
                };

                intentLog.Add( $"Attacker {opponent.Name} ({opponent.BeginningHPR}/{opponent.EndHPR}) with move {theirMTR.Move.MoveSO.Name}" );

            break;
        }

        intentLog.Add( $"" );
        intentLog.Add( $"Final Information for Battle Simulation Event:" );

        float ourHPR                        = attacker.BeginningHPR;
        float theirHPR                      = opponent.BeginningHPR;
        
        var ourEDR                          = _proj.Get_EstimatedDamageResult( attacker, opponent, ourMTR );
        var theirEDR                        = _proj.Get_EstimatedDamageResult( opponent, attacker, theirMTR );

        PotentialToKO ourPTKO               = _proj.Get_PotentialToKOResult( ourEDR, ourMTR, opponent ).PTKO;
        PotentialToKO theirPTKO             = _proj.Get_PotentialToKOResult( theirEDR, theirMTR, attacker ).PTKO;

        var fieldSim                        = _ai.UnitSim.BuildSimField();

        var attackerSimUnit                 = _ai.UnitSim.BuildSimUnit( attacker, ourHPR, ourMTR, fieldSim );
        var opponentSimUnit                 = _ai.UnitSim.BuildSimUnit( opponent, theirHPR, theirMTR, fieldSim );

        var ally = _ai.GetActiveAllyAs_Adapter( attacker.Pokemon );
        var opponentAlly = _ai.GetActiveAllyAs_Adapter( opponent.Pokemon );

        SimulatedUnit allySimUnit = null;
        SimulatedUnit opponentAllySimUnit = null;

        MoveThreatResult allyMTR = null;
        MoveThreatResult opponentAllyMTR = null;

        //--Targeting here will completely rely on PairIntentResult + CoordinationIntentResult
        if( ally != null )
        {
            var allyTarget = opponentAlly ?? opponent;
            allyMTR = _ai.CandidateSelect.GetMove_BestAttack( ally, allyTarget, false, "Our Ally best attack on opponent's ally or opponent if they have no ally" );
            allySimUnit = _ai.UnitSim.BuildSimUnit( ally, ally.BeginningHPR, allyMTR, fieldSim );
        }
        
        if( opponentAlly != null )
        {
            var opponentAllyTarget = ally ?? attacker;
            opponentAllyMTR = _ai.CandidateSelect.GetMove_BestAttack( opponentAlly, opponentAllyTarget, false, "Opponent's Ally best attack on our ally or us if we have no ally" );
            opponentAllySimUnit = _ai.UnitSim.BuildSimUnit( opponentAlly, opponentAlly.BeginningHPR, opponentAllyMTR, fieldSim );
        }

        List<SimulatedUnit> attackerTargets = GetTOPTargets( attackerSimUnit, opponentSimUnit, allySimUnit, opponentAllySimUnit, ourMTR );
        List<SimulatedUnit> opponentTargets = GetTOPTargets( opponentSimUnit, attackerSimUnit, opponentAllySimUnit, allySimUnit, theirMTR );
        List<SimulatedUnit> allyTargets = ally != null ? GetTOPTargets( attackerSimUnit, opponentSimUnit, allySimUnit, opponentAllySimUnit, allyMTR ) : new();
        List<SimulatedUnit> opponentAllyTargets = opponentAlly != null ? GetTOPTargets( attackerSimUnit, opponentSimUnit, allySimUnit, opponentAllySimUnit, opponentAllyMTR ) : new();

        SimulationPackage attackerPack = BuildSimPackage( attackerSimUnit, attackerTargets, attackerModule );
        SimulationPackage opponentPack = BuildSimPackage( opponentSimUnit, opponentTargets, opponentModule );

        SimulationPackage allyPack = ally != null ? BuildSimPackage( allySimUnit, allyTargets, SimModuleType.Attack ) : default;
        SimulationPackage opponentAllyPack = opponentAlly != null ? BuildSimPackage( opponentAllySimUnit, opponentAllyTargets, SimModuleType.Attack ) : default;

        intentLog.Add( $"" );
        intentLog.Add( $"Our Action: {ourResult.ActionType}, Their Intent: {tir.PrimaryIntent.IntentType}" );
        intentLog.Add( $"" );

        intentLog.Add( $"Attacker: {attacker.Name}. HPR: {ourHPR}. Item: {attacker.Item}. EDR: {ourEDR}. PTKO: {ourPTKO}" );
        intentLog.Add( $"Attacker Sim Unit: {attackerSimUnit.Name}. Item: {attackerSimUnit.Item}. HPR: {attackerSimUnit.BeginningHPR}. Move: {attackerSimUnit.MTR?.Move?.MoveSO.Name}" );
        intentLog.Add( $"" );
        intentLog.Add( $"Attacker Ally: {ally?.Name}. HPR: {ourHPR}. Item: {ally?.Item}." );
        intentLog.Add( $"Attacker Ally Sim Unit: {allySimUnit?.Name}. Item: {allySimUnit?.Item}. HPR: {allySimUnit?.BeginningHPR}. Move: {allySimUnit?.MTR?.Move?.MoveSO.Name}" );
        intentLog.Add( $"" );
        intentLog.Add( $"Opponent: {opponent.Name}. HPR: {theirHPR}. Item: {opponent.Item}. EDR: {theirEDR}. PTKO: {theirPTKO}" );
        intentLog.Add( $"Opponent Sim Unit: {opponentSimUnit?.Name}. Item: {opponentSimUnit?.Item}. HPR: {opponentSimUnit?.BeginningHPR}. Move: {opponentSimUnit?.MTR?.Move?.MoveSO.Name}" );
        intentLog.Add( $"" );
        intentLog.Add( $"Opponent Ally: {opponentAlly?.Name}. HPR: {ourHPR}. Item: {opponentAlly?.Item}." );
        intentLog.Add( $"Opponent Ally Sim Unit: {opponentAllySimUnit?.Name}. Item: {opponentAllySimUnit?.Item}. HPR: {opponentAllySimUnit?.BeginningHPR}. Move: {opponentAllySimUnit?.MTR?.Move?.MoveSO.Name}" );
        intentLog.Add( $"" );

        Debug.Log( intentLog.ToString() );
        intentLog.Clear();
        
        var roundPack = BuildRoundPackage( attackerPack, allyPack, opponentPack, opponentAllyPack );
        var bse = BuildBattleSimEvent( roundPack, fieldSim );
        
        return RunSimulation( bse, true );
    }

    public TurnOutcomeProjection BuildLookAheadTOP( TurnOutcomeProjection top1, bool log = false )
    {
        CustomLogSession lookaheadLog = new();

        lookaheadLog.Add( $"===================================" );
        lookaheadLog.Add( $"=====[Building Look Ahead TOP]=====" );
        lookaheadLog.Add( $"===================================" );
        lookaheadLog.Add( $"" );

        IBattleAIUnit attacker = top1.Attacker_EndOfTurnHP > 0f ? top1.Attacker : _ai.CandidateSelect.GetSwitch_Revenge( _ai.Blackboard.TheirActiveBattleAIUnits ).Candidate;
        attacker ??= top1.Attacker;
        _unitSim.UpdateUnitForLookAhead( ref attacker );
        bool attackerWasKOd = attacker.Pokemon != top1.Attacker.Pokemon;

        IBattleAIUnit opponent = top1.Opponent_EndOfTurnHP > 0f ? top1.Opponent : _ai.CandidateSelect.GetSwitch_Revenge( _ai.Blackboard.OurActiveBattleAIUnits ).Candidate;
        opponent ??= top1.Opponent;
        _unitSim.UpdateUnitForLookAhead( ref opponent );
        bool opponentWasKOd = opponent.Pokemon != top1.Opponent.Pokemon;

        MoveThreatResult ourMTR = _ai.CandidateSelect.GetMove_BestAttack( attacker, opponent );
        MoveThreatResult theirMTR = _ai.CandidateSelect.GetMove_BestAttack( opponent, attacker );

        SimModuleType attackerModule = SimModuleType.Attack;
        SimModuleType opponentModule = SimModuleType.Attack;

        float ourHPR                        = attacker.BeginningHPR;
        float theirHPR                      = opponent.BeginningHPR;
        
        var ourEDR                          = _proj.Get_EstimatedDamageResult( attacker, opponent, ourMTR );
        var theirEDR                        = _proj.Get_EstimatedDamageResult( opponent, attacker, theirMTR );

        PotentialToKO ourPTKO               = _proj.Get_PotentialToKOResult( ourEDR, ourMTR, opponent ).PTKO;
        PotentialToKO theirPTKO             = _proj.Get_PotentialToKOResult( theirEDR, theirMTR, attacker ).PTKO;

        var fieldSim                        = _ai.UnitSim.CopySimField( top1.Field );

        var attackerSimUnit                 = _ai.UnitSim.BuildSimUnit( attacker, ourHPR, ourMTR, fieldSim );
        var opponentSimUnit                 = _ai.UnitSim.BuildSimUnit( opponent, theirHPR, theirMTR, fieldSim );

        var ally = top1.AttackerAlly;
        var opponentAlly = top1.OpponentAlly;

        SimulatedUnit allySimUnit = null;
        SimulatedUnit opponentAllySimUnit = null;

        MoveThreatResult allyMTR = null;
        MoveThreatResult opponentAllyMTR = null;

        if( ally != null )
        {
            allyMTR = _ai.CandidateSelect.GetMove_BestAttack( ally, opponent, false, "Ally best attack on current target" );
            allySimUnit = _ai.UnitSim.BuildSimUnit( ally, ally.BeginningHPR, allyMTR, fieldSim );
        }

        if( opponentAlly != null )
        {
            opponentAllyMTR = _ai.CandidateSelect.GetMove_BestAttack( opponentAlly, attacker, false, "Target's Ally best attack on current target" );
            opponentAllySimUnit = _ai.UnitSim.BuildSimUnit( opponentAlly, opponentAlly.BeginningHPR, opponentAllyMTR, fieldSim );
        }

        List<SimulatedUnit> attackerTargets = GetTOPTargets( attackerSimUnit, opponentSimUnit, allySimUnit, opponentAllySimUnit, ourMTR );
        List<SimulatedUnit> opponentTargets = GetTOPTargets( opponentSimUnit, attackerSimUnit, opponentAllySimUnit, allySimUnit, theirMTR );
        List<SimulatedUnit> allyTargets = ally != null ? GetTOPTargets( attackerSimUnit, opponentSimUnit, allySimUnit, opponentAllySimUnit, allyMTR ) : new();
        List<SimulatedUnit> opponentAllyTargets = opponentAlly != null ? GetTOPTargets( attackerSimUnit, opponentSimUnit, allySimUnit, opponentAllySimUnit, opponentAllyMTR ) : new();

        SimulationPackage attackerPack = BuildSimPackage( attackerSimUnit, attackerTargets, attackerModule );
        SimulationPackage opponentPack = BuildSimPackage( opponentSimUnit, opponentTargets, opponentModule );

        SimulationPackage allyPack = ally != null ? BuildSimPackage( allySimUnit, allyTargets, SimModuleType.Attack ) : default;
        SimulationPackage opponentAllyPack = opponentAlly != null ? BuildSimPackage( opponentAllySimUnit, opponentAllyTargets, SimModuleType.Attack ) : default;

        //--For when look ahead uses threat intent and some self action assumption/branching.
        // lookaheadLog.Add( $"" );
        // lookaheadLog.Add( $"Our Action: {ourResult.ActionType}, Their Intent: {tir.PrimaryIntent.IntentType}" );
        // lookaheadLog.Add( $"" );

        lookaheadLog.Add( $"Attacker: {attacker.Name}. HPR: {ourHPR}. Item: {attacker.Item}. EDR: {ourEDR}. PTKO: {ourPTKO}" );
        lookaheadLog.Add( $"Attacker Sim Unit: {attackerSimUnit.Name}. Item: {attackerSimUnit.Item}. HPR: {attackerSimUnit.BeginningHPR}. Move: {attackerSimUnit.MTR?.Move?.MoveSO.Name}" );
        lookaheadLog.Add( $"" );
        lookaheadLog.Add( $"Attacker Ally: {ally?.Name}. HPR: {ourHPR}. Item: {ally?.Item}." );
        lookaheadLog.Add( $"Attacker Ally Sim Unit: {allySimUnit?.Name}. Item: {allySimUnit?.Item}. HPR: {allySimUnit?.BeginningHPR}. Move: {allySimUnit?.MTR?.Move?.MoveSO.Name}" );
        lookaheadLog.Add( $"" );
        lookaheadLog.Add( $"Opponent: {opponent.Name}. HPR: {theirHPR}. Item: {opponent.Item}. EDR: {theirEDR}. PTKO: {theirPTKO}" );
        lookaheadLog.Add( $"Opponent Sim Unit: {opponentSimUnit.Name}. Item: {opponentSimUnit.Item}. HPR: {opponentSimUnit.BeginningHPR}. Move: {opponentSimUnit?.MTR?.Move?.MoveSO.Name}" );
        lookaheadLog.Add( $"" );
        lookaheadLog.Add( $"Opponent Ally: {opponentAlly?.Name}. HPR: {ourHPR}. Item: {opponentAlly?.Item}." );
        lookaheadLog.Add( $"Opponent Ally Sim Unit: {opponentAllySimUnit?.Name}. Item: {opponentAllySimUnit?.Item}. HPR: {opponentAllySimUnit?.BeginningHPR}. Move: {opponentAllySimUnit?.MTR?.Move?.MoveSO.Name}" );
        lookaheadLog.Add( $"" );
        
        int depth = top1.Depth + 1; //--Simulation Round Depth Increase. a top1 depth of 1 should result in top2 depth of 2
        lookaheadLog.Add( $"Depth: {depth}" );
        lookaheadLog.Add( $"" );

        Debug.Log( lookaheadLog.ToString() );
        lookaheadLog.Clear();

        var roundPack = BuildRoundPackage( attackerPack, allyPack, opponentPack, opponentAllyPack );
        var bse2 = BuildBattleSimEvent( roundPack, fieldSim, depth );

        var top2 = RunSimulation( bse2, true );

        top2.ReplacedUnits = new();

        if( top1.Attacker.Pokemon != top2.Attacker.Pokemon )
        {
            if( attackerWasKOd )
                top2.ReplacedUnits.Add( top1.Attacker, ReplacementType.KO );

            if( top1.Attacker.Phazed )
                top2.ReplacedUnits.Add( top1.Attacker, ReplacementType.Phaze );

            //--Eventually if we add look ahead ThreatIntent, and possibly our own switch potential reasoning, that goes here
        }

        if( top1.Opponent.Pokemon != top2.Opponent.Pokemon )
        {
            if( opponentWasKOd )
                top2.ReplacedUnits.Add( top1.Opponent, ReplacementType.KO );

            if( top1.Opponent.Phazed )
                top2.ReplacedUnits.Add( top1.Opponent, ReplacementType.Phaze );

            //--Eventually if we add look ahead ThreatIntent, and possibly our own switch potential reasoning, that goes here
        }

        return top2;
    }

    public TurnOutcomeProjection RunSimulation( BattleSimEvent bse, bool log = false )
    {
        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Running a Round Simulation for {bse.Attacker.Name} (@{bse.Attacker.Item}) vs {bse.Opponent.Name} (@{bse.Opponent.Item}!" );
        _unitSim.TurnSimLog.Add( $"" );
        _unitSim.LogSimField( bse.Field );

        while( bse.SimModules.Count > 0 )
        {
            var module = bse.SimModules[0];
            bse.SimModules.RemoveAt(0);

            _unitSim.TurnSimLog.Add( $"{module.Type} Module has {module.Targets.Count} target(s)!" );

            foreach( var target in module.Targets )
            {
                if( module.Priority == -99 )
                    continue; //--means there is no module

                if( module.Attacker.EndHPR <= 0f )
                {
                    _unitSim.TurnSimLog.Add( $"Module's attacker has 0hp! Skipping module..." );
                    continue;
                }

                var actualTarget = target;

                if( module.Type != SimModuleType.SupportiveStatus )
                {
                    if( actualTarget.EndHPR <= 0f )
                    {
                        if( bse.OpponentAlly != null )
                        {
                            _unitSim.TurnSimLog.Add( $"Module's target has 0 HP, changing target from {actualTarget.Name} to {bse.OpponentAlly.Name}!" );
                            if( module.Attacker.MTR?.Move?.MoveSO.MoveTarget == MoveTarget.Enemy )
                                actualTarget = bse.OpponentAlly;
                        }
                        else
                        {
                            _unitSim.TurnSimLog.Add( $"Module's target has 0 HP and target does not have an ally! Skipping module..." );
                            continue;
                        }
                    }
                }

                if( module.Attacker.Phazed )
                {
                    _unitSim.TurnSimLog.Add( $"Module's original attacker was phazed out! Skipping module..." );
                    _unitSim.TurnSimLog.Add( $"" );
                    module.Attacker.Phazed = false;
                }
                else
                    module.Module?.Invoke( module.Attacker, actualTarget, bse.Field );

                UpdateActiveUnits( bse );

                if( bse.SimModules.Count > 0 )
                    ReOrderModules( ref bse.SimModules, bse.Field );

                _unitSim.TurnSimLog.Add( $"" );
            }
        }

        bse.SimModules.Clear();

        ResolveRoundEndPhases( bse );
        _unitSim.TurnSimLog.Add( $"" );

        _unitSim.LogSimField( bse.Field );
        _unitSim.TurnSimLog.Add( $"" );

        return BuildTOP( bse, log );
    }

    private void ReOrderModules( ref List<SimulationModule> modules, SimulatedField field )
    {
        if( modules?.Count <= 0 )
            return;

        if( field.FieldConditions.ContainsKey( FieldConditionID.TrickRoom ) )
            modules = modules.OrderByDescending( m => m.Priority ).ThenBy( m => m.Attacker.Speed ).ThenByDescending( m => GetSpeedTieBreaker( m ) ).ToList();
        else
            modules = modules.OrderByDescending( m => m.Priority ).ThenByDescending( m => m.Attacker.Speed ).ThenByDescending( m => GetSpeedTieBreaker( m ) ).ToList();
    }

    private int GetSpeedTieBreaker( SimulationModule module )
    {
        bool isAIOpponent = _ai.Blackboard.TheirTeamAdapters.ContainsKey( module.Attacker.Pokemon );
        return isAIOpponent ? 1 : 0;
    }

    private void UpdateActiveUnits( BattleSimEvent bse )
    {
        bse.ActiveUnits.Clear();

        bse.ActiveUnits.Add( bse.Attacker );
        bse.ActiveUnits.Add( bse.Opponent );
    }

    private void ResolvePostMoveEffects( SimulatedUnit attacker, SimulatedUnit target, float damageDone )
    {
        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Resolving Post Move Effects for {attacker.Name} (HP {attacker.EndHPR}) attacking {target.Name} (HP {target.EndHPR})!" );

        bool attackerMakesContact = attacker.MTR.Move.MoveSO.Flags.Contains( MoveFlags.Contact );
        float attackDrainPercent = attacker.MTR.Move.MoveSO.DrainPercentage;
        HealType healType = attacker.MTR.Move.MoveSO.HealType;
        RecoilType recoilType = attacker.MTR.Move.MoveSO.Recoil.RecoilType;
        bool moveChangesStats = attacker.MTR.Move.MoveSO.MoveEffects.StatChangeList != null && attacker.MTR.Move.MoveSO.MoveEffects.StatChangeList.Count > 0;

        //--Contact
        if( attackerMakesContact )
        {
            if( target.Ability == AbilityID.RoughSkin )
                DecreaseHP( attacker, ( 1f/8f ) );

            attacker.EndHPR = Mathf.Clamp01( attacker.EndHPR );

            if( _unitSim.IsFainted( attacker ) )
                return;

            if( target.Item == ItemBattleEffectID.RockyHelmet )
                DecreaseHP( attacker, ( 1f/6f ) );

            if( _unitSim.IsFainted( attacker ) )
                return;

            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} Made contact. HP: {attacker.EndHPR}" );
        }

        //--Sitrus Berry
        if( target.Item == ItemBattleEffectID.SitrusBerry && target.EndHPR <= 0.5f && target.EndHPR > HP_EPSILON )
        {
            IncreaseHP( target, 0.25f );
            target.Item = ItemBattleEffectID.None; //--eat da berry
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {target.Name} Had a sitrus berry! HP: {target.EndHPR}" );
        }

        //--Move Effects such as drain healing and recoil happen after contact/hp change effects.
        if( attackDrainPercent > 0 )
        {
            float drain = attackDrainPercent / 100f;
            IncreaseHP( attacker, drain * damageDone );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} Used a draining move! HP: {attacker.EndHPR}" );
        }

        if( healType != HealType.None )
        {
            if( healType == HealType.PercentOfMaxHP )
            {
                float healAmount = attacker.MTR.Move.MoveSO.HealAmount; //--Just in case to avoid integer division resulting in 0 or 100
                float heal = healAmount / 100f;
                IncreaseHP( attacker, heal );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} Used a self-healing move! HP: {attacker.EndHPR}" );
            }
        }

        if( recoilType != RecoilType.None )
        {
            float recoilDamage = attacker.MTR.Move.MoveSO.Recoil.RecoilDamage;
            float recoil = recoilDamage / 100f;

            switch( recoilType )
            {
                case RecoilType.RecoilByMaxHP:
                    float maxHP = 1f;
                    DecreaseHP( attacker, maxHP * recoil );
                    break;

                case RecoilType.RecoilByDamage:
                    DecreaseHP( attacker, damageDone * recoil );
                    break;

                case RecoilType.RecoilByCurrentHP:
                    float currentHP = attacker.EndHPR;
                    DecreaseHP( attacker, currentHP * recoil );
                    break;

                default:
                    Debug.LogError( "AI Turn Projection: Unknown Recoil Effect!!" );
                    break;
            }

            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} took move recoil! HP: {attacker.EndHPR}" );

            if( _unitSim.IsFainted( attacker ) )
                return;
        }

        //--Life Orb
        if( attacker.Item == ItemBattleEffectID.LifeOrb && damageDone > 0f )
        {
            DecreaseHP( attacker, ( 1f/10f ) );

            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} took Life Orb recoil! HP: {attacker.EndHPR}" );

            if( _unitSim.IsFainted( attacker ) )
                return;
        }

        //--Knock Off
        if( attacker.MTR.Move.MoveSO.Name == "Knock Off" )
        {
            target.Item = ItemBattleEffectID.None;
        }

        //--Guaranteed Stat Changes (close combat, trailblaze, scale shot, etc.)
        if( moveChangesStats && attacker.MTR.Move.MoveSO.MoveCategory != MoveCategory.Status )
        {
            Apply_SetupMove( attacker, attacker.MTR.Move );
        }

        _unitSim.TurnSimLog.Add( $"" );
    }

    private void ResolveRoundEndPhases( BattleSimEvent bse )
    {
        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Resolving Round End Phases!" );
        bse.ActiveUnits.Sort( ( a, b ) => b.Speed.CompareTo( a.Speed ) );

        foreach( var phase in _roundEndPhases )
        {
            phase( null, bse.ActiveUnits, bse.Field, true );

            foreach( var unit in bse.ActiveUnits )
            {
                if( _unitSim.IsFainted( unit ) )
                    continue;

                phase( unit, bse.ActiveUnits, bse.Field, false );
            }
        }

        _unitSim.TurnSimLog.Add( $"" );
    }

    private float Apply_Attack( SimulatedUnit attacker, SimulatedUnit target, MoveThreatResult mtr, SimulatedField field )
    {
        //--Grab info
        float previousHPR = target.EndHPR;
        bool focusSash = target.BeginningHPR == 1f && target.Item == ItemBattleEffectID.FocusSash;

        //--Estimated Damage
        var edr = _proj.Get_EstimatedDamageResult( attacker, target, mtr, field );
        float damage = edr.DamageEstimate;

        //--Get and assign PTKO for post-top analysis
        var attackerPTKO = _proj.Get_PotentialToKOResult( edr, mtr, target ).PTKO;
        attacker.MTR.PTKO = attackerPTKO;

        //--Apply damage
        target.EndHPR -= damage;
        target.EndHPR = Mathf.Clamp01( target.EndHPR );
        target.EndHPR = Mathf.Floor( target.EndHPR * 1000f ) / 1000f;

        if( target.EndHPR <= HP_EPSILON )
        {
            target.EndHPR = focusSash ? 0.001f : 0f;

            if( focusSash )
            {
                target.Item = ItemBattleEffectID.None;
            }
        }

        return previousHPR - target.EndHPR;
    }

    private void DecreaseHP( SimulatedUnit unit, float delta )
    {
        unit.EndHPR -= delta;
        unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
        unit.EndHPR = Mathf.Floor( unit.EndHPR * 1000f ) / 1000f;

        if( unit.EndHPR <= HP_EPSILON )
            unit.EndHPR = 0f;
    }

    private void IncreaseHP( SimulatedUnit unit, float delta )
    {
        unit.EndHPR += delta;
        unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
        unit.EndHPR = Mathf.Floor( unit.EndHPR * 1000f ) / 1000f;
    }

    private void Apply_SetupMove( SimulatedUnit unit, Move move )
    {
        var delta = _unitSim.BuildStatStageDelta( move );

        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Applying Stat Changes for Unit: {unit.Name}, Move: {move?.MoveSO.Name}." );

        _unitSim.TurnSimLog.Add( $"" );
        _unitSim.TurnSimLog.Add( $"Stat Stages Before:" );
        _unitSim.TurnSimLog.Add( $"Attack: {unit.StatStages[Stat.Attack]}" );
        _unitSim.TurnSimLog.Add( $"Defense: {unit.StatStages[Stat.Defense]}" );
        _unitSim.TurnSimLog.Add( $"SpAttack: {unit.StatStages[Stat.SpAttack]}" );
        _unitSim.TurnSimLog.Add( $"SpDefense: {unit.StatStages[Stat.SpDefense]}" );
        _unitSim.TurnSimLog.Add( $"Speed: {unit.StatStages[Stat.Speed]}" );

        unit.StatStages[Stat.Attack]        = unit.StatStages[Stat.Attack]      + delta.Attack;
        unit.StatStages[Stat.Defense]       = unit.StatStages[Stat.Defense]     + delta.Defense;
        unit.StatStages[Stat.SpAttack]      = unit.StatStages[Stat.SpAttack]    + delta.SpAttack;
        unit.StatStages[Stat.SpDefense]     = unit.StatStages[Stat.SpDefense]   + delta.SpDefense;
        unit.StatStages[Stat.Speed]         = unit.StatStages[Stat.Speed]       + delta.Speed;

        _unitSim.TurnSimLog.Add( $"" );
        _unitSim.TurnSimLog.Add( $"Stat Stages After:" );
        _unitSim.TurnSimLog.Add( $"Attack: {unit.StatStages[Stat.Attack]}" );
        _unitSim.TurnSimLog.Add( $"Defense: {unit.StatStages[Stat.Defense]}" );
        _unitSim.TurnSimLog.Add( $"SpAttack: {unit.StatStages[Stat.SpAttack]}" );
        _unitSim.TurnSimLog.Add( $"SpDefense: {unit.StatStages[Stat.SpDefense]}" );
        _unitSim.TurnSimLog.Add( $"Speed: {unit.StatStages[Stat.Speed]}" );
        _unitSim.TurnSimLog.Add( $"" );
    }

    private void Apply_OffensiveStatus( SimulatedUnit target, Move move, SimulatedField field )
    {
        bool severe     = move.MoveEffects.SevereStatus     != SevereConditionID.None ;
        bool vol        = move.MoveEffects.VolatileStatus   != VolatileConditionID.None;
        bool trans      = move.MoveEffects.TransientStatus  != TransientConditionID.None;
        // bool bind       = move.MoveEffects.BindingStatus    != BindingConditionID.None; //--Consider having binding moves be part of this decision line later

        bool statusEffect   =  severe || vol || trans;
        bool court          = move.MoveEffects.CourtCondition   != CourtConditionID.None;
        bool debuff         = move.MoveEffects.StatChangeList?.Count > 0 && ( move.MoveSO.MoveEffects.Target == EffectTarget.Enemy || move.MoveSO.MoveEffects.Target == EffectTarget.OpposingSide );
        bool phaze          = move.MoveSO.MoveEffects.SwitchType == SwitchEffectType.Phaze;

        _unitSim.TurnSimLog.Add( $"Trying to apply an offensive status via {move.MoveSO.Name}!" );
        _unitSim.TurnSimLog.Add( $"" );

        if( statusEffect )
        {
            if( severe )
            {
                if( target.SevereStatus == SevereConditionID.None )
                {
                    _unitSim.SevereConditions[move.MoveEffects.SevereStatus]?.Invoke( target );
                    _unitSim.TurnSimLog.Add( $"Applying {move.MoveEffects.SevereStatus} to {target.Name}!" );
                }
                else
                {
                    _unitSim.TurnSimLog.Add( $"{target.Name} already has the {target.SevereStatus} severe status!" );
                }
            }
        }
        else if( court )
        {
            if( target.CourtLocation == CourtLocation.TopCourt )
            {
                field.TopCourtConditions.Add( move.MoveEffects.CourtCondition, -1 );
                _unitSim.TurnSimLog.Add( $"Applying {move.MoveEffects.CourtCondition} to the Top Court!" );
            }
            else if( target.CourtLocation == CourtLocation.BottomCourt )
            {
                field.BottomCourtConditions.Add( move.MoveEffects.CourtCondition, -1 );
                _unitSim.TurnSimLog.Add( $"Applying {move.MoveEffects.CourtCondition} to the Bottom Court!" );
            }
        }
        else if( debuff )
        {
            Apply_SetupMove( target, move );
        }
        else if( phaze )
        {
            _unitSim.TurnSimLog.Add( $"They phazed {target.Name} out!" );
            var targetAllies = _ai.GetRemainingAllyAdapters( target.Pokemon ).Where( p => p.Pokemon != target.Pokemon ).ToList();
            
            if( targetAllies == null || targetAllies.Count <= 0 )
            {
                _unitSim.TurnSimLog.Add( $"{target.Name} has no more allies on the bench, phazing will do nothing!" );
            }
            
            int r = UnityEngine.Random.Range( 0, targetAllies.Count );
            var replacement = targetAllies[r];

            MoveThreatResult mtr = new()
            {
                Score = 0,
                Modifier = 0,
                TargetCount = 0,
                Targets = null,
                TargetBattleUnits = null,
                Move = null,
                EstimatedDamage = 0f,
                Top = default,

                Type = ActionResultType.Switch,
                ActionType = ActionType.OffensiveSwitch,
                Candidate = null,
            };

            string prevName = target.Name;
            
            target = _unitSim.BuildSimUnit( replacement, replacement.BeginningHPR, mtr, field );
            target.Phazed = true;

            _unitSim.TurnSimLog.Add( $"Replacing {prevName} with {target.Name} ({replacement.Name})!" );

            float entryDamageTaken = Apply_HazardDamage( target );
            _unitSim.TurnSimLog.Add( $"{target.Name} took {entryDamageTaken} damage from hazards!" );

            if( target.EndHPR <= 0f )
                _unitSim.TurnSimLog.Add( $"{target.Name} fainted!" );
        }
    }

    private void Apply_SupportiveStatus( SimulatedUnit target, Move move, SimulatedField field )
    {
        var moveTarget = move.MoveSO.MoveTarget;
        var effects = move.MoveSO.MoveEffects;
        var court = target.CourtLocation == CourtLocation.TopCourt ? field.TopCourtConditions : field.BottomCourtConditions;

        bool isAllySetup = _unitSim.MoveIsSetup( move ) && effects.Target == EffectTarget.AllySide;
        bool isHelpingHand = effects.VolatileStatus == VolatileConditionID.HelpingHand;

        bool isWeather = effects.Weather != WeatherConditionID.None;
        bool isTerrain = effects.Terrain != TerrainID.None;
        bool isField = effects.FieldCondition != FieldConditionID.None;

        bool isTailwind = effects.CourtCondition == CourtConditionID.Tailwind;
        bool isScreens = effects.CourtCondition == CourtConditionID.Reflect || effects.CourtCondition == CourtConditionID.LightScreen || effects.CourtCondition == CourtConditionID.AuroraVeil;
        bool isSafeguard = effects.CourtCondition == CourtConditionID.SafeGuard;

        bool isAllyHeal = move.MoveSO.HealType != HealType.None && moveTarget == MoveTarget.Ally;
        bool isSideHeal = move.MoveSO.HealType != HealType.None && moveTarget == MoveTarget.AllySide;

        _unitSim.TurnSimLog.Add( $"Trying to apply a supportive status via {move.MoveSO.Name}!" );
        _unitSim.TurnSimLog.Add( $"" );

        if( isAllySetup )
        {
            Apply_SetupMove( target, move );
            _unitSim.TurnSimLog.Add( $"Applied setup delta!" );
        }

        if( isHelpingHand && _ai.IsDoubleBattle )
        {
            target.VolatileStatuses.Add( VolatileConditionID.HelpingHand );
            _unitSim.TurnSimLog.Add( $"Applied helping hand!" );
        }

        if( isWeather )
        {
            field.Weather = effects.Weather;
            _unitSim.TurnSimLog.Add( $"Set {effects.Weather}" );
        }

        if( isTerrain )
        {
            field.Terrain = effects.Terrain;
            _unitSim.TurnSimLog.Add( $"Set {effects.Terrain}" );
        }

        if( isField )
        {
            int duration = FieldConditionDB.Conditions[effects.FieldCondition].Duration;
            field.FieldConditions.Add( effects.FieldCondition, duration );
            _unitSim.TurnSimLog.Add( $"Set {effects.FieldCondition}" );
        }

        if( isTailwind || isScreens || isSafeguard )
        {
            int duration = CourtConditionDB.Conditions[effects.CourtCondition].Duration;
            court.Add( effects.CourtCondition, duration );
            _unitSim.TurnSimLog.Add( $"Set {effects.CourtCondition}" );
        }

        if( isAllyHeal || isSideHeal )
        {
            float healAmount = (float)move.MoveSO.HealAmount / 100f;

            target.BeginningHPR += Mathf.Clamp01( healAmount );
            target.EndHPR += Mathf.Clamp01( healAmount );

            _unitSim.TurnSimLog.Add( $"Healing target by {healAmount}, from {target.EndHPR - healAmount} to {target.EndHPR}" );
        }
    }

    private float Apply_HazardDamage( SimulatedUnit unit )
    {
        float previousHPR = unit.EndHPR;
        float damage = _ai.Get_HPRatio_AfterEntryHazards( unit );

        unit.EndHPR -= damage;
        unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
        unit.EndHPR = Mathf.Floor( unit.EndHPR * 1000f ) / 1000f;

        if( unit.EndHPR <= HP_EPSILON )
            unit.EndHPR = 0f;

        return previousHPR - unit.EndHPR;
    }

    private void Tick_WeatherDuration( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field, bool phaseTick )
    {
        if( !phaseTick )
            return;

        if( field.Weather != WeatherConditionID.None && field.WeatherDuration > 0 )
        {
            field.WeatherDuration--;

            if( field.WeatherDuration == 0 )
            {
                field.Weather = WeatherConditionID.None;
            }
        }
    }

    private void Apply_WeatherDamage( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field, bool phaseTick )
    {
        if( phaseTick )
            return;

        if( field.Weather == WeatherConditionID.None || field.WeatherDuration <= 0 )
            return;

        if( field.Weather == WeatherConditionID.SANDSTORM )
        {
            bool typeImmune = _unitSim.CheckTypes( PokemonType.Rock, unit ) || _unitSim.CheckTypes( PokemonType.Ground, unit ) || _unitSim.CheckTypes( PokemonType.Steel, unit );
            bool abilityImmune = unit.Ability == AbilityID.SandForce || unit.Ability == AbilityID.SandRush || unit.Ability == AbilityID.Sandstream || unit.Ability == AbilityID.SandVeil;
            
            if( typeImmune || abilityImmune )
                return;
            else
                DecreaseHP( unit, ( 1f/16f ) );

            unit.EndHPR = Mathf.Clamp01( unit.EndHPR );

            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} took Sandstorm Damage! HP: {unit.EndHPR}" );
        }

        //--Other weathers may heal pokemon with certain abilities
        //--these need to go here
    }

    private void Apply_TerrainChanges( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field, bool phaseTick )
    {
        if( phaseTick )
            return;

        if( field.Terrain == TerrainID.None || field.TerrainDuration <= 0 )
            return;

        if( field.Terrain == TerrainID.Blighted )
        {
            if( !unit.IsUngrounded )
            {
                if( !_unitSim.CheckTypes( PokemonType.Ghost, unit ) && !_unitSim.CheckTypes( PokemonType.Dark, unit ) )
                {
                    DecreaseHP( unit, ( 1f/16f ) );
                    unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
                    _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} took Blighted Terrain Damage! HP: {unit.EndHPR}" );
                }
            }
        }

        if( field.Terrain == TerrainID.Grassy )
        {
            if( !unit.IsUngrounded )
            {
                IncreaseHP( unit, ( 1f/16f ) );
                unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Grassy Terrain! HP: {unit.EndHPR}" );
            }
        }
    }
    
    private void Apply_LeftoversBlackSludge( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field, bool phaseTick )
    {
        if( phaseTick )
            return;

        if( unit.Item == ItemBattleEffectID.Leftovers && unit.EndHPR > HP_EPSILON )
        {
            IncreaseHP( unit, ( 1f/16f ) );
            unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Leftovers! HP: {unit.EndHPR}" );
        }

        if( unit.Item == ItemBattleEffectID.BlackSludge )
        {
            if( _unitSim.CheckTypes( PokemonType.Poison, unit ) && unit.EndHPR > HP_EPSILON )
            {
                IncreaseHP( unit, ( 1f/16f ) );
                unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Black Sludge! HP: {unit.EndHPR}" );
            }
            else
            {
                DecreaseHP( unit, ( 1f/16f ) );
                unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Black Sludge! HP: {unit.EndHPR}" );
            }
        }
    }

    private void Apply_AquaRing( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field, bool phaseTick )
    {
        if( phaseTick )
            return;

        if( unit.VolatileStatuses.Contains( VolatileConditionID.AquaRing ) )
        {
            IncreaseHP( unit, ( 1f/16f ) );
            unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Aqua Ring! HP: {unit.EndHPR}" );
        }
    }

    // private void Apply_LeechSeed( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field, bool phaseTick )
    // {

    // }

    private void Apply_SevereStatus( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field, bool phaseTick )
    {
        if( phaseTick )
            return;

        if( unit.SevereStatus == SevereConditionID.PSN )
        {
            DecreaseHP( unit, ( 1f/8f ) );
            unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Poison! HP: {unit.EndHPR}" );
        }

        if( unit.SevereStatus == SevereConditionID.TOX )
        {
            DecreaseHP( unit, ( unit.SevereStatusTime * ( 1f/16f ) ) );
            unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
            unit.SevereStatusTime++;
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Toxic! HP: {unit.EndHPR}, Toxic Counter: {unit.SevereStatusTime}" );
        }

        if( unit.SevereStatus == SevereConditionID.BRN || unit.SevereStatus == SevereConditionID.FBT )
        {
            DecreaseHP( unit, ( 1f/16f ) );
            unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Burn or Frostbite! HP: {unit.EndHPR}" );
        }

        if( unit.SevereStatus == SevereConditionID.PAR )
        {
            if( unit.SevereStatusTime > 0 )
                unit.SevereStatusTime--;

            if( unit.SevereStatusTime <= 0 )
                unit.SevereStatus = SevereConditionID.None;
        }

        if( unit.SevereStatus == SevereConditionID.SLP )
        {
            if( unit.SevereStatusTime > 0 )
                unit.SevereStatusTime--;

            if( unit.SevereStatusTime <= 0 )
                unit.SevereStatus = SevereConditionID.None;
        }
    }

    private void Apply_Curse( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field, bool phaseTick )
    {
        if( phaseTick )
            return;

        if( unit.VolatileStatuses.Contains( VolatileConditionID.Cursed ) )
        {
            DecreaseHP( unit, ( 1f/4f ) );
            unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Curse! HP: {unit.EndHPR}" );
        }
    }

    private void Apply_BindingDamage( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field, bool phaseTick )
    {
        if( phaseTick )
            return;

        if( unit.Bindings.Count > 0 )
        {
            foreach( var bind in unit.Bindings )
            {
                float damage = 1f/8f;

                if( bind == BindingConditionID.AcidTrap )
                {
                    float effectiveness = TypeChart.GetEffectiveness( PokemonType.Poison, unit.Type.One ) * TypeChart.GetEffectiveness( PokemonType.Poison, unit.Type.Two );
                    damage *= effectiveness;
                }

                DecreaseHP( unit, damage );
                unit.EndHPR = Mathf.Clamp01( unit.EndHPR );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by a Binding Condition! HP: {unit.EndHPR}" );
            }
        }
    }

    private void Apply_CourtEffect( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        
    }

    private void Tick_CourtDuration( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field, bool phaseTick )
    {
        if( !phaseTick )
            return;

        if( field.TopCourtConditions.Count > 0 )
        {
            var topConditions = field.TopCourtConditions.ToDictionary( kvp => kvp.Key, kvp => kvp.Value );

            foreach( var kvp in topConditions )
            {
                var duration = kvp.Value;
                if( duration > 0 )
                {
                    duration--;

                    if( duration == 0 )
                    {
                        field.TopCourtConditions.Remove( kvp.Key );
                    }
                    else
                    {
                        field.TopCourtConditions[kvp.Key] = duration;
                    }
                }
            }
        }

        if( field.BottomCourtConditions.Count > 0 )
        {
            var bottomConditions = field.BottomCourtConditions.ToDictionary( kvp => kvp.Key, kvp => kvp.Value );

            foreach( var kvp in bottomConditions )
            {
                var duration = kvp.Value;
                if( duration > 0 )
                {
                    duration--;

                    if( duration == 0 )
                    {
                        field.BottomCourtConditions.Remove( kvp.Key );
                    }
                    else
                    {
                        field.BottomCourtConditions[kvp.Key] = duration;
                    }
                }
            }
        }
    }

    private void Tick_TerrainDuration( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field, bool phaseTick )
    {
        if( !phaseTick )
            return;

        if( field.Terrain != TerrainID.None && field.TerrainDuration > 0 )
        {
            field.TerrainDuration--;

            if( field.TerrainDuration == 0 )
            {
                field.Terrain = TerrainID.None;
            }
        }
    }

    private void Apply_StatusOrbs( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field, bool phaseTick )
    {
        if( phaseTick )
            return;

        foreach( var mon in activeUnits )
        {
            if( mon.Item == ItemBattleEffectID.FlameOrb && mon.SevereStatus == SevereConditionID.None )
            {
                mon.SevereStatus = SevereConditionID.BRN;
            }

            if( mon.Item == ItemBattleEffectID.ToxicOrb && mon.SevereStatus == SevereConditionID.None )
            {
                mon.SevereStatus = SevereConditionID.PSN;
            }

            if( mon.Item == ItemBattleEffectID.StaticOrb && mon.SevereStatus == SevereConditionID.None )
            {
                mon.SevereStatus = SevereConditionID.PAR;
            }
        }
    }

    private void BuildRoundEndPhaseList()
    {
        _roundEndPhases = new()
        {
            { Tick_WeatherDuration },
            { Apply_WeatherDamage },
            // { Apply_AfterNextRound },
            { Apply_TerrainChanges },
            { Apply_LeftoversBlackSludge },
            { Apply_AquaRing },
            // { Apply_LeechSeed },
            { Apply_SevereStatus },
            { Apply_Curse },
            { Apply_BindingDamage },
            // { Apply_CourtEffect },
            { Tick_CourtDuration },
            { Tick_TerrainDuration },
            { Apply_StatusOrbs },
        };
    }

    private void MoveSuccessDicInit()
    {
        MoveSuccess = new()
        {
            {
                "Fake Out", ( attacker, target, move ) =>
                {
                    var attackerUnit = _ai.GetBattleUnit( attacker.Pokemon );

                    if( attackerUnit.Flags[UnitFlags.TurnsTaken].Count > 0 )
                        return false;
                    else
                        return true;
                }
            }
        };
    }

    public void RunAttackModule( SimulatedUnit attacker, SimulatedUnit target, SimulatedField field )
    {
        Move attMove = attacker.MTR?.Move ?? null;
        int attackerHitCount = attMove == null ? 0 : _unitSim.Get_ExpectedMoveHits( attMove );

        float damageDone = 0f;

        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Running an Attack Module! Attacker {attacker?.Name} (HPR: {attacker.BeginningHPR}), Move: {attMove?.MoveSO.Name} (Hits: {attackerHitCount}), Target: {target?.Name} (HPR: {target.BeginningHPR}" );

        for( int i = 0; i < attackerHitCount; i++ )
        {
            if( !_unitSim.CanActOnTurn( attacker ) )
            {
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {attacker.Name} cannot act!" );
                break;
            }

            damageDone = Apply_Attack( attacker, target, attacker.MTR, field );
            
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {attacker.Name} Attacks! Move used: {attMove.MoveSO.Name}, Expected Hits: {attackerHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
            
            ResolvePostMoveEffects( attacker, target, damageDone );

            if( target.EndHPR <= 0f )
                break;
        }
    }

    public void RunSwitchModule( SimulatedUnit attacker, SimulatedUnit target, SimulatedField field )
    {
        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Running a Switch Module! Unit Switching In: {attacker.Name}, Opponent: {target.Name}!" );
        //--nothing happens when you switch lol. maybe i can move hazard interactions here
        //--and pull them out of TOP building and stuff.
    }

    public void RunSetupModule( SimulatedUnit attacker, SimulatedUnit target, SimulatedField field )
    {
        Move attMove = attacker.MTR?.Move ?? null;

        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Running a Setup Module! Attacker {attacker.Name}, Move: {attMove.MoveSO.Name}, Opponent: {target.Name}!" );

        if( _unitSim.CanActOnTurn( attacker ) )
        {
            //--Attacker sets up
            Apply_SetupMove( attacker, attMove );
        }
    }

    public void RunOffensiveStatusModule( SimulatedUnit attacker, SimulatedUnit target, SimulatedField field )
    {
        Move attMove = attacker.MTR?.Move ?? null;

        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Running an Offensive Status Module! Attacker {attacker.Name}, Move: {attMove.MoveSO.Name}, Opponent: {target.Name}!" );

        if( _unitSim.CanActOnTurn( attacker ) )
        {
            //--Attacker uses offensive status move
            Apply_OffensiveStatus( target, attMove, field ); //--Target, move used by attacking pokemon, field
        }
    }

    public void RunSupportiveStatusModule( SimulatedUnit attacker, SimulatedUnit target, SimulatedField field )
    {
        Move attMove = attacker.MTR?.Move ?? null;

        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Running a Supportive Status Module! Attacker {attacker.Name}, Move: {attMove?.MoveSO.Name}, Opponent: {target.Name}!" );

        if( _unitSim.CanActOnTurn( attacker ) && attMove != null )
        {
            //--Attacker uses supportive status move
            var moveTarget = attMove.MoveSO.MoveTarget;

            if( moveTarget == MoveTarget.AllySide || moveTarget == MoveTarget.Self )
                Apply_SupportiveStatus( attacker, attMove, field ); //--Target (self), move used, field
            else
                Apply_SupportiveStatus( target, attMove, field ); //--Target (not self), move used by attacking pokemon, field
        }
    }

}

public enum ReplacementType { None, KO, DefensiveSwitch, OffensiveSwitch, Phaze, AllySwitch }
public struct TurnOutcomeProjection
{
    public SimulatedField Field;

    public Dictionary<SimulatedUnit, ReplacementType> ReplacedUnits;
    public int Depth;
    
    public SimulatedUnit Attacker;
    public SimulatedUnit Opponent;
    public SimulatedUnit AttackerAlly;
    public SimulatedUnit OpponentAlly;

    public List<SimulatedUnit> FinalTurnOrder;

    public PotentialToKO AttackerPTKO;
    public PotentialToKO OpponentPTKO;
    public PotentialToKO AttackerAllyPTKO;
    public PotentialToKO OpponentAllyPTKO;

    public float Attacker_EndOfTurnHP;
    public float Opponent_EndOfTurnHP;
    public float AttackerAlly_EndOfTurnHP;
    public float OpponentAlly_EndOfTurnHP;

    public bool Attacker_DiesBeforeActing;
    public bool Opponent_DiesBeforeActing;
    public bool AttackerAlly_DiesBeforeActing;
    public bool OpponentAlly_DiesBeforeActing;

    public bool AttackerCanAct;
    public bool OpponentCanAct;
    public bool AttackerAlly_CanAct;
    public bool OpponentAlly_CanAct;

    public bool MutualKO;
    public bool AttackerMovedFirst;
    public bool OpponentMovedFirst;
    public bool AttackerAllyMovedFirst;
    public bool OpponentAllyMovedFirst;

    public bool AttackerHasSweepHorizon;

    public string SimulationLog;
}

public class BattleSimEvent
{
    public int Depth;

    public SimulatedUnit Attacker;
    public SimulatedUnit Opponent;
    public SimulatedUnit AttackerAlly;
    public SimulatedUnit OpponentAlly;

    public List<SimulatedUnit> ActiveUnits;
    public List<SimulationModule> SimModules;

    public SimulatedField Field;

    public PotentialToKO AttackerPTKO;
    public PotentialToKO OpponentPTKO;
    public PotentialToKO AttackerAllyPTKO;
    public PotentialToKO OpponentAllyPTKO;

    public bool AttackerMovesFirst;
    public bool OpponentMovedFirst;
    public bool AttackerAllyMovedFirst;
    public bool OpponentAllyMovedFirst;

    public bool Attacker_CanAct;
    public bool Opponent_CanAct;
    public bool AttackerAlly_CanAct;
    public bool OpponentAlly_CanAct;

    public bool Attacker_DiesBeforeActing;
    public bool Opponent_DiesBeforeActing;
    public bool AttackerAlly_DiesBeforeActing;
    public bool OpponentAlly_DiesBeforeActing;

}

public class SimulationModule
{
    public SimModuleType Type { get; private set; }
    public int Priority { get; private set; }
    public SimulatedUnit Attacker { get; private set; }
    public List<SimulatedUnit> Targets { get; private set; }
    public Action<SimulatedUnit /*attacker*/, SimulatedUnit /*target*/, SimulatedField /*field*/> Module { get; private set; }

    public SimulationModule( SimModuleType type, int priority, SimulatedUnit attacker, List<SimulatedUnit> targets, Action< SimulatedUnit, SimulatedUnit, SimulatedField> module )
    {
        Type = type;
        Priority = priority;
        Attacker = attacker;
        Module = module;

        Targets = targets.ToList();
    }

    public void ChangeTarget( SimulatedUnit newTarget )
    {
        if( Targets.Count == 1 )
        {
            Targets.Clear();
            Targets.Add( newTarget );
        }
    }
}

public struct SimulationPackage
{
    public SimulatedUnit Unit;
    public List<SimulatedUnit> Targets;
    public SimModuleType ModuleType;
    public bool Exists;
}

public struct RoundPackage
{
    public SimulationPackage AttackerPack;
    public SimulationPackage AttackerAllyPack;
    public SimulationPackage OpponentPack;
    public SimulationPackage OpponentAllyPack;
}
