using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum SimModuleType { Attack, Switch, Setup, OffensiveStatus, SupportiveStatus, Protect }
public class BattleAI_BattleSim
{
    private BattleAI _ai;
    private BattleAI_UnitSim _unitSim;
    private BattleAI_Projection _proj;
    private List<Action<SimulatedUnit, List<SimulatedUnit>, SimulatedField>> _roundEndPhases;
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

    public SimulationModule BuildSimModule( SimModuleType type, int priority, SimulatedUnit attacker, SimulatedUnit opponent )
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

        SimulationModule sm = new( type, priority, attacker, opponent, module );

        return sm;
    }

    public BattleSimEvent BuildBattleSimEvent( PotentialToKO attPTKO, PotentialToKO oppPTKO, SimulationPackage attackerPack, SimulationPackage opponentPack, SimulatedField field )
    {
        const int priority_offset = 7;

        var attacker = attackerPack.SimUnit;
        var opponent = opponentPack.SimUnit;

        _unitSim.TurnSimLog.Add( $"===[Building Battle Simulation Event ({attacker.Name}'s {attackerPack.ModuleType} vs {opponent.Name}'s {opponentPack.ModuleType})]===" );

        var units = new List<SimulatedUnit> { attacker, opponent };
        units.Sort( ( a, b ) => b.Speed.CompareTo( a.Speed ) );

        bool attMovesFirst = false;
        int attackerPriority = attackerPack.ModuleType == SimModuleType.Switch ? 99 : attacker.MTR?.Move != null ? ( (int)attacker.MTR.Move.Priority - priority_offset ) : ( (int)MovePriority.Zero - priority_offset );
        int opponentPriority = opponentPack.ModuleType == SimModuleType.Switch ? 99 : opponent.MTR?.Move != null ? ( (int)opponent.MTR.Move.Priority - priority_offset ) : ( (int)MovePriority.Zero - priority_offset );

        //--First we check the actual priority systems. if the systems don't equal each other, we let them determine module order
        //--If they are the same, then we have to break the tie by actual unit speed. If speeds are the same, we will assume the opponent goes first.
        if( attackerPriority != opponentPriority )
        {
            attMovesFirst = attackerPriority > opponentPriority;
        }
        else
        {
            attackerPriority = attacker.Speed;
            opponentPriority = opponent.Speed;
            attMovesFirst = attacker.Speed > opponent.Speed;

            if( attacker.Speed == opponent.Speed )
            {
                attackerPriority = 0;
                opponentPriority = 1000;
            }
        }

        //--Build Sim Module
        var attackerModule = BuildSimModule( attackerPack.ModuleType, attackerPriority, attacker, opponent );
        var opponentModule = BuildSimModule( opponentPack.ModuleType, opponentPriority, opponent, attacker );

        List<SimulationModule> modules = new(){ attackerModule, opponentModule };
        modules = modules.OrderByDescending( m => m.Priority ).ToList();

        _unitSim.TurnSimLog.Add( $"[Turn Simulation] Attacker ({attacker.Name}) Speed: {attacker.Speed}. Opponent ({opponent.Name}) Speed: {opponent.Speed}." );
        _unitSim.TurnSimLog.Add( $"[Turn Simulation] Attacker ({attacker.Name}) Move Priority: {attackerPriority}. Opponent ({opponent.Name}) Move Priority {opponentPriority}." );
        _unitSim.TurnSimLog.Add( $"[Turn Simulation] Attacker ({attacker.Name}) Moves First: {attMovesFirst}." );
        _unitSim.TurnSimLog.Add( $"" );

        BattleSimEvent bse = new()
        {
            Attacker = attacker,
            Opponent = opponent,
            ActiveUnits = units,
            SimModules = modules,

            AttackerPTKO = attPTKO,
            OpponentPTKO = oppPTKO,

            Field = field,

            AttackerMovesFirst = attMovesFirst,
            AttackerCanAct = _unitSim.CanActOnTurn( attacker ),
            OpponentCanAct = _unitSim.CanActOnTurn( opponent ),
        };

        _unitSim.TurnSimLog.Add( $"Attacker {bse.Attacker.Name} (HPR: {bse.Attacker.BeginningHPR}), PTKO: {bse.AttackerPTKO}" );
        _unitSim.TurnSimLog.Add( $"Opponent {bse.Opponent.Name} (HPR: {bse.Opponent.BeginningHPR}), PTKO: {bse.OpponentPTKO}" );
        _unitSim.TurnSimLog.Add( $"" );

        return bse;
    }

    private TurnOutcomeProjection BuildTOP( BattleSimEvent bse, bool log = false )
    {
        TurnOutcomeProjection top = new()
        {
            Attacker = bse.Attacker,
            Opponent = bse.Opponent,

            Field = bse.Field, //--We currently do not make any increments to field. this feature should be expanded on to account for duration tics and such.

            AttackerPTKO = bse.AttackerPTKO,
            OpponentPTKO = bse.OpponentPTKO,

            Attacker_EndOfTurnHP = bse.Attacker.CurrentHPR,
            Opponent_EndOfTurnHP = bse.Opponent.CurrentHPR,

            Attacker_DiesBeforeActing = bse.Attacker_DiesBeforeActing,
            Opponent_DiesBeforeActing = bse.Opponent_DiesBeforeActing,

            AttackerCanAct = bse.AttackerCanAct,
            OpponentCanAct = bse.OpponentCanAct,

            MutualKO = bse.Attacker.CurrentHPR <= 0f && bse.Opponent.CurrentHPR <= 0f,
            AttackerMovedFirst = bse.AttackerMovesFirst,
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

        IBattleAIUnit attacker = null;
        IBattleAIUnit opponent = null;

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
                attacker = attack.Top.Attacker;
                attackerModule = SimModuleType.Attack;

                intentLog.Add( $"Attacker {attacker.Name} ({attacker.BeginningHPR}/{attacker.CurrentHPR}) with move {ourMTR.Move.MoveSO.Name}" );

            break;

            case ActionType.DefensiveSwitch:

                var defSwitch = (SwitchCandidateResult)ourResult;
                attacker = defSwitch.Top.Attacker;
                attackerModule = SimModuleType.Switch;

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = attacker,
                    TargetBattleUnit = null,
                    Move = null,
                    EstimatedDamage = 0,
                };

                intentLog.Add( $"Defensive Switch Candidate {attacker.Name} ({attacker.BeginningHPR}/{attacker.CurrentHPR})." );

            break;

            case ActionType.OffensiveSwitch:

                var offSwitch = (SwitchCandidateResult)ourResult;
                attacker = offSwitch.Top.Attacker;
                attackerModule = SimModuleType.Switch;

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = attacker,
                    TargetBattleUnit = null,
                    Move = null,
                    EstimatedDamage = 0,
                };

                intentLog.Add( $"Offensive Switch Candidate {attacker.Name} ({attacker.BeginningHPR}/{attacker.CurrentHPR})." );

            break;

            case ActionType.Setup:

                var setup = (SetupThreatResult)ourResult;
                attacker = setup.Top.Attacker;
                attackerModule = SimModuleType.Setup;

                _unitSim.UndoStageDelta( attacker, setup.StageDelta );

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = setup.Target,
                    TargetBattleUnit = setup.TargetBattleUnit,
                    Move = setup.Move,
                    EstimatedDamage = 0f,
                };

                intentLog.Add( $"Attacker {attacker.Name} ({attacker.BeginningHPR}/{attacker.CurrentHPR}) with move {ourMTR.Move.MoveSO.Name}" );

            break;

            case ActionType.OffensiveStatus:

                var offStatus = (StatusThreatResult)ourResult;
                attacker = offStatus.Top.Attacker;
                attackerModule = SimModuleType.OffensiveStatus;

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = offStatus.Target,
                    TargetBattleUnit = offStatus.TargetBattleUnit,
                    Move = offStatus.Move,
                    EstimatedDamage = 0f,
                };

                intentLog.Add( $"Attacker {attacker.Name} ({attacker.BeginningHPR}/{attacker.CurrentHPR}) with move {ourMTR.Move.MoveSO.Name}" );

            break;

            case ActionType.SupportiveStatus:

                var suppStatus = (StatusThreatResult)ourResult;
                attacker = suppStatus.Top.Attacker;
                attackerModule = SimModuleType.SupportiveStatus;

                ourMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = suppStatus.Target,
                    TargetBattleUnit = suppStatus.TargetBattleUnit,
                    Move = suppStatus.Move,
                    EstimatedDamage = 0f,
                };

                intentLog.Add( $"Attacker {attacker?.Name} ({attacker?.BeginningHPR}/{attacker?.CurrentHPR}) with move {ourMTR?.Move?.MoveSO.Name}" );

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
                opponent = attack.Top.Attacker;
                opponentModule = SimModuleType.Attack;
                theirMTR = attack;

                intentLog.Add( $"Attacker {opponent.Name} ({opponent.BeginningHPR}/{opponent.CurrentHPR}) with move {theirMTR.Move.MoveSO.Name}" );

            break;

            case IntentType.DefensiveSwitch:

                var defSwitch = (SwitchCandidateResult)tir.PrimaryIntent.IntentResult;
                opponent = defSwitch.Top.Attacker;
                opponentModule = SimModuleType.Switch;

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = opponent,
                    TargetBattleUnit = null,
                    Move = null,
                    EstimatedDamage = 0,
                };

                intentLog.Add( $"Defensive Switch Candidate {opponent.Name} ({opponent.BeginningHPR}/{opponent.CurrentHPR})" );

            break;

            case IntentType.OffensiveSwitch:

                var offSwitch = (SwitchCandidateResult)tir.PrimaryIntent.IntentResult;
                opponent = offSwitch.Top.Attacker;
                opponentModule = SimModuleType.Switch;

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = opponent,
                    TargetBattleUnit = null,
                    Move = null,
                    EstimatedDamage = 0,
                };

                intentLog.Add( $"Offensive Switch Candidate {opponent.Name} ({opponent.BeginningHPR}/{opponent.CurrentHPR})" );

            break;

            case IntentType.Setup:

                var setup = (SetupThreatResult)tir.PrimaryIntent.IntentResult;
                opponent = setup.Top.Attacker;
                opponentModule = SimModuleType.Setup;

                _unitSim.UndoStageDelta( opponent, setup.StageDelta );

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = setup.Target,
                    TargetBattleUnit = setup.TargetBattleUnit,
                    Move = setup.Move,
                    EstimatedDamage = 0f,
                };

                intentLog.Add( $"Attacker {opponent.Name} ({opponent.BeginningHPR}/{opponent.CurrentHPR}) with move {theirMTR.Move.MoveSO.Name}" );

            break;

            case IntentType.OffensiveStatus:

                var offStatus = (StatusThreatResult)tir.PrimaryIntent.IntentResult;
                opponent = offStatus.Top.Attacker;
                opponentModule = SimModuleType.OffensiveStatus;

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = offStatus.Target,
                    TargetBattleUnit = offStatus.TargetBattleUnit,
                    Move = offStatus.Move,
                    EstimatedDamage = 0f,
                };

                intentLog.Add( $"Attacker {opponent.Name} ({opponent.BeginningHPR}/{opponent.CurrentHPR}) with move {theirMTR.Move.MoveSO.Name}" );

            break;

            case IntentType.SupportiveStatus:

                var suppStatus = (StatusThreatResult)tir.PrimaryIntent.IntentResult;
                opponent = suppStatus.Top.Attacker;
                opponentModule = SimModuleType.SupportiveStatus;

                theirMTR = new()
                {
                    Score = 0,
                    Modifier = 0,
                    Target = suppStatus.Target,
                    TargetBattleUnit = suppStatus.TargetBattleUnit,
                    Move = suppStatus.Move,
                    EstimatedDamage = 0f,
                };

                intentLog.Add( $"Attacker {opponent.Name} ({opponent.BeginningHPR}/{opponent.CurrentHPR}) with move {theirMTR.Move.MoveSO.Name}" );

            break;
        }

        intentLog.Add( $"" );
        intentLog.Add( $"Final Information for Battle Simulation Event:" );

        float ourHPR                        = attacker.BeginningHPR;
        float theirHPR                      = opponent.BeginningHPR;
        
        var ourEDR                          = _proj.Get_EstimatedDamageResult( attacker, opponent, ourMTR );
        var theirEDR                        = _proj.Get_EstimatedDamageResult( opponent, attacker, theirMTR );

        PotentialToKO ourPTKO               = _proj.Get_PotentialToKOResult( ourEDR, ourMTR, theirHPR ).PTKO;
        PotentialToKO theirPTKO             = _proj.Get_PotentialToKOResult( theirEDR, theirMTR, ourHPR ).PTKO;

        var fieldSim                        = _ai.UnitSim.BuildSimField();

        var attackerSimUnit                 = _ai.UnitSim.BuildSimUnit( attacker, ourHPR, ourMTR, fieldSim );
        var opponentSimUnit                 = _ai.UnitSim.BuildSimUnit( opponent, theirHPR, theirMTR, fieldSim );

        SimulationPackage attackerPack      = new(){ SimUnit = attackerSimUnit, ModuleType = attackerModule };
        SimulationPackage opponentPack      = new(){ SimUnit = opponentSimUnit, ModuleType = opponentModule };

        intentLog.Add( $"Attacker: {attacker.Name}. HPR: {ourHPR}. EDR: {ourEDR}. PTKO: {ourPTKO}" );
        intentLog.Add( $"Attacker Sim Unit: {attackerSimUnit.Name}. HPR: {attackerSimUnit.BeginningHPR}. Move: {attackerSimUnit.MTR?.Move?.MoveSO.Name}" );
        intentLog.Add( $"" );
        intentLog.Add( $"Opponent: {opponent.Name}. HPR: {theirHPR}. EDR: {theirEDR}. PTKO: {theirPTKO}" );
        intentLog.Add( $"Opponent Sim Unit: {opponentSimUnit.Name}. HPR: {opponentSimUnit.BeginningHPR}. Move: {opponentSimUnit.MTR?.Move?.MoveSO.Name}" );

        Debug.Log( intentLog.ToString() );
        intentLog.Clear();
        
        var bse = BuildBattleSimEvent( ourPTKO, theirPTKO, attackerPack, opponentPack, fieldSim );
        return RunSimulation( bse, true );
    }

    public TurnOutcomeProjection RunSimulation( BattleSimEvent bse, bool log = false )
    {
        //--Order modules by priority. maybe we do this in bsc.
        //--run each module's stored action in action -> priority -> speed order as expected
        //--duing each module, appropriately resolve post action effects
        //--after all modules run, run post round effects, make sure ALL effects and their durations tick, appropriately updating each unit and the field.

        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Running a Round Simulation for {bse.Attacker.Name} vs {bse.Opponent.Name}!" );
        _unitSim.TurnSimLog.Add( $"" );

        foreach( var module in bse.SimModules )
        {
            if( module.Attacker.CurrentHPR <= 0f )
            {
                _unitSim.TurnSimLog.Add( $"Module's attacker has 0hp! Skipping module..." );
                continue;
            }

            if( module.Type != SimModuleType.SupportiveStatus )
            {
                if( module.Opponent.CurrentHPR <= 0f )
                {
                    _unitSim.TurnSimLog.Add( $"Module's target has 0hp! Skipping module..." );
                    continue;
                }
            }

            if( module.Attacker.Phazed )
            {
                _unitSim.TurnSimLog.Add( $"Module's original attacker was phazed out! Skipping module..." );
                _unitSim.TurnSimLog.Add( $"" );
                module.Attacker.Phazed = false;
            }
            else
                module.Module?.Invoke( module.Attacker, module.Opponent, bse.Field );

            UpdateActiveUnits( bse );
            _unitSim.TurnSimLog.Add( $"" );
        }

        bse.SimModules.Clear();

        ResolveRoundEndPhases( bse );

        return BuildTOP( bse, log );
    }

    private void UpdateActiveUnits( BattleSimEvent bse )
    {
        bse.ActiveUnits.Clear();

        bse.ActiveUnits.Add( bse.Attacker );
        bse.ActiveUnits.Add( bse.Opponent );
    }

    private void ResolvePostMoveEffects( SimulatedUnit attacker, SimulatedUnit target, float damageDone )
    {
        _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Resolving Post Move Effects for {attacker.Name} (HP {attacker.CurrentHPR}) attacking {target.Name} (HP {target.CurrentHPR})!" );

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

            attacker.CurrentHPR = Mathf.Clamp01( attacker.CurrentHPR );

            if( _unitSim.IsFainted( attacker ) )
                return;

            if( target.Item == BattleItemEffectID.RockyHelmet )
                DecreaseHP( attacker, ( 1f/6f ) );

            if( _unitSim.IsFainted( attacker ) )
                return;

            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} Made contact. HP: {attacker.CurrentHPR}" );
        }

        //--Sitrus Berry
        if( target.Item == BattleItemEffectID.SitrusBerry && target.CurrentHPR <= 0.5f && target.CurrentHPR > HP_EPSILON )
        {
            IncreaseHP( target, 0.25f );
            target.Item = BattleItemEffectID.None; //--eat da berry
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {target.Name} Had a sitrus berry! HP: {target.CurrentHPR}" );
        }

        //--Move Effects such as drain healing and recoil happen after contact/hp change effects.
        if( attackDrainPercent > 0 )
        {
            float drain = attackDrainPercent / 100f;
            IncreaseHP( attacker, drain * damageDone );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} Used a draining move! HP: {attacker.CurrentHPR}" );
        }

        if( healType != HealType.None )
        {
            if( healType == HealType.PercentOfMaxHP )
            {
                float healAmount = attacker.MTR.Move.MoveSO.HealAmount; //--Just in case to avoid integer division resulting in 0 or 100
                float heal = healAmount / 100f;
                IncreaseHP( attacker, heal );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} Used a self-healing move! HP: {attacker.CurrentHPR}" );
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
                    float currentHP = attacker.CurrentHPR;
                    DecreaseHP( attacker, currentHP * recoil );
                    break;

                default:
                    Debug.LogError( "AI Turn Projection: Unknown Recoil Effect!!" );
                    break;
            }

            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} took move recoil! HP: {attacker.CurrentHPR}" );

            if( _unitSim.IsFainted( attacker ) )
                return;
        }

        //--Life Orb
        if( attacker.Item == BattleItemEffectID.LifeOrb && damageDone > 0f )
        {
            DecreaseHP( attacker, ( 1f/10f ) );

            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} took Life Orb recoil! HP: {attacker.CurrentHPR}" );

            if( _unitSim.IsFainted( attacker ) )
                return;
        }

        //--Knock Off
        if( attacker.MTR.Move.MoveSO.Name == "Knock Off" )
        {
            target.Item = BattleItemEffectID.None;
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
            foreach( var unit in bse.ActiveUnits )
            {
                if( _unitSim.IsFainted( unit ) )
                    continue;

                phase( unit, bse.ActiveUnits, bse.Field );
            }
        }

        _unitSim.TurnSimLog.Add( $"" );
    }

    private float ApplyAttack( SimulatedUnit target, /*PotentialToKO attackingPTKO*/float baseDamage, int hitCount )
    {
        float previousHPR = target.CurrentHPR;
        float damage = hitCount > 0 ? baseDamage / hitCount : 0f;

        target.CurrentHPR -= damage;
        target.CurrentHPR = Mathf.Clamp01( target.CurrentHPR );
        target.CurrentHPR = Mathf.Floor( target.CurrentHPR * 1000f ) / 1000f;

        if( target.CurrentHPR <= HP_EPSILON )
            target.CurrentHPR = 0f;

        return previousHPR - target.CurrentHPR;
    }

    private void DecreaseHP( SimulatedUnit unit, float delta )
    {
        unit.CurrentHPR -= delta;
        unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
        unit.CurrentHPR = Mathf.Floor( unit.CurrentHPR * 1000f ) / 1000f;

        if( unit.CurrentHPR <= HP_EPSILON )
            unit.CurrentHPR = 0f;
    }

    private void IncreaseHP( SimulatedUnit unit, float delta )
    {
        unit.CurrentHPR += delta;
        unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
        unit.CurrentHPR = Mathf.Floor( unit.CurrentHPR * 1000f ) / 1000f;
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
                Target = null,
                TargetBattleUnit = null,
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

            if( target.CurrentHPR <= 0f )
                _unitSim.TurnSimLog.Add( $"{target.Name} fainted!" );
        }
    }

    private void Apply_SupportiveStatus( SimulatedUnit target, Move move, SimulatedField field )
    {
        var moveTarget = move.MoveSO.MoveTarget;
        var effects = move.MoveSO.MoveEffects;
        var court = target.CourtLocation == CourtLocation.TopCourt ? field.TopCourtConditions : field.BottomCourtConditions;
        var battleField = _ai.BattleSystem.Field;
        var realCourt = battleField.ActiveCourts[target.CourtLocation];

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
            target.CurrentHPR += Mathf.Clamp01( healAmount );

            _unitSim.TurnSimLog.Add( $"Healing target by {healAmount}, from {target.CurrentHPR - healAmount} to {target.CurrentHPR}" );
        }
    }

    private float Apply_HazardDamage( SimulatedUnit unit )
    {
        float previousHPR = unit.CurrentHPR;
        float damage = _ai.Get_HPRatio_AfterEntryHazards( unit );

        unit.CurrentHPR -= damage;
        unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
        unit.CurrentHPR = Mathf.Floor( unit.CurrentHPR * 1000f ) / 1000f;

        if( unit.CurrentHPR <= HP_EPSILON )
            unit.CurrentHPR = 0f;

        return previousHPR - unit.CurrentHPR;
    }

    private void Apply_WeatherDamage( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( field.Weather == WeatherConditionID.None )
            return;

        if( field.Weather == WeatherConditionID.SANDSTORM )
        {
            bool typeImmune = _unitSim.CheckTypes( PokemonType.Rock, unit ) || _unitSim.CheckTypes( PokemonType.Ground, unit ) || _unitSim.CheckTypes( PokemonType.Steel, unit );
            bool abilityImmune = unit.Ability == AbilityID.SandForce || unit.Ability == AbilityID.SandRush || unit.Ability == AbilityID.Sandstream || unit.Ability == AbilityID.SandVeil;
            
            if( typeImmune || abilityImmune )
                return;
            else
                DecreaseHP( unit, ( 1f/16f ) );

            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );

            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} took Sandstorm Damage! HP: {unit.CurrentHPR}" );
        }

        //--Other weathers may heal pokemon with certain abilities
        //--these need to go here
    }

    private void Apply_TerrainChanges( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( field.Terrain == TerrainID.None )
            return;

        if( field.Terrain == TerrainID.Blighted )
        {
            if( !unit.IsUngrounded )
            {
                if( !_unitSim.CheckTypes( PokemonType.Ghost, unit ) && !_unitSim.CheckTypes( PokemonType.Dark, unit ) )
                {
                    DecreaseHP( unit, ( 1f/16f ) );
                    unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                    _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} took Blighted Terrain Damage! HP: {unit.CurrentHPR}" );
                }
            }
        }

        if( field.Terrain == TerrainID.Grassy )
        {
            if( !unit.IsUngrounded )
            {
                IncreaseHP( unit, ( 1f/16f ) );
                unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Grassy Terrain! HP: {unit.CurrentHPR}" );
            }
        }
    }
    
    private void Apply_LeftoversBlackSludge( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( unit.Item == BattleItemEffectID.Leftovers && unit.CurrentHPR > HP_EPSILON )
        {
            IncreaseHP( unit, ( 1f/16f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Leftovers! HP: {unit.CurrentHPR}" );
        }

        if( unit.Item == BattleItemEffectID.BlackSludge )
        {
            if( _unitSim.CheckTypes( PokemonType.Poison, unit ) && unit.CurrentHPR > HP_EPSILON )
            {
                IncreaseHP( unit, ( 1f/16f ) );
                unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Black Sludge! HP: {unit.CurrentHPR}" );
            }
            else
            {
                DecreaseHP( unit, ( 1f/16f ) );
                unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Black Sludge! HP: {unit.CurrentHPR}" );
            }
        }
    }

    private void Apply_AquaRing( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( unit.VolatileStatuses.Contains( VolatileConditionID.AquaRing ) )
        {
            IncreaseHP( unit, ( 1f/16f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Aqua Ring! HP: {unit.CurrentHPR}" );
        }
    }

    // private void Apply_LeechSeed( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    // {
        
    // }

    private void Apply_SevereStatus( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( unit.SevereStatus == SevereConditionID.PSN )
        {
            DecreaseHP( unit, ( 1f/8f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Poison! HP: {unit.CurrentHPR}" );
        }

        if( unit.SevereStatus == SevereConditionID.TOX )
        {
            DecreaseHP( unit, ( unit.SevereStatusTime * ( 1f/16f ) ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            unit.SevereStatusTime++;
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Toxic! HP: {unit.CurrentHPR}, Toxic Counter: {unit.SevereStatusTime}" );
        }

        if( unit.SevereStatus == SevereConditionID.BRN || unit.SevereStatus == SevereConditionID.FBT )
        {
            DecreaseHP( unit, ( 1f/16f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Burn or Frostbite! HP: {unit.CurrentHPR}" );
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

    private void Apply_Curse( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( unit.VolatileStatuses.Contains( VolatileConditionID.Cursed ) )
        {
            DecreaseHP( unit, ( 1f/4f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Curse! HP: {unit.CurrentHPR}" );
        }
    }

    private void Apply_BindingDamage( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
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
                unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by a Binding Condition! HP: {unit.CurrentHPR}" );
            }
        }
    }

    private void BuildRoundEndPhaseList()
    {
        _roundEndPhases = new()
        {
            { Apply_WeatherDamage },
            { Apply_TerrainChanges },
            { Apply_LeftoversBlackSludge },
            { Apply_AquaRing },
            // { Apply_LeechSeed },
            { Apply_SevereStatus },
            { Apply_Curse },
            { Apply_BindingDamage },
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

            damageDone = ApplyAttack( target, attacker.MTR.EstimatedDamage, attackerHitCount );
            
            _unitSim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {attacker.Name} Attacks! Move used: {attMove.MoveSO.Name}, Expected Hits: {attackerHitCount}, Hit: {i+1}. Damage Done: {damageDone}" );
            
            ResolvePostMoveEffects( attacker, target, damageDone );

            if( target.CurrentHPR <= 0f )
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

        if( _unitSim.CanActOnTurn( attacker ) )
        {
            //--Attacker uses offensive status move
            Apply_SupportiveStatus( target, attMove, field ); //--Target, move used by attacking pokemon, field
        }
    }

}

public struct TurnOutcomeProjection
{
    public SimulatedField Field;
    
    public SimulatedUnit Attacker;
    public SimulatedUnit Opponent;
    public SimulatedUnit AttackerAlly;
    public SimulatedUnit OpponentAlly;

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

    public bool AttackerCanAct;
    public bool OpponentCanAct;
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
    public SimulatedUnit Opponent { get; private set; }
    public List<SimulatedUnit> Targets { get; private set; }
    public Action<SimulatedUnit /*attacker*/, SimulatedUnit /*target*/, SimulatedField /*field*/> Module { get; private set; }

    public SimulationModule( SimModuleType type, int priority, SimulatedUnit attacker, SimulatedUnit opponent, Action< SimulatedUnit, SimulatedUnit, SimulatedField> module )
    {
        Type = type;
        Priority = priority;
        Attacker = attacker;
        Opponent = opponent;
        Module = module;
    }
}

public struct SimulationPackage
{
    public SimulatedUnit SimUnit;
    public SimModuleType ModuleType;
}
