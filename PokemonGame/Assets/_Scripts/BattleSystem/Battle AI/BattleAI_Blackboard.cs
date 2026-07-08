using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

public class BattleAI_Blackboard
{
    private readonly BattleAI _ai;
    public Dictionary<BattleUnit, UnitTracker> MyActiveUnits { get; private set; }
    public ReadOnlyCollection<Pokemon> OurTeamPokemon { get; private set; }
    public ReadOnlyCollection<Pokemon> TheirTeamPokemon { get; private set; }
    public Dictionary<Pokemon, BattleAI_PokemonAdapter> OurTeamAdapters { get; private set; }
    public Dictionary<Pokemon, BattleAI_PokemonAdapter> TheirTeamAdapters { get; private set; }
    public List<IBattleAIUnit> OurActiveBattleAIUnits { get; private set; }
    public List<IBattleAIUnit> TheirActiveBattleAIUnits { get; private set; }
    public Dictionary<Pokemon, PieceValue> OurTeamPieceValues { get; private set; }
    public Dictionary<Pokemon, PieceValue> TheirTeamPieceValues { get; private set; }
    public SimulatedField CurrentFieldSnapshot { get; private set; }
    public TeamComposition OurTeamComposition { get; private set; }
    public TeamComposition TheirTeamComposition { get; private set; }
    public GamePlan GamePlan { get; private set; }

    //--Each Battle AI monob should now become an ai "agent".
    //--An "agent" should be able to use all tools available to it. it makes decisions and submits its decisions to the battle system.
    //--universal tracking information should be handled by the blackboard. the blackboard can be accessed by each one if its OWN agents.
    //--each ai trainer will have its own blackboard, in the event of ai vs ai battles, or player + ai vs ai ( and even + a 3rd ai) multi battles.
    //--It should be a refactor goal to have the blackboard own all of the brain layer and scoring layer functions as well.
    //--for example, in a player vs ai double battle, there is one blackboard, which then generates one set of classes for its two agents to access for decision making.
    //--the blackboard itself will naturally build all unit trackers and anything else of that nature, such as team adapters and active unit adapters.

    //--ohh, no, an agent doesn't run its own code - the blackboard should choose commands for each unit.
    //--so realistically, BattleAI becomes the blackboard, and instead battle unit objects should receive an "ai agent" class, or maybe even not that, just the flag inside of BattleUnit
    //--noting whether it is an ai controlled unit or not, because a lot of battle system architecture itself depends on that flag.
    //--the BattleAI class should simply loop over its own units and choose a command for each one. essentially moving the part of this that already exists
    //--out of the ai turn state and into the main ai command selection. "ThisUnitAdapter" should effectively get phased out, and replaced across all analysis
    //--by the "current unit the ai is making a decision for". then, we can skip marking units as ai, and instead, we simply turn on the BattleAI object associated with each team's side
    //--this will be a dynamic option to allow for multiple ai controllers depending on what the battle calls for. regular player vs 1 ai single and double battles will obviously have
    //--1 BattleAI mono object turned on for the top court. ai vs ai singles/doubles will have an object for top and bottom courts. and then any ai controlled slots in a multibattle
    //--will simply have their own controllers as well.

    //--so if BattleAI.cs becomes the main brain and orchestrates decision making for units, i think the blackboard class should assist by actually handling all static information and info tracking
    //--that means team adapters, team comp, game plan, last actions, last active pokemon, current active units on both sides, etc. all of that should exist here, and be extracted out of BattleAI.cs
    //--that way, when BattleAI.cs or any downstream decision making function needs access to static/tracked information, it will simply look at the blackboard. the blackboard can even have functions
    //--to update learned information across any given battle. improved estimates on EV and nature spreads, for example, could live here.

    public BattleAI_Blackboard( BattleAI ai )
    {
        _ai = ai;
    }
    
    public void Init( List<BattleUnit> battleUnits )
    {
        OurTeamPieceValues = new();
        TheirTeamPieceValues = new();
        TheirActiveBattleAIUnits = new();
        OurActiveBattleAIUnits = new();

        //--TODO: this needs to be adjusted for wild battles!
        OurTeamPokemon = new( _ai.Trainer.Party );
        TheirTeamPokemon = new( _ai.BattleSystem.GetOpposingParty( OurTeamPokemon[0] ) );

        MyActiveUnits = new();
        for( int i = 0; i < battleUnits.Count; i++ )
        {
            var unit = battleUnits[i];
            // Debug.LogError( $"Unit ({i}) is: {unit.Pokemon.NickName}");
            UnitTracker tracker = new()
            {
                CurrentPokemon = unit.Pokemon,
                SwitchAmount = 0,
                SetupAmount = 0,
            };

            MyActiveUnits.Add( unit, tracker );
        }

        SetupTeamAdapters();
        SetActiveBattleAIUnits();
        SetCurrentFieldSnapshot();

        OurTeamComposition = CreateTeamComposition( OurTeamAdapters.Values.ToList() );
        TheirTeamComposition = CreateTeamComposition( TheirTeamAdapters.Values.ToList() );

        GamePlan = CreateGamePlan( OurTeamComposition, TheirTeamComposition );

        UpdateTeamPieceValues();
    }

    public void SetupTeamAdapters()
    {
        OurTeamAdapters = new();
        TheirTeamAdapters = new();

        var ourTeam = OurTeamPokemon;
        var theirTeam = TheirTeamPokemon;

        // Debug.LogError( $"Our Team Count: {ourTeam.Count}, Their Team Count: {theirTeam.Count}" );

        for( int i = 0; i < ourTeam.Count; i++ )
        {
            var mon = ourTeam[i];
            BattleAI_PokemonAdapter adapter = new( mon, _ai );
            // Debug.LogError( $"Our Adapter: {adapter.Name}" );
            OurTeamAdapters.Add( adapter.Pokemon, adapter );
        }

        for( int i = 0; i < theirTeam.Count; i++ )
        {
            var mon = theirTeam[i];
            BattleAI_PokemonAdapter adapter = new( mon, _ai );
            // Debug.LogError( $"Their Adapter: {adapter.Name}" );
            TheirTeamAdapters.Add( adapter.Pokemon, adapter );
        }
    }

    public void UpdateTeamAdapters()
    {
        var ourTeam = OurTeamPokemon;
        var theirTeam = TheirTeamPokemon;

        Action<Pokemon, Dictionary<Pokemon, BattleAI_PokemonAdapter>> update = ( mon, adapters ) =>
        {
            if( adapters.TryGetValue( mon, out var adapter ) )
            {
                adapter.BeginningHPR = _ai.Get_HPRatio( mon );
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

    public void SetCurrentFieldSnapshot()
    {
        CurrentFieldSnapshot = _ai.UnitSim.BuildSimField();
    }

    public void SetActiveBattleAIUnits()
    {
        var ourUnits = MyActiveUnits.Keys.ToList();
        var theirUnits = _ai.BattleSystem.GetOpposingUnits( ourUnits[0] );

        // Debug.LogError( $"Our Units Count: {ourUnits.Count}, Their Units Count: {theirUnits.Count}" );

        OurActiveBattleAIUnits.Clear();
        TheirActiveBattleAIUnits.Clear();

        for( int i = 0; i < ourUnits.Count; i++ )
        {
            // Debug.LogError( $"Our unit is: {ourUnits[i].Pokemon.NickName}" );
            if( OurTeamAdapters.TryGetValue( ourUnits[i].Pokemon, out var adapter ) )
            {
                OurActiveBattleAIUnits.Add( adapter );
            }
        }

        for( int i = 0; i < theirUnits.Count; i++ )
        {
            // Debug.LogError( $"Checking Their Team Adapters for unit key. Their Team Adapter Count: {TheirTeamAdapters.Count}" );
            // Debug.LogError( $"Their unit is: {theirUnits[i].Pokemon.NickName}." );
            if( TheirTeamAdapters.TryGetValue( theirUnits[i].Pokemon, out var adapter ) )
            {
                TheirActiveBattleAIUnits.Add( adapter );
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
            if( traits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || traits.Contains( RoleTrait.SpeciallyOffensiveSetup ) || primary == RoleClass.SetupSweeper || _ai.UnitSim.PokemonIsIronDefenseBodyPress( pokemon.Pokemon ) )
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
                if( pokemon.Ability == AbilityID.Drought || _ai.UnitSim.CheckHasMove( pokemon, "Sunny Day" ) )
                {
                    sunSetter++;
                    tc.StrategyScores[TeamStrategy.Sun]++;
                }

                if( pokemon.Ability == AbilityID.Drizzle || _ai.UnitSim.CheckHasMove( pokemon, "Rain Dance" ) )
                {
                    rainSetter++;
                    tc.StrategyScores[TeamStrategy.Rain]++;
                }

                if( pokemon.Ability == AbilityID.Sandstream || _ai.UnitSim.CheckHasMove( pokemon, "Sandstorm" ) )
                {
                    sandSetter++;
                    tc.StrategyScores[TeamStrategy.Sand]++;
                }

                if( pokemon.Ability == AbilityID.SnowWarning || _ai.UnitSim.CheckHasMove( pokemon, "Snowscape" ) )
                {
                    snowSetter++;
                    tc.StrategyScores[TeamStrategy.Snow]++;
                }
            }

            bool sunBen = _ai.UnitSim.Get_WeatherContextScore( pokemon.Pokemon, WeatherConditionID.SUNNY ) > 0;
            bool rainBen = _ai.UnitSim.Get_WeatherContextScore( pokemon.Pokemon, WeatherConditionID.RAIN ) > 0;
            bool sandBen = _ai.UnitSim.Get_WeatherContextScore( pokemon.Pokemon, WeatherConditionID.SANDSTORM ) > 0;
            bool snowBen = _ai.UnitSim.Get_WeatherContextScore( pokemon.Pokemon, WeatherConditionID.SNOW ) > 0;

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
        int ourWinConScore = int.MinValue;
        int theirWinConScore = int.MinValue;

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
        var ourWinConAdapter = _ai.GetPokemonAs_Adapter( gp.OurPrimaryWinCon );
        var theirWinConAdapter = _ai.GetPokemonAs_Adapter( gp.TheirPrimaryWinCon );

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
            bool weSporePowder = weSleep && ( _ai.UnitSim.CheckHasMove( ourMon, "Sleep Powder" ) || _ai.UnitSim.CheckHasMove( ourMon, "Spore" ) );
            bool weParaPowder = weParalyze && _ai.UnitSim.CheckHasMove( ourMon, "Stun Spore" );
            bool weTWave = weParalyze && _ai.UnitSim.CheckHasMove( ourMon, "Thunder Wave" );
            bool wePowder = weSporePowder || weParaPowder || _ai.UnitSim.CheckHasMove( ourMon, "Poison Powder" ) || _ai.UnitSim.CheckHasMove( ourMon, "Rage Powder" );

            bool weLockdown = weSleep || weParalyze || weTaunt || weEncore || weFakeOut;

            foreach( var theirMon in theirTeam )
            {
                int advantageScore = 0;
                int dangerScore = 0;

                var ee = _ai.Projection.EvaluateExchange( ourMon, theirMon );

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
        // Debug.LogError( $"WinCon: {ourWinCon?.NickName}" );
        BattleAI_PokemonAdapter winConAdapter = _ai.GetPokemonAs_Adapter( ourWinCon );
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

            var ee = _ai.Projection.EvaluateExchange( winConAdapter, mon );
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
            bool theySporePowder = theySleep && ( _ai.UnitSim.CheckHasMove( mon, "Sleep Powder" ) || _ai.UnitSim.CheckHasMove( mon, "Spore" ) );
            bool theyParaPowder = theyParalyze && _ai.UnitSim.CheckHasMove( mon, "Stun Spore" );
            bool theyTWave = theyParalyze && _ai.UnitSim.CheckHasMove( mon, "Thunder Wave" );
            bool theyPowder = theySporePowder || theyParaPowder || _ai.UnitSim.CheckHasMove( mon, "Poison Powder" ) || _ai.UnitSim.CheckHasMove( mon, "Rage Powder" );

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

            if( theirRP.Traits.Contains( RoleTrait.HazardSetter ) && _ai.UnitSim.CheckHasMove( mon, "Stealth Rock" ) )
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
        BattleAI_PokemonAdapter winConAdapter = _ai.GetPokemonAs_Adapter( ourWinCon );
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
            bool weSporePowder = weSleep && ( _ai.UnitSim.CheckHasMove( mon, "Sleep Powder" ) || _ai.UnitSim.CheckHasMove( mon, "Spore" ) );
            bool weParaPowder = weParalyze && _ai.UnitSim.CheckHasMove( mon, "Stun Spore" );
            bool weTWave = weParalyze && _ai.UnitSim.CheckHasMove( mon, "Thunder Wave" );
            bool wePowder = weSporePowder || weParaPowder || _ai.UnitSim.CheckHasMove( mon, "Poison Powder" ) || _ai.UnitSim.CheckHasMove( mon, "Rage Powder" );

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
                var ee = _ai.Projection.EvaluateExchange( mon, _ai.GetPokemonAs_Adapter( blocker.Key ) );
                var enablerPTKO = ee.AttackerPTKOR.PTKO;
                var blockerPTKO = ee.OpponentPTKOR.PTKO;
                var blockerAdapter = _ai.GetPokemonAs_Adapter( blocker.Key );

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

            if( _ai.UnitSim.CheckHasMove( mon, "Wish" ) )
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

    public void UpdateTeamPieceValues()
    {
        var ourTeam = OurTeamPokemon.Where( p => p.CurrentHP > 0 ).ToList();
        var theirTeam = TheirTeamPokemon.Where( p => p.CurrentHP > 0 ).ToList();
        
        RefreshTeamPieceValues( ourTeam, theirTeam );
    }

    public void RefreshTeamPieceValues( List<Pokemon> ourTeam, List<Pokemon> theirTeam )
    {
        List<IBattleAIUnit> ourTeamAIUnits = new();
        
        for( int i = 0; i < ourTeam.Count; i++ )
        {
            BattleAI_PokemonAdapter mon = _ai.GetPokemonAs_Adapter( ourTeam[i] );
            ourTeamAIUnits.Add( mon );
        }

        OurTeamPieceValues = CalculateTeamPieceValues( ourTeamAIUnits );

        List<IBattleAIUnit> theirTeamAIUnits = new();
        
        for( int i = 0; i < theirTeam.Count; i++ )
        {
            BattleAI_PokemonAdapter mon = _ai.GetPokemonAs_Adapter( theirTeam[i] );
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
        var speedTiers = PV_GetRankBonuses( team, mon => _ai.GetUnitContextualSpeed( mon ) );

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
        var oppTeam = _ai.BattleSystem.GetOpposingParty( pokemon.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
        int score = 50;

        score += attackingRanks[pokemon];
        score += speedRanks[pokemon];

        //--PTKO Stuff here
        int threatCount = 0;
        int spreadPressure = 0;
        for( int i = 0; i < oppTeam.Count; i++ )
        {
            BattleAI_PokemonAdapter opp = _ai.GetPokemonAs_Adapter( oppTeam[i] );
            var ptko = _ai.Projection.Get_NeutralPTKO( pokemon, opp );
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
}

public struct UnitTracker
{
    //--This Slot Tracking
    public Pokemon CurrentPokemon;
    public Pokemon PreviousPokemon;
    public ActionEvaluation LastAction;
    public int SwitchAmount;
    public int SetupAmount;

    //--This Slot's Opponent Tracking
    public BattleUnit LastTargetSlot;
    public Pokemon LastTarget;
    public List<Pokemon> LastOpponents;
}
