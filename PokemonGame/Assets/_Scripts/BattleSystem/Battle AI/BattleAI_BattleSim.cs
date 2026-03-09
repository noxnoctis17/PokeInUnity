using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleAI_BattleSim
{
    private BattleAI _ai;
    private BattleAI_UnitSim _unitSim;
    private BattleAI_Projection _proj;
    private List<Action<SimulatedUnit, List<SimulatedUnit>, SimulatedField>> _roundEndPhases;
    private int _rounds;
    private const float HP_EPSILON = 0.0009f;

    public BattleAI_BattleSim( BattleAI ai )
    {
        _ai = ai;
        _unitSim = _ai.UnitSim;
        _proj = _ai.Projection;
        BuildRoundEndPhaseList();
        _rounds = 0;
    }

    public BattleSimContext Get_BattleSimContext( PotentialToKO attPTKO, PotentialToKO oppPTKO, SimulatedUnit attacker, SimulatedUnit opponent, SimulatedField field )
    {
        // _sim.TurnSimLog.Add( $"===[Turn Simulation][Getting Sim Context]===" );
        var units = new List<SimulatedUnit> { attacker, opponent };
        units.Sort( ( a, b ) => b.Speed.CompareTo( a.Speed ) );

        bool attMovesFirst = false;
        var attMovePrio = attacker.MTR.Move.Priority;
        var oppMovePrio = opponent.MTR.Move.Priority;

        if( attacker.Speed > opponent.Speed )
        {
            if( attMovePrio > oppMovePrio )
                attMovesFirst = true;
            else if( oppMovePrio > attMovePrio )
                attMovesFirst = false;
            else
                attMovesFirst = true;
        }
        else
        {
            if( attMovePrio > oppMovePrio )
                attMovesFirst = true;
            else if( oppMovePrio > attMovePrio )
                attMovesFirst = false;
            else
                attMovesFirst = false;
        }


        BattleSimContext ctx = new()
        {
            AttackerPTKO = attPTKO,
            OpponentPTKO = oppPTKO,

            Attacker = attacker,
            Opponent = opponent,
            ActiveUnits = units,

            Field = field,

            AttackerMovesFirst = attMovesFirst,
        };

        // _sim.TurnSimLog.Add( $"Attacker {ctx.Attacker.Name} PTKO: {ctx.AttackerPTKO}" );
        // _sim.TurnSimLog.Add( $"Opponent {ctx.Opponent.Name} PTKO: {ctx.OpponentPTKO}" );
        // _sim.TurnSimLog.Add( $"Attacker {ctx.Attacker.Name} Moves first: {ctx.AttackerMovesFirst}" );

        return ctx;
    }

    private TurnOutcomeProjection BuildTOP( BattleSimContext ctx )
    {
        TurnOutcomeProjection top = new()
        {
            Attacker = ctx.Attacker,
            Opponent = ctx.Opponent,

            AttackerPTKO = ctx.AttackerPTKO,
            OpponentPTKO = ctx.OpponentPTKO,

            Attacker_EndOfTurnHP = ctx.Attacker.CurrentHPR,
            Opponent_EndOfTurnHP = ctx.Opponent.CurrentHPR,

            Attacker_DiesBeforeActing = ctx.Attacker_DiesBeforeActing,
            Opponent_DiesBeforeActing = ctx.Opponent_DiesBeforeActing,

            MutualKO = ctx.Attacker.CurrentHPR <= 0f && ctx.Opponent.CurrentHPR <= 0f,
        };

        // _sim.LogTop( top );
        // Debug.Log( _sim.TurnSimLog.ToString() );
        // string path = Application.persistentDataPath + "/BattleAI_TurnOutcomeProjectionLog.txt";
        // System.IO.File.AppendAllText( path, _sim.TurnSimLog.ToString() + "\n" );
        // _sim.TurnSimLog.Clear();
        _rounds = 0;

        return top;
    }

    public TurnOutcomeProjection SimulateAttackRound( BattleSimContext ctx, string reason = "Attack Simulation Reasons" )
    {
        _rounds++;
        // _sim.TurnSimLog.Add( $"===[Beginning Round Simulation for ROUND: {_rounds}. (Reason: [{reason}])]===" );

        ResolveMovePhase( ctx );
        ResolveRoundEndPhases( ctx );

        return BuildTOP( ctx );
    }

    public TurnOutcomeProjection SimulateSwitchRound( BattleSimContext ctx, bool attackerIsSwitch, bool opponentIsSwitch, string reason = "Switch Simulation Reasons" )
    {
        _rounds++;
        // _sim.TurnSimLog.Add( $"===[Beginning Round Simulation for ROUND: {_rounds}. (Reason: [{reason}])]===" );

        ctx.AttackerIsSwitch = attackerIsSwitch;
        ctx.OpponentIsSwitch = opponentIsSwitch;

        ResolveSwitchPhase( ctx );
        ResolveRoundEndPhases( ctx );

        return BuildTOP( ctx );
    }

    private void ResolveMovePhase( BattleSimContext ctx )
    {
        // _sim.TurnSimLog.Add( $"===[(Round: {_rounds}) Resolving Move Phase]===" );
        // _sim.TurnSimLog.Add( $"===[(Round: {_rounds}) Attacker {ctx.Attacker.Name} HPR: {ctx.Attacker.CurrentHPR}. Opponent {ctx.Opponent.Name} HPR: {ctx.Opponent.CurrentHPR}]===" );

        var attMove = ctx.Attacker.MTR.Move;
        var oppMove = ctx.Opponent.MTR.Move;

        int attackerHitCount = _unitSim.Get_ExpectedMoveHits( ctx.Attacker.MTR.Move );
        int opponentHitCount = _unitSim.Get_ExpectedMoveHits( ctx.Opponent.MTR.Move );

        float damageDone = 0f;
        if( ctx.AttackerMovesFirst )
        {
            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} moves first!" );
            //--Attacker does damage to opponent
            for( int i = 0; i < attackerHitCount; i++ )
            {
                damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attMove );
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} Attacks! Move used: {attMove}, Expected Hits: {attackerHitCount}, Hit: {i}. Damage Done: {damageDone}" );
                ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
                if( ctx.Opponent.CurrentHPR <= 0f )
                    break;
            }

            if( ctx.Opponent.CurrentHPR <= 0f )
            {
                ctx.Opponent_DiesBeforeActing = true;
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} KO'd its opponent before it could act! {ctx.Opponent_DiesBeforeActing}. Damage Done: {damageDone}" );
            }
            else
            {
                //--Opponent does damage to Attacker
                for( int i = 0; i < opponentHitCount; i++ )
                {
                    damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, oppMove );
                    // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} Attacks! Move used: {oppMove}, Expected Hits: {opponentHitCount}, Hit: {i}. Damage Done: {damageDone}" );
                    ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
                    if( ctx.Attacker.CurrentHPR <= 0f )
                        break;
                }
            }

        }
        else
        {
            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} moves first!" );
            //--Opponent does damage to Attacker
            for( int i = 0; i < opponentHitCount; i++ )
            {
                damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, oppMove );
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} Attacks! Move used: {oppMove}, Expected Hits: {opponentHitCount}, Hit: {i}. Damage Done: {damageDone}" );
                ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
                if( ctx.Attacker.CurrentHPR <= 0f )
                    break;
            }

            if( ctx.Attacker.CurrentHPR <= 0f )
            {
                ctx.Attacker_DiesBeforeActing = true;
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} KO'd its opponent before it could act! {ctx.Attacker_DiesBeforeActing}. Damage Done: {damageDone}" );
            }
            else
            {
                //--Attacker does damage to opponent
                for( int i = 0; i < attackerHitCount; i++ )
                {
                    damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attMove );
                    // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} Attacks! Move used: {attMove}, Expected Hits: {attackerHitCount}, Hit: {i}. Damage Done: {damageDone}" );
                    ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
                    if( ctx.Opponent.CurrentHPR <= 0f )
                        break;
                }
            }

        }
    }

    private void ResolveSwitchPhase( BattleSimContext ctx )
    {
        // _sim.TurnSimLog.Add( $"===[(Round: {_rounds}) Resolving Switch Phase]===" );
        // _sim.TurnSimLog.Add( $"===[(Round: {_rounds}) Attacker {ctx.Attacker.Name} HPR: {ctx.Attacker.CurrentHPR}. Opponent {ctx.Opponent.Name} HPR: {ctx.Opponent.CurrentHPR}]===" );

        var attMove = ctx.Attacker.MTR.Move;
        var oppMove = ctx.Opponent.MTR.Move;

        int attackerHitCount = _unitSim.Get_ExpectedMoveHits( ctx.Attacker.MTR.Move );
        int opponentHitCount = _unitSim.Get_ExpectedMoveHits( ctx.Opponent.MTR.Move );

        float damageDone = 0f;
        if( ctx.OpponentIsSwitch && !ctx.AttackerIsSwitch )
        {
            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} moves first!" );
            //--Attacker does damage to opponent
            for( int i = 0; i < attackerHitCount; i++ )
            {
                damageDone = ApplyAttack( ctx.Opponent, ctx.AttackerPTKO, attMove );
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} Attacks! Move used: {attMove}, Expected Hits: {attackerHitCount}, Hit: {i}. Damage Done: {damageDone}" );
                ResolvePostMoveEffects( ctx.Attacker, ctx.Opponent, damageDone );
            }

            if( ctx.Opponent.CurrentHPR <= 0f )
            {
                ctx.Opponent_DiesBeforeActing = true;
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Attacker {ctx.Attacker.Name} KO'd its opponent on entry! {ctx.Opponent_DiesBeforeActing}. Damage Done: {damageDone}" );
            }
        }
        else if( !ctx.OpponentIsSwitch && ctx.AttackerIsSwitch )
        {
            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} moves first!" );
            //--Opponent does damage to Attacker
            for( int i = 0; i < opponentHitCount; i++ )
            {
                damageDone = ApplyAttack( ctx.Attacker, ctx.OpponentPTKO, oppMove );
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} Attacks! Move used: {oppMove}, Expected Hits: {opponentHitCount}, Hit: {i}. Damage Done: {damageDone}" );
                ResolvePostMoveEffects( ctx.Opponent, ctx.Attacker, damageDone );
            }

            if( ctx.Attacker.CurrentHPR <= 0f )
            {
                ctx.Attacker_DiesBeforeActing = true;
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Opponent {ctx.Opponent.Name} KO'd its opponent on entry! {ctx.Attacker_DiesBeforeActing}. Damage Done: {damageDone}" );
            }
        }

        ctx.OpponentIsSwitch = false;
        ctx.AttackerIsSwitch = false;
    }

    private void ResolvePostMoveEffects( SimulatedUnit attacker, SimulatedUnit target, float damageDone )
    {
        // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Resolving Post Move Effects for {attacker.Name} (HP {attacker.CurrentHPR}) attacking {target.Name} (HP {target.CurrentHPR})!" );

        bool attackerMakesContact = attacker.MTR.Move.MoveSO.Flags.Contains( MoveFlags.Contact );
        float attackDrainPercent = attacker.MTR.Move.MoveSO.DrainPercentage;
        HealType healType = attacker.MTR.Move.MoveSO.HealType;
        RecoilType recoilType = attacker.MTR.Move.MoveSO.Recoil.RecoilType;

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

            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} Made contact. HP: {attacker.CurrentHPR}" );
        }

        //--Sitrus Berry
        if( target.Item == BattleItemEffectID.SitrusBerry && target.CurrentHPR <= 0.5f )
        {
            IncreaseHP( attacker, 0.25f );
            target.Item = BattleItemEffectID.None; //--eat da berry
            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {target.Name} Had a sitrus berry! HP: {target.CurrentHPR}" );
        }

        //--Move Effects such as drain healing and recoil happen after contact/hp change effects.
        if( attackDrainPercent > 0 )
        {
            float drain = attackDrainPercent / 100f;
            IncreaseHP( attacker, drain * damageDone );
            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} Used a draining move! HP: {attacker.CurrentHPR}" );
        }

        if( healType != HealType.None )
        {
            if( healType == HealType.PercentOfMaxHP )
            {
                float healAmount = attacker.MTR.Move.MoveSO.HealAmount; //--Just in case to avoid integer division resulting in 0 or 100
                float heal = healAmount / 100f;
                IncreaseHP( attacker, heal );
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} Used a self-healing move! HP: {attacker.CurrentHPR}" );
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

            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} took move recoil! HP: {attacker.CurrentHPR}" );

            if( _unitSim.IsFainted( attacker ) )
                return;
        }

        //--Life Orb
        if( attacker.Item == BattleItemEffectID.LifeOrb && damageDone > 0f )
        {
            DecreaseHP( attacker, ( 1f/10f ) );

            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {attacker.Name} took Life Orb recoil! HP: {attacker.CurrentHPR}" );

            if( _unitSim.IsFainted( attacker ) )
                return;
        }
    }

    private void ResolveRoundEndPhases( BattleSimContext ctx )
    {
        // _sim.TurnSimLog.Add( $"(Round: {_rounds}) Resolving Round End Phases!" );
        ctx.ActiveUnits.Sort( ( a, b ) => b.Speed.CompareTo( a.Speed ) );

        foreach( var phase in _roundEndPhases )
        {
            foreach( var unit in ctx.ActiveUnits )
            {
                if( _unitSim.IsFainted( unit ) )
                    continue;

                phase( unit, ctx.ActiveUnits, ctx.Field );
            }
        }
    }

    private float ApplyAttack( SimulatedUnit unit, PotentialToKO ptko, Move move )
    {
        float effectiveness = TypeChart.GetEffectiveness( move.MoveType, unit.Type.One ) * TypeChart.GetEffectiveness( move.MoveType, unit.Type.Two );
        float previousHPR = unit.CurrentHPR;
        float damage = effectiveness == 0 ? 0 : _proj.Get_PTKODamagePercent( ptko ); //--Account for 0 damage. consider PTKO class for 0 damage moves.
        unit.CurrentHPR -= damage;
        unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
        unit.CurrentHPR = Mathf.Floor( unit.CurrentHPR * 1000f ) / 1000f;

        if( unit.CurrentHPR <= HP_EPSILON )
            unit.CurrentHPR = 0f;

        return previousHPR - unit.CurrentHPR;
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

    private void Apply_WeatherDamage( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( field.Weather == WeatherConditionID.None )
            return;

        if( field.Weather == WeatherConditionID.SANDSTORM )
        {
            if( _unitSim.CheckTypes( PokemonType.Rock, unit ) || _unitSim.CheckTypes( PokemonType.Ground, unit ) || _unitSim.CheckTypes( PokemonType.Steel, unit ) )
                return;
            else
                DecreaseHP( unit, ( 1f/16f ) );

            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );

            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} took Sandstorm Damage! HP: {unit.CurrentHPR}" );
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
                    // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} took Blighted Terrain Damage! HP: {unit.CurrentHPR}" );
                }
            }
        }

        if( field.Terrain == TerrainID.Grassy )
        {
            if( !unit.IsUngrounded )
            {
                IncreaseHP( unit, ( 1f/16f ) );
                unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Grassy Terrain! HP: {unit.CurrentHPR}" );
            }
        }
    }
    
    private void Apply_LeftoversBlackSludge( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( unit.Item == BattleItemEffectID.Leftovers )
        {
            IncreaseHP( unit, ( 1f/16f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Leftovers! HP: {unit.CurrentHPR}" );
        }

        if( unit.Item == BattleItemEffectID.BlackSludge )
        {
            if( _unitSim.CheckTypes( PokemonType.Poison, unit ) )
            {
                IncreaseHP( unit, ( 1f/16f ) );
                unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Black Sludge! HP: {unit.CurrentHPR}" );
            }
            else
            {
                DecreaseHP( unit, ( 1f/16f ) );
                unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Black Sludge! HP: {unit.CurrentHPR}" );
            }

            
        }
    }

    private void Apply_AquaRing( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( unit.VolatileStatuses.Contains( VolatileConditionID.AquaRing ) )
        {
            IncreaseHP( unit, ( 1f/16f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was healed by Aqua Ring! HP: {unit.CurrentHPR}" );
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
            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Poison! HP: {unit.CurrentHPR}" );
        }

        if( unit.SevereStatus == SevereConditionID.TOX )
        {
            DecreaseHP( unit, ( unit.SevereStatusDuration * ( 1f/16f ) ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            unit.SevereStatusDuration++;
            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Toxic! HP: {unit.CurrentHPR}, Toxic Counter: {unit.ToxicCounter}" );
        }

        if( unit.SevereStatus == SevereConditionID.BRN || unit.SevereStatus == SevereConditionID.FBT )
        {
            DecreaseHP( unit, ( 1f/16f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Burn or Frostbite! HP: {unit.CurrentHPR}" );
        }
    }

    private void Apply_Curse( SimulatedUnit unit, List<SimulatedUnit> activeUnits, SimulatedField field )
    {
        if( unit.VolatileStatuses.Contains( VolatileConditionID.Cursed ) )
        {
            DecreaseHP( unit, ( 1f/4f ) );
            unit.CurrentHPR = Mathf.Clamp01( unit.CurrentHPR );
            // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by Curse! HP: {unit.CurrentHPR}" );
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
                // _sim.TurnSimLog.Add( $"(Round: {_rounds}) {unit.Name} was hurt by a Binding Condition! HP: {unit.CurrentHPR}" );
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

}

public class BattleSimContext
{
    public SimulatedUnit Attacker;
    public SimulatedUnit Opponent;
    public List<SimulatedUnit> ActiveUnits;

    public SimulatedField Field;

    public PotentialToKO AttackerPTKO;
    public PotentialToKO OpponentPTKO;

    public bool AttackerMovesFirst;
    public bool AttackerIsSwitch;
    public bool OpponentIsSwitch;

    public bool Attacker_DiesBeforeActing;
    public bool Opponent_DiesBeforeActing;
}
