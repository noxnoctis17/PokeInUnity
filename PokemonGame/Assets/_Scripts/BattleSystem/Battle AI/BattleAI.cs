using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;
using System.Collections.ObjectModel;
using System.Collections;

public enum AIDecisionType { Attack, RandomMove, ChosenMove, OffensiveSwitch, DefensiveSwitch, SpeedControl, Weather, FakeOut, Protect, }
public enum PotentialToKO { Untouchable, HardWall, Sturdy, Safe, TwoHKO, Risky, Dangerous, OHKO }
public enum TempoState { WinningHard, Winning, Neutral, Losing, LosingHard }
public enum ExchangeState { Neutral, Pressure, OpponentForcedOut }
public enum SurvivalClass { FailedSacrifice, UsefulSacrifice, Safe, FragileCounterPressure, }
public class BattleAI : MonoBehaviour
{
    private int _round;
    private BattleAI_ActionEvaluation _actionEval;
    public BattleSystem BattleSystem { get; private set; }
    public BattleTrainer Trainer { get; private set; }
    public bool IsDoubleBattle { get; private set; }

//-----------------------------------------------------------------------------------------------------
//-------------------------------[AI SYSTEM CLASSES]---------------------------------------------------
//-----------------------------------------------------------------------------------------------------

    public BattleAI_MoveCommand MoveCommand { get; private set; }
    public BattleAI_SwitchCommand SwitchCommand { get; private set; }
    public BattleAI_Projection Projection { get; private set; }
    public BattleAI_BattleSim BattleSim { get; private set; }
    public BattleAI_UnitSim UnitSim { get; private set; }
    public BattleAI_FinalReasoning Final { get; private set; }
    public BattleAI_RoleDetection RoleDetection { get; private set; }
    public BattleAI_StatSpreads StatSpreads { get; private set; }
    public BattleAI_ThreatIntent ThreatIntent { get; private set; }

//-----------------------------------------------------------------------------------------------------
//-------------------------------------[TRACKERS]------------------------------------------------------
//-----------------------------------------------------------------------------------------------------

    public float TrainerSkillModifier { get; private set; }
    public int SwitchAmount { get; private set; }
    public int SetupAmount { get; private set; }
    public int Round => _round;
    public CurrentPlan CurrentPlan { get; private set; }
    public Dictionary<string, UniqueWallingScoreMove> UniqueStatCalls { get; private set; }
    public List<string> LevelDamageMoves { get; private set; }
    public Dictionary<Pokemon, PieceValue> OurTeamPieceValues { get; private set; }
    public Dictionary<Pokemon, PieceValue> TheirTeamPieceValues { get; private set; }
    public CustomLogSession CurrentLog { get; private set; }

//-----------------------------------------------------------------------------------------------------
//---------------------------------[UNIT REFERENCES]---------------------------------------------------
//-----------------------------------------------------------------------------------------------------

    public BattleUnit Unit { get; private set; }
    public ReadOnlyCollection<Pokemon> OurTeamPokemon { get; private set; }
    public ReadOnlyCollection<Pokemon> TheirTeamPokemon { get; private set; }
    public Dictionary<Pokemon, BattleAI_PokemonAdapter> OurTeamAdapters { get; private set; }
    public Dictionary<Pokemon, BattleAI_PokemonAdapter> TheirTeamAdapters { get; private set; }
    public BattleAI_PokemonAdapter ThisUnitAdapter { get; private set; }
    public Pokemon LastSentInPokemon { get; private set; }
    public List<IBattleAIUnit> LastOpposingPokemon { get; private set; }
    public List<IBattleAIUnit> TheirBattleAIUnits { get; private set; }
    public List<IBattleAIUnit> OurBattleAIUnits { get; private set; }
    public SimulatedField CurrentFieldSnapshot { get; private set; }
    public TeamComposition OurTeamComposition { get; private set; }
    public TeamComposition TheirTeamComposition { get; private set; }
    public GamePlan GamePlan { get; private set; }

//-----------------------------------------------------------------------------------------------------
//-----------------------------------------------------------------------------------------------------
//-----------------------------------------------------------------------------------------------------

    public void InitializeAI( BattleSystem battleSystem, BattleUnit battleUnit )
    {
        BattleSystem = battleSystem;
        Unit = battleUnit;
        Trainer = Unit.Trainer;

        if( battleSystem.BattleType != BattleType.WildBattle_1v1 )
            TrainerSkillModifier = Mathf.Clamp01( battleSystem.TopTrainer1.TrainerSkillLevel / 100f );

        UnitSim         = new( this );
        Projection      = new( this );
        BattleSim       = new( this );
        MoveCommand     = new( this );
        SwitchCommand   = new( this );
        _actionEval     = new( this );
        Final           = new( this );
        RoleDetection   = new( this );
        StatSpreads     = new( this );
        ThreatIntent    = new( this );

        _round = 0;
        SetupAmount = 0;

        if( battleSystem.BattleType == BattleType.TrainerDoubles || battleSystem.BattleType == BattleType.AI_Doubles )
            IsDoubleBattle = true;
        else
            IsDoubleBattle = false;

        OurTeamPieceValues = new();
        TheirTeamPieceValues = new();
        TheirBattleAIUnits = new();
        OurBattleAIUnits = new();

        //--TODO: this needs to be adjusted for wild battles!
        OurTeamPokemon = new( Trainer.Party );
        TheirTeamPokemon = new( BattleSystem.GetOpposingParty( Unit.Pokemon ) );

        InitializeUniqueStatCalls();
        SetupTeamAdapters();
        SetActiveBattleAIUnits();
        SetCurrentFieldSnapshot();

        OurTeamComposition = CreateTeamComposition( OurTeamAdapters.Values.ToList() );
        TheirTeamComposition = CreateTeamComposition( TheirTeamAdapters.Values.ToList() );

        GamePlan = CreateGamePlan( OurTeamComposition, TheirTeamComposition );

        BattleSystem.OnNewRound += UpdateTeamAdapters;
        BattleSystem.OnNewRound += SetCurrentFieldSnapshot;
    }

    private void SetupTeamAdapters()
    {
        OurTeamAdapters = new();
        TheirTeamAdapters = new();

        var ourTeam = BattleSystem.GetAllyParty( Unit.Pokemon );
        var theirTeam = BattleSystem.GetOpposingParty( Unit.Pokemon );

        for( int i = 0; i < ourTeam.Count; i++ )
        {
            var mon = ourTeam[i];
            BattleAI_PokemonAdapter adapter = new( mon, this );
            OurTeamAdapters.Add( adapter.Pokemon, adapter );
        }

        for( int i = 0; i < theirTeam.Count; i++ )
        {
            var mon = theirTeam[i];
            BattleAI_PokemonAdapter adapter = new( mon, this );
            TheirTeamAdapters.Add( adapter.Pokemon, adapter );
        }
    }

    public void UpdateTeamAdapters()
    {
        var ourTeam = BattleSystem.GetAllyParty( Unit.Pokemon );
        var theirTeam = BattleSystem.GetOpposingParty( Unit.Pokemon );

        Action<Pokemon, Dictionary<Pokemon, BattleAI_PokemonAdapter>> update = ( mon, adapters ) =>
        {
            if( adapters.TryGetValue( mon, out var adapter ) )
            {
                adapter.BeginningHPR = Get_HPRatio( mon );
                adapter.CurrentHPR = adapter.BeginningHPR;

                adapter.Type = ( mon.PokeSO.Type1, mon.PokeSO.Type2 ); //--Once type changes are possible, update this

                adapter.ActiveMoves.Clear();
                adapter.ActiveMoves = new( mon.ActiveMoves );

                adapter.Ability = mon.AbilityID;
                adapter.Item = mon.BattleItemEffect != null ? mon.BattleItemEffect.ID : BattleItemEffectID.None;

                adapter.SevereStatus = mon.SevereStatus != null ? mon.SevereStatus.ID : SevereConditionID.None;
                adapter.SevereStatusTime = mon.SevereStatusTime;
                adapter.VolatileStatuses = new( mon.VolatileStatuses.Keys );
                adapter.Bindings = new( mon.BindingStatuses.Keys );
                adapter.StatStages = mon.CloneStatStages();
                adapter.DirectStatModifiers = mon.CloneDirectModifiers();

                adapter.CalculateStats();
                adapter.SetExpendability();

                adapters[mon] = adapter;
            }
        };

        for( int i = 0; i < ourTeam.Count; i++ )
        {
            var mon = ourTeam[i];
            update( mon, OurTeamAdapters );
        }

        for( int i = 0; i < theirTeam.Count; i++ )
        {
            var mon = theirTeam[i];
            update( mon, TheirTeamAdapters );   
        }
    }

    private void SetCurrentFieldSnapshot()
    {
        CurrentFieldSnapshot = UnitSim.BuildSimField();
    }

    private void SetActiveBattleAIUnits()
    {
        var ourUnits = BattleSystem.GetAllyUnits( Unit );
        var theirUnits = BattleSystem.GetOpposingUnits( Unit );

        // Debug.LogError( $"Our Units Count: {ourUnits.Count}, Their Units Count: {theirUnits.Count}" );

        OurBattleAIUnits.Clear();
        TheirBattleAIUnits.Clear();

        for( int i = 0; i < ourUnits.Count; i++ )
        {
            // Debug.LogError( $"Our unit is: {ourUnits[i].Pokemon.NickName}" );
            if( OurTeamAdapters.TryGetValue( ourUnits[i].Pokemon, out var adapter ) )
            {
                OurBattleAIUnits.Add( adapter );
            }
        }

        for( int i = 0; i < theirUnits.Count; i++ )
        {
            // Debug.LogError( $"Checking Their Team Adapters for unit key. Their Team Adapter Count: {TheirTeamAdapters.Count}" );
            // Debug.LogError( $"Their unit is: {theirUnits[i].Pokemon.NickName}." );
            if( TheirTeamAdapters.TryGetValue( theirUnits[i].Pokemon, out var adapter ) )
            {
                TheirBattleAIUnits.Add( adapter );
            }
        }
    }

    private TeamComposition CreateTeamComposition( List<BattleAI_PokemonAdapter> team )
    {
        CustomLogSession tcLog = new();

        tcLog.Add( $"=====================================" );
        tcLog.Add( $"=====[Creating Team Composition]=====" );
        tcLog.Add( $"=====================================" );
        tcLog.Add( "" );
        tcLog.Add( $"===[Team]===" );

        int teamCount = 0;
        foreach( var mon in team )
        {
            teamCount++;
            tcLog.Add( $"{teamCount}. {mon.Name}" );
        }

        TeamComposition tc = new()
        {
            Strategies = new(),
            Strengths = new(),
            StrategyScores = new()
            {
                { TeamStrategy.HazardPressure,      0 },
                { TeamStrategy.PivotCycling,        0 },
                { TeamStrategy.SetupSweeping,       0 },
                { TeamStrategy.WeatherAbuse,        0 },
                { TeamStrategy.SpeedControl,        0 },
                { TeamStrategy.TrickRoom,           0 },
                { TeamStrategy.StatusAttrition,     0 },
                { TeamStrategy.ScreenSupport,       0 },
                { TeamStrategy.Phazing,             0 },
                { TeamStrategy.Sun,                 0 },
                { TeamStrategy.Rain,                0 },
                { TeamStrategy.Sand,                0 },
                { TeamStrategy.Snow,                0 },
            },

            Team = team.ToList(),
        };

        int spinBlocker = 0;
        int phazer = 0;

        int trickRoomSetter = 0;
        int trickRoomAbuser = 0;
        int slowAttacker = 0;

        int sunSetter = 0;
        int rainSetter = 0;
        int sandSetter = 0;
        int snowSetter = 0;

        int sunUser = 0;
        int rainUser = 0;
        int sandUser = 0;
        int snowUser = 0;

        BattleAI_PokemonAdapter bestPrimary_Sweeper = null;
        BattleAI_PokemonAdapter bestPrimary_SetupSweeper = null;
        BattleAI_PokemonAdapter bestPrimary_Pivot = null;
        BattleAI_PokemonAdapter bestPrimary_Disrupter = null;
        BattleAI_PokemonAdapter bestPrimary_PhysicalWall = null;
        BattleAI_PokemonAdapter bestPrimary_SpecialWall = null;
        BattleAI_PokemonAdapter bestPrimary_HazardSetter = null;
        BattleAI_PokemonAdapter bestPrimary_SpeedControlProvider = null;
        BattleAI_PokemonAdapter bestPrimary_WeatherSetter = null;
        BattleAI_PokemonAdapter bestPrimary_TrickRoomSetter = null;

        int sweeperScore = int.MinValue;
        int setupSweeperScore = int.MinValue;
        int pivotScore = int.MinValue;
        int disrupterScore = int.MinValue;
        int physicalWallScore = int.MinValue;
        int specialWallScore = int.MinValue;
        int hazardSetterScore = int.MinValue;
        int speedControlScore = int.MinValue;
        int weatherSetterScore = int.MinValue;
        int trickRoomSetterScore = int.MinValue;

        foreach( var pokemon in team )
        {
            var primary = pokemon.RoleProfile.PrimaryRole;
            var secondary = pokemon.RoleProfile.SecondaryRoles;
            var biases = pokemon.RoleProfile.Biases;
            var traits = pokemon.RoleProfile.Traits;
            var signals = pokemon.RoleProfile.Signals;
            var roleScores = pokemon.RoleProfile.RoleScores;

            //----------------------------------------------------
            //--Strength Accumulation-----------------------------
            //---------------------------------------------------

            tc.Strengths.Offense +=
                signals.PhysicalOffense +
                signals.SpecialOffense +
                signals.BurstDamage +
                signals.SustainedDamage +
                signals.Wallbreaking;

            tc.Strengths.Bulk +=
                signals.PhysicalBulk +
                signals.SpecialBulk +
                signals.SelfSustain +
                signals.DamageAbsorbing;

            tc.Strengths.Utility +=
                signals.SupportUtility +
                signals.OffensiveUtility +
                signals.Disruption +
                signals.TeamSupport +
                signals.BattlefieldControl;

            tc.Strengths.Speed +=
                signals.SpeedControl +
                signals.SpeedPressure +
                signals.RevengeKilling;

            tc.Strengths.Setup +=
                signals.SetupPressure +
                signals.OffensiveSetupPressure +
                signals.DefensiveSetupPressure;

            tc.Strengths.Pressure +=
                signals.HazardPressure +
                signals.PassivePressure +
                signals.SpreadDamagePressure +
                signals.Pivoting;

            //----------------------------------------------------
            //--Strategy Detection--------------------------------
            //----------------------------------------------------

            //--Hazard Pressure
            if( traits.Contains( RoleTrait.HazardSetter ) || traits.Contains( RoleTrait.SpinBlocker ) )
                tc.StrategyScores[TeamStrategy.HazardPressure]++;

            if( traits.Contains( RoleTrait.SpinBlocker ) )
                spinBlocker++;

            //--Pivot Cycling
            if( traits.Contains( RoleTrait.PivotMove ) || traits.Contains( RoleTrait.FastPivot ) || traits.Contains( RoleTrait.SlowPivot ) || traits.Contains( RoleTrait.Regenerator ) )
                tc.StrategyScores[TeamStrategy.PivotCycling]++;

            //--Setup Sweeping
            if( traits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || traits.Contains( RoleTrait.SpeciallyOffensiveSetup ) || primary == RoleClass.SetupSweeper || UnitSim.PokemonIsIronDefenseBodyPress( pokemon.Pokemon ) )
                tc.StrategyScores[TeamStrategy.SetupSweeping]++;

            //--Status Attrition
            if( traits.Contains( RoleTrait.StatusSpreader ) || traits.Contains( RoleTrait.ToxicPressure ) || traits.Contains( RoleTrait.BurnPressure ) || traits.Contains( RoleTrait.FrostbitePressure ) || traits.Contains( RoleTrait.RecoveryMove ) || primary == RoleClass.Wall )
                tc.StrategyScores[TeamStrategy.StatusAttrition]++;

            //--Screen Support
            if( traits.Contains( RoleTrait.ScreenSetter ) )
                tc.StrategyScores[TeamStrategy.ScreenSupport]++;

            //--Phazing
            if( traits.Contains( RoleTrait.Phazes ) )
            {
                tc.StrategyScores[TeamStrategy.Phazing]++;
                phazer++;
            }

            //--Speed Control
            if( traits.Contains( RoleTrait.SpeedControl ) || traits.Contains( RoleTrait.TailwindSetter ) || traits.Contains( RoleTrait.TrickRoomSetter ) )
                tc.StrategyScores[TeamStrategy.SpeedControl]++;

            //--Trick Room
            if( traits.Contains( RoleTrait.TrickRoomSetter ) )
            {
                tc.StrategyScores[TeamStrategy.TrickRoom]++;
                trickRoomSetter++;
            }

            if( primary == RoleClass.TrickRoomAbuser )
            {
                tc.StrategyScores[TeamStrategy.TrickRoom]++;
                trickRoomAbuser++;
            }

            if( primary == RoleClass.BulkyAttacker || ( ( primary == RoleClass.Sweeper || primary == RoleClass.SetupSweeper || secondary.Contains( RoleClass.Sweeper ) ) && ( biases.Contains( RoleBias.SlowSpeed ) || biases.Contains( RoleBias.TrickRoomSpeed ) ) ) )
                slowAttacker++;

            //--Weather
            if( traits.Contains( RoleTrait.WeatherSetter ) )
            {
                if( pokemon.Ability == AbilityID.Drought || UnitSim.CheckHasMove( pokemon, "Sunny Day" ) )
                {
                    sunSetter++;
                    tc.StrategyScores[TeamStrategy.Sun]++;
                }

                if( pokemon.Ability == AbilityID.Drizzle || UnitSim.CheckHasMove( pokemon, "Rain Dance" ) )
                {
                    rainSetter++;
                    tc.StrategyScores[TeamStrategy.Rain]++;
                }

                if( pokemon.Ability == AbilityID.Sandstream || UnitSim.CheckHasMove( pokemon, "Sandstorm" ) )
                {
                    sandSetter++;
                    tc.StrategyScores[TeamStrategy.Sand]++;
                }

                if( pokemon.Ability == AbilityID.SnowWarning || UnitSim.CheckHasMove( pokemon, "Snowscape" ) )
                {
                    snowSetter++;
                    tc.StrategyScores[TeamStrategy.Snow]++;
                }
            }

            bool sunBen = UnitSim.Get_WeatherContextScore( pokemon.Pokemon, WeatherConditionID.SUNNY ) > 0;
            bool rainBen = UnitSim.Get_WeatherContextScore( pokemon.Pokemon, WeatherConditionID.RAIN ) > 0;
            bool sandBen = UnitSim.Get_WeatherContextScore( pokemon.Pokemon, WeatherConditionID.SANDSTORM ) > 0;
            bool snowBen = UnitSim.Get_WeatherContextScore( pokemon.Pokemon, WeatherConditionID.SNOW ) > 0;

            if( sunBen )
            {
                sunUser++;
                tc.StrategyScores[TeamStrategy.Sun]++;
            }

            if( rainBen )
            {
                rainUser++;
                tc.StrategyScores[TeamStrategy.Rain]++;
            }

            if( sandBen )
            {
                sandUser++;
                tc.StrategyScores[TeamStrategy.Sand]++;
            }

            if( snowBen )
            {
                snowUser++;
                tc.StrategyScores[TeamStrategy.Snow]++;
            }

            //----------------------------------------------------
            //--Primary Unit Selection----------------------------
            //----------------------------------------------------
            if( roleScores[RoleClass.Sweeper] > sweeperScore )
            {
                bestPrimary_Sweeper = pokemon;
                sweeperScore = roleScores[RoleClass.Sweeper];
            }

            if( roleScores[RoleClass.SetupSweeper] > setupSweeperScore && ( traits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || traits.Contains( RoleTrait.PhysicallyOffensiveSetup ) ) )
            {
                bestPrimary_SetupSweeper = pokemon;
                setupSweeperScore = roleScores[RoleClass.SetupSweeper];
            }

            if( roleScores[RoleClass.Pivot] > pivotScore && ( traits.Contains( RoleTrait.PivotMove ) || traits.Contains( RoleTrait.FastPivot ) || traits.Contains( RoleTrait.SlowPivot ) ) )
            {
                bestPrimary_Pivot = pokemon;
                pivotScore = roleScores[RoleClass.Pivot];
            }

            if( roleScores[RoleClass.Disrupter] > disrupterScore && biases.Contains( RoleBias.Disruptive ) )
            {
                bestPrimary_Disrupter = pokemon;
                disrupterScore = roleScores[RoleClass.Disrupter];
            }

            if( roleScores[RoleClass.Wall] > physicalWallScore && biases.Contains( RoleBias.PhysicallyBulky ) )
            {
                bestPrimary_PhysicalWall = pokemon;
                physicalWallScore = roleScores[RoleClass.Wall];
            }

            if( roleScores[RoleClass.Wall] > specialWallScore && biases.Contains( RoleBias.SpeciallyBulky ) )
            {
                bestPrimary_SpecialWall = pokemon;
                specialWallScore = roleScores[RoleClass.Wall];
            }

            if( roleScores[RoleClass.HazardControl] > hazardSetterScore && traits.Contains( RoleTrait.HazardSetter ) )
            {
                bestPrimary_HazardSetter = pokemon;
                hazardSetterScore = roleScores[RoleClass.HazardControl];
            }

            if( roleScores[RoleClass.FieldControl] > speedControlScore && ( traits.Contains( RoleTrait.SpeedControl ) || traits.Contains( RoleTrait.TailwindSetter ) || traits.Contains( RoleTrait.TrickRoomSetter ) ) )
            {
                bestPrimary_SpeedControlProvider = pokemon;
                speedControlScore = roleScores[RoleClass.FieldControl];
            }

            if( signals.BattlefieldControl > weatherSetterScore && traits.Contains( RoleTrait.WeatherSetter ) )
            {
                bestPrimary_WeatherSetter = pokemon;
                weatherSetterScore = signals.BattlefieldControl;
            }

            if( roleScores[RoleClass.FieldControl] > trickRoomSetterScore && traits.Contains( RoleTrait.TrickRoomSetter ) )
            {
                bestPrimary_TrickRoomSetter = pokemon;
                trickRoomSetterScore = roleScores[RoleClass.FieldControl];
            }
        }

        tcLog.Add( "" );
        tcLog.Add( $"===[Strengths]===" );
        tcLog.Add( $"Offense: {tc.Strengths.Offense}" );
        tcLog.Add( $"Bulk: {tc.Strengths.Bulk}" );
        tcLog.Add( $"Utility: {tc.Strengths.Utility}" );
        tcLog.Add( $"Speed: {tc.Strengths.Speed}" );
        tcLog.Add( $"Setup: {tc.Strengths.Setup}" );
        tcLog.Add( $"Pressure: {tc.Strengths.Pressure}" );

        tcLog.Add( "" );
        tcLog.Add( $"===[Strategy Scores]===" );
        tcLog.Add( $"HazardPressure: {tc.StrategyScores[TeamStrategy.HazardPressure]}" );
        tcLog.Add( $"PivotCycling: {tc.StrategyScores[TeamStrategy.PivotCycling]}" );
        tcLog.Add( $"SetupSweeping: {tc.StrategyScores[TeamStrategy.SetupSweeping]}" );
        tcLog.Add( $"SpeedControl: {tc.StrategyScores[TeamStrategy.SpeedControl]}" );
        tcLog.Add( $"TrickRoom: {tc.StrategyScores[TeamStrategy.TrickRoom]}, Setters: {trickRoomSetter}, Abusers: {trickRoomAbuser}, Slow Attackers: {slowAttacker}" );
        tcLog.Add( $"StatusAttrition: {tc.StrategyScores[TeamStrategy.StatusAttrition]}" );
        tcLog.Add( $"ScreenSupport: {tc.StrategyScores[TeamStrategy.ScreenSupport]}" );
        tcLog.Add( $"Phazing: {tc.StrategyScores[TeamStrategy.Phazing]}" );
        tcLog.Add( $"Sun: {tc.StrategyScores[TeamStrategy.Sun]}, Setters: {sunSetter}, Users: {sunUser}" );
        tcLog.Add( $"Rain: {tc.StrategyScores[TeamStrategy.Rain]}, Setters: {rainSetter}, Users: {rainUser}" );
        tcLog.Add( $"Sand: {tc.StrategyScores[TeamStrategy.Sand]}, Setters: {sandSetter}, Users: {sandUser}" );
        tcLog.Add( $"Snow: {tc.StrategyScores[TeamStrategy.Snow]}, Setters: {snowSetter}, Users: {snowUser}" );
        tcLog.Add( $"WeatherAbuse: {tc.StrategyScores[TeamStrategy.WeatherAbuse]}" );

        //----------------------------------------------------
        //--Assign Best-Scored Primary Units, if any----------
        //----------------------------------------------------
        tc.Primary_Sweeper = bestPrimary_Sweeper;
        tc.Primary_SetupSweeper = bestPrimary_SetupSweeper;
        tc.Primary_Pivot = bestPrimary_Pivot;
        tc.Primary_Disruption = bestPrimary_Disrupter;
        tc.Primary_PhysicalWall = bestPrimary_PhysicalWall;
        tc.Primary_SpecialWall = bestPrimary_SpecialWall;
        tc.Primary_HazardSetter = bestPrimary_HazardSetter;
        tc.Primary_SpeedControlProvider = bestPrimary_SpeedControlProvider;
        tc.Primary_WeatherSetter = bestPrimary_WeatherSetter;
        tc.Primary_TrickRoomSetter = bestPrimary_TrickRoomSetter;

        tcLog.Add( "" );
        tcLog.Add( $"===[Primary Units]===" );
        tcLog.Add( $"Primary_Sweeper: {tc.Primary_Sweeper?.Name}" );
        tcLog.Add( $"Primary_SetupSweeper: {tc.Primary_SetupSweeper?.Name}" );
        tcLog.Add( $"Primary_Pivot: {tc.Primary_Pivot?.Name}" );
        tcLog.Add( $"Primary_Disruption: {tc.Primary_Disruption?.Name}" );
        tcLog.Add( $"Primary_PhysicalWall: {tc.Primary_PhysicalWall?.Name}" );
        tcLog.Add( $"Primary_SpecialWall: {tc.Primary_SpecialWall?.Name}" );
        tcLog.Add( $"Primary_HazardSetter: {tc.Primary_HazardSetter?.Name}" );
        tcLog.Add( $"Primary_SpeedControlProvider: {tc.Primary_SpeedControlProvider?.Name}" );
        tcLog.Add( $"Primary_WeatherSetter: {tc.Primary_WeatherSetter?.Name}" );
        tcLog.Add( $"Primary_TrickRoomSetter: {tc.Primary_TrickRoomSetter?.Name}" );

        //----------------------------------------------------
        //--Determine Available Team Strategies---------------
        //----------------------------------------------------
        if( tc.StrategyScores[TeamStrategy.HazardPressure] >= 2 || ( tc.StrategyScores[TeamStrategy.HazardPressure] >= 1 && ( spinBlocker > 0 || phazer > 0 )) )
            tc.Strategies.Add( TeamStrategy.HazardPressure );

        if( tc.StrategyScores[TeamStrategy.PivotCycling] >= 2 )
            tc.Strategies.Add( TeamStrategy.PivotCycling );

        if( tc.StrategyScores[TeamStrategy.SetupSweeping] >= 1 )
            tc.Strategies.Add( TeamStrategy.SetupSweeping );

        if( tc.StrategyScores[TeamStrategy.StatusAttrition] >= 2 )
            tc.Strategies.Add( TeamStrategy.StatusAttrition );

        if( tc.StrategyScores[TeamStrategy.ScreenSupport] >= 1 )
            tc.Strategies.Add( TeamStrategy.ScreenSupport );

        if( tc.StrategyScores[TeamStrategy.Phazing] >= 1 )
            tc.Strategies.Add( TeamStrategy.Phazing );

        if( tc.StrategyScores[TeamStrategy.SpeedControl] >= 1 )
            tc.Strategies.Add( TeamStrategy.SpeedControl );

        if( trickRoomSetter > 0 && trickRoomAbuser > 0 )
            tc.Strategies.Add( TeamStrategy.TrickRoom );

        if( sunSetter > 0 && sunUser > 0 )
        {
            tc.Strategies.Add( TeamStrategy.WeatherAbuse );
            tc.Strategies.Add( TeamStrategy.Sun );
        }

        if( rainSetter > 0 && rainUser > 0 )
        {
            tc.Strategies.Add( TeamStrategy.WeatherAbuse );
            tc.Strategies.Add( TeamStrategy.Rain );
        }

        if( sandSetter > 0 && sandUser > 0 )
        {
            tc.Strategies.Add( TeamStrategy.WeatherAbuse );
            tc.Strategies.Add( TeamStrategy.Sand );
        }

        if( snowSetter > 0 && snowUser > 0 )
        {
            tc.Strategies.Add( TeamStrategy.WeatherAbuse );
            tc.Strategies.Add( TeamStrategy.Snow );
        }

        tcLog.Add( "" );
        tcLog.Add( $"===[Available Strategies]===" );
        foreach( var strat in tc.Strategies )
            tcLog.Add( $"{strat}" );

        //----------------------------------------------------
        //--Determine Primary Archetype-----------------------
        //----------------------------------------------------
        tc.ArchetypeScores = new()
        {
            { TeamArchetype.HyperOffense,   0 },
            { TeamArchetype.BulkyOffense,   0 },
            { TeamArchetype.Balance,        0 },
            { TeamArchetype.Stall,          0 },
            { TeamArchetype.HardTrickRoom,  0 },
        };

        float offense = tc.Strengths.Offense;
        float bulk = tc.Strengths.Bulk;
        float utility = tc.Strengths.Utility;
        float speed = tc.Strengths.Speed;
        float setup = tc.Strengths.Setup;
        float pressure = tc.Strengths.Pressure;

        //--Hyper Offense
        tc.ArchetypeScores[TeamArchetype.HyperOffense] =
            offense * 1.5f +
            speed * 1.25f +
            setup -
            bulk * 0.5f;

        //--Bulky Offense
        tc.ArchetypeScores[TeamArchetype.BulkyOffense] =
            Mathf.Min( offense, bulk ) * 2f +
            setup +
            pressure;

        //--Balance
        float totalStrength =
            offense +
            bulk +
            utility +
            speed +
            setup +
            pressure;

        List<float> strengths = new(){ offense, bulk, utility, speed, setup, pressure };

        float biggestStrength = strengths.Max();

        tc.ArchetypeScores[TeamArchetype.Balance] =
            totalStrength -
            biggestStrength;

        //--Stall
        tc.ArchetypeScores[TeamArchetype.Stall] =
            bulk * 1.5f +
            utility +
            pressure -
            setup -
            offense * 0.7f;

        //--Trick Room
        bool highTrickRoomPresence = trickRoomSetter >= 1 && ( trickRoomAbuser >= 3 || ( trickRoomAbuser >= 2 && slowAttacker >= 2 ) );
        tc.ArchetypeScores[TeamArchetype.HardTrickRoom] =
            trickRoomSetter * 3 +
            trickRoomAbuser * 2 +
            slowAttacker * 2 +
            setup * 0.75f;

        if( highTrickRoomPresence )
            tc.ArchetypeScores[TeamArchetype.HardTrickRoom] *= 1.5f;
        else
            tc.ArchetypeScores[TeamArchetype.HardTrickRoom] *= 0.25f;

        var sortedArchetypes = tc.ArchetypeScores.OrderByDescending( scores => scores.Value );

        tcLog.Add( "" );
        tcLog.Add( $"===[Archetype Scores]===" );
        foreach( var kvp in sortedArchetypes )
            tcLog.Add( $"{kvp.Key}: {kvp.Value}" );

        tc.PrimaryArchetype = sortedArchetypes.First().Key;
        tc.SecondaryArchetype = sortedArchetypes.Skip( 1 ).First().Key;

        tcLog.Add( "" );
        tcLog.Add( $"===========================================" );
        tcLog.Add( $"===========================================" );
        tcLog.Add( "" );

        Debug.Log( tcLog.ToString() );
        tcLog.Clear();

        return tc;
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

    public void IncreaseSetupAmount()
    {
        SetupAmount++;
    }

    public void ResetSetupAmount()
    {
        SetupAmount = 0;
    }

    public void SetLastSentInPokemon( Pokemon pokemon )
    {
        LastSentInPokemon = pokemon;
    }

    public void SetLastOpposingPokemon( List<IBattleAIUnit> opponents )
    {
        LastOpposingPokemon = opponents;
    }

    public List<Pokemon> GetRemainingAllyPokemon( Pokemon pokemon )
    {
        return BattleSystem.GetAllyParty( pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
    }

    public List<Pokemon> GetRemainingOpposingPokemon( Pokemon pokemon )
    {
        return BattleSystem.GetOpposingParty( pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
    }

    public List<IBattleAIUnit> GetRemainingPartyAs_IBattleAIUnits( Pokemon pokemon )
    {
        List<IBattleAIUnit> remaining = new();
        var party = GetTeamAs_IBattleAIUnit( pokemon );

        for( int i = 0; i < party.Count; i++ )
        {
            var mon = party[i];
            if( mon.CurrentHPR > 0f )
                remaining.Add( mon );
            else
                continue;
        }

        return remaining;
    }

    public List<BattleAI_PokemonAdapter> GetAllyTeamAs_Adapter( Pokemon pokemon )
    {
        List<BattleAI_PokemonAdapter> adapters = new();

        if( OurTeamAdapters.ContainsKey( pokemon ) )
        {
            foreach( var kvp in OurTeamAdapters )
            {
                adapters.Add( kvp.Value );
            }

            return adapters;
        }

        if( TheirTeamAdapters.ContainsKey( pokemon ) )
        {
            foreach( var kvp in TheirTeamAdapters )
            {
                adapters.Add( kvp. Value );
            }

            return adapters;
        }

        if( adapters.Count <= 0 )
            Debug.LogError( $"Pokemon not found in either team! You're fucked!" );

        return adapters;
    }

    public BattleAI_PokemonAdapter GetPokemonAs_Adapter( Pokemon pokemon )
    {
        if( OurTeamAdapters.TryGetValue( pokemon, out var ourAdapter ) )
        {
            return ourAdapter;
        }

        if( TheirTeamAdapters.TryGetValue( pokemon, out var theirAdapter ) )
        {
            return theirAdapter;
        }

        Debug.LogError( $"Pokemon not found in either team! You're fucked!" );
        return null;
    }

    public List<IBattleAIUnit> GetTeamAs_IBattleAIUnit( Pokemon pokemon )
    {
        List<IBattleAIUnit> adapters = new();

        if( OurTeamAdapters.ContainsKey( pokemon ) )
        {
            foreach( var kvp in OurTeamAdapters )
            {
                adapters.Add( kvp.Value );
            }

            return adapters;
        }

        if( TheirTeamAdapters.ContainsKey( pokemon ) )
        {
            foreach( var kvp in TheirTeamAdapters )
            {
                adapters.Add( kvp.Value );
            }

            return adapters;
        }

        if( adapters.Count <= 0 )
            Debug.LogError( $"Pokemon not found in either team! You're fucked!" );

        return adapters;
    }

    public List<IBattleAIUnit> GetOpposingTeamAs_IBattleAIUnit( Pokemon pokemon )
    {
        List<IBattleAIUnit> adapters = new();

        if( OurTeamAdapters.ContainsKey( pokemon ) )
        {
            foreach( var kvp in TheirTeamAdapters )
            {
                adapters.Add( kvp.Value );
            }

            return adapters;
        }

        if( TheirTeamAdapters.ContainsKey( pokemon ) )
        {
            foreach( var kvp in OurTeamAdapters )
            {
                adapters.Add( kvp.Value );
            }

            return adapters;
        }

        if( adapters.Count <= 0 )
            Debug.LogError( $"Pokemon not found in either team! You're fucked!" );

        return adapters;
    }

    public BattleAI_PokemonAdapter GetPokemonAs_IBattleAIUnit( Pokemon pokemon )
    {
        if( OurTeamAdapters.TryGetValue( pokemon, out var ourAdapter ) )
        {
            return ourAdapter;
        }

        if( TheirTeamAdapters.TryGetValue( pokemon, out var theirAdapter ) )
        {
            return theirAdapter;
        }

        Debug.LogError( $"Pokemon not found in either team! You're fucked!" );
        return null;
    }

    public List<IBattleAIUnit> GetActiveAllyUnits_AsBattleAIUnits( Pokemon pokemon )
    {
        if( OurTeamAdapters.ContainsKey( pokemon ) )
        {
            return OurBattleAIUnits;
        }

        if( TheirTeamAdapters.ContainsKey( pokemon ) )
        {
            return TheirBattleAIUnits;
        }

        Debug.LogError( $"Pokemon not found in either side's active units! You're fucked!" );
        return null;
    }

    public List<IBattleAIUnit> GetActiveOpposingUnits_AsBattleAIUnits( Pokemon pokemon )
    {
        if( OurTeamAdapters.ContainsKey( pokemon ) )
        {
            return TheirBattleAIUnits;
        }

        if( TheirTeamAdapters.ContainsKey( pokemon ) )
        {
            return OurBattleAIUnits;
        }

        Debug.LogError( $"Pokemon not found in either side's active units! You're fucked!" );
        return null;
    }

    public BattleUnit GetBattleUnit( Pokemon pokemon )
    {
        for( int i = 0; i < BattleSystem.PlayerUnits.Count; i++ )
        {
            var unit = BattleSystem.PlayerUnits[i];
            if( unit.Pokemon == pokemon )
                return unit;
        }

        for( int i = 0; i < BattleSystem.EnemyUnits.Count; i++ )
        {
            var unit = BattleSystem.EnemyUnits[i];
            if( unit.Pokemon == pokemon )
                return unit;
        }

        return null;
    }

    public List<IBattleAIUnit> CreateBattleAIUnits_FromBattleUnits( List<BattleUnit> units )
    {
        List<IBattleAIUnit> aiUnits = new();

        for( int i = 0; i < units.Count; i++ )
        {
            BattleAI_PokemonAdapter monAdapter = GetPokemonAs_Adapter( units[i].Pokemon );
            aiUnits.Add( monAdapter );
        }

        return aiUnits;
    }

    public List<IBattleAIUnit> CreateBattleAIUnits_FromPokemon( List<Pokemon> party )
    {
        List<IBattleAIUnit> aiUnits = new();

        for( int i = 0; i < party.Count; i++ )
        {
            BattleAI_PokemonAdapter monAdapter = GetPokemonAs_Adapter( party[i] );
            aiUnits.Add( monAdapter );
        }

        return aiUnits;
    }

    public Pokemon RequestedForcedSwitch()
    {
        var opposingUnits = BattleSystem.GetOpposingUnits( Unit );
        int oppPokemon = 0;

        for( int i = 0; i < opposingUnits.Count; i++ )
        {
            var opp = opposingUnits[i];
            if( opp.Pokemon != null )
                oppPokemon++;
            else
                continue;
        }

        if( oppPokemon <= 0 )
        {
            Debug.Log( $"[AI Scoring][Request Forced Switch] Chose to get a Vacuum Switch!" );
            return SwitchCommand.GetSwitch_Vacuum();
        }
        else
        {
            Debug.Log( $"[AI Scoring][Request Forced Switch] Chose to get a Revenge Switch!" );
            var opps = CreateBattleAIUnits_FromBattleUnits( opposingUnits );
            return SwitchCommand.GetSwitch_Revenge( opps ).Pokemon;
        }
    }

    public Pokemon RequestRandomSwitch()
    {
        var ourParty = BattleSystem.GetAllyParty( Unit.Pokemon );
        var ourActiveUnits = BattleSystem.GetAllyUnits( Unit );
        var bench = ourParty.Where( p => !ourActiveUnits.Any( u => u.Pokemon == p ) && p.CurrentHP > 0  ).ToList();

        int r = UnityEngine.Random.Range( 0, bench.Count );

        return bench[r];
    }

    public Pokemon RequestLead()
    {
        Debug.Log( $"[AI] Lead pokemon requested using GetSwitch_Vacuum!" );
        return SwitchCommand.GetSwitch_Vacuum();
    }

    public IEnumerator ChooseCommand()
    {
        CurrentLog = new();
        _round = BattleSystem.Rounds;

        yield return null;

        //--Handle Adapters
        SetActiveBattleAIUnits();
        ThisUnitAdapter = GetPokemonAs_Adapter( Unit.Pokemon );
        yield return null;

        CurrentLog.Add( $"=====[Choose Command][TURN {_round} - {ThisUnitAdapter.Name}, Offensive Piece Value: {OurTeamPieceValues[ThisUnitAdapter?.Pokemon].OffensiveValue}]=====" );

        if( Unit.Pokemon.IsFainted || Unit.Pokemon.CurrentHP == 0 )
            yield break;

        //--Handle Two Turn/Charge/Recharge Moves
        if( Unit.Flags[UnitFlags.Charging].IsActive && Unit.Flags[UnitFlags.Charging].Count > 0 )
        {
            var move = Unit.Flags[UnitFlags.Charging].Move;
            List<BattleUnit> targets = new() { Unit.Flags[UnitFlags.Charging].Target, };
            BattleSystem.SetMoveCommand( Unit, targets, move , true );
            yield break;
        }

        //--Recharging should simply skip the turn altogether. After ChooseCommand() completes, we increment command count in the AI turn state,
        //--So there shouldn't be any hang ups, at least not in singles. --2/12/26, pre-doubles testing lol
        if( Unit.Flags[UnitFlags.Recharging].IsActive )
            yield break;

        //--Opposing Threats. This will eventually be a layer that handles actually selecting a target.
        //--singles should always target TheirBattleAIUnits[0], and doubles should run the target selection logic.
        //--this makes me wonder, however, because target selection in doubles is inherently tied to a target's intentions on the field
        //--i almost think that threat intent should happen at this layer in tandem with target selection. in the case of singles,
        //--we simply get the target and its intent. ThreatResult should include the threat intent information, and be passed down.
        //--threat intent is going to potentially use some things we do after this step currently, so we'll have to see exactly how to make this work.
        //--it's likely target selection + threat intent will simply have to be a wrapper of the brain layer, with the results of things like
        //--exchange eval and board context being extracted from threat intent.
        
        IBattleAIUnit target = null;
        ThreatIntentCandidates tic = new();
        ThreatIntentResult tir = new();
        
        if( IsDoubleBattle )
        {
            //--get threat candidates 1
            //--get threat candidates 2
            //--threat intent 1
            //--threat intent 2
            //--target selection function based on threat intent from each target
        }
        else
        {
            target = TheirBattleAIUnits[0];
            ThreatBrain tb = ThreatIntent.ReadThreatBrain( target );
            tic = ThreatIntent.GetThreatCandidates( target, ThisUnitAdapter, tb );
            tir = ThreatIntent.GetThreatIntentResult( tic, tb );
        }

        CurrentLog.Add( $"" );
        CurrentLog.Add( $"We think they are going to: {tir.PrimaryIntent}" );
        CurrentLog.Add( $"" );

        yield return null;
        
        //--Get Best Action based on high level heuristics, turn outcome simulation, flat board analysis, and simulaiton result adjustments.
        ActionEvaluation bestAction = null;
        Action<ActionEvaluation> getBestAction = ( ae ) =>
        {
            bestAction = ae;
        };

        yield return GetBestAction( tir, getBestAction );

        if( bestAction == null )
        {
            Debug.LogError( $"The ai didn't return an action bro, breaking!" );
            yield break;
        }

        yield return null;

        CurrentLog.Add( $"===[FINAL DECISION: {Unit.Pokemon.NickName} chose the {bestAction.Type} Action! Final Score: {bestAction.Score}]===" );
        Debug.Log( CurrentLog.ToString() );
        string path = Application.persistentDataPath + "/BattleAI_ChooseCommandLog.txt";
        System.IO.File.AppendAllText( path, CurrentLog.ToString() + "\n" + "\n" + "\n" + "\n" + "\n" );
        CurrentLog.Clear();

        switch( bestAction.Type )
        {
            case ActionType.Attack: MoveCommand.SubmitMoveCommand( bestAction );
                break;

            case ActionType.DefensiveSwitch: SwitchCommand.SubmitSwitchCommand( bestAction.SwitchPayload );
                break;

            case ActionType.OffensiveSwitch: SwitchCommand.SubmitSwitchCommand( bestAction.SwitchPayload );
                break;

            case ActionType.Setup: MoveCommand.SubmitMoveCommand( bestAction );
                IncreaseSetupAmount();
                break;

            case ActionType.OffensiveStatus: MoveCommand.SubmitMoveCommand( bestAction );
                break;
        }

        yield return null;
    }

    private IEnumerator GetBestAction( ThreatIntentResult tir, Action<ActionEvaluation> getBestAction )
    {
        yield return null;

        var target = tir.Threat;
        //--Brain Layer Evaluations
        var exchangeEval    = Projection.EvaluateExchange( ThisUnitAdapter, target );
        var tempo           = Projection.GetTempoState( exchangeEval );
        var boardContext    = Projection.GetBoardContext( ThisUnitAdapter, target, exchangeEval );
        var threatProfile   = GetThreatProfile( exchangeEval, boardContext, target );
        var currentPlan     = Projection.EvaluateCurrentPlan( exchangeEval, boardContext, threatProfile, GamePlan, CurrentPlan );
        CurrentPlan         = currentPlan;

        yield return null;

        //--Action Candidates + TOP
        var bestAttack              = MoveCommand.GetMove_BestAttack( ThisUnitAdapter, target, true, "Get Best Action" );
        yield return null;

        var defensiveSwitch         = SwitchCommand.GetSwitch_Defensive( ThisUnitAdapter );
        yield return null;

        var offensiveSwitch         = SwitchCommand.GetSwitch_Offensive( ThisUnitAdapter );
        yield return null;

        var bestSetup               = MoveCommand.GetMove_Setup( ThisUnitAdapter, target, true );
        yield return null;

        var bestOffensiveStatus     = MoveCommand.GetMove_OffensiveStatus( ThisUnitAdapter, target, true );
        yield return null;


        List<ActionEvaluation> actions = new();

        //--Attack. This is the only thing that should never actually be null. Eventually, this will return Struggle in the event there is no available attack at all due to taunt/encore/choice lock or lack of PP
        ActionEvaluation attackActionEval = default;
        if( bestAttack.Move != null )
        {
            attackActionEval = Build_AttackAction( tempo, exchangeEval, boardContext, bestAttack, tir );
            actions.Add( attackActionEval );
        }
        yield return null;
        yield return null;

        //--Defensive Switch
        ActionEvaluation defSwitchActionEval = default;
        if( defensiveSwitch.Pokemon != null )
        {
            defSwitchActionEval = Build_DefensiveSwitchAction( tempo, exchangeEval, boardContext, defensiveSwitch, tir );
            actions.Add( defSwitchActionEval );
        }
        yield return null;
        yield return null;

        //--Offensive Switch
        ActionEvaluation offSwitchActionEval = default;
        if( offensiveSwitch.Pokemon != null )
        {
            offSwitchActionEval = Build_OffensiveSwitchAction( tempo, exchangeEval, boardContext, offensiveSwitch, tir );
            actions.Add( offSwitchActionEval );
        }
        yield return null;
        yield return null;

        //--Setup. swords dance, iron defense, dragon dance
        ActionEvaluation setupActionEval = default;
        if( bestSetup.Move != null )
        {
            setupActionEval = Build_SetupAction( tempo, exchangeEval, boardContext, bestSetup, tir );
            actions.Add( setupActionEval );
        }
        yield return null;
        yield return null;

        //--Offensive Status. Thunder Wave, Toxic, Stealth Rocks, Sleep Powder, Growl
        ActionEvaluation offensiveStatusActionEval = default;
        if( bestOffensiveStatus.Move != null )
        {
            offensiveStatusActionEval = Build_OffensiveStatusAction( tempo, exchangeEval, boardContext, bestOffensiveStatus, tir );
            actions.Add( offensiveStatusActionEval );
        }
        yield return null;
        yield return null;

        //--Support Status
        //--screens, manual weather, redirection, trick room, tailwind, howl
        yield return null;
        yield return null;

        var doomedOutcome = CheckIfDoomedTurn( actions, exchangeEval );
        yield return null;
        yield return null;

        if( doomedOutcome.DoomedTurn )
        {
            //--Sacrifice Evaluation of all actions
            Debug.Log( $"[Doomed!] TURN {_round} is doomed! It's all doomed! beginning Sacrifice Line Evaluations." );
            CurrentLog.Add( $"[Doomed!] TURN {_round} is doomed! It's all doomed! beginning Sacrifice Line Evaluations." );
            //--Standard Evaluation of all actions
            for( int i = 0; i < actions.Count; i++ )
            {
                actions[i] = _actionEval.EvaluateSacrificeLine( actions[i], doomedOutcome );
                yield return null;
            }
        }
        else
        {
            //--Standard Evaluation of all actions
            for( int i = 0; i < actions.Count; i++ )
            {
                actions[i] = _actionEval.EvaluateAction( actions[i] );
                var survivalClass = Projection.ClassifySurvival( actions[i], doomedOutcome );
                yield return null;

                actions[i].Score += _actionEval.EvaluateThreatResponse( actions[i], threatProfile, doomedOutcome, boardContext, survivalClass );
                yield return null;
                yield return null;
                
                //--PBS
                var pbs = Projection.BuildPBS( actions[i], boardContext, survivalClass );
                int futureScore = Projection.EvaluatePBS( pbs );
                CurrentLog.Add( $"Action: {actions[i].Type}. Future Score from EvaluatePBS: {futureScore}" );
                actions[i].PBS = pbs;
                yield return null;
                yield return null;

                int currentPlanBias = Projection.GetCurrentPlanBias( actions[i], pbs, boardContext, CurrentPlan, survivalClass );
                CurrentLog.Add( $"Action: {actions[i].Type}. Current Plan is: {CurrentPlan.Type}. Bias: {currentPlanBias}" );
                CurrentLog.Add( $"" );
                yield return null;
                yield return null;

                int gamePlanAlignment = GamePlanAlignment( actions[i], GamePlan );
                yield return null;
                yield return null;

                actions[i].Score += futureScore + currentPlanBias + gamePlanAlignment;
                yield return null;
            }
        }

        //--Attack Action Text
        string attackActionText = bestAttack.Move != null ?
        $"Attack ({bestAttack.Move?.MoveSO.Name}): {attackActionEval.Score} (Survival Class: {attackActionEval.SurvivalClass})" : $"Attack not found!";

        //--Defensive Switch Action Text
        string defensiveSwitchActionText = defensiveSwitch.Pokemon != null ?
        $"Defensive Switch ({defensiveSwitch.Pokemon?.NickName}): {defSwitchActionEval.Score} (Survival Class: {defSwitchActionEval.SurvivalClass})" : $"Defensive Switch not found!";

        //--Offensive Switch Action Text
        string offensiveSwitchActionText = offensiveSwitch.Pokemon != null ?
        $"Offensive Switch ({offensiveSwitch.Pokemon?.NickName}): {offSwitchActionEval.Score} (Survival Class: {offSwitchActionEval.SurvivalClass})" : $"Offensive Switch not found!";

        //--Setup Action Text
        string setupActionText = bestSetup.Move != null ?
        $"Setup Move ({bestSetup.Move?.MoveSO.Name}): {setupActionEval.Score} (Survival Class: {setupActionEval.SurvivalClass})" : $"Setup move not found!";

        //--Offensive Status Action Text
        string offensiveStatusActionText = bestOffensiveStatus.Move != null ?
        $"Offensive Status Move ({bestOffensiveStatus.Move?.MoveSO.Name}): {offensiveStatusActionEval.Score} (Survival Class: {offensiveStatusActionEval.SurvivalClass})" : $"Offensive Status move not found!";

        CurrentLog.Add( $"===[Final Option Scores]===" );
        CurrentLog.Add( attackActionText );
        CurrentLog.Add( defensiveSwitchActionText );
        CurrentLog.Add( offensiveSwitchActionText );
        CurrentLog.Add( setupActionText );
        CurrentLog.Add( offensiveStatusActionText );
        CurrentLog.Add( $"" );

        actions = actions.OrderByDescending( a => a.Score ).ToList();
        ActionEvaluation bestAction;

        yield return null;

        if( !doomedOutcome.DoomedTurn )
            bestAction = Final.ApplyFinalReasoning( actions, exchangeEval, boardContext, CurrentPlan, threatProfile );
        else
            bestAction = actions.FirstOrDefault();

        yield return null;

        //--Select highest scored ActionEvaluation
        getBestAction?.Invoke( bestAction );
    }

    private ActionEvaluation Build_AttackAction( TempoStateResult tempo, ExchangeEvaluation exchangeEval, BoardContext boardContext, MoveThreatResult bestAttack, ThreatIntentResult tir )
    {
        var top = BattleSim.BuildIntentTOP( ActionType.Attack, bestAttack, tir );

        int attackScore = MoveCommand.AttackScore( tempo, exchangeEval, boardContext, bestAttack, top );
        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Attack Score: {attackScore}" );
        CurrentLog.Add( $"" );
        
        var attackActionEval = _actionEval.BuildActionEvaluation( ActionType.Attack, attackScore, bestAttack.Target, bestAttack.TargetBattleUnit, bestAttack.Move, top, exchangeEval );
        CurrentLog.Add( $"" );
        attackActionEval.Score += _actionEval.EvaluateBattlefieldState( attackActionEval, boardContext );

        return attackActionEval;
    }

    private ActionEvaluation Build_DefensiveSwitchAction( TempoStateResult tempo, ExchangeEvaluation exchangeEval, BoardContext boardContext, SwitchCandidateResult defensiveSwitch, ThreatIntentResult tir )
    {
        var top = BattleSim.BuildIntentTOP( ActionType.DefensiveSwitch, defensiveSwitch, tir );

        int defSwitchScore = SwitchCommand.DefensiveSwitchScore( tempo, exchangeEval, defensiveSwitch, boardContext, top );
        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Defensive Switch Score: {defSwitchScore} via Candidate: {defensiveSwitch.Pokemon?.NickName}" );
        CurrentLog.Add( $"" );

        var defSwitchActionEval = _actionEval.BuildActionEvaluation( ActionType.DefensiveSwitch, defSwitchScore, null, null, defensiveSwitch.Pokemon, top, exchangeEval );
        CurrentLog.Add( $"" );
        defSwitchActionEval.Score += _actionEval.EvaluateBattlefieldState( defSwitchActionEval, boardContext );

        return defSwitchActionEval;
    }

    private ActionEvaluation Build_OffensiveSwitchAction( TempoStateResult tempo, ExchangeEvaluation exchangeEval, BoardContext boardContext, SwitchCandidateResult offensiveSwitch, ThreatIntentResult tir )
    {
        var top = BattleSim.BuildIntentTOP( ActionType.OffensiveSwitch, offensiveSwitch, tir );

        int offSwitchScore = SwitchCommand.OffensiveSwitchScore( tempo, exchangeEval, offensiveSwitch, boardContext, top );
        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Offensive Switch Score: {offSwitchScore} via Candidate: {offensiveSwitch.Pokemon?.NickName}" );
        CurrentLog.Add( $"" );

        var offSwitchActionEval = _actionEval.BuildActionEvaluation( ActionType.OffensiveSwitch, offSwitchScore, null, null, offensiveSwitch.Pokemon, top, exchangeEval );
        CurrentLog.Add( $"" );
        offSwitchActionEval.Score += _actionEval.EvaluateBattlefieldState( offSwitchActionEval, boardContext );

        return offSwitchActionEval;
    }

    private ActionEvaluation Build_SetupAction( TempoStateResult tempo, ExchangeEvaluation exchangeEval, BoardContext boardContext, SetupThreatResult bestSetup, ThreatIntentResult tir )
    {
        var top = BattleSim.BuildIntentTOP( ActionType.Setup, bestSetup, tir );

        int setupScore = MoveCommand.SetupScore( tempo, exchangeEval, boardContext, bestSetup, top );
        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Setup Score: {setupScore}" );
        CurrentLog.Add( $"" );

        var setupActionEval = _actionEval.BuildActionEvaluation( ActionType.Setup, setupScore, bestSetup.Target, bestSetup.TargetBattleUnit, bestSetup.Move, top, exchangeEval );
        CurrentLog.Add( $"" );
        setupActionEval.Score += _actionEval.EvaluateBattlefieldState( setupActionEval, boardContext );

        return setupActionEval;
    }

    private ActionEvaluation Build_OffensiveStatusAction( TempoStateResult tempo, ExchangeEvaluation exchangeEval, BoardContext boardContext, StatusThreatResult bestOffensiveStatus, ThreatIntentResult tir )
    {
        var top = BattleSim.BuildIntentTOP( ActionType.OffensiveStatus, bestOffensiveStatus, tir );

        int statusScore = MoveCommand.OffensiveStatusScore( tempo, exchangeEval, boardContext, bestOffensiveStatus, top );
        CurrentLog.Add( $"{Unit.Pokemon.NickName}'s Offensive Status Score: {statusScore}" );
        CurrentLog.Add( $"" );

        var statusActionEval = _actionEval.BuildActionEvaluation( ActionType.OffensiveStatus, statusScore, bestOffensiveStatus.Target, bestOffensiveStatus.TargetBattleUnit, bestOffensiveStatus.Move, top, exchangeEval );
        CurrentLog.Add( $"" );
        statusActionEval.Score += _actionEval.EvaluateBattlefieldState( statusActionEval, boardContext );

        return statusActionEval;
    }

    private DoomedOutcome CheckIfDoomedTurn( List<ActionEvaluation> actions, ExchangeEvaluation exchangeEval )
    {
        //--Guaranteed Piece Loss
        int pieceLossCount = 0;
        for( int i = 0; i < actions.Count; i++ )
        {
            var action = actions[i];
            if( action.Top1.Attacker_EndOfTurnHP <= 0f )
                pieceLossCount++;
        }

        bool nearGuaranteedPieceLoss = pieceLossCount == actions.Count - 1;
        bool alwaysLoseAPiece = pieceLossCount == actions.Count;

        //--Attacker Cannot Act
        bool opponentThreatensKO        = exchangeEval.OpponentThreatensKO;
        bool attackerMovesFirst         = exchangeEval.AttackerMovesFirst;

        bool attackerCannotAct = opponentThreatensKO && !attackerMovesFirst;

        //--No Viable Switches
        int switchActionCount = 0;
        int unviableSwitches = 0;
        for( int i = 0; i < actions.Count; i++ )
        {
            var action = actions[i];
            if( action.Type == ActionType.OffensiveSwitch || action.Type == ActionType.DefensiveSwitch )
            {
                switchActionCount++;
                var switchLookAhead = MoveCommand.GetMove_BestAttack( action.Top1.Attacker, action.Top1.Opponent ).Top;

                //--We use the look ahead PTKOs because those are the PTKOs that would be in effect for the following round. we use the "current" switch simulation HP Ratios because those would be the values we start the following round with.
                bool forceSwitchNextRound = UnitSim.PredictSwitchProbability( switchLookAhead.AttackerPTKO, switchLookAhead.OpponentPTKO, switchLookAhead.AttackerMovedFirst, switchLookAhead.Attacker.BeginningHPR, switchLookAhead.Opponent.BeginningHPR, switchLookAhead.Opponent.Expendability ) > 0.7f;

                //--Does this line enable a revenge kill?
                bool canKO = switchLookAhead.AttackerPTKO >= PotentialToKO.Dangerous;
                bool enablesRevenge = canKO && ( switchLookAhead.OpponentPTKO <= PotentialToKO.Risky || switchLookAhead.AttackerMovedFirst );

                bool diesNextTurn = switchLookAhead.Attacker_DiesBeforeActing || switchLookAhead.Attacker_EndOfTurnHP <= 0f;

                bool unstablePosition = diesNextTurn;
                bool badFollowUp = switchLookAhead.AttackerPTKO <= PotentialToKO.Safe && !switchLookAhead.AttackerMovedFirst;

                if( enablesRevenge )
                    continue;

                //--This checks to see if the incoming damage when we switch in was the TwoHKO damage range (0.55f damage on incoming) or more, and then checks the look ahead attack round for how threatening we are the following turn.
                if( unstablePosition || ( badFollowUp && forceSwitchNextRound ) )
                    unviableSwitches++;
            }
            else
                continue;
        }

        int viableSwitches = switchActionCount - unviableSwitches;
        bool allSwitchesDoomed = viableSwitches == 0;

        //--Opponent Sweep Check
        List<Pokemon> ourTeamToBeSwept = null;
        int fasterThan = 0;
        int threatCount = 0;
        bool theyKO;
        bool sweepBeginning;
        bool sweepIncoming;

        for( int i = 0; i < actions.Count; i++ )
        {
            var action = actions[i];
            BattleAI_PokemonAdapter revengeCandidate = null;
            float switchProb = UnitSim.PredictSwitchProbability( action.Top1.OpponentPTKO, action.Top1.AttackerPTKO, action.Top1.AttackerMovedFirst, action.Top1.Opponent.CurrentHPR, action.Top1.Attacker.CurrentHPR, action.Top1.Attacker.Expendability );
            bool readSwitch = UnityEngine.Random.value <= switchProb;

            if( action.Top1.Attacker_DiesBeforeActing || action.Top1.Attacker_EndOfTurnHP <= 0 )
            {
                var switchCandidate = SwitchCommand.GetSwitch_Revenge( TheirBattleAIUnits ).Pokemon;
                if( switchCandidate != null )
                    revengeCandidate = GetPokemonAs_Adapter( switchCandidate );
            }
            else if( readSwitch )
            {
                var switchCandidate = SwitchCommand.GetSwitch_Defensive( ThisUnitAdapter ).Pokemon;
                if( switchCandidate != null )
                    revengeCandidate = GetPokemonAs_Adapter( switchCandidate );
            }

            IBattleAIUnit nextPokemon;
            if( revengeCandidate != null )
                nextPokemon = revengeCandidate;
            else
                nextPokemon = action.Top1.Attacker;

            //--Keep in mind, this simulation is from the perspective of the opponent attacking us. Therefore, inside this TOP, WE are the opponent.
            var opponentSweepTOP = MoveCommand.GetMove_BestAttack( action.Top1.Opponent, nextPokemon ).Top;
            
            ourTeamToBeSwept = GetRemainingAllyPokemon( nextPokemon.Pokemon );
            bool movesFirst = opponentSweepTOP.Attacker.Speed > opponentSweepTOP.Opponent.Speed;
            bool theyForceSwitch = UnitSim.PredictSwitchProbability( opponentSweepTOP.AttackerPTKO, opponentSweepTOP.OpponentPTKO, movesFirst, opponentSweepTOP.Attacker.CurrentHPR, opponentSweepTOP.Opponent.CurrentHPR, opponentSweepTOP.Opponent.Expendability ) > 0.7f;

            theyKO = opponentSweepTOP.Opponent_DiesBeforeActing || opponentSweepTOP.Opponent_EndOfTurnHP <= 0f;
            sweepBeginning = theyKO || theyForceSwitch;

            if( sweepBeginning )
            {
                foreach( var ally in ourTeamToBeSwept )
                {
                    int allySpeed = GetUnitContextualSpeed( ally );

                    if( opponentSweepTOP.Attacker.Speed > allySpeed )
                        fasterThan++;

                    BattleAI_PokemonAdapter us = GetPokemonAs_Adapter( ally );
                    var ptko = Projection.Get_NeutralPTKO( opponentSweepTOP.Attacker, us );
                    if( ptko >= PotentialToKO.TwoHKO && opponentSweepTOP.Attacker.Speed > allySpeed || ptko >= PotentialToKO.Risky )
                        threatCount++;
                }
            }
        }

        if( fasterThan >= ourTeamToBeSwept.Count - 1 && ( threatCount > 3 || threatCount >= ourTeamToBeSwept.Count - 1 ) )
            sweepIncoming = true;
        else
            sweepIncoming = false;

        //--No Tempo Recovery Line Exists
        int tempoRecoveryScore = 0;
        TurnOutcomeProjection tempoCreatedTOP = default;
        for( int i = 0; i < actions.Count; i++ )
        {
            var action = actions[i];
            BattleAI_PokemonAdapter revengeCandidate = null;
            float switchProb = UnitSim.PredictSwitchProbability( action.Top1.AttackerPTKO, action.Top1.OpponentPTKO, action.Top1.AttackerMovedFirst, action.Top1.Attacker.CurrentHPR, action.Top1.Opponent.CurrentHPR, action.Top1.Attacker.Expendability );
            bool readSwitch = UnityEngine.Random.value <= switchProb;

            if( action.Top1.Attacker_DiesBeforeActing || action.Top1.Attacker_EndOfTurnHP <= 0 )
            {
                var switchCandidate = SwitchCommand.GetSwitch_Revenge( TheirBattleAIUnits ).Pokemon;
                if( switchCandidate != null )
                    revengeCandidate = GetPokemonAs_Adapter( switchCandidate );
            }
            else if( readSwitch )
            {
                var switchCandidate = SwitchCommand.GetSwitch_Revenge( TheirBattleAIUnits ).Pokemon;
                if( switchCandidate != null )
                    revengeCandidate = GetPokemonAs_Adapter( switchCandidate );
            }

            IBattleAIUnit nextPokemon;
            if( revengeCandidate != null )
                nextPokemon = revengeCandidate;
            else
                nextPokemon = action.Top1.Attacker;

            var followUp = MoveCommand.GetMove_BestAttack( nextPokemon, action.Top1.Opponent ).Top;

            bool revengeKill = followUp.Opponent_DiesBeforeActing || followUp.Opponent_EndOfTurnHP <= 0 || ( followUp.OpponentPTKO >= PotentialToKO.TwoHKO && followUp.AttackerMovedFirst );

            float switchNextProb = UnitSim.PredictSwitchProbability( followUp.AttackerPTKO, followUp.OpponentPTKO, followUp.AttackerMovedFirst, followUp.Attacker.BeginningHPR, action.Top1.Opponent.CurrentHPR, action.Top1.Opponent.Expendability );
            bool forcesSwitch = switchNextProb >= 0.7f;

            bool favorableTrade = action.Top1.Opponent_EndOfTurnHP <= 0f || action.Top1.MutualKO;

            bool stabilizesNextTurn = followUp.Attacker_EndOfTurnHP > 0f && followUp.Attacker_EndOfTurnHP > 0.35f && followUp.OpponentPTKO <= PotentialToKO.TwoHKO;

            if( revengeKill )           tempoRecoveryScore += 2;
            if( forcesSwitch )          tempoRecoveryScore += 2;
            if( favorableTrade )        tempoRecoveryScore += 1;
            if( stabilizesNextTurn )    tempoRecoveryScore += 1;
        }

        bool noTempoRecoveryLine = tempoRecoveryScore == 0;
        bool weakTempoRecovery   = tempoRecoveryScore <= 2;

        //--Final Safe Line Check
        bool safeLineExists = false;
        for( int i = 0; i < actions.Count; i++ )
        {
            var action = actions[i];

            bool survives = action.Top1.Attacker_EndOfTurnHP > 0f && !action.Top1.Attacker_DiesBeforeActing;
            bool stabilizes = action.Top1.OpponentPTKO <= PotentialToKO.TwoHKO || action.Top1.Attacker_EndOfTurnHP >= 0.4f;

            if( survives && stabilizes )
            {
                safeLineExists = true;
                break;
            }
            else
                continue;
        }

        //--Overall Pressure check
        float pressure = 0;
        
        if( nearGuaranteedPieceLoss )       pressure += 1.0f;
        if( alwaysLoseAPiece )              pressure += 2.0f;

        if( allSwitchesDoomed )             pressure += 2.0f;
        else if( viableSwitches == 1 )      pressure += 1.0f;

        if( sweepIncoming )                 pressure += 2.5f;

        if( noTempoRecoveryLine )           pressure += 2.5f;
        else if( weakTempoRecovery )        pressure += 1.0f;

        if( attackerCannotAct )             pressure += 1.5f;

        if( safeLineExists )                pressure -= 2.5f;

        bool doomedTurn = pressure >= 5f;

        if( doomedTurn && safeLineExists && !sweepIncoming )
            doomedTurn = false;

        return new()
        {
            NearGuaranteedPieceLoss = nearGuaranteedPieceLoss,
            AlwaysLoseAPiece = alwaysLoseAPiece,
            OpponentThreatensKO = opponentThreatensKO,
            AttackerMovesFirst = attackerMovesFirst,
            AttackerCannotAct = attackerCannotAct,
            ViableSwitches = viableSwitches,
            AllSwitchesDoomed = allSwitchesDoomed,
            SweepIncoming = sweepIncoming,
            NoTempoRecoveryLine = noTempoRecoveryLine,
            TempoRecoveredTOP = tempoCreatedTOP,

            PressureScore = pressure,
            DoomedTurn = doomedTurn,
        };
    }

    public ThreatProfile GetThreatProfile( ExchangeEvaluation exchangeEval, BoardContext boardContext, IBattleAIUnit opponent )
    {
        ThreatProfile profile = new()
        {
            ThreatUnit = opponent,
            ThreatensImmediateKO = exchangeEval.OpponentThreatensKO,
            OutspeedsCurrent = exchangeEval.OpponentMovesFirst,
            ThreatPTKO = exchangeEval.OpponentPTKOR.PTKO,
        };

        CurrentLog.Add( $"" );
        CurrentLog.Add( $"===================================" );
        CurrentLog.Add( $"=====[Building Threat Profile]=====" );
        CurrentLog.Add( $"===================================" );

        //--Check opponent current sweep potential
        int threatened = 0;
        int faster = 0;

        var allies = boardContext.MyTeamAlive;

        foreach( var ally in allies )
        {
            int allySpeed = GetUnitContextualSpeed( ally );

            if( opponent.Speed > allySpeed )
                faster++;

            var ptko = Projection.Get_NeutralPTKO( opponent, ally );
            if( ( ptko >= PotentialToKO.TwoHKO && opponent.Speed > allySpeed ) || ptko > PotentialToKO.Risky )
                threatened++;
        }

        profile.ThreatenedAlliesCount = threatened;
        profile.OutspeedsAlliesCount = faster;

        profile.SweepPotential = faster >= allies.Count - 1 && ( threatened >= allies.Count - 1 || threatened > 3 );
        CurrentLog.Add( $"Threatened Allies: {threatened}. Outsped Allies: {faster}. Sweep Potential: {profile.SweepPotential}" );

        //--Are we forced to switch
        profile.ForcesSwitch = exchangeEval.AttackerSwitches;
        CurrentLog.Add( $"Exchange Evaluation predicted the opponent might force us to switch this turn: {profile.ForcesSwitch}" );

        //--Constraint Pressure. How many of our mons struggle against the opponent?
        float constraintPressure = 0f;
        int unSafeCount = 0;
        int stalledCount = 0;
        int safeResponses = 0;

        foreach( var ally in allies )
        {
            var ex = Projection.EvaluateExchange( ally, opponent );

            bool weFailToPressure = ex.AttackerPTKOR.PTKO < PotentialToKO.Risky;
            bool theyThreatenUs = ex.OpponentPTKOR.PTKO >= PotentialToKO.Risky;
            bool theyOutsustain = opponent.RoleProfile.Traits.Contains( RoleTrait.RecoveryMove ) && ex.AttackerPTKOR.PTKO < PotentialToKO.Dangerous;

            bool weSurvive = ex.OpponentPTKOR.PTKO < PotentialToKO.Dangerous;
            bool wePressure = ex.AttackerPTKOR.PTKO >= PotentialToKO.Risky;
            bool notCrippled = ex.OpponentPTKOR.PTKO <= PotentialToKO.TwoHKO;

            if( weFailToPressure )
                stalledCount++;

            if( weFailToPressure && theyThreatenUs )
                unSafeCount++;

            if( theyOutsustain )
                stalledCount++;

            if( weSurvive && wePressure )
                safeResponses++;

            if( notCrippled )
                safeResponses++;
        }

        constraintPressure += unSafeCount * 0.5f;
        constraintPressure += stalledCount * 0.35f;
        constraintPressure += Mathf.Max( 0, allies.Count - safeResponses );

        if( profile.ForcesSwitch )
            constraintPressure += 1.5f;

        if( profile.ThreatenedAlliesCount >= allies.Count - 1 )
            constraintPressure += 2f;

        if( opponent.RoleProfile.Traits.Contains( RoleTrait.TrappingMove ) )
            constraintPressure += 2f;

        if( opponent.RoleProfile.Traits.Contains( RoleTrait.ShadowTag ) )
            constraintPressure += 3f;

        if( opponent.RoleProfile.Traits.Contains( RoleTrait.WideMoveCoverage ) )
            constraintPressure += 1f;

        profile.ConstrainingPressure = constraintPressure;

        //--Offensive Pressure
        float immediatePressure = 0f;

        if( profile.ThreatPTKO >= PotentialToKO.Dangerous )
            immediatePressure += 2f;

        if( profile.ThreatensImmediateKO )
            immediatePressure += 2f;

        if( profile.OutspeedsCurrent )
            immediatePressure += 2f;

        if( opponent.RoleProfile.PrimaryRole == RoleClass.RevengeKiller || opponent.RoleProfile.PrimaryRole == RoleClass.Sweeper )
            immediatePressure += 1f;

        profile.ImmediatePressure = immediatePressure;

        //--Escalating Pressure
        float escalatingPressure = 0f;
        bool escalating = opponent.RoleProfile.PrimaryRole == RoleClass.SetupSweeper ||
        opponent.RoleProfile.SecondaryRoles.Contains( RoleClass.SetupSweeper ) ||
        opponent.RoleProfile.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup ) ||
        opponent.RoleProfile.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup );

        if( escalating )
            escalatingPressure += 2f;

        escalatingPressure += profile.ImmediatePressure * 0.5f;
        profile.EscalatingPressure = escalatingPressure;

        //--Persistant Pressure
        float persistentPressure = 0f;

        if( opponent.RoleProfile.PrimaryRole == RoleClass.Wall )
            persistentPressure += 2f;

        if( opponent.RoleProfile.Biases.Contains( RoleBias.AttritionFocused ) )
            persistentPressure += 1f;

        if( opponent.RoleProfile.Traits.Contains( RoleTrait.RecoveryMove ) )
            persistentPressure += 1.5f;

        if( opponent.RoleProfile.Traits.Contains( RoleTrait.Phazes ) )
            persistentPressure += 1f;

        profile.PersistentPressure = persistentPressure;

        //--Disruptive Pressure
        float disruptivePressure = 0f;

        if( opponent.RoleProfile.Traits.Contains( RoleTrait.StatusSpreader ) )
            disruptivePressure += 1f;

        if( opponent.RoleProfile.Traits.Contains( RoleTrait.HazardSetter ) )
            disruptivePressure += 1f;

        if( opponent.RoleProfile.Traits.Contains( RoleTrait.Taunt ) )
            disruptivePressure += 1f;

        if( opponent.RoleProfile.Traits.Contains( RoleTrait.Encore ) )
            disruptivePressure += 1f;

        if( opponent.RoleProfile.Traits.Contains( RoleTrait.SpeedControl ) )
            disruptivePressure += 1f;

        profile.DisruptivePressure = disruptivePressure;

        //--Classify Threat Type
        Dictionary<ThreatType, float> threatScores = new()
        {
            { ThreatType.Constraining, profile.ConstrainingPressure },
            { ThreatType.Immediate, profile.ImmediatePressure },
            { ThreatType.Escalating, profile.EscalatingPressure },
            { ThreatType.Persistent, profile.PersistentPressure },
            { ThreatType.Disruptive, profile.DisruptivePressure },
        };

        profile.Type = threatScores.OrderByDescending( ts => ts.Value ).First().Key;

        //--Decay
        float decayScore = 0f;

        //--Self Debuffs
        bool oppHasSelfDebuffMove = UnitSim.CheckHasSelfDebuffMove( opponent.ActiveMoves );
        bool oppHasRecoilMove = UnitSim.CheckHasRecoilMove( opponent.ActiveMoves );

        if( oppHasSelfDebuffMove )
            decayScore += 2f;

        if( opponent.Item == BattleItemEffectID.LifeOrb || oppHasRecoilMove )
        {
            decayScore += 1f;

            if( opponent.CurrentHPR <= 0.55f )
                decayScore += 1f;
        }

        if( exchangeEval.AttackerSurvives && exchangeEval.AttackerThreatensKO )
            decayScore += 1f;

        if( opponent.CurrentHPR <= 0.35f )
            decayScore += 1f;

        profile.DecayScore = decayScore;
        profile.IsDecaying = decayScore >= 2f;

        //--Pressure
        float basePressure = CalculateThreatPressure( profile );

        //--Decay reduces the persistence of a threat
        float decayMultiplier = 1f - Mathf.Clamp( profile.DecayScore * 0.15f, 0f, 0.5f );

        profile.PressureScore = basePressure * decayMultiplier;

        //--Urgency
        profile.Urgency = GetThreatUrgency( profile.PressureScore );

        // profile.Exists = profile.Urgency >= ThreatUrgency.Medium;
        // profile.Exists = profile.Type != ThreatType.None || profile.Urgency >= ThreatUrgency.Medium;
        profile.Exists = true;

        CurrentLog.Add( $"Pressure Score: {profile.PressureScore}. Urgency: {profile.Urgency}. Threat Exists: {profile.Exists}. Threat Type: {profile.Type}" );
        CurrentLog.Add( $"===================================" );
        CurrentLog.Add( $"" );

        return profile;
    }

    private float CalculateThreatPressure( ThreatProfile profile )
    {
        float pressure = 0;

        pressure += profile.ConstrainingPressure * 0.8f;
        pressure += profile.ImmediatePressure * 0.5f;
        pressure += profile.EscalatingPressure * 0.8f;
        pressure += profile.PersistentPressure * 0.7f;
        pressure += profile.DisruptivePressure * 0.6f;

        if( profile.ThreatensImmediateKO )      pressure += 1.5f;
        if( profile.OutspeedsCurrent )          pressure += 1.0f;
        if( profile.SweepPotential )            pressure += 2.0f;
        if( profile.ForcesSwitch )              pressure += 1.0f;

        return pressure;
    }

    private ThreatUrgency GetThreatUrgency( float pressure )
    {
        ThreatUrgency urgency;

        if( pressure >= 7f )            urgency = ThreatUrgency.Critical;
        else if( pressure >= 5f )       urgency = ThreatUrgency.High;
        else if( pressure >= 3f )       urgency = ThreatUrgency.Medium;
        else if( pressure > 0f )        urgency = ThreatUrgency.Low;
        else                            urgency = ThreatUrgency.None;

        return urgency;
    }

    public GamePlan CreateGamePlan( TeamComposition ourComp, TeamComposition theirComp )
    {
        GamePlan gp = new()
        {
            OurBlockers = new(),
            OurEnablers = new(),
            TheirBlockers = new(),
            TheirEnablers = new(),
        };

        var ourTeam = ourComp.Team;
        var theirTeam = theirComp.Team;

        Dictionary<Pokemon, GamePlanAnalysis> ourTeamScores = new();
        Dictionary<Pokemon, GamePlanAnalysis> theirTeamScores = new();

        static bool isPrimaryUnit( TeamComposition comp, BattleAI_PokemonAdapter mon )
        {
            if (mon == comp.Primary_Disruption || mon == comp.Primary_HazardSetter || mon == comp.Primary_PhysicalWall || mon == comp.Primary_SpecialWall ||
                mon == comp.Primary_Pivot || mon == comp.Primary_SetupSweeper || mon == comp.Primary_SpeedControlProvider || mon == comp.Primary_Sweeper ||
                mon == comp.Primary_TrickRoomSetter || mon == comp.Primary_WeatherSetter)
                return true;
            else
                return false;
        }

        foreach ( var mon in ourTeam )
        {
            ourTeamScores.Add( mon.Pokemon, new(){ AdvantageScore = 0, DangerScore = 0, EnableScore = 0, BlockScore = 0, WinningMatchups = 0, LosingMatchups = 0 } );
            // Debug.LogError( $"Adding {mon.Pokemon.NickName} to Our Team Scores");
        }

        foreach( var mon in theirTeam )
        {
            theirTeamScores.Add( mon.Pokemon, new(){ AdvantageScore = 0, DangerScore = 0, EnableScore = 0, BlockScore = 0, WinningMatchups = 0, LosingMatchups = 0 } );
            // Debug.LogError( $"Adding {mon.Pokemon.NickName} to Their Team Scores");
        }

        ourTeamScores = EvaluateWinConCandidates( ourComp, theirComp, ourTeamScores, isPrimaryUnit );
        theirTeamScores = EvaluateWinConCandidates( theirComp, ourComp, theirTeamScores, isPrimaryUnit );

        //--Get WinCons
        Pokemon ourWinCon = null;
        Pokemon theirWinCon = null;
        int ourWinConScore = 0;
        int theirWinConScore = 0;

        foreach( var kvp in ourTeamScores )
        {
            var plan = kvp.Value;
            int score = ( plan.AdvantageScore - plan.DangerScore ) + ( ( plan.WinningMatchups - plan.LosingMatchups ) * 2 );
            kvp.Value.WinConScore = score;

            if( score > ourWinConScore )
            {
                ourWinConScore = score;
                ourWinCon = kvp.Key;
            }
        }

        foreach( var kvp in theirTeamScores )
        {
            var plan = kvp.Value;
            int score = ( plan.AdvantageScore - plan.DangerScore ) + ( ( plan.WinningMatchups - plan.LosingMatchups ) * 2 );
            kvp.Value.WinConScore = score;

            if( score > theirWinConScore )
            {
                theirWinConScore = score;
                theirWinCon = kvp.Key;
            }
        }

        gp.OurPrimaryWinCon = ourWinCon;
        gp.TheirPrimaryWinCon = theirWinCon;

        //--Get Blockers
        ourTeamScores = EvaluateWinConBlockers( theirWinCon, ourWinCon, theirComp, ourComp, ourTeamScores, isPrimaryUnit );
        theirTeamScores = EvaluateWinConBlockers( ourWinCon, theirWinCon, ourComp, theirComp, theirTeamScores, isPrimaryUnit );

        var ourBlockers = ourTeamScores.OrderByDescending( kvp => kvp.Value.BlockScore ).Where( x => x.Value.BlockScore > 0 ).Take( 3 ).ToList();
        var theirBlockers = theirTeamScores.OrderByDescending( kvp => kvp.Value.BlockScore ).Where( x => x.Value.BlockScore > 0 ).Take( 3 ).ToList();

        gp.OurBlockers = ourBlockers.Select( kvp => kvp.Key ).ToList();
        gp.TheirBlockers = theirBlockers.Select( kvp => kvp.Key ).ToList();

        //--Get Enablers
        ourTeamScores = EvaluateWinConEnablers( ourWinCon, ourComp, theirComp, ourTeamScores, theirBlockers, isPrimaryUnit );
        theirTeamScores = EvaluateWinConEnablers( theirWinCon, theirComp, ourComp, theirTeamScores, ourBlockers, isPrimaryUnit );

        var ourEnablers = ourTeamScores.OrderByDescending( kvp => kvp.Value.EnableScore ).Where( x => x.Value.EnableScore > 0 ).Take( 3 ).ToList();
        gp.OurEnablers = ourEnablers.Select( kvp => kvp.Key ).ToList();

        var theirEnablers =  theirTeamScores.OrderByDescending( kvp => kvp.Value.EnableScore ).Where( x => x.Value.EnableScore > 0 ).Take( 3 ).ToList();
        gp.TheirEnablers = theirEnablers.Select( kvp => kvp.Key ).ToList();

        //--Log Game Plan
        CustomLogSession gpLog = new();
        var ourWinConAdapter = GetPokemonAs_Adapter( gp.OurPrimaryWinCon );
        var theirWinConAdapter = GetPokemonAs_Adapter( gp.TheirPrimaryWinCon );

        gpLog.Add( $"=====================" );
        gpLog.Add( $"=====[Game Plan]=====" );
        gpLog.Add( $"=====================" );
        gpLog.Add( $"" );

        gpLog.Add( $"Our Primary Team Archetype: {ourComp.PrimaryArchetype}" );
        gpLog.Add( $"Their Primary Team Archetype: {theirComp.PrimaryArchetype}" );
        gpLog.Add( $"" );

        gpLog.Add( $"===[Win Condition]===" );
        gpLog.Add( $"Our Win Con: {gp.OurPrimaryWinCon.NickName} (Role: {ourWinConAdapter.RoleProfile.PrimaryRole})" );
        gpLog.Add( $"Advantage: {ourTeamScores[gp.OurPrimaryWinCon].AdvantageScore}" );
        gpLog.Add( $"Danger: {ourTeamScores[gp.OurPrimaryWinCon].DangerScore}" );
        gpLog.Add( $"Winning Matchups: {ourTeamScores[gp.OurPrimaryWinCon].WinningMatchups}" );
        gpLog.Add( $"Losing Matchups: {ourTeamScores[gp.OurPrimaryWinCon].LosingMatchups}" );
        gpLog.Add( $"Final WinCon Score: {ourTeamScores[gp.OurPrimaryWinCon].WinConScore}" );

        gpLog.Add( $"" );
        gpLog.Add( $"Their Win Con: {gp.TheirPrimaryWinCon.NickName} (Role: {theirWinConAdapter.RoleProfile.PrimaryRole})" );
        gpLog.Add( $"Advantage: {theirTeamScores[gp.TheirPrimaryWinCon].AdvantageScore}" );
        gpLog.Add( $"Danger: {theirTeamScores[gp.TheirPrimaryWinCon].DangerScore}" );
        gpLog.Add( $"Winning Matchups: {theirTeamScores[gp.TheirPrimaryWinCon].WinningMatchups}" );
        gpLog.Add( $"Losing Matchups: {theirTeamScores[gp.TheirPrimaryWinCon].LosingMatchups}" );
        gpLog.Add( $"Final WinCon Score: {theirTeamScores[gp.TheirPrimaryWinCon].WinConScore}" );

        gpLog.Add( $"" );
        gpLog.Add( $"===[Units to Eliminate]===" );
        gpLog.Add( $"=[Their Top Blockers]=" );
        foreach( var kvp in theirBlockers )
            gpLog.Add( $"{kvp.Key.NickName} ({kvp.Value.BlockScore})" );
        
        gpLog.Add( $"" );
        gpLog.Add( $"=[Their Top Enablers]=" );
        foreach( var kvp in theirEnablers )
            gpLog.Add( $"{kvp.Key.NickName} ({kvp.Value.EnableScore})" );
        
        gpLog.Add( $"" );
        gpLog.Add( $"===[Units to Preserve]===" );
        gpLog.Add( $"=[Our Top Blockers]=" );
        foreach( var kvp in ourBlockers )
            gpLog.Add( $"{kvp.Key.NickName} ({kvp.Value.BlockScore})" );

        gpLog.Add( $"" );
        gpLog.Add( $"=[Our Top Enablers]=" );
        foreach( var kvp in ourEnablers )
            gpLog.Add( $"{kvp.Key.NickName} ({kvp.Value.EnableScore})" );

        gpLog.Add( $"" );
        gpLog.Add( $"===[Our Team's Scores]===" );
        foreach( var kvp in ourTeamScores )
        {
            gpLog.Add( $"[{kvp.Key.NickName}]" );
            gpLog.Add( $"Advantage Score: {kvp.Value.AdvantageScore}" );
            gpLog.Add( $"Danger Score: {kvp.Value.DangerScore}" );
            gpLog.Add( $"Enable Score: {kvp.Value.EnableScore}" );
            gpLog.Add( $"Block Score: {kvp.Value.BlockScore}" );
            gpLog.Add( $"Winning Matchups: {kvp.Value.WinningMatchups}" );
            gpLog.Add( $"Losing Matchups: {kvp.Value.LosingMatchups}" );
            gpLog.Add( $"Final WinCon Score: {kvp.Value.WinConScore}" );
            gpLog.Add( $"" );
        }

        gpLog.Add( $"" );
        gpLog.Add( $"===[Their Team's Scores]===" );
        foreach( var kvp in theirTeamScores )
        {
            gpLog.Add( $"[{kvp.Key.NickName}]" );
            gpLog.Add( $"Advantage Score: {kvp.Value.AdvantageScore}" );
            gpLog.Add( $"Danger Score: {kvp.Value.DangerScore}" );
            gpLog.Add( $"Enable Score: {kvp.Value.EnableScore}" );
            gpLog.Add( $"Block Score: {kvp.Value.BlockScore}" );
            gpLog.Add( $"Winning Matchups: {kvp.Value.WinningMatchups}" );
            gpLog.Add( $"Losing Matchups: {kvp.Value.LosingMatchups}" );
            gpLog.Add( $"Final WinCon Score: {kvp.Value.WinConScore}" );
            gpLog.Add( $"" );
        }

        Debug.Log( gpLog.ToString() );
        gpLog.Clear();

        return gp;
    }

    public GamePlan GetOpponentGamePlan( GamePlan gp )
    {
        GamePlan theirPlan = new()
        {
            OurPrimaryWinCon = gp.TheirPrimaryWinCon,
            OurBlockers = gp.TheirBlockers,
            OurEnablers = gp.TheirEnablers,

            TheirPrimaryWinCon = gp.OurPrimaryWinCon,
            TheirBlockers = gp.OurBlockers,
            TheirEnablers = gp.OurEnablers,
        };

        return theirPlan;
    }

    private Dictionary<Pokemon, GamePlanAnalysis> EvaluateWinConCandidates( TeamComposition team1, TeamComposition team2, Dictionary<Pokemon, GamePlanAnalysis> ourTeamScores, Func<TeamComposition, BattleAI_PokemonAdapter, bool> isPrimaryUnit )
    {
        var ourTeam = team1.Team;
        var theirTeam = team2.Team;

        var ourComp = team1;
        var theirComp = team2;

        foreach( var ourMon in ourTeam )
        {
            var ourScores = ourTeamScores[ourMon.Pokemon];
            var ourRP = ourMon.RoleProfile;
            var pr = ourRP.PrimaryRole;
            bool ourMonIsPrimaryUnit = isPrimaryUnit( ourComp, ourMon );

            bool offensiveRole = pr == RoleClass.BulkyAttacker || pr == RoleClass.RevengeKiller || pr == RoleClass.SetupSweeper || pr == RoleClass.SetupSweeper || pr == RoleClass.WallBreaker;
            bool defensiveRole = pr == RoleClass.Wall || pr == RoleClass.DefensiveSetup || pr == RoleClass.BulkyAttacker && ourRP.SecondaryRoles.Contains( RoleClass.Wall );

            bool weHaveRecovery = ourRP.Traits.Contains( RoleTrait.RecoveryAbility ) || ourRP.Traits.Contains( RoleTrait.RecoveryItem ) || ourRP.Traits.Contains( RoleTrait.RecoveryMove );

            bool weBurn = ourRP.Traits.Contains( RoleTrait.BurnPressure );
            bool weFrost = ourRP.Traits.Contains( RoleTrait.FrostbitePressure );
            bool weSleep = ourRP.Traits.Contains( RoleTrait.SleepPressure );
            bool weParalyze = ourRP.Traits.Contains( RoleTrait.ParalysisPressure );
            bool weTaunt = ourRP.Traits.Contains( RoleTrait.Taunt );
            bool weEncore = ourRP.Traits.Contains( RoleTrait.Encore );
            bool weFakeOut = ourRP.Traits.Contains( RoleTrait.FakeOut );
            bool weSporePowder = weSleep && ( UnitSim.CheckHasMove( ourMon, "Sleep Powder" ) || UnitSim.CheckHasMove( ourMon, "Spore" ) );
            bool weParaPowder = weParalyze && UnitSim.CheckHasMove( ourMon, "Stun Spore" );
            bool weTWave = weParalyze && UnitSim.CheckHasMove( ourMon, "Thunder Wave" );
            bool wePowder = weSporePowder || weParaPowder || UnitSim.CheckHasMove( ourMon, "Poison Powder" ) || UnitSim.CheckHasMove( ourMon, "Rage Powder" );

            bool weLockdown = weSleep || weParalyze || weTaunt || weEncore || weFakeOut;

            foreach( var theirMon in theirTeam )
            {
                int advantageScore = 0;
                int dangerScore = 0;

                var ee = Projection.EvaluateExchange( ourMon, theirMon );

                var ourPTKO = ee.AttackerPTKOR.PTKO;
                var theirPTKO = ee.OpponentPTKOR.PTKO;

                var theirRP = theirMon.RoleProfile;

                bool theirMonIsPrimaryUnit = isPrimaryUnit( theirComp, theirMon );

                bool theySleep = theirRP.Traits.Contains( RoleTrait.SleepPressure );
                bool theyParalyze = theirRP.Traits.Contains( RoleTrait.ParalysisPressure );
                bool theyTaunt = theirRP.Traits.Contains( RoleTrait.Taunt );
                bool theyEncore = theirRP.Traits.Contains( RoleTrait.Encore );
                bool theyFakeOut = theirRP.Traits.Contains( RoleTrait.FakeOut );
                bool theyLockdown = theySleep || theyParalyze || theyTaunt || theyEncore || theyFakeOut;

                //----------------------------------
                //--Advantage Checks----------------
                //----------------------------------
                //--Offense
                if( ee.AttackerKillsFirst )
                {
                    advantageScore += 3;

                    if( theirMonIsPrimaryUnit )
                        advantageScore += 1;
                }
                else if( ee.AttackerThreatensKO )
                {
                    advantageScore += 2;

                    if( theirMonIsPrimaryUnit )
                        advantageScore += 1;
                }

                if( ee.OpponentSwitches )
                    advantageScore += 1;

                if( ee.AttackerSurvives )
                    advantageScore += 1;

                if( weLockdown )
                    advantageScore += 1;

                //--Role Performance
                if( ourRP.PrimaryRole == RoleClass.Sweeper )
                {
                    if( ourPTKO >= PotentialToKO.Dangerous )
                        advantageScore += 1;

                    if( ee.AttackerMovesFirst )
                        advantageScore += 1;
                }

                if( ourRP.PrimaryRole == RoleClass.SetupSweeper )
                {
                    if( theirPTKO <= PotentialToKO.Risky && ee.AttackerMovesFirst )
                    {
                        advantageScore += 1;

                        if( ourPTKO <= PotentialToKO.Risky && ( ourRP.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || ourRP.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup ) ) )
                            advantageScore += 1;
                    }
                }

                if( ourRP.PrimaryRole == RoleClass.WallBreaker && ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.DefensiveSetup ) )
                {
                    if( ourPTKO == PotentialToKO.OHKO && ee.AttackerSurvives )
                        advantageScore += 3;
                    else if( ourPTKO == PotentialToKO.Dangerous && ee.AttackerSurvives )
                        advantageScore += 2;
                    else if( ourPTKO == PotentialToKO.Risky && ee.AttackerSurvives )
                        advantageScore += 1;
                }

                if( ourRP.PrimaryRole == RoleClass.Wall )
                {
                    if( theirPTKO <= PotentialToKO.Safe )
                        advantageScore += 2;

                    if( ( theirPTKO <= PotentialToKO.TwoHKO || theirPTKO <= PotentialToKO.Risky && ee.AttackerMovesFirst ) && weHaveRecovery )
                            advantageScore += 1;

                    if( ourRP.Biases.Contains( RoleBias.PassivePressure ) )
                        advantageScore += 1;
                }

                if( ourRP.PrimaryRole == RoleClass.Disrupter && weLockdown )
                {
                    advantageScore += 2;

                    if( ee.AttackerMovesFirst || theirPTKO <= PotentialToKO.TwoHKO )
                        advantageScore += 1;

                    if( ourRP.Biases.Contains( RoleBias.PassivePressure ) )
                        advantageScore += 1;
                }

                if( ourRP.Biases.Contains( RoleBias.SpeedControl ) )
                {
                    advantageScore += 1;

                    if( ourComp.Strengths.Speed <= 15 )
                        advantageScore += 3;
                    else if( ourComp.Strengths.Speed <= 35 )
                        advantageScore += 2;
                    else if( ourComp.Strengths.Speed > 35 )
                        advantageScore += 1;
                }

                //----------------------------------
                //--Danger Checks-------------------
                //----------------------------------

                //--Offense
                if( ee.OpponentKillsFirst )
                {
                    dangerScore += 2;

                    if( ourMonIsPrimaryUnit )
                        dangerScore += 1;
                }

                if( ee.AttackerSwitches )
                    dangerScore += 2;

                if( ourPTKO <= PotentialToKO.TwoHKO )
                    dangerScore += 1;

                if( ourPTKO <= PotentialToKO.Risky && theirPTKO >= PotentialToKO.Dangerous )
                    dangerScore += 1;

                if( theyLockdown )
                    dangerScore += 1;

                //--Role Performance
                if( offensiveRole && ourPTKO <= PotentialToKO.Safe )
                    dangerScore += 2;
                else if( offensiveRole && ourPTKO <= PotentialToKO.TwoHKO )
                    dangerScore += 1;

                if( ourRP.PrimaryRole == RoleClass.SetupSweeper )
                {
                    bool safeSetupWindow = theirPTKO <= PotentialToKO.TwoHKO || ( theirPTKO <= PotentialToKO.Risky && ee.AttackerMovesFirst );
                    if( !safeSetupWindow )
                        dangerScore += 2;
                }

                if( ourRP.PrimaryRole == RoleClass.Disrupter )
                {
                    if( weBurn && ( theirRP.Traits.Contains( RoleTrait.BurnImmune ) || theirRP.Traits.Contains( RoleTrait.StatusMoveImmune ) ) )
                        dangerScore += 1;

                    if( weFrost && ( theirRP.Traits.Contains( RoleTrait.FrostImmune ) || theirRP.Traits.Contains( RoleTrait.StatusMoveImmune ) ) )
                        dangerScore += 1;

                    if( weSleep && ( theirRP.Traits.Contains( RoleTrait.SleepImmune ) || weSporePowder && theirRP.Traits.Contains( RoleTrait.PowderImmune ) || theirRP.Traits.Contains( RoleTrait.StatusMoveImmune ) ) )
                        dangerScore += 1;

                    if( weTWave && ( theirRP.Traits.Contains( RoleTrait.ThunderWaveImmune ) || weParaPowder && theirRP.Traits.Contains( RoleTrait.PowderImmune ) || theirRP.Traits.Contains( RoleTrait.StatusMoveImmune )  ) )
                        dangerScore += 1;

                    if( weTaunt && ( theirRP.Traits.Contains( RoleTrait.TauntImmune ) || theirRP.Traits.Contains( RoleTrait.StatusMoveImmune ) ) )
                        dangerScore += 1;

                    if( weFakeOut && theirRP.Traits.Contains( RoleTrait.FakeOutImmune ) )
                        dangerScore += 1;

                    if( wePowder && theirRP.Traits.Contains( RoleTrait.PowderImmune ) )
                        dangerScore += 1;
                }

                if( defensiveRole && theirPTKO >= PotentialToKO.Dangerous )
                    dangerScore += 1;

                if( advantageScore > dangerScore )
                    ourScores.WinningMatchups++;

                if( dangerScore > advantageScore )
                    ourScores.LosingMatchups++;

                ourScores.AdvantageScore += advantageScore;
                ourScores.DangerScore += dangerScore;
            }

            ourTeamScores[ourMon.Pokemon] = ourScores;
        }

        return ourTeamScores;
    }

    private Dictionary<Pokemon, GamePlanAnalysis> EvaluateWinConBlockers( Pokemon ourWinCon, Pokemon theirWinCon, TeamComposition ourComp, TeamComposition theirComp, Dictionary<Pokemon, GamePlanAnalysis> theirTeamScores, Func<TeamComposition, BattleAI_PokemonAdapter, bool> isPrimaryUnit )
    {
        BattleAI_PokemonAdapter winConAdapter = GetPokemonAs_Adapter( ourWinCon );
        var winConRP = winConAdapter.RoleProfile;
        var winConPR = winConRP.PrimaryRole;
        var winConSR = winConRP.SecondaryRoles;
        
        bool winConOffensiveRole = winConPR == RoleClass.BulkyAttacker || winConPR == RoleClass.RevengeKiller || winConPR == RoleClass.SetupSweeper || winConPR == RoleClass.SetupSweeper || winConPR == RoleClass.WallBreaker;
        bool winConDefensiveRole = winConPR == RoleClass.Wall || winConPR == RoleClass.DefensiveSetup || winConPR == RoleClass.BulkyAttacker && winConSR.Contains( RoleClass.Wall );
        bool winConUtilityRole = winConPR == RoleClass.Disrupter || winConPR == RoleClass.FieldControl || winConSR.Contains( RoleClass.FieldControl ) || winConPR == RoleClass.HazardControl ||
            winConSR.Contains( RoleClass.HazardControl ) || winConPR == RoleClass.Pivot || winConSR.Contains( RoleClass.Pivot ) || winConPR == RoleClass.UtilitySupport;
        
        bool winConIsPrimaryUnit = isPrimaryUnit( ourComp, winConAdapter );

        bool benefitsSpeedControl = winConRP.Biases.Contains( RoleBias.MiddlingSpeed ) || winConRP.Biases.Contains( RoleBias.AwkwardSpeed ) || winConRP.Biases.Contains( RoleBias.SlowSpeed );
        bool benefitsTrickRoom = winConRP.Biases.Contains( RoleBias.AwkwardSpeed ) || winConRP.Biases.Contains( RoleBias.SlowSpeed ) || winConRP.Biases.Contains( RoleBias.TrickRoomSpeed );
        bool benefitsTailwind = winConRP.Biases.Contains( RoleBias.MiddlingSpeed ) || winConRP.Biases.Contains( RoleBias.AwkwardSpeed );

        float rocksWeakness = TypeChart.GetEffectiveness( PokemonType.Rock, ourWinCon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( PokemonType.Rock, ourWinCon.PokeSO.Type2 );

        var theirTeam = theirComp.Team;

        foreach( var mon in theirTeam )
        {
            int blockScore = 0;
            var theirScores = theirTeamScores[mon.Pokemon];

            var ee = Projection.EvaluateExchange( winConAdapter, mon );
            var ourPTKO = ee.AttackerPTKOR.PTKO;
            var theirPTKO = ee.OpponentPTKOR.PTKO;
            bool weHaveSafeSetupWindow = theirPTKO <= PotentialToKO.TwoHKO || ( theirPTKO <= PotentialToKO.Risky && ee.AttackerMovesFirst );
            bool theyHaveSafeSetupWindow = ourPTKO <= PotentialToKO.TwoHKO || ( ourPTKO <= PotentialToKO.Risky && ee.OpponentMovesFirst );

            var theirRP = mon.RoleProfile;
            var theirPR = theirRP.PrimaryRole;
            bool theirMonIsPrimaryUnit = isPrimaryUnit( theirComp, mon );

            bool theyOffensiveRole = theirPR == RoleClass.BulkyAttacker || theirPR == RoleClass.RevengeKiller || theirPR == RoleClass.SetupSweeper || theirPR == RoleClass.SetupSweeper || theirPR == RoleClass.WallBreaker;
            bool theyDefensiveRole = theirPR == RoleClass.Wall || theirPR == RoleClass.DefensiveSetup || theirPR == RoleClass.BulkyAttacker && theirRP.SecondaryRoles.Contains( RoleClass.Wall );
            bool theyUtilityRole = theirPR == RoleClass.Disrupter || theirPR == RoleClass.FieldControl || theirRP.SecondaryRoles.Contains( RoleClass.FieldControl ) || theirPR == RoleClass.HazardControl ||
                theirRP.SecondaryRoles.Contains( RoleClass.HazardControl ) || theirPR == RoleClass.Pivot || theirRP.SecondaryRoles.Contains( RoleClass.Pivot ) || theirPR == RoleClass.UtilitySupport;

            bool theyHaveRecovery = theirRP.Traits.Contains( RoleTrait.RecoveryAbility ) || theirRP.Traits.Contains( RoleTrait.RecoveryItem ) || theirRP.Traits.Contains( RoleTrait.RecoveryMove );

            bool theyBurn = theirRP.Traits.Contains( RoleTrait.BurnPressure );
            bool theyFrost = theirRP.Traits.Contains( RoleTrait.FrostbitePressure );
            bool theySleep = theirRP.Traits.Contains( RoleTrait.SleepPressure );
            bool theyParalyze = theirRP.Traits.Contains( RoleTrait.ParalysisPressure );
            bool theyTaunt = theirRP.Traits.Contains( RoleTrait.Taunt );
            bool theyEncore = theirRP.Traits.Contains( RoleTrait.Encore );
            bool theyFakeOut = theirRP.Traits.Contains( RoleTrait.FakeOut );
            bool theySporePowder = theySleep && ( UnitSim.CheckHasMove( mon, "Sleep Powder" ) || UnitSim.CheckHasMove( mon, "Spore" ) );
            bool theyParaPowder = theyParalyze && UnitSim.CheckHasMove( mon, "Stun Spore" );
            bool theyTWave = theyParalyze && UnitSim.CheckHasMove( mon, "Thunder Wave" );
            bool theyPowder = theySporePowder || theyParaPowder || UnitSim.CheckHasMove( mon, "Poison Powder" ) || UnitSim.CheckHasMove( mon, "Rage Powder" );

            bool theyLockdown = theySleep || theyParalyze || theyTaunt || theyEncore || theyFakeOut;

            bool theySpeedControl = theirRP.Traits.Contains( RoleTrait.SpeedControl );
            bool theyTailwind = theirRP.Traits.Contains( RoleTrait.TailwindSetter );
            bool theyTrickRoom = theirRP.Traits.Contains( RoleTrait.TrickRoomSetter );
            bool theySetHazards = theirRP.Traits.Contains( RoleTrait.HazardSetter );
            bool theyRemoveHazards = theirRP.Traits.Contains( RoleTrait.HazardRemover );

            //---------------
            //--Reward Pass--
            //---------------

            //--Their offensive pressure against us
            if( ee.OpponentKillsFirst )
                blockScore += 4;
            else if( theirPTKO >= PotentialToKO.Dangerous )
                blockScore += 3;
            else if( theirPTKO >= PotentialToKO.Risky )
                blockScore += 2;

            if( ourPTKO <= PotentialToKO.TwoHKO )
                blockScore += 1;

            if( theirPTKO >= PotentialToKO.Risky && ( ourPTKO <= PotentialToKO.Safe || ( ourPTKO <= PotentialToKO.TwoHKO && ee.OpponentMovesFirst ) ) )
                blockScore += 1;

            //--Their Role Performance
            if( theirPR == RoleClass.SetupSweeper && theyHaveSafeSetupWindow )
                blockScore += 2;

            if( winConOffensiveRole && theyDefensiveRole )
            {
                if( winConRP.Biases.Contains( RoleBias.Physical ) && theirRP.Biases.Contains( RoleBias.PhysicallyBulky ) )
                    blockScore += 2;

                if( winConRP.Biases.Contains( RoleBias.Special ) && theirRP.Biases.Contains( RoleBias.SpeciallyBulky ) )
                    blockScore += 2;

                if( ourPTKO <= PotentialToKO.Safe && theyHaveRecovery )
                    blockScore += 3;
                else if( ourPTKO <= PotentialToKO.Risky && theyHaveRecovery )
                    blockScore += 1;
            }

            if( theyDefensiveRole )
            {
                if( ourPTKO <= PotentialToKO.TwoHKO && theyHaveRecovery )
                    blockScore += 2;
            }

            if( winConOffensiveRole || theyUtilityRole )
            {
                if( theyBurn && winConRP.Traits.Contains( RoleTrait.BurnWeak ) )
                    blockScore += 2;

                if( theyFrost && winConRP.Traits.Contains( RoleTrait.FrostWeak ) )
                    blockScore += 2;
            }

            if( theirRP.Traits.Contains( RoleTrait.HazardSetter ) && UnitSim.CheckHasMove( mon, "Stealth Rock" ) )
            {
                if( rocksWeakness >= 3f )
                    blockScore += 2;
                else if( rocksWeakness > 1f )
                    blockScore += 1;
            }

            if( winConDefensiveRole && theyOffensiveRole )
            {
                if( theirRP.Biases.Contains( RoleBias.Physical ) && winConRP.Biases.Contains( RoleBias.SpeciallyBulky ) )
                    blockScore += 2;

                if( theirRP.Biases.Contains( RoleBias.Special ) && winConRP.Biases.Contains( RoleBias.PhysicallyBulky ) )
                    blockScore += 2;
            }

            if( winConDefensiveRole )
            {
                if( theirRP.Traits.Contains( RoleTrait.ToxicPressure ) && winConRP.Traits.Contains( RoleTrait.ToxicWeak ) )
                    blockScore += 2;

                if( theirPR == RoleClass.WallBreaker && theirPTKO >= PotentialToKO.Risky )
                    blockScore += 1;
            }

            if( ( winConOffensiveRole || winConUtilityRole ) && theyLockdown )
                blockScore += 1;

            if( winConUtilityRole || winConPR == RoleClass.SetupSweeper )
            {
                if( theyEncore )
                    blockScore += 2;

                if( theyTaunt )
                    blockScore += 2;

                if( winConUtilityRole && winConRP.Traits.Contains( RoleTrait.HazardSetter ) && theyRemoveHazards )
                    blockScore += 1;

                if( theirRP.Traits.Contains( RoleTrait.Phazes ) )
                    blockScore += 2;
            }

            if( winConPR == RoleClass.SetupSweeper )
            {
                if( theirRP.Traits.Contains( RoleTrait.Haze ) )
                    blockScore += 2;

                if( mon.Ability == AbilityID.Unaware )
                    blockScore += 3;

                if( !weHaveSafeSetupWindow )
                    blockScore += 2;
            }

            if( winConRP.Biases.Contains( RoleBias.FastSpeed ) || winConRP.Biases.Contains( RoleBias.MiddlingSpeed ) )
            {
                if( theyParalyze && ( !winConRP.Traits.Contains( RoleTrait.PowderImmune ) && theyParaPowder || !winConRP.Traits.Contains( RoleTrait.ThunderWaveImmune ) && theyTWave ) )
                    blockScore += 2;

                if( theyTrickRoom )
                    blockScore += 2;

                if( theyTailwind )
                    blockScore += 1;
            }

            if( benefitsSpeedControl && theySpeedControl )
                blockScore += 1;

            if( mon.Pokemon == theirWinCon )
            {
                if( blockScore >= 5 )
                    blockScore += 4;
                else if( blockScore >= 3 )
                    blockScore += 3;
                else if( blockScore > 1 )
                    blockScore += 2;
            }
            else if( theirMonIsPrimaryUnit && blockScore > 1 )
                blockScore += 2;

            //----------------
            //--Penalty Pass--
            //----------------

            if( ee.AttackerKillsFirst )
                blockScore -= 3;
            else if( ourPTKO >= PotentialToKO.Dangerous && ( theirPTKO <= PotentialToKO.Safe || theirPTKO <= PotentialToKO.TwoHKO && ee.AttackerMovesFirst ) )
                blockScore -= 2;

            if( theyBurn && winConRP.Traits.Contains( RoleTrait.BurnImmune ) )
                blockScore -= 1;

            if( ( theyTWave && winConRP.Traits.Contains( RoleTrait.ThunderWaveImmune ) ) || ( theyParaPowder && winConRP.Traits.Contains( RoleTrait.PowderImmune ) ) )
                blockScore -= 1;

            if( theySporePowder && ( winConRP.Traits.Contains( RoleTrait.SleepImmune ) || winConRP.Traits.Contains( RoleTrait.PowderImmune ) ) )
                blockScore -= 1;

            if( theySleep && winConRP.Traits.Contains( RoleTrait.SleepImmune ) )
                blockScore -= 1;

            if( theirRP.Traits.Contains( RoleTrait.ToxicPressure ) && winConRP.Traits.Contains( RoleTrait.PoisonToxImmune ) )
                blockScore -= 1;

            if( theirRP.Traits.Contains( RoleTrait.Taunt ) && winConRP.Traits.Contains( RoleTrait.TauntImmune ) )
                blockScore -= 1;

            if( theirRP.Traits.Contains( RoleTrait.Encore ) && winConRP.Traits.Contains( RoleTrait.StatusMoveImmune ) )
                blockScore -= 1;

            if( winConPR == RoleClass.SetupSweeper && weHaveSafeSetupWindow )
                blockScore -= 2;

            if( theyDefensiveRole && ourPTKO >= PotentialToKO.Risky )
                blockScore -= 2;

            if( theyUtilityRole || theirPR == RoleClass.SetupSweeper )
            {
                if( winConRP.Traits.Contains( RoleTrait.Taunt ) )
                    blockScore -= 2;

                if( winConRP.Traits.Contains( RoleTrait.Encore ) )
                    blockScore -= 2;
                
                if( winConRP.Traits.Contains( RoleTrait.Phazes ) )
                    blockScore -= 2;
            }

            if( theirPR == RoleClass.SetupSweeper )
            {
                if( winConRP.Traits.Contains( RoleTrait.Haze ) )
                    blockScore -= 2;

                if( winConAdapter.Ability == AbilityID.Unaware )
                    blockScore -= 2;

                if( !theyHaveSafeSetupWindow )
                    blockScore -= 2;
            }

            if( theyDefensiveRole && winConPR == RoleClass.WallBreaker && ourPTKO >= PotentialToKO.Risky )
                blockScore -= 1;

            if( ( mon.Pokemon == theirWinCon || theirMonIsPrimaryUnit ) && blockScore <= 0 )
                blockScore -= 3;

            if( blockScore >= 5 )
                theirScores.WinningMatchups += 1;
            else if( blockScore <= 0 )
                theirScores.LosingMatchups += 1;

            theirScores.BlockScore += blockScore;
            theirTeamScores[mon.Pokemon] = theirScores;
        }

        return theirTeamScores;
    }

    private Dictionary<Pokemon, GamePlanAnalysis> EvaluateWinConEnablers( Pokemon ourWinCon, TeamComposition ourComp, TeamComposition theirComp, Dictionary<Pokemon, GamePlanAnalysis> ourTeamScores, List<KeyValuePair<Pokemon, GamePlanAnalysis>> theirBlockers, Func<TeamComposition, BattleAI_PokemonAdapter, bool> isPrimaryUnit )
    {
        BattleAI_PokemonAdapter winConAdapter = GetPokemonAs_Adapter( ourWinCon );
        var winConRP = winConAdapter.RoleProfile;
        var winConPR = winConRP.PrimaryRole;
        var winConSR = winConRP.SecondaryRoles;
        
        bool winConOffensiveRole = winConPR == RoleClass.BulkyAttacker || winConPR == RoleClass.RevengeKiller || winConPR == RoleClass.SetupSweeper || winConPR == RoleClass.SetupSweeper || winConPR == RoleClass.WallBreaker;
        bool winConDefensiveRole = winConPR == RoleClass.Wall || winConPR == RoleClass.DefensiveSetup || winConPR == RoleClass.BulkyAttacker && winConSR.Contains( RoleClass.Wall );
        bool winConUtilityRole = winConPR == RoleClass.Disrupter || winConPR == RoleClass.FieldControl || winConSR.Contains( RoleClass.FieldControl ) || winConPR == RoleClass.HazardControl ||
            winConSR.Contains( RoleClass.HazardControl ) || winConPR == RoleClass.Pivot || winConSR.Contains( RoleClass.Pivot ) || winConPR == RoleClass.UtilitySupport;
        
        bool winConIsPrimaryUnit = isPrimaryUnit( ourComp, winConAdapter );

        bool benefitsSpeedControl = winConRP.Biases.Contains( RoleBias.MiddlingSpeed ) || winConRP.Biases.Contains( RoleBias.AwkwardSpeed ) || winConRP.Biases.Contains( RoleBias.SlowSpeed );
        bool benefitsTrickRoom = winConRP.Biases.Contains( RoleBias.AwkwardSpeed ) || winConRP.Biases.Contains( RoleBias.SlowSpeed ) || winConRP.Biases.Contains( RoleBias.TrickRoomSpeed );
        bool benefitsTailwind = winConRP.Biases.Contains( RoleBias.MiddlingSpeed ) || winConRP.Biases.Contains( RoleBias.AwkwardSpeed );

        float rocksWeakness = TypeChart.GetEffectiveness( PokemonType.Rock, ourWinCon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( PokemonType.Rock, ourWinCon.PokeSO.Type2 );

        bool winConHasRecovery = winConRP.Traits.Contains( RoleTrait.RecoveryAbility ) || winConRP.Traits.Contains( RoleTrait.RecoveryItem ) || winConRP.Traits.Contains( RoleTrait.RecoveryMove );

        var team = ourComp.Team.Where( p => p.Pokemon != ourWinCon ).ToList();

        foreach( var mon in team )
        {
            int enableScore = 0;
            var ourScores = ourTeamScores[mon.Pokemon];

            var ourRP = mon.RoleProfile;
            var pr = ourRP.PrimaryRole;
            bool ourMonIsPrimaryUnit = isPrimaryUnit( ourComp, mon );

            bool offensiveRole = pr == RoleClass.BulkyAttacker || pr == RoleClass.RevengeKiller || pr == RoleClass.SetupSweeper || pr == RoleClass.SetupSweeper || pr == RoleClass.WallBreaker;
            bool defensiveRole = pr == RoleClass.Wall || pr == RoleClass.DefensiveSetup || pr == RoleClass.BulkyAttacker && ourRP.SecondaryRoles.Contains( RoleClass.Wall );
            bool utilityRole = pr == RoleClass.Disrupter || pr == RoleClass.FieldControl || ourRP.SecondaryRoles.Contains( RoleClass.FieldControl ) || pr == RoleClass.HazardControl ||
                ourRP.SecondaryRoles.Contains( RoleClass.HazardControl ) || pr == RoleClass.Pivot || ourRP.SecondaryRoles.Contains( RoleClass.Pivot ) || pr == RoleClass.UtilitySupport;

            bool weHaveRecovery = ourRP.Traits.Contains( RoleTrait.RecoveryAbility ) || ourRP.Traits.Contains( RoleTrait.RecoveryItem ) || ourRP.Traits.Contains( RoleTrait.RecoveryMove );

            bool weBurn = ourRP.Traits.Contains( RoleTrait.BurnPressure );
            bool weFrost = ourRP.Traits.Contains( RoleTrait.FrostbitePressure );
            bool weSleep = ourRP.Traits.Contains( RoleTrait.SleepPressure );
            bool weParalyze = ourRP.Traits.Contains( RoleTrait.ParalysisPressure );
            bool weTaunt = ourRP.Traits.Contains( RoleTrait.Taunt );
            bool weEncore = ourRP.Traits.Contains( RoleTrait.Encore );
            bool weFakeOut = ourRP.Traits.Contains( RoleTrait.FakeOut );
            bool weSporePowder = weSleep && ( UnitSim.CheckHasMove( mon, "Sleep Powder" ) || UnitSim.CheckHasMove( mon, "Spore" ) );
            bool weParaPowder = weParalyze && UnitSim.CheckHasMove( mon, "Stun Spore" );
            bool weTWave = weParalyze && UnitSim.CheckHasMove( mon, "Thunder Wave" );
            bool wePowder = weSporePowder || weParaPowder || UnitSim.CheckHasMove( mon, "Poison Powder" ) || UnitSim.CheckHasMove( mon, "Rage Powder" );

            bool weLockdown = weSleep || weParalyze || weTaunt || weEncore || weFakeOut;

            bool weSpeedControl = ourRP.Traits.Contains( RoleTrait.SpeedControl );
            bool weTailwind = ourRP.Traits.Contains( RoleTrait.TailwindSetter );
            bool weTrickRoom = ourRP.Traits.Contains( RoleTrait.TrickRoomSetter );
            bool weSetHazards = ourRP.Traits.Contains( RoleTrait.HazardSetter );
            bool weRemoveHazards = ourRP.Traits.Contains( RoleTrait.HazardRemover );

            bool wePivot = pr == RoleClass.Pivot || ourRP.Traits.Contains( RoleTrait.PivotMove ) || ourRP.Traits.Contains( RoleTrait.FastPivot ) || ourRP.Traits.Contains( RoleTrait.SlowPivot );

            //--How we handle our Win Con's blockers
            foreach( var blocker in theirBlockers )
            {
                var ee = Projection.EvaluateExchange( mon, GetPokemonAs_Adapter( blocker.Key ) );
                var enablerPTKO = ee.AttackerPTKOR.PTKO;
                var blockerPTKO = ee.OpponentPTKOR.PTKO;
                var blockerAdapter = GetPokemonAs_Adapter( blocker.Key );

                var theirRP = blockerAdapter.RoleProfile;
                bool theirMonIsPrimaryUnit = isPrimaryUnit( theirComp, blockerAdapter );

                bool theyHaveRecovery = theirRP.Traits.Contains( RoleTrait.RecoveryAbility ) || theirRP.Traits.Contains( RoleTrait.RecoveryItem ) || theirRP.Traits.Contains( RoleTrait.RecoveryMove );

                //----------------------------------
                //--Positive Checks----------------
                //----------------------------------

                //--Offense
                if( ee.AttackerKillsFirst )
                {
                    enableScore += 3;

                    if( theirMonIsPrimaryUnit )
                        enableScore += 2;

                    if( blocker.Value.BlockScore >= 10 )
                        enableScore += 3;
                    else if( blocker.Value.BlockScore >= 5 )
                        enableScore += 2;
                }
                else if( enablerPTKO >= PotentialToKO.Risky && ee.AttackerMovesFirst || enablerPTKO >= PotentialToKO.Dangerous )
                {
                    enableScore += 2;

                    if( theirMonIsPrimaryUnit )
                        enableScore += 1;

                    if( blocker.Value.BlockScore >= 10 )
                        enableScore += 3;
                    else if( blocker.Value.BlockScore >= 5 )
                        enableScore += 2;
                }

                if( ee.OpponentSwitches )
                    enableScore += 1;

                if( ee.AttackerSurvives )
                    enableScore += 1;

                if( theyHaveRecovery && enablerPTKO >= PotentialToKO.Risky )
                {
                    enableScore += 1;

                    if( ee.AttackerMovesFirst )
                        enableScore += 1;
                }

                //--Role Performance
                if( ourRP.PrimaryRole == RoleClass.Sweeper )
                {
                    if( enablerPTKO >= PotentialToKO.Dangerous )
                        enableScore += 1;

                    if( ee.AttackerMovesFirst )
                        enableScore += 1;
                }

                if( ourRP.PrimaryRole == RoleClass.SetupSweeper )
                {
                    if( blockerPTKO <= PotentialToKO.Risky && ee.AttackerMovesFirst )
                    {
                        enableScore += 1;

                        if( enablerPTKO <= PotentialToKO.Risky && ( ourRP.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || ourRP.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup ) ) )
                            enableScore += 1;
                    }
                }

                if( ourRP.PrimaryRole == RoleClass.WallBreaker && ( theirRP.PrimaryRole == RoleClass.Wall || theirRP.PrimaryRole == RoleClass.DefensiveSetup ) )
                {
                    if( enablerPTKO == PotentialToKO.OHKO && ee.AttackerSurvives )
                    {
                        enableScore += 3;

                        if( theirMonIsPrimaryUnit )
                            enableScore += 2;
                    }
                    else if( enablerPTKO == PotentialToKO.Dangerous && ee.AttackerSurvives )
                    {
                        enableScore += 2;

                        if( theirMonIsPrimaryUnit )
                            enableScore += 1;
                    }
                    else if( enablerPTKO == PotentialToKO.Risky && ee.AttackerSurvives )
                    {
                        enableScore += 1;

                        if( theirMonIsPrimaryUnit )
                            enableScore += 1;
                    }
                }

                if( ourRP.PrimaryRole == RoleClass.Wall )
                {
                    if( blockerPTKO <= PotentialToKO.Safe )
                    {
                        enableScore += 2;
                        
                        if( theirMonIsPrimaryUnit )
                            enableScore += 1;
                    }

                    if( ( blockerPTKO <= PotentialToKO.TwoHKO || blockerPTKO <= PotentialToKO.Risky && ee.AttackerMovesFirst ) && weHaveRecovery )
                            enableScore += 1;

                    if( ourRP.Biases.Contains( RoleBias.PassivePressure ) )
                        enableScore += 1;
                }

                if( ourRP.PrimaryRole == RoleClass.Disrupter && weLockdown )
                {
                    enableScore += 2;

                    if( ee.AttackerMovesFirst || blockerPTKO <= PotentialToKO.TwoHKO )
                        enableScore += 1;

                    if( ourRP.Biases.Contains( RoleBias.PassivePressure ) )
                        enableScore += 1;

                    if( theirMonIsPrimaryUnit )
                        enableScore += 1;
                }

                if( ourRP.Biases.Contains( RoleBias.SpeedControl ) )
                {
                    enableScore += 1;

                    if( ourComp.Strengths.Speed <= 15 )
                        enableScore += 3;
                    else if( ourComp.Strengths.Speed <= 35 )
                        enableScore += 2;
                    else if( ourComp.Strengths.Speed > 35 )
                        enableScore += 1;
                }

                if( ( weSleep && ( theirRP.Traits.Contains( RoleTrait.SleepImmune ) || weSporePowder && theirRP.Traits.Contains( RoleTrait.PowderImmune ) || theirRP.Traits.Contains( RoleTrait.StatusMoveImmune ) ) ) ||
                    ( weTWave && ( theirRP.Traits.Contains( RoleTrait.ThunderWaveImmune ) || weParaPowder && theirRP.Traits.Contains( RoleTrait.PowderImmune ) || theirRP.Traits.Contains( RoleTrait.StatusMoveImmune )  ) ) )
                {
                    enableScore += 1;

                    if( blocker.Value.BlockScore >= 10 )
                        enableScore += 3;
                    else if( blocker.Value.BlockScore >= 5 )
                        enableScore += 2;

                    if( theirMonIsPrimaryUnit )
                        enableScore += 1;
                }

                //----------------------------------
                //--Negative Checks-----------------
                //----------------------------------

                //--Offense
                if( ee.OpponentKillsFirst )
                    enableScore -= 3;
                else if( blockerPTKO >= PotentialToKO.Risky && ee.OpponentMovesFirst || blockerPTKO >= PotentialToKO.Dangerous )
                    enableScore -= 2;

                if( enablerPTKO <= PotentialToKO.TwoHKO && blockerPTKO >= PotentialToKO.Risky )
                    enableScore -= 2;

                //--Role Performance
                if( offensiveRole && enablerPTKO <= PotentialToKO.Safe )
                    enableScore -= 2;
                else if( offensiveRole && enablerPTKO <= PotentialToKO.TwoHKO )
                    enableScore -= 1;

                if( ourRP.PrimaryRole == RoleClass.SetupSweeper )
                {
                    bool safeSetupWindow = blockerPTKO <= PotentialToKO.TwoHKO || ( blockerPTKO <= PotentialToKO.Risky && ee.AttackerMovesFirst );
                    if( !safeSetupWindow )
                        enableScore -= 2;
                }

                if( ourRP.PrimaryRole == RoleClass.Disrupter )
                {
                    if( weBurn && ( theirRP.Traits.Contains( RoleTrait.BurnImmune ) || theirRP.Traits.Contains( RoleTrait.StatusMoveImmune ) ) )
                        enableScore -= 1;

                    if( weFrost && ( theirRP.Traits.Contains( RoleTrait.FrostImmune ) || theirRP.Traits.Contains( RoleTrait.StatusMoveImmune ) ) )
                        enableScore -= 1;

                    if( weSleep && ( theirRP.Traits.Contains( RoleTrait.SleepImmune ) || weSporePowder && theirRP.Traits.Contains( RoleTrait.PowderImmune ) || theirRP.Traits.Contains( RoleTrait.StatusMoveImmune ) ) )
                        enableScore -= 1;

                    if( weTWave && ( theirRP.Traits.Contains( RoleTrait.ThunderWaveImmune ) || weParaPowder && theirRP.Traits.Contains( RoleTrait.PowderImmune ) || theirRP.Traits.Contains( RoleTrait.StatusMoveImmune )  ) )
                        enableScore -= 1;

                    if( weTaunt && ( theirRP.Traits.Contains( RoleTrait.TauntImmune ) || theirRP.Traits.Contains( RoleTrait.StatusMoveImmune ) ) )
                        enableScore -= 1;

                    if( weFakeOut && theirRP.Traits.Contains( RoleTrait.FakeOutImmune ) )
                        enableScore -= 1;

                    if( wePowder && theirRP.Traits.Contains( RoleTrait.PowderImmune ) )
                        enableScore -= 1;
                }

                if( defensiveRole && blockerPTKO >= PotentialToKO.Dangerous )
                    enableScore -= 1;
            }

            //--How we support our Win Con directly
            if( benefitsSpeedControl && weSpeedControl )
            {
                enableScore += 1;

                if( weTailwind && benefitsTailwind )
                    enableScore += 1;
            }

            if( benefitsTrickRoom && weTrickRoom )
                enableScore += 3;

            if( weRemoveHazards && theirComp.Primary_HazardSetter != null )
            {
                if( rocksWeakness >= 4f )
                    enableScore += 3;
                else if( rocksWeakness >= 2f )
                    enableScore += 2;
                else if( rocksWeakness >= 1f )
                    enableScore += 1;
            }

            if( ourTeamScores[ourWinCon].WinningMatchups >= 5 && weSetHazards )
                enableScore += 3;
            else if( ourTeamScores[ourWinCon].WinningMatchups >= 3 && weSetHazards )
                enableScore += 2;
            else if( ourTeamScores[ourWinCon].WinningMatchups >= 2 && weSetHazards )
                enableScore += 1;

            if( UnitSim.CheckHasMove( mon, "Wish" ) )
            {
                enableScore += 2;

                if( winConDefensiveRole || rocksWeakness >= 2 )
                    enableScore += 1;

                if( !winConHasRecovery )
                    enableScore += 1;
            }

            if( wePivot )
            {
                if( winConPR == RoleClass.SetupSweeper )
                    enableScore += 3;
                else if( winConPR == RoleClass.WallBreaker )
                    enableScore += 3;
                else if( winConOffensiveRole )
                    enableScore += 2;
            }

            if( winConPR == RoleClass.SetupSweeper )
            {
                if( weSleep )
                    enableScore += 2;

                if( weParalyze )
                    enableScore += 1;

                if( weEncore )
                    enableScore += 2;

                if( weTaunt )
                    enableScore += 2;
            }

            ourScores.EnableScore += enableScore;
            ourTeamScores[mon.Pokemon] = ourScores;
        }

        return ourTeamScores;
    }

    public enum GPHealthBand { Healthy, Pressured, Critical, NearlyDead, Dead }
    private GPHealthBand Get_GamePlanHealthBand( float hpr )
    {
        if( hpr >= 0.7f )               return GPHealthBand.Healthy;
        else if( hpr >= 0.4f )          return GPHealthBand.Pressured;
        else if( hpr >= 0.2f )          return GPHealthBand.Critical;
        else if( hpr > 0f )             return GPHealthBand.NearlyDead;
        else                            return GPHealthBand.Dead;
    }

    private int EvaluateHPStateChange( GPHealthBand start, GPHealthBand end, int roleValue, bool penalties = false )
    {
        int shift = (int)start - (int)end;
        int score = 0;

        if( shift > 0 )
        {
            if( shift == 1 )            score = roleValue;
            else if( shift == 2 )       score = roleValue + 5;
            else if( shift == 3 )       score = roleValue + 15;
            else if( shift == 4 )       score = roleValue + 25;

            return score;
        }

        if( shift < 0 )
        {
            if( shift == -1 )            score = roleValue;
            else if( shift == -2 )       score = roleValue + 5;
            else if( shift == -3 )       score = roleValue + 15;
            else if( shift == -4 )       score = roleValue + 25;

            if( penalties )
                score += roleValue / 2;

            return -score;
        }

        return score;
    }

    public struct GPAData
    {
        public int WinConValue;
        public int BlockerValue;
        public int EnablerValue;

        public GamePlan GamePlan;
        public ActionEvaluation Action;

        public TurnOutcomeProjection TOP1;
        public TurnOutcomeProjection TOP2;
        public SimulatedUnit Attacker1;
        public SimulatedUnit Attacker2;
        public SimulatedUnit Opponent1;
        public SimulatedUnit Opponent2;

        public float DamageDoneNow;
        public float DamageDoneNext;
        public float DamageTakenNow;
        public float DamageTakenNext;

        public GPHealthBand Attacker1_StartBand;
        public GPHealthBand Attacker1_EndBand;
        public GPHealthBand Attacker2_StartBand;
        public GPHealthBand Attacker2_EndBand;
        public GPHealthBand Opponent1_StartBand;
        public GPHealthBand Opponent1_EndBand;
        public GPHealthBand Opponent2_StartBand;
        public GPHealthBand Opponent2_EndBand;

        public bool WeAreDifferentMonNext;
        public bool TheyAreDifferentMonNext;
        public bool WeSwitchedThisTurn;

        public bool WeFaintNow;
        public bool WeFaintNext;
        public bool WeAreDelayKOd;
        public bool TheyFaintNow;
        public bool TheyFaintNext;
        public bool TheyAreDelayKOd;

        public bool WeAreWinCon;
        public bool WeAreBlocker;
        public bool WeAreEnabler;

        public bool TheyAreWinCon;
        public bool TheyAreBlocker;
        public bool TheyAreEnabler;
    }

    private int GamePlanAlignment( ActionEvaluation action, GamePlan gp )
    {
        //--Unit Values
        const int wincon_value = 20;
        const int blocker_value = 15;
        const int enabler_value = 10;

        int score = 0;

        var top1 = action.Top1;
        var top2 = action.Top2;

        var attacker1 = action.Top1.Attacker;
        var attacker2 = action.Top2.Attacker;
        var opponent1 = action.Top1.Opponent;
        var opponent2 = action.Top2.Opponent;

        GPAData gpd = new()
        {
            WinConValue = wincon_value,
            BlockerValue = blocker_value,
            EnablerValue = enabler_value,

            GamePlan = gp,
            Action = action,

            TOP1 = action.Top1,
            TOP2 = action.Top2,
            Attacker1 = action.Top1.Attacker,
            Attacker2 = action.Top2.Attacker,
            Opponent1 = action.Top1.Opponent,
            Opponent2 = action.Top2.Opponent,

            DamageDoneNow = Mathf.Floor( ( opponent1.BeginningHPR - top1.Opponent_EndOfTurnHP ) * 1000f ) / 1000f,
            DamageDoneNext = Mathf.Floor( ( opponent2.BeginningHPR - top2.Opponent_EndOfTurnHP ) * 1000f ) / 1000f,
            DamageTakenNow = Mathf.Floor( ( attacker1.BeginningHPR - top1.Attacker_EndOfTurnHP ) * 1000f ) / 1000f,
            DamageTakenNext = Mathf.Floor( ( attacker2.BeginningHPR - top2.Attacker_EndOfTurnHP ) * 1000f ) / 1000f,

            Attacker1_StartBand = Get_GamePlanHealthBand( top1.Attacker.BeginningHPR ),
            Attacker1_EndBand = Get_GamePlanHealthBand( top1.Attacker_EndOfTurnHP ),

            Attacker2_StartBand = Get_GamePlanHealthBand( top2.Attacker.BeginningHPR ),
            Attacker2_EndBand = Get_GamePlanHealthBand( top2.Attacker_EndOfTurnHP ),

            Opponent1_StartBand = Get_GamePlanHealthBand( top1.Opponent.BeginningHPR ),
            Opponent1_EndBand = Get_GamePlanHealthBand( top1.Opponent_EndOfTurnHP ),

            Opponent2_StartBand = Get_GamePlanHealthBand( top2.Opponent.BeginningHPR ),
            Opponent2_EndBand = Get_GamePlanHealthBand( top2.Opponent_EndOfTurnHP ),
            
            WeAreDifferentMonNext = attacker1.Pokemon != attacker2.Pokemon,
            TheyAreDifferentMonNext = opponent1.Pokemon != opponent2.Pokemon,

            WeSwitchedThisTurn = action.Type == ActionType.OffensiveSwitch || action.Type == ActionType.DefensiveSwitch,

            WeFaintNow = top1.Attacker_EndOfTurnHP <= 0f,
            WeFaintNext = top2.Attacker_EndOfTurnHP <= 0f,
            WeAreDelayKOd = top1.Attacker_EndOfTurnHP <= 0f && attacker1.Pokemon == attacker2.Pokemon,

            TheyFaintNow = top1.Opponent_EndOfTurnHP <= 0f,
            TheyFaintNext = top2.Opponent_EndOfTurnHP <= 0f,
            TheyAreDelayKOd = top1.Opponent_EndOfTurnHP <= 0f && opponent1.Pokemon == opponent2.Pokemon,

            WeAreWinCon = attacker1.Pokemon == gp.OurPrimaryWinCon,
            WeAreBlocker = gp.OurBlockers.Contains( attacker1.Pokemon ),
            WeAreEnabler = gp.OurEnablers.Contains( attacker1.Pokemon ),

            TheyAreWinCon = opponent1.Pokemon == gp.TheirPrimaryWinCon,
            TheyAreBlocker = gp.TheirBlockers.Contains( opponent1.Pokemon ),
            TheyAreEnabler = gp.TheirEnablers.Contains( opponent1.Pokemon ),
        };

        CurrentLog.Add( $"" );
        CurrentLog.Add( $"===============================================" );
        CurrentLog.Add( $"=====[Game Plan Alignment ({action.Type})]=====" );
        CurrentLog.Add( $"===============================================" );
        CurrentLog.Add( $"" );

        score += GPScore_OffensiveProgress( gpd );
        score += GPScore_PreservationProgress( gpd );
        score += GPScore_SevereStatusProgress( gpd );
        score += GPScore_StatChangeProgress( gpd );
        score += GPScore_DisruptionProgress( gpd );
        score += GPScore_PositionProgress( gpd );
        score += GPScore_BattlefieldProgress( gpd );

        //-----------------------------------------------------------------------------------
        //--Preserving Our Critical Units----------------------------------------------------
        //-----------------------------------------------------------------------------------

        CurrentLog.Add( $"Final Game Plan Alignment score for {attacker1.Name}'s {action.Type}: {score}" );
        CurrentLog.Add( $"===============================================" );
        CurrentLog.Add( $"" );

        return score;
    }

    private int GPScore_OffensiveProgress( GPAData gpd )
    {
        int score = 0;
        
        //-----------------------------------------------------------------------------------
        //--Offensive Progress Against Opponent----------------------------------------------
        //-----------------------------------------------------------------------------------
        if( !gpd.WeSwitchedThisTurn )
        {
            //--Win Con--------------------------------------------------------------------------
            if( gpd.TheyAreWinCon )
            {
                CurrentLog.Add( $"[Offensive Progress][Win Con] Target is their Win Condition" );

                score -= EvaluateHPStateChange( gpd.Opponent1_StartBand, gpd.Opponent1_EndBand, gpd.WinConValue );
                CurrentLog.Add( $"[Offensive Progress][Win Con] This Turn HP Band: {gpd.Opponent1_StartBand} -> {gpd.Opponent1_EndBand}. Bands Crossed: {(int)gpd.Opponent1_StartBand - (int)gpd.Opponent1_EndBand}. Score: {score}" );
                
                if( gpd.TheyFaintNow )
                {
                    score += gpd.WinConValue + 5;
                    CurrentLog.Add( $"[Offensive Progress][Win Con] They faint this turn. Score: {score}" );
                }
                else if( gpd.TheyAreDelayKOd )
                {
                    score += gpd.WinConValue;
                    CurrentLog.Add( $"[Offensive Progress][Win Con] They faint next turn. Score: {score}" );
                }
            }
            //--Blocker------------------------------------------------------------------------------
            else if( gpd.TheyAreBlocker )
            {
                CurrentLog.Add( $"[Offensive Progress][Blocker] Target is one of their Blockers" );

                score -= EvaluateHPStateChange( gpd.Opponent1_StartBand, gpd.Opponent1_EndBand, gpd.BlockerValue );
                CurrentLog.Add( $"[Offensive Progress][Blocker] This Turn HP Band: {gpd.Opponent1_StartBand} -> {gpd.Opponent1_EndBand}. Bands Crossed: {(int)gpd.Opponent1_StartBand - (int)gpd.Opponent1_EndBand}. Score: {score}" );
                
                if( gpd.TheyFaintNow )
                {
                    score += gpd.BlockerValue + 10;
                    CurrentLog.Add( $"[Offensive Progress][Blocker] They faint this turn. Score: {score}" );
                }
                else if( gpd.TheyAreDelayKOd )
                {
                    score += gpd.BlockerValue + 5;
                    CurrentLog.Add( $"[Offensive Progress][Blocker] They faint next turn. Score: {score}" );
                }
            }
            //--Enabler-------------------------------------------------------------------------------
            else if( gpd.TheyAreEnabler )
            {
                CurrentLog.Add( $"[Offensive Progress][Enabler] Target is one of their Enablers." );

                score -= EvaluateHPStateChange( gpd.Opponent1_StartBand, gpd.Opponent1_EndBand, gpd.EnablerValue );
                CurrentLog.Add( $"[Offensive Progress][Enabler] This Turn HP Band: {gpd.Opponent1_StartBand} -> {gpd.Opponent1_EndBand}. Bands Crossed: {(int)gpd.Opponent1_StartBand - (int)gpd.Opponent1_EndBand}. Score: {score}" );
                
                if( gpd.TheyFaintNow )
                {
                    score += gpd.EnablerValue + 5;
                    CurrentLog.Add( $"[Offensive Progress][Enabler] They faint this turn. Score: {score}" );
                }
                else if( gpd.TheyAreDelayKOd )
                {
                    score += gpd.EnablerValue;
                    CurrentLog.Add( $"[Offensive Progress][Enabler] They faint next turn. Score: {score}" );
                }
            }
        }

        return score;
    }

    private int GPScore_PreservationProgress( GPAData gpd )
    {
        int score = 0;

        //--Win Con--------------------------------------------------------------------------
        if( gpd.WeAreWinCon && !gpd.WeSwitchedThisTurn )
        {
            CurrentLog.Add( $"[Preservation][Win Con] This is our WinCon" );

            score += EvaluateHPStateChange( gpd.Attacker1_StartBand, gpd.Attacker1_EndBand, gpd.WinConValue );
            CurrentLog.Add( $"[Offensive Progress][Win Con] This Turn HP Band: {gpd.Attacker1_StartBand} -> {gpd.Attacker1_EndBand}. Bands Crossed: {(int)gpd.Attacker1_StartBand - (int)gpd.Attacker1_EndBand}. Score: {score}" );   
        }
        else if( gpd.WeAreWinCon && gpd.WeSwitchedThisTurn )
        {
            if( gpd.DamageTakenNow >= 0.4f )
            {
                score -= gpd.WinConValue + 10;
                CurrentLog.Add( $"[Preservation][Win Con] We switch in our win condition this turn and take significant incoming damage. Score: {score}" );
            }
        }
        //--Blocker------------------------------------------------------------------------------
        else if( gpd.WeAreBlocker && !gpd.WeSwitchedThisTurn )
        {
            CurrentLog.Add( $"[Preservation][Blocker] This is one of our Blockers" );

            score += EvaluateHPStateChange( gpd.Attacker1_StartBand, gpd.Attacker1_EndBand, gpd.BlockerValue );
            CurrentLog.Add( $"[Offensive Progress][Blocker] This Turn HP Band: {gpd.Attacker1_StartBand} -> {gpd.Attacker1_EndBand}. Bands Crossed: {(int)gpd.Attacker1_StartBand - (int)gpd.Attacker1_EndBand}. Score: {score}" );                
        }
        else if( gpd.WeAreBlocker && gpd.WeSwitchedThisTurn )
        {
            if( gpd.DamageTakenNow >= 0.4f )
            {
                score -= gpd.BlockerValue + 5;
                CurrentLog.Add( $"[Preservation][Blocker] We switch in one of our blockers this turn and take significant incoming damage. Score: {score}" );
            }
        }
        //--Enabler------------------------------------------------------------------------------
        else if( gpd.WeAreEnabler && !gpd.WeSwitchedThisTurn )
        {
            CurrentLog.Add( $"[Preservation][Enabler] This is one of our Enablers" );

            score += EvaluateHPStateChange( gpd.Attacker1_StartBand, gpd.Attacker1_EndBand, gpd.EnablerValue );
            CurrentLog.Add( $"[Offensive Progress][Enabler] This Turn HP Band: {gpd.Attacker1_StartBand} -> {gpd.Attacker1_EndBand}. Bands Crossed: {(int)gpd.Attacker1_StartBand - (int)gpd.Attacker1_EndBand}. Score: {score}" );                
        }
        else if( gpd.WeAreEnabler && gpd.WeSwitchedThisTurn )
        {
            if( gpd.DamageTakenNow >= 0.4f )
            {
                score -= gpd.EnablerValue;
                CurrentLog.Add( $"[Preservation][Enabler] We switch in one of our enablers this turn and take significant incoming damage. Score: {score}" );
            }
        }

        CurrentLog.Add( $"[Preservation] Final Preservation Progress Score: {score}" );

        return score;
    }

    private int GPScore_SevereStatusProgress( GPAData gpd )
    {
        int score = 0;
        
        //--Data!
        var ourTraits = gpd.Attacker1.RoleProfile.Traits;
        var theirTraits = gpd.Opponent1.RoleProfile.Traits;

        //--Severe Status Application
        //--We Apply
        bool weBurn = !gpd.TheyAreDifferentMonNext && gpd.Opponent1.SevereStatus == SevereConditionID.None && gpd.Opponent2.SevereStatus == SevereConditionID.BRN;
        bool weFrost = !gpd.TheyAreDifferentMonNext && gpd.Opponent1.SevereStatus == SevereConditionID.None && gpd.Opponent2.SevereStatus == SevereConditionID.FBT;
        bool wePoison = !gpd.TheyAreDifferentMonNext && gpd.Opponent1.SevereStatus == SevereConditionID.None && gpd.Opponent2.SevereStatus == SevereConditionID.PSN;
        bool weToxic = !gpd.TheyAreDifferentMonNext && gpd.Opponent1.SevereStatus == SevereConditionID.None && gpd.Opponent2.SevereStatus == SevereConditionID.TOX;
        bool weParalyze = !gpd.TheyAreDifferentMonNext && gpd.Opponent1.SevereStatus == SevereConditionID.None && gpd.Opponent2.SevereStatus == SevereConditionID.PAR;
        bool weSleep = !gpd.TheyAreDifferentMonNext && gpd.Opponent1.SevereStatus == SevereConditionID.None && gpd.Opponent2.SevereStatus == SevereConditionID.SLP;

        bool weStatus = weBurn || weFrost || wePoison || weToxic || weParalyze || weSleep;

        //--They Apply
        bool theyBurn = !gpd.TheyAreDifferentMonNext && gpd.Attacker1.SevereStatus == SevereConditionID.None && gpd.Attacker2.SevereStatus == SevereConditionID.BRN;
        bool theyFrost = !gpd.TheyAreDifferentMonNext && gpd.Attacker1.SevereStatus == SevereConditionID.None && gpd.Attacker2.SevereStatus == SevereConditionID.FBT;
        bool theyPoison = !gpd.TheyAreDifferentMonNext && gpd.Attacker1.SevereStatus == SevereConditionID.None && gpd.Attacker2.SevereStatus == SevereConditionID.PSN;
        bool theyToxic = !gpd.TheyAreDifferentMonNext && gpd.Attacker1.SevereStatus == SevereConditionID.None && gpd.Attacker2.SevereStatus == SevereConditionID.TOX;
        bool theyParalyze = !gpd.TheyAreDifferentMonNext && gpd.Attacker1.SevereStatus == SevereConditionID.None && gpd.Attacker2.SevereStatus == SevereConditionID.PAR;
        bool theySleep = !gpd.TheyAreDifferentMonNext && gpd.Attacker1.SevereStatus == SevereConditionID.None && gpd.Attacker2.SevereStatus == SevereConditionID.SLP;

        bool theyStatus = theyBurn || theyFrost || theyPoison || theyToxic || theyParalyze || theySleep;

        //--Value of Status Application Checks (did we burn a physical attacker? did they toxic our wall?)
        bool theyAreBurnWeak = theirTraits.Contains( RoleTrait.BurnWeak );
        bool theyAreFrostWeak = theirTraits.Contains( RoleTrait.FrostWeak );
        bool theyAreToxicWeak = theirTraits.Contains( RoleTrait.ToxicWeak );
        bool theyAreParalysisWeak = theirTraits.Contains( RoleTrait.ParalysisWeak  );

        bool weAreBurnWeak = ourTraits.Contains( RoleTrait.BurnWeak );
        bool weAreFrostWeak = ourTraits.Contains( RoleTrait.FrostWeak );
        bool weAreToxicWeak = ourTraits.Contains( RoleTrait.ToxicWeak );
        bool weAreParalysisWeak = ourTraits.Contains( RoleTrait.ParalysisWeak  );

        bool wePassiveRecover = ourTraits.Contains( RoleTrait.RecoveryItem ) || ourTraits.Contains( RoleTrait.RecoveryAbility );
        bool theyPassiveRecover = theirTraits.Contains( RoleTrait.RecoveryItem ) || theirTraits.Contains( RoleTrait.RecoveryAbility );

        int paralysisValue = gpd.TheyAreWinCon ? 15 : gpd.TheyAreBlocker ? 10 : gpd.TheyAreEnabler ? 5 : 0;
        int sleepValue = gpd.TheyAreWinCon ? 20 : gpd.TheyAreBlocker ? 15 : gpd.TheyAreEnabler ? 10 : 0;

        bool weAreGamePlanTarget = gpd.WeAreWinCon || gpd.WeAreBlocker || gpd.WeAreEnabler;
        bool theyAreGamePlanTarget = gpd.TheyAreWinCon || gpd.TheyAreBlocker || gpd.TheyAreEnabler;

        if( weStatus && theyAreGamePlanTarget )
        {
            score += 5;
            CurrentLog.Add( $"[Severe Status] We status them and they are a game plan target." );

            if( gpd.TheyAreWinCon )
            {
                score += gpd.WinConValue;
                CurrentLog.Add( $"[Severe Status] They are the opponent's Win Condition." );
            }
            else if( gpd.TheyAreBlocker )
            {
                score += gpd.BlockerValue;
                CurrentLog.Add( $"[Severe Status] They are one of the opponent's Blockers." );
            }
            else if( gpd.TheyAreEnabler )
            {
                score += gpd.EnablerValue;
                CurrentLog.Add( $"[Severe Status] They are one of the opponent's Enablers." );
            }

            if( weBurn && theyAreBurnWeak )
            {
                score += 10;
                CurrentLog.Add( $"[Severe Status] We burn them and they are burn weak." );
            }

            if( weFrost && theyAreFrostWeak )
            {
                score += 10;
                CurrentLog.Add( $"[Severe Status] We frostbite them and they are frostbite weak." );
            }

            if( wePoison && theyPassiveRecover )
            {
                score += 5;
                CurrentLog.Add( $"[Severe Status] We poison them and they have passive recovery, effectively neutralizing it." );
            }

            if( weToxic && theyAreToxicWeak )
            {
                score += 15;
                CurrentLog.Add( $"[Severe Status] We toxic them and they are toxic weak (probably a wall)." );
            }

            if( weParalyze && theyAreParalysisWeak )
            {
                score += paralysisValue;
                CurrentLog.Add( $"[Severe Status] We paralyze them and they are paralysis weak." );
            }

            if( weSleep )
            {
                score += sleepValue;
                CurrentLog.Add( $"[Severe Status] We sleep them." );
            }
        }

        if( theyStatus && weAreGamePlanTarget )
        {
            score -= 10;
            CurrentLog.Add( $"[Severe Status] They status us and we are essential to our game plan. Score: {score}" );

            if( gpd.WeAreWinCon )
            {
                score -= gpd.WinConValue + 10;
                CurrentLog.Add( $"[Severe Status] We are our Win Condition. Score: {score}" );
            }
            else if( gpd.WeAreBlocker )
            {
                score -= gpd.BlockerValue + 7;
                CurrentLog.Add( $"[Severe Status] We are one of our Blockers. Score: {score}" );
            }
            else if( gpd.WeAreEnabler )
            {
                score -= gpd.EnablerValue + 5;
                CurrentLog.Add( $"[Severe Status] We are one of our Enablers. Score: {score}" );
            }

            if( theyBurn && weAreBurnWeak )
            {
                score -= 10;
                CurrentLog.Add( $"[Severe Status] They burn us and we are burn weak. Score: {score}" );
            }

            if( theyFrost && weAreFrostWeak )
            {
                score -= 10;
                CurrentLog.Add( $"[Severe Status] They frostbite us and we are frostbite weak. Score: {score}" );
            }

            if( theyPoison && wePassiveRecover )
            {
                score -= 5;
                CurrentLog.Add( $"[Severe Status] They poison us and we have passive recovery, effectively neutralizing it. Score: {score}" );
            }

            if( theyToxic && weAreToxicWeak )
            {
                score -= 15;
                CurrentLog.Add( $"[Severe Status] They toxic us and we are toxic weak (probably a wall). Score: {score}" );
            }

            if( theyParalyze && weAreParalysisWeak )
            {
                score -= paralysisValue;
                CurrentLog.Add( $"[Severe Status] They paralyze us and we are paralysis weak. Score: {score}" );
            }

            if( theySleep )
            {
                score -= sleepValue;
                CurrentLog.Add( $"[Severe Status] They sleep us. Score: {score}" );
            }
        }

        CurrentLog.Add( $"[Severe Status] Final Severe Status Progress Score: {score}" );

        return score;
    }

    private int GPScore_StatChangeProgress( GPAData gpd )
    {
        int score = 0;

        if( gpd.WeAreDifferentMonNext )
            return 0;

        var ourRP = gpd.TOP1.Attacker.RoleProfile;
        var ourStatStages1 = gpd.TOP1.Attacker.StatStages;
        var ourStatStages2 = gpd.TOP2.Attacker.StatStages;

        bool statsChanged = false;

        bool weAreGamePlanUnit = gpd.WeAreWinCon || gpd.WeAreBlocker || gpd.WeAreEnabler;

        foreach( var kvp in ourStatStages2 )
        {
            if( ourStatStages1.TryGetValue( kvp.Key, out var stage ) && stage != ourStatStages2[kvp.Key] && !gpd.WeSwitchedThisTurn )
            {
                statsChanged = true;
                break;
            }
        }

        if( statsChanged )
        {
            var gp = gpd.GamePlan;
            var oppTeam = GetAllyTeamAs_Adapter( gpd.TOP1.Opponent.Pokemon ).Where( p => p.CurrentHPR > 0f ).ToList();
            Dictionary<Pokemon, ( TurnOutcomeProjection before, TurnOutcomeProjection after )> ourTOPs = new();
            Dictionary<Pokemon, ( TurnOutcomeProjection before, TurnOutcomeProjection after )> theirTOPs = new();

            foreach( var opp in oppTeam )
            {
                var ourBefore = MoveCommand.GetMove_BestAttack( gpd.Attacker1, opp ).Top;
                var ourAfter = MoveCommand.GetMove_BestAttack( gpd.Attacker2, opp ).Top;

                ourTOPs.Add( opp.Pokemon, ( ourBefore, ourAfter ) );

                var theirBefore = MoveCommand.GetMove_BestAttack( opp, gpd.Attacker1 ).Top;
                var theirAfter = MoveCommand.GetMove_BestAttack( opp, gpd.Attacker2 ).Top;

                theirTOPs.Add( opp.Pokemon, ( theirBefore, theirAfter ) );
            }

            int carryPotential = 0;

            int offensiveStagesGained = 0;
            int defensiveStagesGained = 0;
            int speedStagesGained = 0;

            int offensiveStagesLost = 0;
            int defensiveStagesLost = 0;
            int speedStagesLost = 0;

            int kosBefore = 0;
            int kosAfter = 0;

            int gpKOsBefore = 0;
            int gpKOsAfter = 0;

            int outSpeedsBefore = 0;
            int outSpeedsAfter = 0;

            int gpOutSpeedsBefore = 0;
            int gpOutSpeedsAfter = 0;

            int survivabilityGained = 0;
            int survivabilityLost = 0;

            int gpSurvivedBefore = 0;
            int gpSurvivedAfter = 0;

            bool gainedSpeedControl;
            bool lostSpeedControl;

            if( weAreGamePlanUnit )
            {
                //--General Stat Improvement check
                foreach( var sc in ourStatStages2 )
                {
                    var stat = sc.Key;
                    int delta = ourStatStages2[stat] - ourStatStages1[stat];

                    if( delta > 0 )
                    {
                        if( sc.Key == Stat.Attack || sc.Key == Stat.SpAttack )
                            offensiveStagesGained += delta;

                        if( sc.Key == Stat.Defense || sc.Key == Stat.SpDefense )
                            defensiveStagesGained += delta;

                        if( sc.Key == Stat.Speed )
                            speedStagesGained += delta;
                    }

                    if( delta < 0 )
                    {
                        if( sc.Key == Stat.Attack || sc.Key == Stat.SpAttack )
                            offensiveStagesLost -= delta;

                        if( sc.Key == Stat.Defense || sc.Key == Stat.SpDefense )
                            defensiveStagesLost -= delta;

                        if( sc.Key == Stat.Speed )
                            speedStagesLost -= delta;
                    }
                }
            }

            //--Sweep improvement check
            bool atkChanged = ourStatStages1[Stat.Attack] != ourStatStages2[Stat.Attack];
            bool spAtkChanged = ourStatStages1[Stat.SpAttack] != ourStatStages2[Stat.SpAttack];
            bool speedChanged = ourStatStages1[Stat.Speed] != ourStatStages2[Stat.Speed];

            if( atkChanged || spAtkChanged || speedChanged )
            {
                foreach( var kvp in ourTOPs )
                {       
                    var opp = kvp.Key;
                    ( TurnOutcomeProjection ourBefore, TurnOutcomeProjection ourAfter ) = kvp.Value;
                    ( TurnOutcomeProjection theirBefore, TurnOutcomeProjection theirAfter ) = theirTOPs[opp];

                    var ourBeforePTKO = ourBefore.AttackerPTKO;
                    var ourAfterPTKO = ourAfter.AttackerPTKO;

                    var theirBeforePTKO = theirBefore.AttackerPTKO;
                    var theirAfterPTKO = theirAfter.AttackerPTKO;

                    bool weOutSpeedNow = ourBefore.AttackerMovedFirst;
                    bool weOutSpeedNext = ourAfter.AttackerMovedFirst;
                    
                    bool theyAreWinCon = gp.TheirPrimaryWinCon == opp;
                    bool theyAreBlocker = gp.TheirBlockers.Contains( opp );
                    bool theyAreEnabler = gp.TheirEnablers.Contains( opp );
                    bool theyAreGamePlanUnit = theyAreWinCon || theyAreBlocker || theyAreEnabler;

                    if( ourBeforePTKO >= PotentialToKO.Dangerous && ( weOutSpeedNow || theirBeforePTKO <= PotentialToKO.TwoHKO ) )
                    {
                        kosBefore++;

                        if( theyAreGamePlanUnit )
                            gpKOsBefore++;
                    }

                    if( ourAfterPTKO >= PotentialToKO.Dangerous && ( weOutSpeedNext || theirAfterPTKO <= PotentialToKO.TwoHKO ) )
                    {
                        kosAfter++;

                        if( theyAreGamePlanUnit )
                            gpKOsAfter++;
                    }

                    if( weOutSpeedNow )
                    {
                        outSpeedsBefore++;

                        if( theyAreGamePlanUnit )
                            gpOutSpeedsBefore++;
                    }
                    
                    if( weOutSpeedNext )
                    {
                        outSpeedsAfter++;

                        if( theyAreGamePlanUnit )
                            gpOutSpeedsAfter++;
                    }
                }
            }

            //--Walling/Survivability improvement check
            bool defChanged = ourStatStages1[Stat.Defense] != ourStatStages2[Stat.Defense];
            bool spDefChanged = ourStatStages1[Stat.SpDefense] != ourStatStages2[Stat.SpDefense];

            if( defChanged || spDefChanged )
            {
                foreach( var kvp in theirTOPs )
                {
                    var opp = kvp.Key;
                    ( TurnOutcomeProjection theirBefore, TurnOutcomeProjection theirAfter ) = kvp.Value;

                    bool theyAreWinCon = gp.TheirPrimaryWinCon == opp;
                    bool theyAreBlocker = gp.TheirBlockers.Contains( opp );
                    bool theyAreEnabler = gp.TheirEnablers.Contains( opp );
                    bool theyAreGamePlanUnit = theyAreWinCon || theyAreBlocker || theyAreEnabler;

                    var theirBeforePTKO = theirBefore.AttackerPTKO;
                    var theirAfterPTKO = theirAfter.AttackerPTKO;

                    int ptkoDelta = theirBeforePTKO - theirAfterPTKO;

                    if( ptkoDelta > 0 )
                        survivabilityGained += ptkoDelta;

                    if( ptkoDelta < 0 )
                        survivabilityLost += Mathf.Abs( ptkoDelta );

                    if( theyAreGamePlanUnit && ( theirBeforePTKO <= PotentialToKO.TwoHKO && !theirBefore.AttackerMovedFirst || theirBeforePTKO <= PotentialToKO.Safe ) )
                        gpSurvivedBefore++;

                    if( theyAreGamePlanUnit && ( theirAfterPTKO <= PotentialToKO.TwoHKO && !theirBefore.AttackerMovedFirst || theirAfterPTKO <= PotentialToKO.Safe ) )
                        gpSurvivedAfter++;
                }
            }

            int kosGained = kosAfter - kosBefore;
            int gpKOsGained = gpKOsAfter - gpKOsBefore;
            int speedGained = outSpeedsAfter - outSpeedsBefore;
            int gpSpeedGained = gpOutSpeedsAfter - gpOutSpeedsBefore;
            int gpSurvivesGained = gpSurvivedAfter - gpSurvivedBefore;

            gainedSpeedControl = outSpeedsAfter > outSpeedsBefore + 1 || outSpeedsAfter > 3 || ( speedGained > 2 && outSpeedsBefore > 2 );
            lostSpeedControl = outSpeedsAfter < outSpeedsBefore - 1 || outSpeedsBefore >= 3 && outSpeedsAfter < 3 || ( speedGained < 2 && outSpeedsBefore > 3 );

            CurrentLog.Add( $"[Stat Changes] Our stat stages change this turn." );

            if( kosAfter > 3 && kosGained > 0 )
                carryPotential += 5;

            CurrentLog.Add( $"[Stat Changes] KOs After: {kosAfter}. Sweep Value: {carryPotential}" );

            if( kosGained > 2 )
                carryPotential += 5;

            CurrentLog.Add( $"[Stat Changes] KOs Gained: {kosGained}. Sweep Value: {carryPotential}" );

            if( gpKOsGained > 1 )
                carryPotential += 5;

            CurrentLog.Add( $"[Stat Changes] Gameplan KOs Gained: {gpKOsGained}. Sweep Value: {carryPotential}" );

            if( outSpeedsAfter > 3 && speedGained > 0 )
                carryPotential += 5;

            CurrentLog.Add( $"[Stat Changes] Outspeeds After: {outSpeedsAfter}. Sweep Value: {carryPotential}" );

            if( speedGained > 2 )
                carryPotential += 5;

            CurrentLog.Add( $"[Stat Changes] Outspeeds gained Gained: {speedGained}. Sweep Value: {carryPotential}" );

            if( gpSpeedGained > 1 )
                carryPotential += 5;

            if( gpSurvivesGained > 1 )
                carryPotential += 5;

            CurrentLog.Add( $"[Stat Changes] Gameplan Outspeeds Gained: {gpSpeedGained}. Sweep Value: {carryPotential}" );

            score += carryPotential;
            
            score += kosGained * 3;
            score += speedGained * 3;
            score += survivabilityGained * 3;

            CurrentLog.Add( $"[Stat Changes] KOs Gained ({kosGained}) * 3: {kosGained * 3}. Score: {score}" );
            CurrentLog.Add( $"[Stat Changes] Speed Gained ({speedGained}) * 3: {speedGained * 3}. Score: {score}" );
            CurrentLog.Add( $"[Stat Changes] Survivability Gained ({survivabilityGained}) * 3: {survivabilityGained * 3}. Score: {score}" );

            score += gpKOsGained * 5;
            score += gpSpeedGained * 5;
            score += gpSurvivesGained * 5;
            
            CurrentLog.Add( $"[Stat Changes] Gameplan KOs Gained ({gpKOsGained}) * 5: {gpKOsGained * 5}. Score: {score}" );
            CurrentLog.Add( $"[Stat Changes] Gameplan Outspeeds Gained ({gpSpeedGained}) * 5: {gpSpeedGained * 5}. Score: {score}" );
            CurrentLog.Add( $"[Stat Changes] Gameplan Survives Gained ({gpSurvivesGained}) * 5: {gpSurvivesGained * 5}. Score: {score}" );

            score += offensiveStagesGained * 3;
            score += defensiveStagesGained * 3;
            score += speedStagesGained * 5;

            CurrentLog.Add( $"[Stat Changes] Offensive Stages Gained ({offensiveStagesGained}) * 2: {offensiveStagesGained * 2}. Score: {score}" );
            CurrentLog.Add( $"[Stat Changes] Offensive Stages Gained ({defensiveStagesGained}) * 2: {defensiveStagesGained * 2}. Score: {score}" );
            CurrentLog.Add( $"[Stat Changes] Offensive Stages Gained ({speedStagesGained}) * 3: {speedStagesGained * 3}. Score: {score}" );

            score -= offensiveStagesLost * 2;
            score -= defensiveStagesLost * 2;
            score -= speedStagesLost * 3;

            CurrentLog.Add( $"[Stat Changes] Offensive Stages Lost ({offensiveStagesLost}) * 2: {offensiveStagesLost * 2}. Score: {score}" );
            CurrentLog.Add( $"[Stat Changes] Offensive Stages Lost ({defensiveStagesLost}) * 2: {defensiveStagesLost * 2}. Score: {score}" );
            CurrentLog.Add( $"[Stat Changes] Offensive Stages Lost ({speedStagesLost}) * 3: {speedStagesLost * 3}. Score: {score}" );

            score -= survivabilityLost * 3;

            CurrentLog.Add( $"[Stat Changes] Survivability Lost ({survivabilityLost}) * 3: {survivabilityLost * 3}. Score: {score}" );

            if( gainedSpeedControl )
                score += 10;

            if( lostSpeedControl )
                score -= 15;
        }

        return score;
    }

    private int GPScore_DisruptionProgress( GPAData gpd )
    {
        int score = 0;

        //--This entire scoring module will need to be revisited once Threat Intent and doubles coordination exists.

        var ourTraits = gpd.Attacker1.RoleProfile.Traits;
        var theirTraits = gpd.Opponent1.RoleProfile.Traits;

        bool weTaunt = !gpd.TheyAreDifferentMonNext && !gpd.Opponent1.VolatileStatuses.Contains( VolatileConditionID.Taunt ) && gpd.Opponent2.VolatileStatuses.Contains( VolatileConditionID.Taunt );
        bool weEncore = !gpd.TheyAreDifferentMonNext && !gpd.Opponent1.VolatileStatuses.Contains( VolatileConditionID.Encore ) && gpd.Opponent2.VolatileStatuses.Contains( VolatileConditionID.Encore );
        bool weTroatChop = !gpd.TheyAreDifferentMonNext && !gpd.Opponent1.VolatileStatuses.Contains( VolatileConditionID.ThroatChop ) && gpd.Opponent2.VolatileStatuses.Contains( VolatileConditionID.ThroatChop );
        bool weHealBlock = !gpd.TheyAreDifferentMonNext && !gpd.Opponent1.VolatileStatuses.Contains( VolatileConditionID.HealBlocked ) && gpd.Opponent2.VolatileStatuses.Contains( VolatileConditionID.HealBlocked );
        bool weDisable = !gpd.TheyAreDifferentMonNext && !gpd.Opponent1.VolatileStatuses.Contains( VolatileConditionID.Disabled ) && gpd.Opponent2.VolatileStatuses.Contains( VolatileConditionID.Disabled );
        bool weFakeOut = gpd.Attacker1.MTR?.Move?.MoveSO.Name == "Fake Out";
        bool weFollowMe = gpd.Attacker1.MTR?.Move?.MoveSO.Name == "Follow Me";
        bool weRagePowder = gpd.Attacker1.MTR?.Move?.MoveSO.Name == "Rage Powder";
        bool weRedirect = weFollowMe || weRagePowder;

        bool weDisrupt = weTaunt || weEncore || weTroatChop || weHealBlock || weDisable || weFakeOut || weRedirect;

        bool weUsedSoundMove = gpd.Attacker1.MTR.Move != null ? gpd.Attacker1.MTR.Move.MoveSO.Flags.Contains( MoveFlags.Sound ) : false;
        bool weUsedHealingMove = gpd.Attacker1.MTR.Move != null ? gpd.Attacker1.MTR.Move.MoveSO.Flags.Contains( MoveFlags.Heal ) : false;
        MoveTarget ourMoveTarget = gpd.Attacker1.MTR.Move != null ? gpd.Attacker1.MTR.Move.MoveSO.MoveTarget : MoveTarget.Enemy;
        bool weUsedSpreadMove = ourMoveTarget == MoveTarget.OpposingSide || ourMoveTarget == MoveTarget.AllAdjacent;

        bool theyTaunt = !gpd.WeAreDifferentMonNext && !gpd.Attacker1.VolatileStatuses.Contains( VolatileConditionID.Taunt ) && gpd.Attacker2.VolatileStatuses.Contains( VolatileConditionID.Taunt );
        bool theyEncore = !gpd.WeAreDifferentMonNext && !gpd.Attacker1.VolatileStatuses.Contains( VolatileConditionID.Encore ) && gpd.Attacker2.VolatileStatuses.Contains( VolatileConditionID.Encore );
        bool theyTroatChop = !gpd.WeAreDifferentMonNext && !gpd.Attacker1.VolatileStatuses.Contains( VolatileConditionID.ThroatChop ) && gpd.Attacker2.VolatileStatuses.Contains( VolatileConditionID.ThroatChop );
        bool theyHealBlock = !gpd.WeAreDifferentMonNext && !gpd.Attacker1.VolatileStatuses.Contains( VolatileConditionID.HealBlocked ) && gpd.Attacker2.VolatileStatuses.Contains( VolatileConditionID.HealBlocked );
        bool theyDisable = !gpd.WeAreDifferentMonNext && !gpd.Attacker1.VolatileStatuses.Contains( VolatileConditionID.Disabled ) && gpd.Attacker2.VolatileStatuses.Contains( VolatileConditionID.Disabled );
        bool theyFakeOut = gpd.Opponent1.MTR?.Move?.MoveSO.Name == "Fake Out";
        bool theyFollowMe = gpd.Opponent1.MTR?.Move?.MoveSO.Name == "Follow Me";
        bool theyRagePowder = gpd.Opponent1.MTR?.Move?.MoveSO.Name == "Rage Powder";
        bool theyRedirect = theyFollowMe || theyRagePowder;

        bool theyDisrupt = theyTaunt || theyEncore || theyTroatChop || theyHealBlock || theyDisable || theyFakeOut || theyRedirect;

        bool theyUsedSoundMove = gpd.Opponent1.MTR?.Move != null ? gpd.Opponent1.MTR.Move.MoveSO.Flags.Contains( MoveFlags.Sound ) : false;
        bool theyUsedHealingMove = gpd.Opponent1.MTR?.Move != null ? gpd.Opponent1.MTR.Move.MoveSO.Flags.Contains( MoveFlags.Heal ) : false;
        MoveTarget theirMoveTarget = gpd.Opponent1.MTR?.Move != null ? gpd.Opponent1.MTR.Move.MoveSO.MoveTarget : MoveTarget.Enemy;
        bool theyUsedSpreadMove = theirMoveTarget == MoveTarget.OpposingSide || theirMoveTarget == MoveTarget.AllAdjacent;

        bool weAreTauntWeak = ourTraits.Contains( RoleTrait.TauntWeak );
        bool weAreEncoreWeak = ourTraits.Contains( RoleTrait.EncoreWeak );
        bool weAreDisableWeak = gpd.Attacker1.VolatileStatuses.Contains( VolatileConditionID.ChoiceLocked );
        bool weAreSilenceWeak = ourTraits.Contains( RoleTrait.SoundMoves ) || ( weUsedSoundMove && !gpd.TOP1.AttackerMovedFirst );

        bool theyAreTauntWeak = theirTraits.Contains( RoleTrait.TauntWeak );
        bool theyAreEncoreWeak = theirTraits.Contains( RoleTrait.EncoreWeak );
        bool theyAreDisableWeak = gpd.Opponent1.VolatileStatuses.Contains( VolatileConditionID.ChoiceLocked );
        bool theyAreSilenceWeak = theirTraits.Contains( RoleTrait.SoundMoves ) || ( theyUsedSoundMove && gpd.TOP1.AttackerMovedFirst );

        bool weHaveRecoveryMove = ourTraits.Contains( RoleTrait.RecoveryMove );
        bool theyHaveRecoveryMove = theirTraits.Contains( RoleTrait.RecoveryMove );

        bool weAreGamePlanTarget = gpd.WeAreWinCon || gpd.WeAreBlocker || gpd.WeAreEnabler;
        bool theyAreGamePlanTarget = gpd.TheyAreWinCon || gpd.TheyAreBlocker || gpd.TheyAreEnabler;

        if( weDisrupt && ( weAreGamePlanTarget || theyAreGamePlanTarget ) )
        {
            score += 5;
            CurrentLog.Add( $"[Disruption] We disrupt and we are part of our game plan: {weAreGamePlanTarget} or they are a game plan target: {theyAreGamePlanTarget}" );

            if( gpd.TheyAreWinCon || gpd.WeAreWinCon )
            {
                score += gpd.WinConValue;
                CurrentLog.Add( $"[Disruption] They are the opponent's Win Condition. Score: {score}" );
            }
            else if( gpd.TheyAreBlocker || gpd.WeAreBlocker )
            {
                score += gpd.BlockerValue;
                CurrentLog.Add( $"[Disruption] They are one of the opponent's Blockers. Score: {score}" );
            }
            else if( gpd.TheyAreEnabler || gpd.WeAreEnabler )
            {
                score += gpd.EnablerValue;
                CurrentLog.Add( $"[Disruption] They are one of the opponent's Enablers. Score: {score}" );
            }

            if( weTaunt && theyAreTauntWeak )
            {
                score += 10;
                CurrentLog.Add( $"[Disruption] We taunt and they are taunt weak. Score: {score}" );

                if( theyHaveRecoveryMove )
                    score += 5;
            }

            if( weEncore && theyAreEncoreWeak )
            {
                score += 10;
                CurrentLog.Add( $"[Disruption] We encore and they are encore weak. Score: {score}" );

                if( theyHaveRecoveryMove )
                    score += 5;
            }

            if( weTroatChop && theyAreSilenceWeak )
            {
                score += 10;
                CurrentLog.Add( $"[Disruption] We silence and they are silence weak. Score: {score}" );
            }

            if( weHealBlock && theyHaveRecoveryMove )
            {
                score += 15;
                CurrentLog.Add( $"[Disruption] We heal block and they have healing moves weak. Score: {score}" );
            }

            if( weDisable )
            {
                score += 5;
                CurrentLog.Add( $"[Disruption] We disable. Score: {score}" );

                if( theyAreDisableWeak )
                {
                    score += 20;
                    CurrentLog.Add( $"[Disruption] And they are choice locked. Score: {score}" );
                }
            }

            if( weFakeOut && !theirTraits.Contains( RoleTrait.FakeOutImmune ) )
            {
                score += 15;

                if( IsDoubleBattle )
                {
                    score += 5;
                }
            }

            if( IsDoubleBattle && weRedirect && !theyUsedSpreadMove )
            {
                if( weRagePowder && !theirTraits.Contains( RoleTrait.PowderImmune ) )
                {
                    score += 15;
                }
                else if( weFollowMe )
                {
                    score += 15;
                }
            }
        }

        if( theyDisrupt && weAreGamePlanTarget )
        {
            score -= 10;
            CurrentLog.Add( $"[Disruption] They disrupt and we are part of our game plan." );

            if( gpd.WeAreWinCon )
            {
                score -= gpd.WinConValue + 10;
                CurrentLog.Add( $"[Disruption] They are the opponent's Win Condition. Score: {score}" );
            }
            else if( gpd.WeAreBlocker )
            {
                score -= gpd.BlockerValue + 7;
                CurrentLog.Add( $"[Disruption] They are one of the opponent's Blockers. Score: {score}" );
            }
            else if( gpd.WeAreEnabler )
            {
                score -= gpd.EnablerValue + 5;
                CurrentLog.Add( $"[Disruption] They are one of the opponent's Enablers. Score: {score}" );
            }

            if( theyTaunt && weAreTauntWeak )
            {
                score -= 10;
                CurrentLog.Add( $"[Disruption] They taunt and we are taunt weak. Score: {score}" );

                if( weHaveRecoveryMove )
                    score -= 5;
            }

            if( theyEncore && weAreEncoreWeak )
            {
                score -= 10;
                CurrentLog.Add( $"[Disruption] They encore and we are encore weak. Score: {score}" );

                if( weHaveRecoveryMove )
                    score -= 5;
            }

            if( theyTroatChop && weAreSilenceWeak )
            {
                score -= 10;
                CurrentLog.Add( $"[Disruption] They silence and we are silence weak. Score: {score}" );
            }

            if( theyHealBlock && weHaveRecoveryMove )
            {
                score -= 15;
                CurrentLog.Add( $"[Disruption] They heal block and we have on healing moves weak. Score: {score}" );
            }

            if( theyDisable )
            {
                score -= 5;
                CurrentLog.Add( $"[Disruption] They disable. Score: {score}" );

                if( weAreDisableWeak )
                {
                    score -= 10;
                    CurrentLog.Add( $"[Disruption] They disable and we are choice locked. Score: {score}" );
                }
            }

            if( theyFakeOut && !ourTraits.Contains( RoleTrait.FakeOutImmune ) )
            {
                score -= 15;
                CurrentLog.Add( $"[Disruption] They used fake out. Score: {score}" );

                if( IsDoubleBattle )
                {
                    score -= 5;
                    CurrentLog.Add( $"[Disruption] And it's a double battle. Score: {score}" );
                }
            }

            if( IsDoubleBattle && theyRedirect && !weUsedSpreadMove )
            {
                if( theyRagePowder && !ourTraits.Contains( RoleTrait.PowderImmune ) )
                {
                    score -= 15;
                    CurrentLog.Add( $"[Disruption] They used rage powder. Score: {score}" );
                }
                else if( theyFollowMe )
                {
                    score -= 15;
                    CurrentLog.Add( $"[Disruption] They used follow me. Score: {score}" );
                }
            }
        }

        return score;
    }

    private int GPScore_PositionProgress( GPAData gpd )
    {
        int score = 0;
        bool weAreGamePlanUnit = gpd.WeAreWinCon || gpd.WeAreBlocker || gpd.WeAreEnabler;

        //--Current match up
        if( !gpd.WeSwitchedThisTurn && weAreGamePlanUnit )
        {
            CurrentLog.Add( $"[Position Progress] We didn't switch this turn and we are a game plan unit." );

            bool weHaveFavorableMU = gpd.TOP1.AttackerPTKO >= PotentialToKO.Risky && ( gpd.TOP1.AttackerMovedFirst || ( gpd.TOP2.AttackerMovedFirst && gpd.TOP1.OpponentPTKO <= PotentialToKO.TwoHKO ) );
            bool weHaveUnfavorableMU = gpd.TOP1.OpponentPTKO >= PotentialToKO.Risky && ( !gpd.TOP1.AttackerMovedFirst || ( !gpd.TOP2.AttackerMovedFirst && gpd.TOP1.AttackerPTKO <= PotentialToKO.TwoHKO ) ) && !weHaveFavorableMU;

            bool winConInPosition = gpd.WeAreWinCon && ( !gpd.TheyAreBlocker || weHaveFavorableMU );
            bool blockerInPosition = gpd.WeAreBlocker && gpd.TheyAreWinCon;
            bool enablerInPosition = gpd.WeAreEnabler && ( gpd.TheyAreBlocker || gpd.TheyAreWinCon || weHaveFavorableMU );
            
            bool favorablePosition = !weHaveUnfavorableMU && ( winConInPosition || blockerInPosition || enablerInPosition );

            bool winConOutOfPosition = gpd.WeAreWinCon && ( gpd.TheyAreBlocker || weHaveUnfavorableMU );
            bool blockerOutOfPosition = gpd.WeAreBlocker && ( gpd.TheyAreEnabler || weHaveUnfavorableMU );
            bool enablerOutOfPosition = gpd.WeAreEnabler && weHaveUnfavorableMU;

            if( favorablePosition )
            {
                score -= 5; //--Flat reduction to not overly reward simply staying in

                if( winConInPosition )
                {
                    score += gpd.WinConValue;
                    CurrentLog.Add( $"[Position Progress] We are a win con and we are in position. Score: {score}" );
                }
                else if( blockerInPosition )
                {
                    score += gpd.BlockerValue;
                    CurrentLog.Add( $"[Position Progress] We are a blocker and we are in position. Score: {score}" );
                }
                else if( enablerInPosition )
                {
                    score += gpd.EnablerValue;
                    CurrentLog.Add( $"[Position Progress] We are an enabler and we are in position. Score: {score}" );
                }
            }
        }
        //--Switch-in match up
        else if( gpd.WeSwitchedThisTurn && weAreGamePlanUnit )
        {
            bool weEnterSafely = gpd.DamageTakenNow <= 0.2f;

            bool weHaveFavorableMUNext = gpd.TOP2.AttackerPTKO >= PotentialToKO.Risky && ( gpd.TOP2.AttackerMovedFirst || gpd.TOP2.OpponentPTKO <= PotentialToKO.TwoHKO );
            bool weHaveUnfavorableMUNext = gpd.TOP2.OpponentPTKO >= PotentialToKO.Risky && ( !gpd.TOP2.AttackerMovedFirst || gpd.TOP2.OpponentPTKO >= PotentialToKO.Risky && gpd.TOP2.AttackerPTKO <= PotentialToKO.TwoHKO );
            
            CurrentLog.Add( $"[Position Progress] This action is a potential switch in." );

            //--We get switch in safely
            if( weEnterSafely )
            {
                if( gpd.WeAreWinCon )
                {
                    score += 15;
                    CurrentLog.Add( $"[Position Progress] We are a win con and we switched in safely. Score: {score}" );
                }
                else if( gpd.WeAreBlocker )
                {
                    score += 10;
                    CurrentLog.Add( $"[Position Progress] We are a blocker and we switched in safely. Score: {score}" );
                }
                else if( gpd.WeAreEnabler )
                {
                    score += 5;
                    CurrentLog.Add( $"[Position Progress] We are an enabler and we switched in safely. Score: {score}" );
                }
            }

            //--switch is game plan unit and has better current spread across opposing team
            var currentUnit = ThisUnitAdapter;
            int switchFavorableMUs = 0;
            int currentFavorableMUs = 0;

            var theirRemaining = TheirBattleAIUnits.Where( p => p.CurrentHPR > 0f );

            foreach( var mon in theirRemaining )
            {
                var ee = Projection.EvaluateExchange( ThisUnitAdapter, mon );
                bool favorable = ee.AttackerPTKOR.PTKO >= PotentialToKO.Risky && ( ee.AttackerMovesFirst || ee.OpponentPTKOR.PTKO <= PotentialToKO.TwoHKO );

                if( favorable )
                    currentFavorableMUs++;
            }

            foreach( var mon in theirRemaining )
            {
                var ee = Projection.EvaluateExchange( gpd.Attacker1, mon );
                bool favorable = ee.AttackerPTKOR.PTKO >= PotentialToKO.Risky && ( ee.AttackerMovesFirst || ee.OpponentPTKOR.PTKO <= PotentialToKO.TwoHKO );

                if( favorable )
                    switchFavorableMUs++;
            }

                score += ( switchFavorableMUs - currentFavorableMUs ) * 5;
                CurrentLog.Add( $"[Position Progress] Game plan switch ({switchFavorableMUs}) favorable MUs - Current game plan unit favorable MUs ({currentFavorableMUs}). Score: {score}" );
            
            //--Position Checks
            bool winConInPosition = gpd.WeAreWinCon && ( !gpd.TheyAreBlocker || weHaveFavorableMUNext );
            bool blockerInPosition = gpd.WeAreBlocker && gpd.TheyAreWinCon;
            bool enablerInPosition = gpd.WeAreEnabler && ( gpd.TheyAreBlocker || gpd.TheyAreWinCon || weHaveFavorableMUNext );
            
            bool favorablePosition = !weHaveUnfavorableMUNext && ( winConInPosition || blockerInPosition || enablerInPosition );

            bool winConOutOfPosition = gpd.WeAreWinCon && ( gpd.TheyAreBlocker || weHaveUnfavorableMUNext );
            bool blockerOutOfPosition = gpd.WeAreBlocker && ( gpd.TheyAreEnabler || weHaveUnfavorableMUNext );
            bool enablerOutOfPosition = gpd.WeAreEnabler && weHaveUnfavorableMUNext;

            if( favorablePosition )
            {
                if( winConInPosition )
                {
                    score += gpd.WinConValue;
                    CurrentLog.Add( $"[Position Progress] We are switching in a win con and we are in position. Score: {score}" );
                }
                else if( blockerInPosition )
                {
                    score += gpd.BlockerValue;
                    CurrentLog.Add( $"[Position Progress] We are switching in a blocker and we are in position. Score: {score}" );
                }
                else if( enablerInPosition )
                {
                    score += gpd.EnablerValue;
                    CurrentLog.Add( $"[Position Progress] We are switching in an enabler and we are in position. Score: {score}" );
                }
            }
            else if( weHaveUnfavorableMUNext )
            {
                score -= 5;
                if( winConOutOfPosition )
                {
                    score -= gpd.WinConValue;
                    CurrentLog.Add( $"[Position Progress] We are switching in a win con and we are out of position. Score: {score}" );
                }
                else if( blockerOutOfPosition )
                {
                    score -= gpd.BlockerValue;
                    CurrentLog.Add( $"[Position Progress] We are switching in a blocker and we are out of position. Score: {score}" );
                }
                else if( enablerOutOfPosition )
                {
                    score -= gpd.EnablerValue;
                    CurrentLog.Add( $"[Position Progress] We are switching in an enabler and we are out of position. Score: {score}" );
                }
            }
        }

        return score;
    }

    private int GPScore_BattlefieldProgress( GPAData gpd )
    {
        int score = 0;

        var weather1 = CurrentFieldSnapshot.Weather;
        var weather2 = gpd.TOP2.Field.Weather;

        var terrain1 = CurrentFieldSnapshot.Terrain;
        var terrain2 = gpd.TOP2.Field.Terrain;

        var fieldConditions1 = CurrentFieldSnapshot.FieldConditions;
        var fieldConditions2 = gpd.TOP2.Field.FieldConditions;

        var ourCourtNow = gpd.Attacker1.CourtLocation == CourtLocation.TopCourt ? CurrentFieldSnapshot.TopCourtConditions : CurrentFieldSnapshot.BottomCourtConditions;
        var theirCourtNow = gpd.Opponent1.CourtLocation == CourtLocation.TopCourt ? CurrentFieldSnapshot.TopCourtConditions : CurrentFieldSnapshot.BottomCourtConditions;

        var ourCourtNext = gpd.Attacker1.CourtLocation == CourtLocation.TopCourt ? gpd.TOP2.Field.TopCourtConditions : gpd.TOP2.Field.BottomCourtConditions;
        var theirCourtNext = gpd.Opponent1.CourtLocation == CourtLocation.TopCourt ? gpd.TOP2.Field.TopCourtConditions : gpd.TOP2.Field.BottomCourtConditions;

        var winconAdapter = GetPokemonAs_Adapter( gpd.GamePlan.OurPrimaryWinCon );

        var ourRemaining = OurTeamAdapters.Values.Where( p => p.CurrentHPR > 0 ).ToList();
        var theirRemaining = TheirTeamAdapters.Values.Where( p => p.CurrentHPR > 0 ).ToList();

        //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //--Field Creation Checks---------------------------------------------------------------------------------------------------------------------------------------------------------------------
        //--------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------

        //--Tailwind & Trick Room
        bool weSetTailwind = !ourCourtNow.ContainsKey( CourtConditionID.Tailwind ) && ourCourtNext.ContainsKey( CourtConditionID.Tailwind );
        bool weUseTrickRoom = gpd.Action.Type == ActionType.Support && gpd.Action.MovePayload.MoveSO.Name == "Trick Room";
        bool weSetTrickRoom = weUseTrickRoom && !fieldConditions1.ContainsKey( FieldConditionID.TrickRoom ) && fieldConditions2.ContainsKey( FieldConditionID.TrickRoom );
        bool weReverseTrickRoom = weUseTrickRoom && fieldConditions1.ContainsKey( FieldConditionID.TrickRoom ) && !fieldConditions2.ContainsKey( FieldConditionID.TrickRoom );

        //--Weather
        bool weUseSun = ( gpd.Action.Type == ActionType.Support && gpd.Action.MovePayload.MoveSO.Name == "Sunny Day" ) || gpd.Attacker1.Ability == AbilityID.Drought || gpd.Attacker2.Ability == AbilityID.Drought;
        bool weUseRain = ( gpd.Action.Type == ActionType.Support && gpd.Action.MovePayload.MoveSO.Name == "Rain Dance" ) || gpd.Attacker1.Ability == AbilityID.Drizzle || gpd.Attacker2.Ability == AbilityID.Drizzle;
        bool weUseSand = ( gpd.Action.Type == ActionType.Support && gpd.Action.MovePayload.MoveSO.Name == "Sandstorm" ) || gpd.Attacker1.Ability == AbilityID.Sandstream || gpd.Attacker2.Ability == AbilityID.Sandstream;
        bool weUseSnow = ( gpd.Action.Type == ActionType.Support && gpd.Action.MovePayload.MoveSO.Name == "Snowscape" ) || gpd.Attacker1.Ability == AbilityID.SnowWarning || gpd.Attacker2.Ability == AbilityID.SnowWarning;

        bool weSetSun = weUseSun && weather1 != WeatherConditionID.SUNNY && weather2 == WeatherConditionID.SUNNY;
        bool weSetRain = weUseRain && weather1 != WeatherConditionID.RAIN && weather2 == WeatherConditionID.RAIN;
        bool weSetSand = weUseSand && weather1 != WeatherConditionID.SANDSTORM && weather2 == WeatherConditionID.SANDSTORM;
        bool weSetSnow = weUseSnow && weather1 != WeatherConditionID.SNOW && weather2 == WeatherConditionID.SNOW;
        bool weSetWeather = weSetSun || weSetRain || weSetSand || weSetSnow;

        //--Terrain
        bool weSetGrassy = terrain1 != TerrainID.Grassy && terrain2 == TerrainID.Grassy;
        bool weSetBlight = terrain1 != TerrainID.Blighted && terrain2 == TerrainID.Blighted;
        bool weSetPsychic = terrain1 != TerrainID.Psychic && terrain2 == TerrainID.Psychic;
        bool weSetElectric = terrain1 != TerrainID.Electric && terrain2 == TerrainID.Electric;
        bool weSetMisty = terrain1 != TerrainID.Misty && terrain2 == TerrainID.Misty;
        bool weSetTerrain = weSetGrassy || weSetBlight || weSetPsychic || weSetElectric || weSetMisty;

        //--Screens
        bool weSetReflect = !ourCourtNow.ContainsKey( CourtConditionID.Reflect ) && ourCourtNext.ContainsKey( CourtConditionID.Reflect );
        bool weSetLightScreen = !ourCourtNow.ContainsKey( CourtConditionID.LightScreen ) && ourCourtNext.ContainsKey( CourtConditionID.LightScreen );
        bool weSetAuroraVeil = !ourCourtNow.ContainsKey( CourtConditionID.AuroraVeil ) && ourCourtNext.ContainsKey( CourtConditionID.AuroraVeil );
        bool weSetScreens = weSetReflect || weSetLightScreen || weSetAuroraVeil;

        //--Entry Hazards
        bool weSpikes = !theirCourtNow.ContainsKey( CourtConditionID.Spikes ) && theirCourtNext.ContainsKey( CourtConditionID.Spikes );
        bool weStealthRock = !theirCourtNow.ContainsKey( CourtConditionID.StealthRock ) && theirCourtNext.ContainsKey( CourtConditionID.StealthRock );
        bool weLeechSeed = !theirCourtNow.ContainsKey( CourtConditionID.LeechSeed ) && theirCourtNext.ContainsKey( CourtConditionID.LeechSeed );
        bool weToxicSpikes = !theirCourtNow.ContainsKey( CourtConditionID.ToxicSpikes ) && theirCourtNext.ContainsKey( CourtConditionID.ToxicSpikes );
        bool weStickyWeb = !theirCourtNow.ContainsKey( CourtConditionID.StickyWeb ) && theirCourtNext.ContainsKey( CourtConditionID.StickyWeb );
        bool weSetHazards = weSpikes || weStealthRock || weLeechSeed || weToxicSpikes || weStickyWeb;

        //--Friend Guard & Helping Hand
        bool weProvideFriendGuard = gpd.Attacker1.Ability == AbilityID.FriendGuard || gpd.Attacker2.Ability == AbilityID.FriendGuard;
        bool weProvideHelpingHand = gpd.Action.Type == ActionType.Support && gpd.Action.MovePayload.MoveSO.Name == "Helping Hand";

        bool winconBenefitsTrickRoom = winconAdapter.CurrentHPR > 0f && winconAdapter.RoleProfile.PrimaryRole == RoleClass.TrickRoomAbuser || winconAdapter.RoleProfile.SecondaryRoles.Contains( RoleClass.TrickRoomAbuser ) ||
                ( winconAdapter.RoleProfile.PrimaryRole == RoleClass.BulkyAttacker && ( winconAdapter.RoleProfile.Biases.Contains( RoleBias.SlowSpeed ) ||
                winconAdapter.RoleProfile.Biases.Contains( RoleBias.TrickRoomSpeed ) ) );

        bool theyHaveScreensNow = theirCourtNow.ContainsKey( CourtConditionID.Reflect ) || theirCourtNow.ContainsKey( CourtConditionID.LightScreen ) || theirCourtNow.ContainsKey( CourtConditionID.AuroraVeil );
        bool theyHaveScreensNext = theirCourtNext.ContainsKey( CourtConditionID.Reflect ) || theirCourtNext.ContainsKey( CourtConditionID.LightScreen ) || theirCourtNext.ContainsKey( CourtConditionID.AuroraVeil );

        bool theySpiked = ourCourtNow.ContainsKey( CourtConditionID.Spikes );
        bool theyRocks = ourCourtNow.ContainsKey( CourtConditionID.StealthRock );
        bool theySeeded = ourCourtNow.ContainsKey( CourtConditionID.LeechSeed );
        bool theyToxicSpiked = ourCourtNow.ContainsKey( CourtConditionID.ToxicSpikes );
        bool theyStickeyWebbed = ourCourtNow.ContainsKey( CourtConditionID.StickyWeb );
        bool theySetHazardsNow = theySpiked || theyRocks || theySeeded || theyToxicSpiked || theyStickeyWebbed;

        //--Field Creation

        //--Tailwind
        if( weSetTailwind )
        {
            score += 5;

            if( gpd.WeAreEnabler )
                score += gpd.EnablerValue;

            if( OurTeamComposition.Strategies.Contains( TeamStrategy.SpeedControl ) )
                score += 5;

            if( winconAdapter.RoleProfile.Biases.Contains( RoleBias.MiddlingSpeed ) || winconAdapter.RoleProfile.Biases.Contains( RoleBias.AwkwardSpeed ) )
                score += 10;

            // if( IsDoubleBattle && OurAlly == gpd.GamePlan.OurPrimaryWinCon )
                // score += 10;
        }

        //--Trick Room
        if( weSetTrickRoom )
        {
            score += 5;

            if( gpd.WeAreEnabler )
                score += gpd.EnablerValue;

            if( OurTeamComposition.Strategies.Contains( TeamStrategy.TrickRoom ) )
                score += 5;

            if( winconBenefitsTrickRoom )
            {
                score += 10;
            }

            //--if IsDoubleBattle && ( OurAlly == gpd.GamePlan.OurPrimaryWinCon && winconBenefitsTrickRoom || allyBenefitsTrickRoom ) score += 10
        }

        //--Weather
        if( weSetWeather )
        {
            int benefitsSun = 0;
            int benefitsRain = 0;
            int benefitsSand = 0;
            int benefitsSnow = 0;
            
            int teamBeforeWeatherScore = 0;
            int teamAfterWeatherScore = 0;

            bool weatherIsGood = false;

            foreach( var mon in ourRemaining )
            {
                int sunScore = UnitSim.Get_WeatherContextScore( mon.Pokemon, WeatherConditionID.SUNNY );
                int rainScore = UnitSim.Get_WeatherContextScore( mon.Pokemon, WeatherConditionID.RAIN );
                int sandScore = UnitSim.Get_WeatherContextScore( mon.Pokemon, WeatherConditionID.SANDSTORM );
                int snowScore = UnitSim.Get_WeatherContextScore( mon.Pokemon, WeatherConditionID.SNOW );

                teamBeforeWeatherScore += UnitSim.Get_WeatherContextScore( mon.Pokemon, weather1 );
                teamAfterWeatherScore += UnitSim.Get_WeatherContextScore( mon.Pokemon, weather2 );

                if( sunScore > 0 )
                    benefitsSun++;

                if( rainScore > 0 )
                    benefitsRain++;

                if( sandScore > 0 )
                    benefitsSand++;

                if( snowScore > 0 )
                    benefitsSnow++;
            }

            if( weSetSun && benefitsSun > 0 )
            {
                score += benefitsSun * 3;

                if( OurTeamComposition.Strategies.Contains( TeamStrategy.Sun ) )
                {
                    score += 5;
                }

                weatherIsGood = true;
            }

            if( weSetRain && benefitsRain > 0 )
            {
                score += benefitsRain * 3;

                if( OurTeamComposition.Strategies.Contains( TeamStrategy.Rain ) )
                {
                    score += 5;
                }

                weatherIsGood = true;
            }

            if( weSetSand && benefitsSand > 0 )
            {
                score += benefitsSand * 3;

                if( OurTeamComposition.Strategies.Contains( TeamStrategy.Sand ) )
                {
                    score += 5;
                }

                weatherIsGood = true;
            }

            if( weSetSnow && benefitsSnow > 0 )
            {
                score += benefitsSnow * 3;

                if( OurTeamComposition.Strategies.Contains( TeamStrategy.Snow ) )
                {
                    score += 5;
                }

                weatherIsGood = true;
            }

            if( weatherIsGood )
            {
                if( gpd.WeAreEnabler )
                {
                    score += gpd.EnablerValue;
                }
            }

            int weatherImprovement = teamAfterWeatherScore - teamBeforeWeatherScore;
            if( weatherImprovement > 0 )
            {
                score += weatherImprovement;
            }
        }

        //--Terrain
        if( weSetTerrain )
        {
            int benefitsGrassy = 0;
            int benefitsBlight = 0;
            int benefitsPsychic = 0;
            int benefitsElectric = 0;
            int benefitsMisty = 0;

            int teamBeforeTerrainScore = 0;
            int teamAfterTerrainScore = 0;

            bool terrainIsGood = false;

            foreach( var mon in ourRemaining )
            {
                if( UnitSim.Get_TerrainContextScore( mon.Pokemon, TerrainID.Grassy ) > 0 )
                    benefitsGrassy++;

                if( UnitSim.Get_TerrainContextScore( mon.Pokemon, TerrainID.Blighted ) > 0 )
                    benefitsBlight++;

                if( UnitSim.Get_TerrainContextScore( mon.Pokemon, TerrainID.Psychic ) > 0 )
                    benefitsPsychic++;

                if( UnitSim.Get_TerrainContextScore( mon.Pokemon, TerrainID.Electric ) > 0 )
                    benefitsElectric++;

                if( UnitSim.Get_TerrainContextScore( mon.Pokemon, TerrainID.Misty ) > 0 )
                    benefitsMisty++;

                teamBeforeTerrainScore += UnitSim.Get_TerrainContextScore( mon.Pokemon, terrain1 );
                teamAfterTerrainScore += UnitSim.Get_TerrainContextScore( mon.Pokemon, terrain2 );
            }

            if( weSetGrassy && benefitsGrassy > 0 )
            {
                score += 10;
                terrainIsGood = true;
            }

            if( weSetBlight && benefitsBlight > 0 )
            {
                score += 10;
                terrainIsGood = true;
            }

            if( weSetPsychic && benefitsPsychic > 0 )
            {
                score += 10;
                terrainIsGood = true;
            }

            if( weSetElectric && benefitsElectric > 0 )
            {
                score += 10;
                terrainIsGood = true;
            }

            if( weSetMisty && benefitsMisty > 0 )
            {
                score += 10;
                terrainIsGood = true;
            }

            if( terrainIsGood )
            {
                if( gpd.WeAreEnabler )
                {
                    score += gpd.EnablerValue;
                }

                if( OurTeamComposition.Strategies.Contains( TeamStrategy.TerrainControl ) )
                {
                    score += 5;
                }
            }

            int terrainImprovement = teamAfterTerrainScore - teamBeforeTerrainScore;
            if( terrainImprovement > 0 )
            {
                score += terrainImprovement;
            }
        }

        //--Screens
        if( weSetScreens )
        {
            score += 10;

            if( gpd.WeAreEnabler )
                score += gpd.EnablerValue;

            if( OurTeamComposition.Strategies.Contains( TeamStrategy.ScreenSupport ) )
                score += 5;
        }

        if( weSetHazards )
        {
            score += 5;

            if( gpd.WeAreBlocker )
            {
                score += gpd.BlockerValue;
            }
            else if( gpd.WeAreEnabler )
            {
                score += gpd.EnablerValue;
            }

            if( OurTeamComposition.Strategies.Contains( TeamStrategy.HazardPressure ) )
            {
                score += 5;
            }
        }

        //--Friend Guard
        if( weProvideFriendGuard && IsDoubleBattle )
        {
            score += 10;

            if( gpd.WeAreEnabler )
                score += gpd.EnablerValue;
        }

        //--Helping Hand
        if( weProvideHelpingHand && IsDoubleBattle )
        {
            // if ourally is using an attack score += 15

            if( gpd.WeAreEnabler )
                score += gpd.EnablerValue;
        }

        //--Field Denial
        //--Match Tailwind
        if( weSetTailwind && ( theirCourtNow.ContainsKey( CourtConditionID.Tailwind ) || theirCourtNext.ContainsKey( CourtConditionID.Tailwind ) ) )
        {
            score += 10;

            if( theirCourtNow.ContainsKey( CourtConditionID.Tailwind ) && !theirCourtNext.ContainsKey( CourtConditionID.Tailwind ) )
                score += 10;
        }

        //--Reverse Trick Room
        if( weReverseTrickRoom && !winconBenefitsTrickRoom ) //--This is a simple temporary block, we will later rebuild Get_TrickRoomContext() to use role profile information and make this operate similar to weather and terrain
        {
            score += 10;
        }

        //--Remove Weather
        if( weSetWeather )
        {
            int theirSun = 0;
            int theirRain = 0;
            int theirSand = 0;
            int theirSnow = 0;

            int theirTeamBeforeWeatherScore = 0;
            int theirTeamAfterWeatherScore = 0;

            bool isSun = weather1 == WeatherConditionID.SUNNY;
            bool isRain = weather1 == WeatherConditionID.RAIN;
            bool isSand = weather1 == WeatherConditionID.SANDSTORM;
            bool isSnow = weather1 == WeatherConditionID.SNOW;

            foreach( var theirMon in theirRemaining )
            {
                int sunScore = UnitSim.Get_WeatherContextScore( theirMon.Pokemon, WeatherConditionID.SUNNY );
                int rainScore = UnitSim.Get_WeatherContextScore( theirMon.Pokemon, WeatherConditionID.RAIN );
                int sandScore = UnitSim.Get_WeatherContextScore( theirMon.Pokemon, WeatherConditionID.SANDSTORM );
                int snowScore = UnitSim.Get_WeatherContextScore( theirMon.Pokemon, WeatherConditionID.SNOW );

                theirTeamBeforeWeatherScore += UnitSim.Get_WeatherContextScore( theirMon.Pokemon, weather1 );
                theirTeamAfterWeatherScore += UnitSim.Get_WeatherContextScore( theirMon.Pokemon, weather2 );

                if( sunScore > 0 )
                    theirSun++;

                if( rainScore > 0 )
                    theirRain++;

                if( sandScore > 0 )
                    theirSand++;

                if( snowScore > 0 )
                    theirSnow++;
            }

            //--We set a weather they don't benefit from
            if( weSetSun && theirSun == 0 )
            {
                score += 5;
            }

            if( weSetRain && theirRain == 0 )
            {
                score += 5;
            }

            if( weSetSand && theirSand == 0 )
            {
                score += 5;
            }

            if( weSetSnow && theirSnow == 0 )
            {
                score += 5;
            }

            //--We change a weather they do benefit from
            if( !weSetSun && weSetWeather && isSun && theirSun > 0 )
            {
                score += 10;
            }

            if( !weSetRain && weSetWeather && isRain && theirRain > 0 )
            {
                score += 10;
            }

            if( !weSetSand && weSetWeather && isSand && theirSand > 0 )
            {
                score += 10;
            }

            if( !weSetSnow && weSetWeather && isSnow && theirSnow > 0 )
            {
                score += 10;
            }

            int weatherDifference = theirTeamBeforeWeatherScore - theirTeamAfterWeatherScore;
            score += weatherDifference;
        }

        //--Replace Terrain
        if( weSetTerrain )
        {
            int theirGrassy = 0;
            int theirBlight = 0;
            int theirPsychic = 0;
            int theirElectric = 0;
            int theirMisty = 0;

            int theirTeamBeforeTerrainScore = 0;
            int theirTeamAfterTerrainScore = 0;

            bool isGrassy = terrain1 == TerrainID.Grassy;
            bool isBlight = terrain1 == TerrainID.Blighted;
            bool isPsychic = terrain1 == TerrainID.Psychic;
            bool isElectric = terrain1 == TerrainID.Electric;
            bool isMisty = terrain1 == TerrainID.Misty;

            foreach( var theirMon in theirRemaining )
            {
                int grassyScore = UnitSim.Get_TerrainContextScore( theirMon.Pokemon, TerrainID.Grassy );
                int blightScore = UnitSim.Get_TerrainContextScore( theirMon.Pokemon, TerrainID.Blighted );
                int psychicScore = UnitSim.Get_TerrainContextScore( theirMon.Pokemon, TerrainID.Psychic );
                int electricScore = UnitSim.Get_TerrainContextScore( theirMon.Pokemon, TerrainID.Electric );
                int mistyScore = UnitSim.Get_TerrainContextScore( theirMon.Pokemon, TerrainID.Misty );

                theirTeamBeforeTerrainScore += UnitSim.Get_TerrainContextScore( theirMon.Pokemon, terrain1 );
                theirTeamAfterTerrainScore += UnitSim.Get_TerrainContextScore( theirMon.Pokemon, terrain2 );

                if( grassyScore > 0 )
                    theirGrassy++;

                if( blightScore > 0 )
                    theirBlight++;

                if( psychicScore > 0 )
                    theirPsychic++;

                if( electricScore > 0 )
                    theirElectric++;

                if( mistyScore > 0 )
                    theirMisty++;
            }

            //--We set a terrain they don't benefit from
            if( weSetGrassy && theirGrassy == 0 )
            {
                score += 5;
            }

            if( weSetBlight && theirBlight == 0 )
            {
                score += 5;
            }

            if( weSetPsychic && theirPsychic == 0 )
            {
                score += 5;
            }

            if( weSetElectric && theirElectric == 0 )
            {
                score += 5;
            }

            if( weSetMisty && theirMisty == 0 )
            {
                score += 5;
            }

            //--We change a terrain they were benefitting from
            bool weChange = false;
            if( !weSetGrassy && weSetTerrain && isGrassy && theirGrassy > 0 )
            {
                score += 10;
                weChange = true;
            }

            if( !weSetBlight && weSetTerrain && isBlight && theirBlight > 0 )
            {
                score += 10;
                weChange = true;
            }

            if( !weSetPsychic && weSetTerrain && isPsychic && theirPsychic > 0 )
            {
                score += 10;
                weChange = true;
            }

            if( !weSetElectric && weSetTerrain && isElectric && theirElectric > 0 )
            {
                score += 10;
                weChange = true;
            }

            if( !weSetMisty && weSetTerrain && isMisty && theirMisty > 0 )
            {
                score += 10;
                weChange = true;
            }

            if( weChange && TheirTeamComposition.Strategies.Contains( TeamStrategy.TerrainControl ) )
            {
                score += 5;
            }

            int terrainDifference = theirTeamBeforeTerrainScore - theirTeamAfterTerrainScore;
            score += terrainDifference;
        }

        //--Remove Screens
        if( theyHaveScreensNow )
        {
            if( weSetScreens ) //--Small reward for matching them
            {
                score += 5;
            }

            if( !theyHaveScreensNext )
            {
                score += 15;
            }
        }

        //--Clear Hazards
        if( theySetHazardsNow )
        {
            bool theySpikedNext = ourCourtNext.ContainsKey( CourtConditionID.Spikes );
            bool theyRocksNext = ourCourtNext.ContainsKey( CourtConditionID.StealthRock );
            bool theySeededNext = ourCourtNext.ContainsKey( CourtConditionID.LeechSeed );
            bool theyToxicSpikedNext = ourCourtNext.ContainsKey( CourtConditionID.ToxicSpikes );
            bool theyStickeyWebbedNext = ourCourtNext.ContainsKey( CourtConditionID.StickyWeb );
            bool theySetHazardsNext = theySpikedNext || theyRocksNext || theySeededNext || theyToxicSpikedNext || theyStickeyWebbedNext;

            if( !theySetHazardsNext )
            {
                score += 10;

                if( gpd.WeAreBlocker )
                {
                    score += gpd.BlockerValue;
                }
                else if( gpd.WeAreEnabler )
                {
                    score += gpd.EnablerValue;
                }

                if( OurTeamComposition.Strategies.Contains( TeamStrategy.HazardPressure ) )
                {
                    score += 5;
                }
            }
        }

        //--Strategic Pressure
        var theirWinConAdapter = GetPokemonAs_Adapter( gpd.GamePlan.TheirPrimaryWinCon );
        
        //--Trick Room against WinCon
        if( weSetTrickRoom )
        {
            if( theirWinConAdapter.RoleProfile.Biases.Contains( RoleBias.MiddlingSpeed ) || theirWinConAdapter.RoleProfile.Biases.Contains( RoleBias.FastSpeed ) )
            {
                score += 10;

                if( winconBenefitsTrickRoom )
                {
                    score += 5;
                }
            }
        }

        //--Weather against WinCon
        if( weSetWeather )
        {
            int theirWinConWeatherBenefitScore = UnitSim.Get_WeatherContextScore( theirWinConAdapter.Pokemon, weather2 );
            score -= theirWinConWeatherBenefitScore;
        }

        //--Terrain against WinCon
        if( weSetTerrain )
        {
            int theirWinConTerrainBenefitScore = UnitSim.Get_TerrainContextScore( theirWinConAdapter.Pokemon, terrain2 );
            score -= theirWinConTerrainBenefitScore;
        }

        //--Hazards against WinCon
        if( weSetHazards )
        {
            int rocksEffectiveness = (int)TypeChart.GetEffectiveness( PokemonType.Rock, theirWinConAdapter.Type.One ) * (int)TypeChart.GetEffectiveness( PokemonType.Rock, theirWinConAdapter.Type.Two );
            bool theyAreRocksWeak = rocksEffectiveness > 1f;
            if( weStealthRock && theyAreRocksWeak )
            {
                score += rocksEffectiveness * 3;
            }

            if( weToxicSpikes && theirWinConAdapter.RoleProfile.Traits.Contains( RoleTrait.ToxicWeak ) )
            {
                score += 15;
            }

            if( weStickyWeb && theirWinConAdapter.RoleProfile.Traits.Contains( RoleTrait.ParalysisWeak ) ) //--this simply means they don't want to be slowed down
            {
                score += 10;
            }
        }

        //--Ally Synergy
        //--Enabler -> WinCon support
        //--Blocker -> WinCon support
        //--Teamwide battlefield support

        return score;
    }

    public int GetUnitStatValue( IBattleAIUnit pokemon, Stat stat )
    {
        return stat switch
        {
            Stat.HP => pokemon.HP,
            Stat.Attack => pokemon.Attack,
            Stat.Defense => pokemon.Defense,
            Stat.SpAttack => pokemon.SpAttack,
            Stat.SpDefense => pokemon.SpDefense,
            Stat.Speed => pokemon.Speed,
            _ => 100
        };
    }

    public int GetUnitInferredStat( Pokemon pokemon, Stat stat )
    {
        // Debug.Log( $"[AI Scoring][Get Walling Score] Getting {pokemon.NickName}'s inferred {stat}" );
        float statValue = GetCalculatedStat( pokemon, stat );
        // Debug.Log( $"[AI Scoring][Get Walling Score] {pokemon.NickName}'s base {stat} value is: {statValue}" );

        int stage = pokemon.StatStages[stat];
        var stageModifier = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f };
        float directModifier = pokemon.DirectStatModifiers[stat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm );

        if( stage >= 0 )
            statValue *= stageModifier[stage];
        else
            statValue /= stageModifier[-stage];

        //--Apply Direct Stat Change (Burn, Paralysis, Ruin Ability, Weather stat change, etc.)
        statValue *= directModifier;

        int final = Mathf.FloorToInt( statValue );

        // Debug.Log( $"[AI Scoring][Get Walling Score] {pokemon.NickName}'s Final Inferred {stat} value is: {final}" );

        return final;
    }

    //--This function does stat stage and direct modifier calcluation on stats. this is the same as the GetStat() function in the Pokemon class.
    //--Stat property calls from Pokemon return GetStat(). We should do essentially the same here by creating a snapshot of all active pokemon via the now static Adapter dictionaries.
    //--This means that each turn, all pokemon will get their stats changed to "inferred stats" with stat stage and direct modifiers calculated. luckily, neither of these things are available
    //--for benched pokemon, which means they should, theoretically, never have oddly calculated stats. the only time a benched pokemon's stats should be different is when we want to speed-check
    //--a benched pokemon for candidate selection - we should know its effective speed if it were on the field, not what its speed is on the bench. for that, we will use GetContextualSpeed().
    //--Great plan, let's see how much we break. --05/06/26
    public int GetUnitInferredStat( IBattleAIUnit pokemon, Stat stat )
    {
        // Debug.Log( $"[AI Scoring][Get Walling Score] Getting {pokemon.Name}'s inferred {stat}" );
        float statValue = GetUnitStatValue( pokemon, stat );

        int stage = pokemon.StatStages[stat];
        var stageModifier = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f };
        float directModifier = pokemon.DirectStatModifiers[stat].Values.Aggregate( 1.0f, ( acc, dsm ) => acc * dsm );

        stage = Mathf.Clamp( stage, -6, 6 );

        if( stage >= 0 )
            statValue *= stageModifier[stage];
        else
            statValue /= stageModifier[-stage];

        //--Apply Direct Stat Change (Burn, Paralysis, Ruin Ability, Weather stat change, etc.)
        statValue *= directModifier;

        int final = Mathf.FloorToInt( statValue );

        // Debug.Log( $"[AI Scoring][Get Walling Score] {pokemon.Name}'s base {stat} value is: {statValue} with a stage of {stage} and a direct modifier total of {directModifier}" );
        // Debug.Log( $"[AI Scoring][Get Walling Score] {pokemon.Name}'s Final Inferred {stat} value is: {final}" );

        return final;
    }

    public int GetUnitContextualSpeed( Pokemon pokemon )
    {
        int speed = GetUnitInferredStat( pokemon, Stat.Speed );
        var weather = BattleSystem.Field.Weather;

        if( weather != null )
        {
            if( weather.ID == WeatherConditionID.RAIN && pokemon.AbilityID == AbilityID.SwiftSwim && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;

            if( weather.ID == WeatherConditionID.SUNNY && pokemon.AbilityID == AbilityID.Chlorophyll && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;

            if( weather.ID == WeatherConditionID.SANDSTORM && pokemon.AbilityID == AbilityID.SandRush && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;

            if( weather.ID == WeatherConditionID.SNOW && pokemon.AbilityID == AbilityID.SlushRush && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;
        }

        return speed;
    }

    public int GetUnitContextualSpeed( IBattleAIUnit pokemon )
    {
        int speed = pokemon.Speed;
        var weather = BattleSystem.Field.Weather;
        var courtConditions = BattleSystem.Field.ActiveCourts[pokemon.CourtLocation].Conditions;

        //--This function no longer serves active pokemon. Active pokemon receive direct modifiers to their speed when they have a weather speed ability.
        //--This function is now used to contextually check incoming switch candidates.
        var activeAllies = GetActiveAllyUnits_AsBattleAIUnits( pokemon.Pokemon );
        for( int i = 0; i < activeAllies.Count; i++ )
        {
            var ally = activeAllies[i];
            if( ally.Pokemon == pokemon.Pokemon )
                return speed;
        }

        if( weather != null )
        {
            if( weather.ID == WeatherConditionID.RAIN && pokemon.Ability == AbilityID.SwiftSwim && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;

            if( weather.ID == WeatherConditionID.SUNNY && pokemon.Ability == AbilityID.Chlorophyll && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;

            if( weather.ID == WeatherConditionID.SANDSTORM && pokemon.Ability == AbilityID.SandRush && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;

            if( weather.ID == WeatherConditionID.SNOW && pokemon.Ability == AbilityID.SlushRush && !pokemon.DirectStatModifiers[Stat.Speed].ContainsKey( DirectModifierCause.WeatherSPD ) )
                speed *= 2;
        }
        
        if( courtConditions.ContainsKey( CourtConditionID.Tailwind ) )
        {
            speed *= 2;
        }

        return speed;
    }

    public int GetBaseStat( Pokemon pokemon, Stat stat )
    {
        return stat switch
        {
            Stat.HP         => pokemon.PokeSO.MaxHP,
            Stat.Attack     => pokemon.PokeSO.Attack,
            Stat.Defense    => pokemon.PokeSO.Defense,
            Stat.SpAttack   => pokemon.PokeSO.SpAttack,
            Stat.SpDefense  => pokemon.PokeSO.SpDefense,
            Stat.Speed      => pokemon.PokeSO.Speed,
            _ => 0
        };
    }

    public int GetBaseStat( IBattleAIUnit unit, Stat stat )
    {
        var pokemon = unit.Pokemon.PokeSO;

        return stat switch
        {
            Stat.HP         => pokemon.MaxHP,
            Stat.Attack     => pokemon.Attack,
            Stat.Defense    => pokemon.Defense,
            Stat.SpAttack   => pokemon.SpAttack,
            Stat.SpDefense  => pokemon.SpDefense,
            Stat.Speed      => pokemon.Speed,
            _ => 0
        };
    }

    public int GetCalculatedStat( IBattleAIUnit pokemon, Stat stat, bool useStatSpread = true )
    {
        int baseStat = GetBaseStat( pokemon, stat );
        int level = pokemon.Level;
        int iv = 31;
        int ev = 0;
        int calculatedStat;
        var mon = pokemon.Pokemon;
        NatureID nature = NatureID.Neutral;

        if( useStatSpread )
        {
            if( pokemon.StatSpread.Spread.TryGetValue( stat, out var value ) )
                ev = CalcEVs( value );

            nature = pokemon.StatSpread.Nature;
        }

        if( stat == Stat.Accuracy || stat == Stat.CritRatio || stat == Stat.Evasion ) //--These should probably be moved to their own enum tbh. It's just much safer.
        {
            Debug.LogError( $"Passed non stat-stat somehow!" );
            return 100;
        }

        if( stat == Stat.HP )
        {
            calculatedStat = Mathf.FloorToInt( ( ( 2 * baseStat + iv + ev  ) * level / 100f + level ) + 10 );
        }
        else
        {
            calculatedStat = Mathf.FloorToInt( ( ( ( 2 * baseStat + iv + ev ) * level / 100f ) + 5 ) * GetNatureModifier( mon, nature, stat ) );
        }

        return calculatedStat;
    }

    public int GetCalculatedStat( Pokemon pokemon, Stat stat, bool useStatSpread = true )
    {
        int baseStat = GetBaseStat( pokemon, stat );
        int level = pokemon.Level;
        int iv = 31;
        int ev = 0;
        int calculatedStat;
        var mon = pokemon;

        NatureID nature = NatureID.Neutral;

        if( useStatSpread )
        {
            var adapter = GetPokemonAs_Adapter( pokemon );
            
            if( adapter.StatSpread.Spread.TryGetValue( stat, out var value ) )
                ev = CalcEVs( value );

            nature = adapter.StatSpread.Nature;
        }

        if( stat == Stat.Accuracy || stat == Stat.CritRatio || stat == Stat.Evasion ) //--These should probably be moved to their own enum tbh. It's just much safer.
        {
            Debug.LogError( $"Passed non stat-stat {stat} somehow!" );
            return 100;
        }

        if( stat == Stat.HP )
        {
            calculatedStat = Mathf.FloorToInt( ( ( 2 * baseStat + iv + ev  ) * level / 100f + level ) + 10 );
        }
        else
        {
            calculatedStat = Mathf.FloorToInt( ( ( ( 2 * baseStat + iv + ev ) * level / 100f ) + 5 ) * GetNatureModifier( mon, nature, stat ) );
        }

        return calculatedStat;
    }

    private int CalcEVs( int statEVs )
    {
        int ev = statEVs / 4;
        return Mathf.Max( 0, ev );
    }

    private float GetNatureModifier( Pokemon pokemon, NatureID natureID, Stat stat )
    {
        var nature = pokemon.Natures[natureID];

        if( stat == nature.PositiveStat )
            return 1.1f;
        else if( stat == nature.NegativeStat )
            return 0.9f;
        else
            return 1f;
    }

    public int Attack_TempoModifier( TempoStateResult tempo )
    {
        return tempo.TempoState switch
        {
            TempoState.WinningHard  => +45,
            TempoState.Winning      => +25,
            TempoState.Neutral      => 0,
            TempoState.Losing       => -20,
            TempoState.LosingHard   => -40,
            _ => 0
        };
    }

    public int DefensiveSwitch_TempoModifier( TempoStateResult tempo )
    {
        return tempo.TempoState switch
        {
            TempoState.WinningHard  => -45,
            TempoState.Winning      => -25,
            TempoState.Neutral      => 0,
            TempoState.Losing       => +10,
            TempoState.LosingHard   => +25,
            _ => 0
        };
    }

    public int OffensiveSwitch_TempoModifier( TempoStateResult tempo )
    {
        return tempo.TempoState switch
        {
            TempoState.WinningHard  => -30,
            TempoState.Winning      => -15,
            TempoState.Neutral      => +0,
            TempoState.Losing       => -15,
            TempoState.LosingHard   => -35,
            _ => 0
        };
    }

    public int Setup_TempoModifier( TempoStateResult tempo )
    {
        return tempo.TempoState switch
        {
            TempoState.WinningHard  => -35,
            TempoState.Winning      => -15,
            TempoState.Neutral      => +0,
            TempoState.Losing       => +20,
            TempoState.LosingHard   => +10,
            _ => 0
        };
    }

    public ThreatResult GetThreat_ImmediateDamage( List<IBattleAIUnit> opponents, IBattleAIUnit ourPokemon )
    {
        int highestThreat = int.MinValue;
        IBattleAIUnit highestUnit = null;

        foreach( var threat in opponents )
        {
            int threatScore = 100;
            float moveThreat = float.MinValue;

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] Starting threat check on {threat.Pokemon.NickName}. Starting Score: {threatScore}" );

            //--Offensive Pressure
            int atk = threat.Attack;
            int spatk = threat.SpAttack;

            float offensivePressure;

            if( atk > spatk )
                offensivePressure = atk;
            else
                offensivePressure = spatk;

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Offensive Pressure is: {offensivePressure}" );
            
            if( offensivePressure >= 150f )             threatScore += 40;
            else if( offensivePressure >= 125f )        threatScore += 25;
            else if( offensivePressure >= 100f )        threatScore += 10;
            else if( offensivePressure >= 80f )         threatScore += 0;
            else if( offensivePressure >= 65f )         threatScore -= 10;
            else if( offensivePressure >= 50f )         threatScore -= 25;
            else if( offensivePressure < 50f )          threatScore -= 40;

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Offensive Pressure checked. Score: {threatScore}" );

            //--Move Threat
            foreach( var move in threat.ActiveMoves )
            {
                if( move.MoveSO.Power <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                    continue;

                if( threat.VolatileStatuses.Contains( VolatileConditionID.ChoiceLocked ) )
                {
                    var unit = GetBattleUnit( threat.Pokemon );
                    if( unit != null && move != unit.LastUsedMove )
                        continue;
                }

                var field = BattleSystem.Field;

                float effectiveness     = TypeChart.GetEffectiveness( move.MoveType, ourPokemon.Type.One ) * TypeChart.GetEffectiveness( move.MoveType, ourPokemon.Type.Two );
                float stab              = UnitSim.CheckTypes( move.MoveType, threat ) ? 1.5f : 1f;
                float weather           = 1f;
                float terrain           = 1f;
                float item              = 1f;

                if( field.Weather != null )
                {
                    if( UnitSim.WeatherDMGModifiers.TryGetValue( field.Weather.ID, out var mod ) )
                        weather = mod( move );
                }

                if( field.Terrain != null )
                {
                    if( UnitSim.TerrainDMGModifiers.TryGetValue( field.Terrain.ID, out var mod ) )
                        terrain = mod( move );
                }

                if( ourPokemon.Item != BattleItemEffectID.None )
                {
                    if( UnitSim.ItemDMGModifiers.TryGetValue( ourPokemon.Item, out var mod ) )
                        item = mod( ourPokemon, threat, move );
                }

                // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] Score-ing {threat.Pokemon.NickName}'s move {move.MoveSO.Name}. Effectiveness Modifier: {effectiveness}, STAB Modifier: {stab}, Weather Modifier: {weather}" );

                float currentMoveThreat = effectiveness * stab * weather * terrain * item;
                moveThreat = Mathf.Max( moveThreat, currentMoveThreat );

                // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s move {move.MoveSO.Name} checked. Move's Score: {moveThreat}" );
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

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Moves have all been checked. Score: {threatScore}" );
            var ourSpeed = GetUnitContextualSpeed( ourPokemon );
            var threatSpeed = GetUnitContextualSpeed( threat );
            //--Higher speed means the target is more threatening
            if( threatSpeed > ourSpeed )
                threatScore += 20;
            else if( threatSpeed < ourSpeed )
                threatScore -= 20;

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Speed comparison checked. Score: {threatScore}" );

            //--Current HP Ratio. Lower HP means we're more threatened
            float hpRatio = Get_HPRatio( ourPokemon );

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Current HP Ratio is: {hpRatio}" );

            if( hpRatio < 0.25f )           threatScore += 30;
            else if( hpRatio < 0.5f )       threatScore += 15;
            else if( hpRatio < 0.75f )      threatScore += 5;

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] {threat.Pokemon.NickName}'s Current HP Ratio checked. Score: {threatScore}" );

            threatScore = Mathf.Clamp( threatScore, 0, 300 );

            if( threatScore > highestThreat )
            {
                highestThreat = threatScore;
                highestUnit = threat;
            }

            // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] The current most threatening Pokemon is: {highestUnit.Pokemon.NickName}, with a Score of: {highestThreat}" );

        }

        // Debug.Log( $"[AI Scoring][Incoming Immediate Damage Check] The most threatening Pokemon is: {highestUnit.Pokemon.NickName}, with a Score of: {highestThreat}" );

        return new(){ Score = highestThreat, Unit = highestUnit };
    }

    public bool Check_UnitHasPriority( IBattleAIUnit attacker, IBattleAIUnit target )
    {
        for( int i = 0; i < attacker.ActiveMoves.Count; i++ )
        {
            if( BattleSystem.Field.Terrain != null && BattleSystem.Field.Terrain.ID == TerrainID.Psychic )
                return false;
            else
            {
                if( attacker.ActiveMoves[i].Priority > MovePriority.Zero && attacker.ActiveMoves[i].MoveSO.MoveCategory != MoveCategory.Status )
                {
                    if( attacker.ActiveMoves[i].MoveSO.Name == "Fake Out" )
                        return CanUseFakeOut( attacker, target );
                    else
                        return true;
                }
            }
        }

        return false;
    }

    public bool CanUseFakeOut( BattleUnit attacker, BattleUnit target )
    {
        if( !attacker.Pokemon.CheckHasMove( "Fake Out" ) )
            return false;

        if( attacker.Flags[UnitFlags.TurnsTaken].Count > 0 )
            return false;

        if( target.Pokemon.CheckTypes( PokemonType.Ghost ) )
            return false;

        return true;
    }

    public bool CanUseFakeOut( IBattleAIUnit attacker, IBattleAIUnit target )
    {
        var attackerUnit = GetBattleUnit( attacker.Pokemon );

        if( attackerUnit == null )
            return false;

        if( !attackerUnit.Pokemon.CheckHasMove( "Fake Out" ) )
            return false;

        if( attackerUnit.Flags[UnitFlags.TurnsTaken].Count > 0 )
            return false;

        if( UnitSim.CheckTypes( PokemonType.Ghost, target ) )
            return false;

        return true;
    }

    public bool Check_IsLastPokemon()
    {
        if( BattleSystem.BattleType == BattleType.WildBattle_1v1 )
            return true;

        var activeEnemyPokemon = BattleSystem.EnemyUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
        var remainingPokemon = BattleSystem.TopTrainer1.GetHealthyPokemon( dontInclude: activeEnemyPokemon );

        return remainingPokemon == null && activeEnemyPokemon.Count > 0;
    }

    public MoveThreatResult Get_MostThreateningMove( IBattleAIUnit attacker, IBattleAIUnit target, bool preview = false )
    {
        int bestScore = int.MinValue;
        float bestModifier = float.MinValue;
        Move bestMove = null;

        // if( preview )
            // Debug.Log( $"[Setup Action Evaluation Stat Stage Check] preview is true" );

        //--Move Threat
        foreach( var move in attacker.ActiveMoves )
        {
            if( move.MoveSO.Power <= 0 || move.MoveSO.MoveCategory == MoveCategory.Status )
                continue;

            int score = 0;

            float effectiveness     = TypeChart.GetEffectiveness( move.MoveType, target.Type.One ) * TypeChart.GetEffectiveness( move.MoveType, target.Type.Two );

            if( effectiveness == 0 )
                continue;

            // Debug.Log( $"[AI Scoring][Most Threatening Move][{attacker.NickName}][{move.MoveSO.Name}] Effectiveness: {effectiveness}, STAB: {stab}, Weather: {weather}, Terrain: {terrain}, Item: {item}" );

            float modifier = effectiveness * UnitSim.Get_MoveModifier( attacker, target, move );
            // float modifier = effectiveness * stab * weather * terrain * item;

            int movePower = move.MovePower;

            //--Multi hit move power projection
            if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y != 0 )
            {
                int minHits = move.MoveSO.HitRange.x;
                int maxHits = move.MoveSO.HitRange.y;

                int expectedHits = Mathf.FloorToInt( ( minHits + maxHits ) * 0.5f );

                movePower *= expectedHits;
            }
            else if( move.MoveSO.HitRange.x >= 2 && move.MoveSO.HitRange.y == 0 )
            {
                movePower *= move.MoveSO.HitRange.x;
            }

            if( movePower >= 90 )                       score += 30;
            else if( movePower >= 60 )                  score += 20;
            else if( movePower >= 45 )                  score += 15;
            else if( movePower >= 30 )                  score += 10;
            else if( movePower >= 15 )                  score += 5;

            if( modifier >= 9f )                 score += 90; //--Upper bounds, this move is 4x effective, has STAB, and benefits from weather.
            else if( modifier >= 6f )            score += 60; //--This move is 4x effective, and either has STAB OR benefits from weather.
            else if( modifier >= 4f )            score += 40; //--This move is 4x effective, or has some combination of 2x effective, stab, and weather.
            else if( modifier >= 3f )            score += 30; //--This move is 3x effective. It likely has 2x type effectiveness + stab.
            else if( modifier >= 2f )            score += 20;
            else if( modifier >= 1.5f )          score += 15;
            else if( modifier >= 1f )            score += 0;
            else if( modifier >= 0.5f )          score -= 20;
            else if( modifier >= 0.25f )         score -= 40;
            else if( modifier == 0f )            score = 0;

            int accuracy = move.MoveSO.Accuracy;
            if( accuracy < 70 )                         score -= 35;
            else if( accuracy < 80 )                    score -= 20;
            else if( accuracy < 90 )                    score -= 10;
            else if( accuracy < 100 )                   score -= 5;

            float tarHPR                    = Get_HPRatio( target );
            MoveThreatResult mtr            = new(){ Score = 0, Modifier = modifier, Move = move };
            var attEDR                      = Projection.Get_EstimatedDamageResult( attacker, target, mtr );
            PotentialToKOResult attPTKOR    = Projection.Get_PotentialToKOResult( attEDR, mtr, tarHPR );

            score += Mathf.FloorToInt( attEDR.DamageEstimate * 150 );

            int targetSpeed = GetUnitContextualSpeed( target );
            int attackerSpeed = GetUnitContextualSpeed( attacker );

            if( attPTKOR.PTKO > PotentialToKO.Risky )
                score += 20;

            if( targetSpeed > attackerSpeed && move.Priority > MovePriority.Zero && attPTKOR.PTKO > PotentialToKO.Risky )
                score += 50;
            else if( targetSpeed > attackerSpeed && move.Priority > MovePriority.Zero )
                score += 20;

            if( score > bestScore )
            {
                bestModifier = modifier;
                bestMove = move;
                bestScore = score;
            }

            //--If the attacker is choice-locked, when we get to the move we're locked into we log all of the scores and force-break from the loop
            //--because we cannot use any other move, and should always return this move as the "most threatening" because it is the ONLY threatening move.
            var attUnit = GetBattleUnit( attacker.Pokemon );
            if( attUnit != null )
            {
                if( attUnit.Flags[UnitFlags.ChoiceItem].IsActive )
                {
                    if( attUnit.LastUsedMove != null && attUnit.LastUsedMove == move )
                    {
                        bestModifier = modifier;
                        bestMove = move;
                        bestScore = score;
                        break;
                    }
                }
            }

            // Debug.Log( $"[AI Scoring][Most Threatening Move][{attacker.NickName}][{move.MoveSO.Name}] Modifier: {currentModifier}" );
        }

        bestMove ??= UnitSim.GetRandomMove( attacker );

        return new(){ Score = bestScore, Modifier = bestModifier, Move = bestMove };
    }

    public List<(int PTKO, Pokemon Mon )> GetTopThreats( List<Pokemon> team, Pokemon me )
    {
        List<( int ptko, Pokemon mon )> threats = new();
        BattleAI_PokemonAdapter ourMon = GetPokemonAs_Adapter( me );

        for( int i = 0; i < team.Count; i ++ )
        {
            BattleAI_PokemonAdapter theirMon = GetPokemonAs_Adapter( team[i] );

            //--MTRs
            var ourMTR = MoveCommand.GetMove_BestAttack( ourMon, theirMon );
            var theirMTR = MoveCommand.GetMove_BestAttack( theirMon, ourMon );

            //--EDRs
            var ourEDR = Projection.Get_EstimatedDamageResult( ourMon, theirMon, ourMTR );
            var theirEDR = Projection.Get_EstimatedDamageResult( theirMon, ourMon, theirMTR );

            //--PTKOs
            var ourPTKO = Projection.Get_PotentialToKOResult( ourEDR, ourMTR, theirMon.CurrentHPR ).PTKO;
            var theirPTKO = Projection.Get_PotentialToKOResult( theirEDR, theirMTR, ourMon.CurrentHPR ).PTKO;

            if( theirPTKO - 1 > ourPTKO || theirPTKO > PotentialToKO.Risky && theirMTR.Top.AttackerMovedFirst )
                threats.Add( ( (int)theirPTKO, team[i] ) );
        }

        threats.Sort( ( a, b ) => a.CompareTo( ( a.ptko, a.mon ) ) );

        return threats;
    }

    public void RefreshTeamPieceValues( List<Pokemon> ourTeam, List<Pokemon> theirTeam )
    {
        List<IBattleAIUnit> ourTeamAIUnits = new();
        
        for( int i = 0; i < ourTeam.Count; i++ )
        {
            BattleAI_PokemonAdapter mon = GetPokemonAs_Adapter( ourTeam[i] );
            ourTeamAIUnits.Add( mon );
        }

        OurTeamPieceValues = CalculateTeamPieceValues( ourTeamAIUnits );

        List<IBattleAIUnit> theirTeamAIUnits = new();
        
        for( int i = 0; i < theirTeam.Count; i++ )
        {
            BattleAI_PokemonAdapter mon = GetPokemonAs_Adapter( theirTeam[i] );
            theirTeamAIUnits.Add( mon );
        }

        TheirTeamPieceValues = CalculateTeamPieceValues( theirTeamAIUnits );

        foreach( var kvp in OurTeamAdapters )
        {
            kvp.Value.SetExpendability();
        }

        foreach( var kvp in TheirTeamAdapters )
        {
            kvp.Value.SetExpendability();
        }
    }

    public Dictionary<Pokemon, PieceValue> CalculateTeamPieceValues( List<IBattleAIUnit> team )
    {
        // Debug.Log( $"[AI Scoring][Piece Value] Refreshing Team Piece Values!" );
        Dictionary<Pokemon, PieceValue> teamPieceValues = new();

        var attackingTiers = PV_GetRankBonuses( team, mon => Mathf.Max( mon.Attack, mon.SpAttack ) );
        var speedTiers = PV_GetRankBonuses( team, mon => GetUnitContextualSpeed( mon ) );

        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            
            ( int offensiveValue, int threatCount, int speedScore ) = PV_GetOffensiveValue( mon, attackingTiers, speedTiers );

            PieceValue value = new()
            {
                OffensiveValue = offensiveValue,
                ThreatCount = threatCount,
                SpeedScore = speedScore,
            };

            teamPieceValues.Add( mon.Pokemon, value );
            // Debug.Log( $"[AI Scoring][Piece Value] {mon.Name} value assigned! Offensive Value: {value.OffensiveValue}, Speed Score: {value.SpeedScore}" );
        }

        return teamPieceValues;
    }

    public PieceValue GetPokemon_PieceValue( Pokemon pokemon )
    {
        if( OurTeamPieceValues.TryGetValue( pokemon, out var ourPV ) )
        {
            return ourPV;
        }

        if( TheirTeamPieceValues.TryGetValue( pokemon, out var theirPV ) )
        {
            return theirPV;
        }

        return default;
    }

    private ( int OffensiveValue, int threatCount, int SpeedScore ) PV_GetOffensiveValue( IBattleAIUnit pokemon, Dictionary<IBattleAIUnit, int> attackingRanks, Dictionary<IBattleAIUnit, int> speedRanks )
    {
        var oppTeam = BattleSystem.GetOpposingParty( pokemon.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
        int score = 50;

        score += attackingRanks[pokemon];
        score += speedRanks[pokemon];

        //--PTKO Stuff here
        int threatCount = 0;
        int spreadPressure = 0;
        for( int i = 0; i < oppTeam.Count; i++ )
        {
            BattleAI_PokemonAdapter opp = GetPokemonAs_Adapter( oppTeam[i] );
            var ptko = Projection.Get_NeutralPTKO( pokemon, opp );
            if( ptko >= PotentialToKO.TwoHKO )
                threatCount++;

            spreadPressure += ptko switch
            {
                PotentialToKO.TwoHKO    => 3,
                PotentialToKO.Risky     => 5,
                PotentialToKO.Dangerous => 10,
                PotentialToKO.OHKO      => 15,
                _ => 0
            };
        }

        if( threatCount > 2 )          score += 5;

        return ( score, threatCount, speedRanks[pokemon] );
    }

    private Dictionary<IBattleAIUnit, int> PV_GetRankBonuses( List<IBattleAIUnit> team, Func<IBattleAIUnit, int> valueSelector )
    {
        List<( IBattleAIUnit Mon, int Value )> statList = new();
        Dictionary<IBattleAIUnit, int> tiers = new();

        for( int i = 0; i < team.Count; i++ )
        {
            var mon = team[i];
            int value = valueSelector( mon );
            statList.Add( ( mon, value ) );
        }

        var sorted = statList.OrderByDescending( t => t.Value ).Select( t => t.Mon ).ToList();

        for( int i = 0; i < sorted.Count; i++ )
        {
            int score = 0;

            if( i == 0 )        score = 15;
            else if( i == 1 )   score = 10;
            else if( i == 2 )   score = 5;

            tiers.Add( sorted[i], score );
        }

        return tiers;
    }

    public float Get_HPRatio( Pokemon pokemon )
    {
        float currentHP = pokemon.CurrentHP;
        float maxHP = pokemon.MaxHP;

        // Debug.Log( $"[AI Scoring][Getting HP Ratio] {pokemon.NickName}'s HP Ratio is: {currentHP/maxHP}" );
        return currentHP / maxHP;
    }

    public float Get_HPRatio( IBattleAIUnit pokemon )
    {
        return pokemon.CurrentHPR;
    }

    public float Get_HPRatio_AfterEntryHazards( Pokemon pokemon )
    {
        // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Getting HP Ratio for {pokemon.NickName} after taking entry hazard damage!" );
        float hpR = Get_HPRatio( pokemon );
        float damage = Get_EntryHazardDamage( pokemon );

        float finalHPR = Mathf.Max( 0f, hpR - damage );
        // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName}'s Raw HPR: {hpR}, HPR after Hazards: {finalHPR}" );

        return finalHPR;
    }

    public float Get_HPRatio_AfterEntryHazards( IBattleAIUnit pokemon )
    {
        // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Getting HP Ratio for {pokemon.NickName} after taking entry hazard damage!" );
        float hpR = Get_HPRatio( pokemon );
        float damage = Get_EntryHazardDamage( pokemon );

        float finalHPR = Mathf.Max( 0f, hpR - damage );
        // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName}'s Raw HPR: {hpR}, HPR after Hazards: {finalHPR}" );

        return finalHPR;
    }

    public float Get_EntryHazardDamage( Pokemon pokemon )
    {
        float damage = 0;
        var myCourtLoc = BattleSystem.Field.GetPokemonCourtLocationFromTrainer( pokemon );

        // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName} was found in the {myCourtLoc}!" );

        //--Heavy duty boots prevents hazard damage.
        if( pokemon.HeldItem != null && pokemon.BattleItemEffect?.ID == BattleItemEffectID.HeavyDutyBoots )
        {
            // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName} is holding Heavy Duty Boots! No hazard damage should be taken! Damage: {damage}" );
            return damage;
        }

        var court = BattleSystem.Field.ActiveCourts[myCourtLoc];
        if( court.Conditions.ContainsKey( CourtConditionID.StealthRock ) )
        {
            float effectiveness = TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.PokeSO.Type2 );
            damage += ( 1f / 8f ) * effectiveness;
            // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Stealth Rock was found in the {myCourtLoc}! Damage: {damage}" );
        }

        if( court.Conditions.ContainsKey( CourtConditionID.Spikes ) )
        {
            var spikes = court.Conditions[CourtConditionID.Spikes];
            int layers = spikes.Layers;

            if( layers == 1 )
                damage += 1f / 8f;
            else if( layers == 2 )
                damage += 1f / 6f;
            else if( layers >= 3 )
                damage += 1f / 4f;

            // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Spikes ({layers}) were found in the {myCourtLoc}! Damage: {damage}" );
        }

        return damage;
    }

    public float Get_EntryHazardDamage( IBattleAIUnit pokemon )
    {
        float damage = 0;
        var myCourtLoc = BattleSystem.Field.GetPokemonCourtLocationFromTrainer( pokemon.PID );

        // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName} was found in the {myCourtLoc}!" );

        //--Heavy duty boots prevents hazard damage.
        if( pokemon.Item == BattleItemEffectID.HeavyDutyBoots )
        {
            // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] {pokemon.NickName} is holding Heavy Duty Boots! No hazard damage should be taken! Damage: {damage}" );
            return damage;
        }

        var court = BattleSystem.Field.ActiveCourts[myCourtLoc];
        if( court.Conditions.ContainsKey( CourtConditionID.StealthRock ) )
        {
            float effectiveness = TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.Type.One ) * TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.Type.Two );
            damage += ( 1f / 8f ) * effectiveness;
            // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Stealth Rock was found in the {myCourtLoc}! Damage: {damage}" );
        }

        if( court.Conditions.ContainsKey( CourtConditionID.Spikes ) )
        {
            var spikes = court.Conditions[CourtConditionID.Spikes];
            int layers = spikes.Layers;

            if( layers == 1 )
                damage += 1f / 8f;
            else if( layers == 2 )
                damage += 1f / 6f;
            else if( layers >= 3 )
                damage += 1f / 4f;

            // Debug.Log( $"[AI Scoring][HP Ratio][Hazard Damage] Spikes ({layers}) were found in the {myCourtLoc}! Damage: {damage}" );
        }

        return damage;
    }

    public float Get_EntryHazardDamage( IBattleAIUnit pokemon, CourtConditionID hazard, int layers = 1 )
    {
        float damage = 0;

        //--Heavy duty boots prevents hazard damage.
        if( pokemon.Item == BattleItemEffectID.HeavyDutyBoots )
            return damage;

        if( hazard == CourtConditionID.StealthRock )
        {
            float effectiveness = TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.Type.One ) * TypeChart.GetEffectiveness( PokemonType.Rock, pokemon.Type.Two );
            damage += ( 1f / 8f ) * effectiveness;
        }

        if( hazard == CourtConditionID.Spikes )
        {
            if( layers == 1 )
                damage += 1f / 8f;
            else if( layers == 2 )
                damage += 1f / 6f;
            else if( layers >= 3 )
                damage += 1f / 4f;
        }

        return damage;
    }

    private void InitializeUniqueStatCalls()
    {
        UniqueStatCalls = new()
        {
            { "Body Press", new(){ AttackingStat = Stat.Defense, DefendingStat = Stat.Defense } },
        };
    }

    public Dictionary<Pokemon, SwitchCandidateResult> GetLikely_DefensiveSwitches( IBattleAIUnit theirActiveMon, int thresholdValue = 40, bool ignoreThreshold = false )
    {
        Dictionary<Pokemon, SwitchCandidateResult> likelySwitches = new();
        List<SwitchCandidateResult> likelySRCs = new();

        var scr = SwitchCommand.GetSwitch_Defensive( theirActiveMon, true );
        Dictionary<Pokemon, SwitchCandidateResult> allCandidates = new();

        CustomLogSession likelyLog = new();

        likelyLog.Add( $"" );
        likelyLog.Add( $"=[Getting Likely Defensive Switches]=" );

        if( scr.ReturnAllList == null || scr.ReturnAllList.Count <= 0 )
        {
            Debug.LogError( "GetSwitch_Defensive() returned an empty Return All List, which likely means there are no viable defensive candidates." );
            return likelySwitches;
        }
        else
            allCandidates = scr.ReturnAllList.ToDictionary( kvp => kvp.Key, kvp => kvp.Value );

        likelyLog.Add( $"All Candidates Count: {allCandidates.Count}." );
        foreach( var cand in allCandidates )
            likelyLog.Add( $"{cand.Key.NickName}, {cand.Value.Score}" );
            
        allCandidates = allCandidates.OrderByDescending( c => c.Value.Score ).ToDictionary( kvp => kvp.Key, kvp => kvp.Value );

        var bestCandidateKVP = allCandidates.First();
        int bestCandidateScore = bestCandidateKVP.Value.Score;
        likelyLog.Add( $"Best Candidate Score: {bestCandidateScore}." );

        int count = 0;
        foreach( var kvp in allCandidates )
        {
            count++;
            var mon = kvp.Key;
            var s = kvp.Value.Score;
            int threshold = ignoreThreshold ? 0 : bestCandidateScore - thresholdValue;
            likelyLog.Add( $"Threshold: {threshold}." );
            likelyLog.Add( $"Checking Key {mon.NickName}, Value: {kvp.Value.Pokemon.NickName}, Score: {s}." );

            if( s >= threshold )
                likelySwitches.Add( kvp.Key, kvp.Value );

            if( !ignoreThreshold && count > 3 ) //--Caps at top 4 candidates
                break;
        }

        likelyLog.Add( $"Likely Switches Count: {likelySwitches.Count}." );
        likelyLog.Add( $"" );

        Debug.Log( likelyLog.ToString() );
        likelyLog.Clear();

        return likelySwitches;
    }

    public Dictionary<Pokemon, SwitchCandidateResult> GetLikely_OffensiveSwitches( IBattleAIUnit theirActiveMon, int thresholdValue = 40, bool ignoreThreshold = false )
    {
        Dictionary<Pokemon, SwitchCandidateResult> likelySwitches = new();
        List<SwitchCandidateResult> likelySRCs = new();

        var scr = SwitchCommand.GetSwitch_Offensive( theirActiveMon, true );
        Dictionary<Pokemon, SwitchCandidateResult> allCandidates = new();

        CustomLogSession likelyLog = new();

        likelyLog.Add( $"" );
        likelyLog.Add( $"=[Getting Likely Offensive Switches]=" );

        if( scr.ReturnAllList == null || scr.ReturnAllList.Count <= 0 )
        {
            Debug.LogError( "GetSwitch_Offensive() returned an empty Return All List, which likely means there are no viable offensive candidates." );
            return likelySwitches;
        }
        else
            allCandidates = scr.ReturnAllList.ToDictionary( kvp => kvp.Key, kvp => kvp.Value );

        likelyLog.Add( $"All Candidates Count: {allCandidates.Count}." );
        foreach( var cand in allCandidates )
            likelyLog.Add( $"{cand.Key.NickName}, {cand.Value.Score}" );
        
        var sorted = allCandidates.OrderByDescending( c => c.Value.Score ).ToDictionary( kvp => kvp.Key, kvp => kvp.Value );

        var bestCandidateKVP = sorted.First();
        int bestCandidateScore = bestCandidateKVP.Value.Score;
        likelyLog.Add( $"Best Candidate Score: {bestCandidateScore}." );

        int count = 0;
        foreach( var kvp in sorted )
        {
            count++;
            var mon = kvp.Key;
            var s = kvp.Value.Score;
            int threshold = ignoreThreshold ? 0 : bestCandidateScore - thresholdValue;
            likelyLog.Add( $"Threshold: {threshold}." );
            likelyLog.Add( $"Checking Key {mon.NickName}, Value: {kvp.Value.Pokemon.NickName}, Score: {s}." );

            if( s >= threshold )
                likelySwitches.Add( kvp.Key, kvp.Value );

            if( !ignoreThreshold && count > 3 ) //--Caps at top 4 candidates
                break;
        }

        likelyLog.Add( $"Likely Switches Count: {likelySwitches.Count}." );
        likelyLog.Add( $"" );

        Debug.Log( likelyLog.ToString() );
        likelyLog.Clear();

        return likelySwitches;
    }
    
}

public struct ThreatResult
{
    public int Score { get; set; }
    public IBattleAIUnit Unit { get; set; }
}

public class MoveThreatResult
{
    public float Score { get; set; }
    public float Modifier { get; set; }
    public IBattleAIUnit Target { get; set; }
    public BattleUnit TargetBattleUnit { get; set; }
    public Move Move { get; set; }
    public float EstimatedDamage { get; set; }
    public TurnOutcomeProjection Top { get; set; }
}

public struct SetupThreatResult
{
    public Move Move;
    public IBattleAIUnit Target;
    public BattleUnit TargetBattleUnit;
    public TurnOutcomeProjection Top;

    public StatStageDelta StageDelta;

    public PotentialToKOResult BeforePTKOR;
    public PotentialToKOResult AfterPTKOR;

    public int SetupValue;
    public int SweepCount;
    public int ImprovedPTKOs;
}

public struct StatusThreatResult
{
    public OffensiveStatusType Type;
    public int Score;
    public int StatusValue;
    public Move Move;
    public IBattleAIUnit Target;
    public BattleUnit TargetBattleUnit;
    public TurnOutcomeProjection Top;

    public int TeamCoverage;
    public int BoardAmbiguity;
    public int Reliability;
    public int ImmediateImpact;

    public PotentialToKOResult AttackerPTKOR;
    public PotentialToKOResult OpponentPTKOR;
    public bool OpponentSwitches;
}

//--This stores the stage changes for setup moves.
public struct StatStageDelta
{
    public int HP;
    public int Attack;
    public int Defense;
    public int SpAttack;
    public int SpDefense;
    public int Speed;

    public int Accuracy;
    public int Evasion;

    public float CritRatio;
}

public struct SwitchCandidateResult
{
    public int Score { get; set; }
    public Pokemon Pokemon { get; set; }
    public PotentialToKOResult SwitchOffensePTKOR { get; set; }
    public PotentialToKOResult SwitchDefensePTKOR { get; set; }
    public float HPRatio { get; set; }
    public bool IsLegitimate { get; set; }
    public bool MovesFirst { get; set; }
    public TurnOutcomeProjection Top { get; set; }

    public Dictionary<Pokemon, SwitchCandidateResult> ReturnAllList;
}

public struct EstimatedDamageResult
{
    public int Score;
    public float DamageEstimate;
    public float LowRollEstimate;
    public int AttackingStatStage;
    public int DefendingStatStage;
    public float AttackingDirectModifier;
    public float DefendingDirectModifier;
    public IBattleAIUnit Attacker;
    public IBattleAIUnit Target;
}

public struct PotentialToKOResult
{
    public int Score { get; set; }
    public PotentialToKO PTKO { get; set; }
    public float Modifier { get; set; }
}

public struct TempoStateResult
{
    public TempoState TempoState { get; set; }
    public bool AttackerHasPriority { get; set; }
    public bool TargetHasPriority { get; set; }
    public string AttackerName { get; set; }
    public string TargetName { get; set; }
}

public struct ExchangeEvaluation
{
    public string AttackerName;
    public string OpponentName;

    public bool AttackerMovesFirst;
    public bool OpponentMovesFirst;

    public bool AttackerHasPriorityMove;
    public bool OpponentHasPriorityMove;

    public bool AttackerThreatensKO;
    public bool OpponentThreatensKO;

    public bool AttackerKillsFirst;
    public bool OpponentKillsFirst;

    public bool AttackerSurvives;
    public bool OpponentSurvives;

    public PotentialToKOResult AttackerPTKOR;
    public PotentialToKOResult OpponentPTKOR;

    public float AttackerHPR;
    public float OpponentHPR;

    public bool OpponentSwitches;
    public bool AttackerSwitches;
    public float OpponentSwitchProbability;
    public float AttackerSwitchProbability;

    public string AttackerMoveName;
    public string OpponentMoveName;

    public ExchangeState ExchangeState;
}

public struct BoardContext
{
    public bool IsForcedTrade;
    public bool HasSafePivot;

    public bool IsAhead;
    public bool IsBehind;

    public float MyTeamHPPercent;
    public float OppTeamHPPercent;

    public int MyRemainingPieces;
    public int OppRemainingPieces;

    public bool IsTerminal;
    public float MyExpendability;

    public List<IBattleAIUnit> MyTeamAlive;
    public List<IBattleAIUnit> OppTeamAlive;

    public BattlefieldState BattlefieldState;
}

public struct PieceValue
{
    public int OffensiveValue;
    public int DefensiveValue;
    public int ThreatCount;
    public int SpeedScore;
    public int SetupValue;
    public int SupportValue;
}

public struct UniqueWallingScoreMove
{
    public Stat AttackingStat;
    public Stat DefendingStat;
}

public class ActionEvaluation
{
    public ActionType Type;
    public int Score;
    public Pokemon Actor;
    public BattleUnit Target;
    public Move MovePayload;
    public Pokemon SwitchPayload;
    public TurnOutcomeProjection Top1;
    public TurnOutcomeProjection Top2;
    public ProjectedBoardState PBS;

    public ExchangeEvaluation ExchangeEvaluation;

    public bool NextTurn_WeAreForcedOut;
    public bool NextTurn_TheyAreForcedOut;

    public SurvivalClass SurvivalClass;
}

public struct MaterialStatus
{
    public int MyRemainingPieces;
    public int OppRemainingPieces;

    public float MyTeamHPPercent;
    public float OppTeamHPPercent;

    public bool IsAhead;
    public bool IsBehind;
}

public enum PlanType { None, Stabilize, Trade, Aggress, EnableSweep, PreventSweep }
public class CurrentPlan
{
    public PlanType Type;
    public Pokemon FocusMon;
    public float Confidence;
    public bool AllowSacrifice;
    public bool SweepPotential;

    public int TurnsActive;
}

public enum ThreatUrgency { None, Low, Medium, High, Critical }
public enum ThreatType
{
    None,
    Immediate,      //--can immediately swing/remove units
    Escalating,     //--Becomes exceedingly dangerous if unchecked
    Persistent,     //--Hard to remove and/or provides long-term pressure
    Disruptive,     //--Status, hazards, denial, speed control, etc.
    Constraining,   //--Limits safe actions/switches
}

public enum ExpectedThreatBehavior
{
    Aggressive, //--Attack!
    Reactive, //--Switch defensively?
    Passive, //--Stay in, maybe make a suboptimal switch?
    SetupAction, //--Swords dance
    RecoveryAction, //--Slack off
    UtilityAction, //--Thunder wave, sleep powder, light screen
}

public struct ThreatProfile
{
    public bool Exists;

    public ThreatType Type;
    public ExpectedThreatBehavior ExpectedThreatBehavior;

    public IBattleAIUnit ThreatUnit;
    public Pokemon ThreatPokemon;

    //--Main Signals
    public bool ThreatensImmediateKO;
    public bool OutspeedsCurrent;
    public PotentialToKO ThreatPTKO;

    //--Team-wide Pressure
    public int ThreatenedAlliesCount; //--How many of our remaining mons it pressures
    public int OutspeedsAlliesCount; //--How many of our remaining mons it outspeeds

    //--Behavior Flags
    public bool ForcesSwitch;
    public bool SweepPotential;

    //--Pressure Scores & Urgency
    public float PressureScore;
    public float ConstrainingPressure;
    public float ImmediatePressure;
    public float EscalatingPressure;
    public float PersistentPressure;
    public float DisruptivePressure;
    public ThreatUrgency Urgency;

    //--Decay
    public float DecayScore;
    public bool IsDecaying;
}

public struct BattlefieldState
{
    public int Round;
    public bool IsEarlyGame;
    public bool IsMidGame;
    public bool IsLateGame;

    public WeatherConditionID Weather;
    public int WeatherDuration;
    public TerrainID Terrain;
    public int TerrainDuration;

    public int EntryHazardsOn_MySide;
    public int EntryHazardsOn_TheirSide;

    public bool WeHave_Tailwind;
    public bool TheyHave_Tailwind;
    public bool WeHave_TailwindSetter;
    public bool TheyHave_TailwindSetter;

    public int OurTailwindDuration;
    public int TheirTailwindDuration;

    public bool TrickRoomActive;
    public bool WeHave_TrickRoomAdvantage;
    public bool TheyHave_TrickRoomAdvantage;
    public bool WeHave_TrickRoomSetter;
    public bool TheyHave_TrickRoomSetter;
    public int TrickRoomDuration;

    public bool WeHave_WeatherControl;
    public bool TheyHave_WeatherControl;
    public bool WeHave_WeatherSetter_Ability;
    public bool TheyHave_WeatherSetter_Ability;
    public bool WeHave_WeatherSetter_Move;
    public bool TheyHave_WeatherSetter_Move;

    public bool WeHave_TerrainSetter_Ability;
    public bool TheyHave_TerrainSetter_Ability;
    public bool WeHave_TerrainSetter_Move;
    public bool TheyHave_TerrainSetter_Move;
    public bool WeHave_TerrainControl;
    public bool TheyHave_TerrainControl;

    public bool WeHave_Reflect;
    public bool WeHave_LightScreen;
    public bool WeHave_AuroraVeil;
    public bool WeHave_ReflectSetter;
    public bool WeHave_LightScreenSetter;
    public bool WeHave_AuroraSetter;
    public int OurReflectDuration;
    public int OurLightScreenDuration;
    public int OurAuroraVeilDuration;

    public bool TheyHave_Reflect;
    public bool TheyHave_LightScreen;
    public bool TheyHave_AuroraVeil;
    public bool TheyHave_ReflectSetter;
    public bool TheyHave_LightScreenSetter;
    public bool TheyHave_AuroraSetter;
    public int TheirReflectDuration;
    public int TheirLightScreenDuration;
    public int TheirAuroraVeilDuration;

    public bool WeHave_FieldControl;
    public bool TheyHave_FieldControl;
    public int FieldControlDelta;
}

public enum TeamArchetype
{
    HyperOffense,
    BulkyOffense,
    Balance,
    Stall,
    HardTrickRoom,
}

public enum TeamStrategy
{
    HazardPressure,
    PivotCycling,
    SetupSweeping,
    WeatherAbuse,
    SpeedControl,
    TrickRoom,
    StatusAttrition,
    ScreenSupport,
    Phazing,
    Sun,
    Rain,
    Sand,
    Snow,
    TerrainControl,
}

public struct TeamStrengthProfile
{
    public int Offense;
    public int Bulk;
    public int Utility;
    public int Speed;
    public int Setup;
    public int Pressure;
}

public class TeamComposition
{
    public TeamArchetype PrimaryArchetype;
    public TeamArchetype SecondaryArchetype;
    public Dictionary<TeamArchetype, float> ArchetypeScores;

    public TeamStrengthProfile Strengths;
    
    public HashSet<TeamStrategy> Strategies;
    public Dictionary<TeamStrategy, int> StrategyScores;

    public BattleAI_PokemonAdapter Primary_Sweeper;
    public BattleAI_PokemonAdapter Primary_SetupSweeper;
    public BattleAI_PokemonAdapter Primary_Pivot;
    public BattleAI_PokemonAdapter Primary_PhysicalWall;
    public BattleAI_PokemonAdapter Primary_SpecialWall;
    public BattleAI_PokemonAdapter Primary_Disruption;
    public BattleAI_PokemonAdapter Primary_HazardSetter;
    public BattleAI_PokemonAdapter Primary_SpeedControlProvider;
    public BattleAI_PokemonAdapter Primary_WeatherSetter;
    public BattleAI_PokemonAdapter Primary_TrickRoomSetter;

    public List<BattleAI_PokemonAdapter> Team;
}

public class GamePlan
{
    public Pokemon OurPrimaryWinCon;
    public Pokemon TheirPrimaryWinCon;
    public List<Pokemon> OurBlockers;
    public List<Pokemon> OurEnablers;
    public List<Pokemon> TheirBlockers;
    public List<Pokemon> TheirEnablers;
}

public class GamePlanAnalysis
{
    public int AdvantageScore;
    public int DangerScore;
    public int EnableScore;
    public int BlockScore;
    public int WinningMatchups;
    public int LosingMatchups;
    public int WinConScore;
}
