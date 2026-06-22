using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BattleAI_RoleDetection
{
    private BattleAI _ai;
    private BattleAI_UnitSim _us;

    public BattleAI_RoleDetection( BattleAI ai )
    {
        _ai = ai;
        _us = _ai.UnitSim;
    }

    public RoleProfile GetPokemonRole( BattleAI_PokemonAdapter adapter )
    {
        RoleSignals rs = new();

        foreach( var move in adapter.ActiveMoves )
        {
            AnalyzeMoveSignals( adapter, move, ref rs );
        }

        AnalyzeStatSignals( adapter, ref rs );
        AnalyzeItemSignals( adapter, ref rs );
        AnalyzeAbilitySignals( adapter, ref rs );
        AnalyzeMoveSetSignals( adapter, ref rs );

        return ResolveRoleSignals( adapter, ref rs );;
    }

    //--PureOffense, OffensiveHybrid, OffensiveUtility, SupportiveUtility, Setup, Hazard, Pivot, Disruption, Recovery, BattlefieldControl,
    private void AnalyzeMoveSignals( BattleAI_PokemonAdapter adapter, Move move, ref RoleSignals rs )
    {
        var moveSO = move.MoveSO;
        var cat = move.MoveSO.MoveCategory;
        var moveEffects = move.MoveSO.MoveEffects;
        var statChanges = moveEffects.StatChangeList;
        int speed = adapter.Pokemon.PokeSO.Speed;
        HashSet<MoveRoleFlag> flags = new();

        //----------------------------------------------------------------------------
        //------------------------Pure Offense & Offensive Hybrid---------------------
        //----------------------------------------------------------------------------
        //--All non-status moves are offensive moves that do damage. if a move has guaranteed stat drops, or does set damage, or is fake out (fake out might be better considered as disruption. flinch is disruptive.)
        //--those are offensive utility. Moves like snarl, seismic toss, and super fang come to mind here. 
        if( cat != MoveCategory.Status )
        {
            if( moveSO.Name == "Fake Out" )
                flags.Add( MoveRoleFlag.OffensiveHybrid );

            if( statChanges != null && statChanges.Count > 0 )
            {
                if( moveEffects.Target == EffectTarget.Enemy || moveEffects.Target == EffectTarget.OpposingSide )
                    flags.Add( MoveRoleFlag.OffensiveHybrid );

                foreach( var sv in statChanges )
                {
                    if( sv.Stat == Stat.Speed && sv.Change < 0 )
                        flags.Add( MoveRoleFlag.Disruption );
                }
            }

            if( moveSO.SetDamageType != SetDamageType.None )
                flags.Add( MoveRoleFlag.OffensiveHybrid );

            flags.Add( MoveRoleFlag.PureOffense );
        }
        else
        {
            //--------------------------------------------------------------
            //-----------------------Offensive Utility----------------------
            //--------------------------------------------------------------
            //--These will be moves that either reduce opponent's stats or apply a status effect of some kind without doing damage.
            bool moveIsDebuff = _us.MoveIsDebuff( move );
            if( moveIsDebuff )
                flags.Add( MoveRoleFlag.OffensiveUtility );

            bool moveAppliesStatus = _us.MoveAppliesStatus( move );
            if( moveAppliesStatus )
                flags.Add( MoveRoleFlag.OffensiveUtility );

            //--------------------------------------------------------------
            //----------------------Supportive Utility----------------------
            //--------------------------------------------------------------
            //--These will be moves that provide a supportive role, such as redirection (maybe this gets its own category later), screens, or ally-side buffs like howl. moves like aromatherapy and wish belong here too.
            bool moveIsSupport = _us.MoveIsSupport( move );
            if( moveIsSupport )
                flags.Add( MoveRoleFlag.SupportiveUtility );

            //--------------------------------------------------------------
            //----------------------------Setup-----------------------------
            //--------------------------------------------------------------
            bool moveIsSetup = _us.MoveIsSetup( move );
            if( moveIsSetup )
                flags.Add( MoveRoleFlag.Setup );

            //--------------------------------------------------------------
            //----------------------------Hazard----------------------------
            //--------------------------------------------------------------
            bool moveIsHazard = _us.MoveIsEntryHazard( move );
            if( moveIsHazard )
                flags.Add( MoveRoleFlag.Hazard );

            //--------------------------------------------------------------
            //----------------------------Pivot-----------------------------
            //--------------------------------------------------------------
            bool moveIsPivot = _us.MoveIsPivot( move );
            if( moveIsPivot )
                flags.Add( MoveRoleFlag.Pivot );

            //--------------------------------------------------------------
            //--------------------------Disruption--------------------------
            //--------------------------------------------------------------
            bool moveIsPhaze = _us.MoveIsPhaze( move );
            if( moveIsPhaze || moveEffects.VolatileStatus == VolatileConditionID.Taunt || moveEffects.VolatileStatus == VolatileConditionID.Encore )
                flags.Add( MoveRoleFlag.Disruption );

            if( moveEffects.SevereStatus == SevereConditionID.PAR )
                flags.Add( MoveRoleFlag.Disruption );

            //--------------------------------------------------------------
            //---------------------------Recovery---------------------------
            //--------------------------------------------------------------
            bool moveIsSelfHeal = _us.MoveIsSelfHeal( move );
            if( moveIsSelfHeal )
                flags.Add( MoveRoleFlag.Recovery );

            //--------------------------------------------------------------
            //----------------------Battlefield Control---------------------
            //--------------------------------------------------------------
            bool moveIsBattlefieldControl = _us.MoveIsBattlefieldControl( move );
            if( moveIsBattlefieldControl )
                flags.Add( MoveRoleFlag.BattlefieldControl );
        }

        //--------------------------------------------------------------
        //----------------------Role Signal Scoring---------------------
        //--------------------------------------------------------------

        int movePower = move.MovePower * _us.Get_ExpectedMoveHits( move );
        int movePowerWeight =
            movePower >= 100 ? 20 :
            movePower >= 75 ? 15 :
            movePower >= 40 ? 10 : 5;

        switch( cat )
        {
            case MoveCategory.Physical: rs.PhysicalAttackCount++; break;
            case MoveCategory.Special: rs.SpecialAttackCount++; break;
            case MoveCategory.Status: rs.StatusMoveCount++; break;
        }

        foreach( var flag in flags )
        {
            switch( flag )
            {
                case MoveRoleFlag.PureOffense:
                    if( cat == MoveCategory.Physical )
                        rs.PhysicalOffense += movePowerWeight;
                    else if( cat == MoveCategory.Special )
                        rs.SpecialOffense += movePowerWeight;

                    if( movePower >= 100 )
                    {
                        rs.BurstDamage += 10;

                        if( speed <= 60 )
                            rs.TrickRoomBenefits += 15;
                        if( speed <= 70 )
                            rs.TrickRoomBenefits += 10;
                        if( speed <= 80 )
                            rs.TrickRoomBenefits += 5;
                    }

                    rs.BurstDamage += movePowerWeight / 2;
                    rs.SustainedDamage += movePowerWeight / 3;

                    if( _us.MoveHasDrawback( move ) )
                        rs.BurstDamage += 10;

                    if( statChanges != null & statChanges.Count > 0 )
                    {
                        foreach( var sc in statChanges )
                        {
                            if( sc.Change > 0 )
                            {
                                rs.SetupPressure += 5;
                                if( sc.Stat == Stat.Speed )
                                    rs.TrickRoomBenefits -= 10;
                            }

                            if( sc.Change < 0 && sc.Stat == Stat.Speed )
                                rs.TrickRoomBenefits += 10;
                        }
                    }

                    if( move.MoveTarget == MoveTarget.OpposingSide || move.MoveTarget == MoveTarget.AllAdjacent )
                    {
                        rs.SpreadDamagePressure += 20;
                        rs.BurstDamage += 5;

                        if( speed <= 60 )
                            rs.TrickRoomBenefits += 10;
                        if( speed <= 70 )
                            rs.TrickRoomBenefits += 5;
                    }
                break;

                case MoveRoleFlag.OffensiveHybrid:
                    if( cat == MoveCategory.Physical )
                        rs.PhysicalOffense += 10;
                    else if( cat == MoveCategory.Special )
                        rs.SpecialOffense += 10;

                    rs.OffensiveUtility += 15;
                    rs.Disruption += 5;

                    if( moveSO.Name == "Fake Out" )
                    {
                        rs.RevengeKilling += 10;
                        rs.Disruption += 15;
                    }
                break;

                case MoveRoleFlag.OffensiveUtility:
                    rs.OffensiveUtility += 20;
                    rs.PassivePressure += 10;
                    rs.Disruption += 5;
                break;

                case MoveRoleFlag.SupportiveUtility:
                    rs.SupportUtility += 30;
                    rs.TeamSupport += 20;
                    rs.SelfSustain += 5;
                break;

                case MoveRoleFlag.Setup:
                    rs.SetupMoveCount++;

                    foreach( var sc in statChanges )
                    {
                        if( sc.Change > 0 )
                        {
                            bool big = sc.Change > 1;
                            rs.SetupPressure += big ? 30 : 20;

                            if( sc.Stat == Stat.Attack )
                            {
                                rs.PhysicalSetup += big ? 10 : 5;
                                rs.PhysicalOffense += big ? 10 : 5;
                                rs.OffensiveSetupPressure += big ? 15 : 10;
                            }

                            if( sc.Stat == Stat.SpAttack )
                            {
                                rs.SpecialSetup += big ? 10 : 5;
                                rs.SpecialOffense += big ? 10 : 5;
                                rs.OffensiveSetupPressure += big ? 15 : 10;
                            }

                            if( sc.Stat == Stat.Defense )
                            {
                                rs.PhysicalSetup += big ? 10 : 5;
                                rs.PhysicalBulk += big ? 10 : 5;
                                rs.DefensiveSetupPressure += big ? 15 : 10;
                            }

                            if( sc.Stat == Stat.SpDefense )
                            {
                                rs.SpecialSetup += big ? 10 : 5;
                                rs.SpecialBulk += big ? 10 : 5;
                                rs.DefensiveSetupPressure += big ? 15 : 10;
                            }

                            if( sc.Stat == Stat.Speed )
                            {
                                rs.SpeedControl += big ? 10 : 5;
                                rs.SpeedPressure += big ? 15 : 10;
                                rs.TrickRoomBenefits -= big ? 20 : 15;
                            }
                        }
                        else if( sc.Change < 0 && sc.Stat == Stat.Speed )
                            rs.TrickRoomBenefits += 5;
                    }
                break;

                case MoveRoleFlag.Hazard:
                    rs.HazardPressure += 20;
                    rs.PassivePressure += 10;

                    if( moveSO.Name == "Sticky Web" )
                        rs.SpeedControl += 10;

                    if( moveSO.Name == "Toxic Spikes" )
                        rs.OffensiveUtility += 10;

                    if( moveSO.Name == "Stealth Rock" )
                        rs.PassivePressure += 5;

                    if( moveSO.Name == "Leech Seed" )
                        rs.SelfSustain += 10;
                break;

                case MoveRoleFlag.Pivot:
                    rs.Pivoting += 15;
                    rs.SpeedPressure += 5;
                break;

                case MoveRoleFlag.Disruption:
                    rs.Disruption += 20;
                    rs.SupportUtility += 10;

                    if( moveEffects.VolatileStatus == VolatileConditionID.Encore )
                        rs.SetupPressure += 5;
                break;

                case MoveRoleFlag.Recovery:
                    rs.SelfSustain += 20;
                    rs.PhysicalBulk += 5;
                    rs.SpecialBulk += 5;
                break;

                case MoveRoleFlag.BattlefieldControl:
                    rs.BattlefieldControl += 15;

                    if( moveEffects.CourtCondition == CourtConditionID.Tailwind )
                    {
                        rs.SpeedControl += 15;
                        rs.TeamSupport += 10;
                    }

                    if( moveEffects.FieldCondition == FieldConditionID.TrickRoom )
                    {
                        rs.SpeedControl += 25;
                        rs.TeamSupport += 5;
                        rs.Disruption += 10;
                    }

                    if( moveEffects.Terrain != TerrainID.None )
                    {
                        rs.BattlefieldControl += 10;
                        rs.SupportUtility += 10;
                    }
                break;
            }
        }
    }

    private void AnalyzeStatSignals( BattleAI_PokemonAdapter adapter, ref RoleSignals rs )
    {
        //--------------------------------------------------------------
        //------------------Stat Information Gathering------------------
        //--------------------------------------------------------------

        int hp = adapter.Pokemon.PokeSO.MaxHP;
        int atk = adapter.Pokemon.PokeSO.Attack;
        int def = adapter.Pokemon.PokeSO.Defense;
        int spatk = adapter.Pokemon.PokeSO.SpAttack;
        int spdef = adapter.Pokemon.PokeSO.SpDefense;
        int spe = adapter.Pokemon.PokeSO.Speed;


        int offensiveStat = Mathf.Max( atk, spatk );
        int offensiveDifference = Mathf.Abs( atk - spatk );
        int offensivePressure = offensiveStat + ( spe / 2 );

        int physicalBulk = hp * def;
        int specialBulk = hp * spdef;

        List<int> stats = new() { hp, atk, def, spatk, spdef, spe };

        int highestStat = stats.Max();
        int lowestStat = stats.Min();

        int statSpread = highestStat - lowestStat;

        //--------------------------------------------------------------
        //---------------------Relative Comparisons---------------------
        //--------------------------------------------------------------

        //--Stat spread
        if( statSpread <= 40 )
            rs.HybridRoleLeaning = true;

        // //--Attacking Stats
        if( atk - spatk >= 20 )
        {
            rs.PhysicalOffense += 5;
            rs.PhysicalOffenseLeaning = true;
        }
        else if( spatk - atk >= 20 )
        {
            rs.SpecialOffense += 5;
            rs.SpecialOffenseLeaning = true;
        }
        else if( offensiveDifference <= 15 )
        {
            rs.PhysicalOffense += 5;
            rs.SpecialOffense += 5;
            rs.MixedOffenseLeaning = true;
        }

        // //--Defensive Stats
        if( physicalBulk > specialBulk * 1.2f )
        {
            rs.PhysicalBulk += 5;
            rs.PhysicalDefenseLeaning = true;
        }
        else if( specialBulk > physicalBulk * 1.2f )
        {
            rs.SpecialBulk += 5;
            rs.SpecialDefenseLeaning = true;
        }

        //--------------------------------------------------------------
        //----------------------Role Contributions----------------------
        //--------------------------------------------------------------

        //--Offensive Base Stat Thresholds
        //  135+	elite
        //  115–134	very strong
        //  95–114	strong
        //  75–94	moderate
        //  < 75	weak

        //--Attack
        if( atk >= 135 )
            rs.PhysicalOffense += 20;
        else if( atk >= 115 )
            rs.PhysicalOffense += 15;
        else if( atk >= 95 )
            rs.PhysicalOffense += 10;
        else if( atk >= 75 )
            rs.PhysicalOffense += 5;
        else if( atk < 75 )
            rs.PhysicalOffense -= 5;

        //--Special Attack
        if( spatk >= 135 )
            rs.SpecialOffense += 20;
        else if( spatk >= 115 )
            rs.SpecialOffense += 15;
        else if( spatk >= 95 )
            rs.SpecialOffense += 10;
        else if( spatk >= 75 )
            rs.SpecialOffense += 5;
        else if( spatk < 75 )
            rs.SpecialOffense -= 5;

        //--Speed Base Stat Thresholds
        //  120+	elite
        //  110–119	fast
        //  100-109	respectable
        //  90-99	middling
        //  70-89	awkward
        //  60-69   slow
        //  < 60    trick room
        
        //--Speed
        if( spe >= 135 )
        {
            rs.SpeedPressure += 35;
            rs.RevengeKilling += 25;
            rs.TrickRoomBenefits -= 50;
        }
        else if( spe >= 120 )
        {
            rs.SpeedPressure += 30;
            rs.RevengeKilling += 20;
            rs.TrickRoomBenefits -= 45;
        }
        else if( spe >= 110 )
        {
            rs.SpeedPressure += 25;
            rs.RevengeKilling += 15;
            rs.TrickRoomBenefits -= 40;
        }
        else if( spe >= 100 )
        {
            rs.SpeedPressure += 20;
            rs.RevengeKilling += 10;
            rs.TrickRoomBenefits -= 35;
        }
        else if( spe >= 90 )
        {
            rs.SpeedPressure += 10;
            rs.TrickRoomBenefits -= 30;
        }
        else if( spe >= 70 )
        {
            rs.SpeedPressure += 0;
        }
        else if( spe >= 50 )
        {
            rs.SpeedPressure -= 15;
            rs.TrickRoomBenefits += 10;
        }
        else
        {
            rs.SpeedPressure -= 25;
            rs.TrickRoomBenefits += 15;
        }

        //--Defenses & HP

        //--Physical Bulk
        if( physicalBulk > offensivePressure * 1.25f )
            rs.PhysicalBulk += 25;

        //--Special Bulk
        if( specialBulk > offensivePressure * 1.25f )
            rs.SpecialBulk += 25;

        //--HP Stat Thresholds
        //  125+    gargantuan --we don't want big hp personalities like chansey and blissey to be lumped in with 105 base hp mons, they're not even close to the same class. even 125+ may not be appropriate, but we can handle special cases in the future if necessary.
        //  105-124	massive
        //  85–104	high
        //  65–84	moderate
        //  < 65	low

        //--HP
        if( hp >= 125 )
        {
            if( def > spdef )
                rs.PhysicalBulk += 10;
            else if( spdef > def )
                rs.SpecialBulk += 10;
        }
        else if( hp >= 90 )
        {
            if( def > spdef )
                rs.PhysicalBulk += 5;
            else if( spdef > def )
                rs.SpecialBulk += 5;
        }

        //--Defensive Base Stat Thresholds
        //  > 120   insurmountable
        //  110-120	elite
        //  90–109	bulky
        //  70–89	respectable
        //  < 70	fragile

        //--Defense
        if( def >= 120 )
            rs.PhysicalBulk += 10;
        else if( def > 90 )
            rs.PhysicalBulk += 5;
        else if( def < 65 )
            rs.PhysicalBulk -= 5;

        //--Special Defense
        if( spdef >= 120 )
            rs.SpecialBulk += 10;
        else if( spdef > 90 )
            rs.SpecialBulk += 5;
        else if( spdef < 65 )
            rs.SpecialBulk -= 5;
    }

    private void AnalyzeItemSignals( BattleAI_PokemonAdapter adapter, ref RoleSignals rs )
    {
        var item = adapter.Item;
        
        if( item == BattleItemEffectID.None )
            return;

        if( item == BattleItemEffectID.ChoiceBand )
        {
            rs.PhysicalOffense += 25;
            rs.BurstDamage += 20;
            rs.Wallbreaking += 15;
        }

        if( item == BattleItemEffectID.ChoiceSpecs )
        {
            rs.SpecialOffense += 25;
            rs.BurstDamage += 20;
            rs.Wallbreaking += 15;
        }

        if( item == BattleItemEffectID.ChoiceScarf )
        {
            rs.SpeedPressure += 25;
            rs.RevengeKilling += 15;
            rs.BurstDamage += 5;
        }

        if( item == BattleItemEffectID.LifeOrb )
        {
            rs.BurstDamage += 15;
            rs.SustainedDamage += 10;

            rs.PhysicalOffense += 10;
            rs.SpecialOffense += 10;
        }

        if( item == BattleItemEffectID.ExpertBelt )
        {
            rs.BurstDamage += 10;
            rs.SustainedDamage += 5;

            rs.PhysicalOffense += 5;
            rs.SpecialOffense += 5;
        }

        if( item == BattleItemEffectID.FocusSash )
        {
            rs.RevengeKilling += 10;
            rs.SetupPressure += 15;
            rs.SpeedPressure += 5;
            rs.Disruption += 15;
        }

        if( item == BattleItemEffectID.SitrusBerry )
        {
            rs.SelfSustain += 25;
            rs.PhysicalBulk += 15;
            rs.SpecialBulk += 15;
            rs.PassivePressure += 5;
            rs.SupportUtility += 5;
            rs.DamageAbsorbing += 15;
        }

        if( item == BattleItemEffectID.Leftovers )
        {
            rs.SelfSustain += 20;
            rs.PhysicalBulk += 10;
            rs.SpecialBulk += 10;
            rs.PassivePressure += 5;
            rs.DamageAbsorbing += 5;
        }

        if( item == BattleItemEffectID.BlackSludge )
        {
            rs.SelfSustain += 20;
            rs.PhysicalBulk += 10;
            rs.SpecialBulk += 10;
            rs.PassivePressure += 5;
            rs.TeamSupport += 5;
            rs.SupportUtility += 5;
            rs.DamageAbsorbing += 5;
        }

        if( item == BattleItemEffectID.RockyHelmet )
        {
            rs.PhysicalBulk += 10;
            rs.PassivePressure += 15;
            rs.Disruption += 10;
            rs.DamageAbsorbing += 5;
        }

        if( item == BattleItemEffectID.LightClay )
        {
            rs.SupportUtility += 25;
            rs.TeamSupport += 20;
        }

        if( item == BattleItemEffectID.HeatRock || item == BattleItemEffectID.DampRock || item == BattleItemEffectID.IcyRock || item == BattleItemEffectID.SmoothRock )
        {
            rs.BattlefieldControl += 25;
            rs.TeamSupport += 15;
            rs.SetupPressure += 5;
        }

        if( item == BattleItemEffectID.FlameOrb || item == BattleItemEffectID.ToxicOrb || item == BattleItemEffectID.StaticOrb )
        {
            var ability = adapter.Ability;

            if( ability == AbilityID.Guts )
            {
                rs.PhysicalOffense += 30;
                rs.BurstDamage += 25;
                rs.Wallbreaking += 20;
            }

            if( ability == AbilityID.MarvelScale )
            {
                rs.PhysicalBulk += 30;
                rs.DamageAbsorbing += 10;
                rs.SupportUtility += 10;
            }

            if( ability == AbilityID.QuickFeet )
            {
                rs.SpeedPressure += 25;
                rs.BurstDamage += 5;
                rs.OffensiveUtility += 5;

                if( rs.PhysicalOffense > rs.SpecialOffense )
                    rs.PhysicalOffense += 10;
                else if( rs.SpecialOffense > rs.PhysicalOffense )
                    rs.SpecialOffense += 10;
            }

            if( ability == AbilityID.PoisonHeal && item == BattleItemEffectID.ToxicOrb )
            {
                rs.SelfSustain += 25;
                rs.OffensiveUtility += 5;
                rs.PhysicalBulk += 10;
                rs.SpecialBulk += 10;
                rs.DamageAbsorbing += 5;
                rs.PassivePressure += 5;
            }
        }

        if( item == BattleItemEffectID.HeavyDutyBoots )
        {
            rs.Pivoting += 15;
            rs.SustainedDamage += 5;
            rs.SelfSustain += 5;
        }

        if( item == BattleItemEffectID.AirBalloon )
        {
            rs.Pivoting += 5;
            rs.SetupPressure += 5;
            rs.SelfSustain += 5;
            rs.SustainedDamage += 5;
            rs.TeamSupport += 5;
        }

        if( item == BattleItemEffectID.Charcoal || item == BattleItemEffectID.MysticWater || item == BattleItemEffectID.MetalCoat )
        {
            rs.SustainedDamage += 5;
            
            if( rs.PhysicalOffenseLeaning )
                rs.PhysicalOffense += 10;
            
            if( rs.SpecialOffenseLeaning )
                rs.SpecialOffense += 10;
        }
    }

    private void AnalyzeAbilitySignals( BattleAI_PokemonAdapter adapter, ref RoleSignals rs )
    {
        var ability = adapter.Ability;

        //----------------------------------
        //------Offensive Amplication-------
        //----------------------------------
        if( _us.PokemonHasAbility_OffensiveAmplification( adapter.Pokemon ) )
        {
            rs.BurstDamage += 15;
            rs.SustainedDamage += 10;

            if( rs.PhysicalOffenseLeaning )
                rs.PhysicalOffense += 10;

            if( rs.SpecialOffenseLeaning )
                rs.SpecialOffense += 10;
        }

        //----------------------------------
        //---Tempo & Speed Manipulation-----
        //----------------------------------
        if( _us.PokemonHasAbility_SpeedManipulation( adapter.Pokemon ) )
        {
            rs.SpeedPressure += 15;
            rs.RevengeKilling += 10;
        }

        //----------------------------------
        //--------Defensive Sustain---------
        //----------------------------------
        if( _us.PokemonHasAbility_DefensiveSustain( adapter.Pokemon ) )
        {
            rs.PhysicalBulk += 10;
            rs.SpecialBulk += 10;
            rs.SelfSustain += 10;

            if( ability == AbilityID.Levitate )
            {
                rs.Pivoting += 10;
                rs.SelfSustain += 5;
                rs.DamageAbsorbing += 5;
            }
        }

        //----------------------------------
        //-------Utility & Disruption-------
        //----------------------------------
        if( _us.PokemonHasAbility_UtilityOrDisruption( adapter.Pokemon ) )
        {
            rs.SupportUtility += 5;
            rs.TeamSupport += 5;

            if( ability == AbilityID.Intimidate )
            {
                rs.SupportUtility += 20;
                rs.Disruption += 15;

                rs.PhysicalBulk += 10;
                rs.TeamSupport += 10;
                rs.DamageAbsorbing += 10;
            }

            if( ability == AbilityID.Demoralize  )
            {
                rs.SupportUtility += 20;
                rs.Disruption += 15;

                rs.SpecialBulk += 10;
                rs.TeamSupport += 10;
                rs.DamageAbsorbing += 10;
            }

            if( ability == AbilityID.Prankster )
            {
                rs.SupportUtility += 20;
                rs.Disruption += 20;
                rs.SpeedControl += 15;
                rs.SetupPressure += 5;
            }

            if( ability == AbilityID.MagicBounce )
            {
                rs.Disruption += 15;
                rs.SupportUtility += 10;
                rs.TeamSupport += 10;
            }

            if( ability == AbilityID.ArenaTrap )
            {
                rs.Disruption += 25;
                rs.Wallbreaking += 10;
                rs.RevengeKilling += 10;
            }

            if( ability == AbilityID.StormDrain || ability == AbilityID.LightningRod || ability == AbilityID.Triage || ability == AbilityID.LeafGuard || ability == AbilityID.Healer || ability == AbilityID.FriendGuard )
            {
                rs.SupportUtility += 15;
                rs.TeamSupport += 10;
                rs.DamageAbsorbing += 15;
                rs.PhysicalBulk += 5;
                rs.SpecialBulk += 5;
            }
            
            if( ability == AbilityID.Sandstream )
            {
                rs.SupportUtility += 5;
                rs.TeamSupport += 5;
                rs.PassivePressure += 15;

                if( _us.CheckTypes( PokemonType.Rock, adapter ) || _us.CheckTypes( PokemonType.Ground, adapter ) || _us.CheckTypes( PokemonType.Steel, adapter ) )
                {
                    rs.SpecialBulk += 10;
                    rs.DamageAbsorbing += 5;
                }
            }
        }

        //----------------------------------
        //----------Team Archetype----------
        //----------------------------------
        if( _us.PokemonHasAbility_TeamArchetype( adapter.Pokemon ) )
        {
            rs.BattlefieldControl += 20;
            rs.TeamSupport += 20;
            rs.SetupPressure += 10;

            if( _us.PokemonHasWeatherSetter_Ability( adapter.Pokemon ) )
                rs.BattlefieldControl += 10;
        }

        //----------------------------------
        //-------Punishment Abilities-------
        //----------------------------------
        if( _us.PokemonHasAbility_Punishment( adapter.Pokemon ) )
        {
            rs.PassivePressure += 15;
            rs.Disruption += 10;
            rs.DamageAbsorbing += 10;
        }

        //----------------------------------
        //------Counterplay/Anti-Meta-------
        //----------------------------------
        if( _us.PokemonHasAbility_CounterPlay( adapter.Pokemon ) )
        {
            rs.Disruption += 5;
            rs.SupportUtility += 5;

            if( ability == AbilityID.Defiant || ability == AbilityID.Competitive )
                rs.SetupPressure += 10;
        }
    }

    private void AnalyzeMoveSetSignals( BattleAI_PokemonAdapter adapter, ref RoleSignals rs )
    {
        if( rs.PhysicalAttackCount >= 3 )
            rs.PhysicalOffense += 10;

        if( rs.SpecialAttackCount >= 3 )
            rs.SpecialOffense += 10;

        if( ( rs.PhysicalAttackCount >= 2 && rs.SpecialAttackCount >= 1 ) || ( rs.SpecialAttackCount >= 2 && rs.PhysicalAttackCount >= 1 ) )
            rs.MixedOffenseLeaning = true;
    }

    private RoleProfile ResolveRoleSignals( BattleAI_PokemonAdapter adapter, ref RoleSignals rs )
    {
        RoleProfile rp = new()
        {
            SecondaryRoles = new(),
            Biases = new(),
            Traits = new(),
            RoleScores = new(),
            Signals = rs,
        };

        //-----Role Classes------
        var roleScores = AssignRoleClasses( adapter, ref rs, ref rp );
        var sortedroles = roleScores.OrderByDescending( rs => rs.Value ).ToList();
        rp.RoleScores = roleScores;

        rp.PrimaryRole = sortedroles[0].Key;
        int primaryScore = sortedroles[0].Value;

        foreach( var role in sortedroles.Skip( 1 ) )
        {
            if( primaryScore - role.Value >= 15 )
            {
                rp.SecondaryRoles.Add( role.Key );
            }
        }

        //------Role Biases------
        AssignRoleBiases( adapter, ref rs, ref rp );

        
        //------Role Traits------
        AssignRoleTraits( adapter, ref rs, ref rp );

        //------Lead Bias-------
        // Will depend on things like hazard presence, taunt, sash, speed, pivot, suicide traits, disruption. need traits assigned before lead can be assigned.

        //--Logging
        // CustomLogSession roleLog = new();

        int baseHP = adapter.Pokemon.PokeSO.MaxHP;
        int baseAttack = adapter.Pokemon.PokeSO.Attack;
        int baseDefense = adapter.Pokemon.PokeSO.Defense;
        int baseSpAttack = adapter.Pokemon.PokeSO.SpAttack;
        int baseSpDefense = adapter.Pokemon.PokeSO.SpDefense;
        int baseSpeed = adapter.Pokemon.PokeSO.Speed;

        string move1 = adapter.ActiveMoves[0] != null ? adapter.ActiveMoves[0].MoveSO.Name : "NONE";
        string move2 = adapter.ActiveMoves[1] != null ? adapter.ActiveMoves[1].MoveSO.Name : "NONE";
        string move3 = adapter.ActiveMoves[2] != null ? adapter.ActiveMoves[2].MoveSO.Name : "NONE";
        string move4 = adapter.ActiveMoves[3] != null ? adapter.ActiveMoves[3].MoveSO.Name : "NONE";

        // roleLog.Add( $"=========================================" );
        // roleLog.Add( $"====[Begin Role Log ({adapter.Name})]====" );
        // roleLog.Add( $"=========================================" );
        // roleLog.Add( $"[Base Stats] HP: {baseHP}, Atk: {baseAttack}, Def: {baseDefense}, SpAtk: {baseSpAttack}, SpDef: {baseSpDefense}, Spe: {baseSpeed}" );
        // roleLog.Add( $"[Meta Data] Moves: 1. {move1}, 2. {move2}, 3. {move3}, 4. {move4} | Ability: {adapter.Ability} | Item: {adapter.Item}" );
        // roleLog.Add( $"" );
        // roleLog.Add( $"=====================" );
        // roleLog.Add( $"===[Role Signals]===" );
        // roleLog.Add( $"=====================" );
        // roleLog.Add( $"" );
        // roleLog.Add( $"=[Offense]=" );
        // roleLog.Add( $"Physical Offense: {rs.PhysicalOffense}" );
        // roleLog.Add( $"Special Offense: {rs.SpecialOffense}" );
        // roleLog.Add( $"Physical Offense Leaning: {rs.PhysicalOffenseLeaning}" );
        // roleLog.Add( $"Special Offense Leaning: {rs.SpecialOffenseLeaning}" );
        // roleLog.Add( $"Mixed Offense Leaning: {rs.MixedOffenseLeaning}" );
        // roleLog.Add( $"" );
        // roleLog.Add( $"Burst Damage: {rs.BurstDamage}" );
        // roleLog.Add( $"Sustained Damage: {rs.SustainedDamage}" );
        // roleLog.Add( $"Passive Pressure: {rs.PassivePressure}" );
        // roleLog.Add( $"Spread Damage Pressure: {rs.SpreadDamagePressure}" );
        // roleLog.Add( $"" );
        // roleLog.Add( $"=[Bulk]=" );
        // roleLog.Add( $"Physical Bulk: {rs.PhysicalBulk}" );
        // roleLog.Add( $"Special Bulk: {rs.SpecialBulk}" );
        // roleLog.Add( $"Physical Defense Leaning: {rs.PhysicalDefenseLeaning}" );
        // roleLog.Add( $"Special Defense Leaning: {rs.SpecialDefenseLeaning}" );
        // roleLog.Add( $"Mixed Defense Leaning: {rs.MixedDefenseLeaning}" );
        // roleLog.Add( $"Self Sustain: {rs.SelfSustain}" );
        // roleLog.Add( $"" );
        // roleLog.Add( $"=[Tempo]=" );
        // roleLog.Add( $"Speed Pressure: {rs.SpeedPressure}" );
        // roleLog.Add( $"Speed Control: {rs.SpeedControl}" );
        // roleLog.Add( $"Trick Room Abuser: {rs.TrickRoomBenefits}" );
        // roleLog.Add( $"Pivoting: {rs.Pivoting}" );
        // roleLog.Add( $"" );
        // roleLog.Add( $"=[Utility]=" );
        // roleLog.Add( $"Offensive Utility: {rs.OffensiveUtility}" );
        // roleLog.Add( $"Support Utility: {rs.SupportUtility}" );
        // roleLog.Add( $"Disruption: {rs.Disruption}" );
        // roleLog.Add( $"Hazard Pressure: {rs.HazardPressure}" );
        // roleLog.Add( $"Battlefield Control: {rs.BattlefieldControl}" );
        // roleLog.Add( $"" );
        // roleLog.Add( $"=[Setup Intent]=" );
        // roleLog.Add( $"Setup Pressure: {rs.SetupPressure}" );
        // roleLog.Add( $"Offensive Setup Pressure: {rs.OffensiveSetupPressure}" );
        // roleLog.Add( $"Defensive Setup Pressure: {rs.DefensiveSetupPressure}" );
        // roleLog.Add( $"Physical Setup: {rs.PhysicalSetup}" );
        // roleLog.Add( $"Special Setup: {rs.SpecialSetup}" );
        // roleLog.Add( $"" );
        // roleLog.Add( $"=[Strategic Intent]=" );
        // roleLog.Add( $"Wallbreaking: {rs.Wallbreaking}" );
        // roleLog.Add( $"Revenge Killing: {rs.RevengeKilling}" );
        // roleLog.Add( $"Team Support: {rs.TeamSupport}" );
        // roleLog.Add( $"Damage Absorbing: {rs.DamageAbsorbing}" );
        // roleLog.Add( $"" );
        // roleLog.Add( $"=[Misc.]=" );
        // roleLog.Add( $"Physical Move Count: {rs.PhysicalAttackCount}" );
        // roleLog.Add( $"Special Move Count: {rs.SpecialAttackCount}" );
        // roleLog.Add( $"Status Move Count: {rs.StatusMoveCount}" );
        // roleLog.Add( $"Setup Move Count: {rs.SetupMoveCount}" );
        // roleLog.Add( $"Hybrid Role Leaning: {rs.HybridRoleLeaning}" );
        // roleLog.Add( $"" );
        // roleLog.Add( $"=====================" );
        // roleLog.Add( $"====[Role Class]=====" );
        // roleLog.Add( $"=====================" );
        foreach( var role in sortedroles )
        {
            // roleLog.Add( $"Role: {role.Key}, Score: {role.Value}" );
        }
        // roleLog.Add( $"" );
        // roleLog.Add( $"=====================" );
        // roleLog.Add( $"====[Role Biases]====" );
        // roleLog.Add( $"=====================" );
        foreach( var bias in rp.Biases )
        {
            // roleLog.Add( $"Role Bias: {bias}" );
        }
        // roleLog.Add( $"" );
        // roleLog.Add( $"=====================" );
        // roleLog.Add( $"====[Role Traits]====" );
        // roleLog.Add( $"=====================" );
        foreach( var trait in rp.Traits )
        {
            // roleLog.Add( $"Role Trait: {trait}" );
        }
        // roleLog.Add( $"" );
        // roleLog.Add( $"=========================================" );
        // roleLog.Add( $"=====[End Role Log ({adapter.Name})]=====" );
        // roleLog.Add( $"=========================================" );
        // roleLog.Add( $"" );

        // Debug.Log( roleLog.ToString() );
        // string path = Application.persistentDataPath + "/BattleAI_RoleLog.txt";
        // System.IO.File.AppendAllText( path, roleLog.ToString() + "\n" + "\n" + "\n" + "\n" + "\n" );
        // roleLog.Clear();

        return rp;
    }

    private Dictionary<RoleClass, int> AssignRoleClasses( BattleAI_PokemonAdapter adapter, ref RoleSignals rs, ref RoleProfile rp )
    {
        //-----------------------
        //---Base Comparisons----
        //-----------------------
        int offense = Mathf.Max( rs.PhysicalOffense, rs.SpecialOffense );
        int bulk = Mathf.Max( rs.PhysicalBulk, rs.SpecialBulk );
        int utility = rs.SupportUtility + rs.OffensiveUtility;
        int setup = rs.SetupPressure + rs.OffensiveSetupPressure + rs.DefensiveSetupPressure;
        int sustain = rs.SelfSustain + rs.DamageAbsorbing;
        int fieldControl = rs.HazardPressure + rs.BattlefieldControl;

        //--Role Scores
        Dictionary<RoleClass, int> roleScores = new()
        {
            {
                RoleClass.Sweeper,
                offense +
                rs.SpeedPressure +
                rs.BurstDamage +
                rs.OffensiveSetupPressure +
                rs.RevengeKilling -
                ( bulk / 4 ) -
                ( rs.SupportUtility / 2 )
            },
            {
                RoleClass.BulkyAttacker,
                offense +
                ( bulk < 40 ? -( bulk / 2 ) : bulk ) +
                rs.SustainedDamage +
                sustain -
                ( rs.SpeedPressure / 4 )
            },
            {
                RoleClass.RevengeKiller,
                rs.RevengeKilling +
                rs.SpeedPressure +
                rs.BurstDamage - 
                ( rs.SelfSustain / 4 )
            },
            {
                RoleClass.WallBreaker,
                rs.Wallbreaking +
                rs.BurstDamage +
                offense -
                ( rs.SupportUtility / 3 )
            },
            {
                RoleClass.Wall,
                bulk +
                sustain +
                rs.Disruption +
                rs.PassivePressure +
                rs.SupportUtility -
                ( rs.SpeedPressure / 4 ) -
                ( rs.BurstDamage / 4 )
            },
            {
                RoleClass.DefensiveSetup,
                rs.DefensiveSetupPressure +
                bulk +
                sustain +
                rs.PassivePressure
            },
            {
                RoleClass.UtilitySupport,
                rs.SupportUtility +
                ( rs.OffensiveUtility / 2 ) +
                rs.TeamSupport +
                rs.SpeedControl +
                rs.BattlefieldControl +
                rs.Pivoting +
                rs.SelfSustain -
                ( rs.BurstDamage / 2 )
            },
            {
                RoleClass.Disrupter,
                rs.Disruption +
                rs.OffensiveUtility +
                rs.PassivePressure +
                rs.SpeedControl +
                ( rs.SpeedPressure / 2 )
            },
            {
                RoleClass.Pivot,
                rs.Pivoting +
                rs.SpeedPressure +
                utility +
                rs.SustainedDamage +
                sustain
            },
            {
                RoleClass.HazardControl,
                rs.HazardPressure +
                rs.PassivePressure +
                rs.SupportUtility +
                bulk
            },
            {
                RoleClass.FieldControl,
                rs.BattlefieldControl +
                rs.SpeedControl +
                rs.TeamSupport +
                rs.SetupPressure
            },
            {
                RoleClass.SetupSweeper,
                ( rs.SetupPressure == 0 ? -25 : rs.SetupPressure < 10 ? -rs.SetupPressure : rs.SetupPressure ) +
                ( rs.OffensiveSetupPressure == 0 ? -25 : rs.OffensiveSetupPressure < 10 ? -( rs.OffensiveSetupPressure * 2 ) : rs.OffensiveSetupPressure ) +
                rs.BurstDamage +
                rs.SpeedPressure +
                offense -
                ( rs.SupportUtility / 3 )
            },
            {
                RoleClass.TrickRoomAbuser,
                rs.TrickRoomBenefits +
                ( rs.BurstDamage < 30 ? 0 : rs.BurstDamage * 2 )+
                rs.SpreadDamagePressure +
                ( rs.BurstDamage > 30 ? bulk / 2 : bulk / 4 ) -
                rs.SpeedPressure
            },
            {
                RoleClass.AllRounder,
                ( rs.HybridRoleLeaning ? 40 : 0 ) +
                ( offense / 2 ) +
                ( bulk / 2 ) +
                utility +
                rs.SustainedDamage +
                rs.SpeedPressure
            }
        };

        return roleScores;
    }

    private void AssignRoleBiases( BattleAI_PokemonAdapter adapter, ref RoleSignals rs, ref RoleProfile rp )
    {
        //-----------------------
        //---Base Comparisons----
        //-----------------------
        int offense = Mathf.Max( rs.PhysicalOffense, rs.SpecialOffense );
        int offenseGap = Mathf.Abs( rs.PhysicalOffense - rs.SpecialOffense );
        int speed = adapter.Pokemon.PokeSO.Speed;
        int bulk = Mathf.Max( rs.PhysicalBulk, rs.SpecialBulk );
        int utility = rs.SupportUtility + rs.OffensiveUtility;
        int setup = rs.SetupPressure + rs.OffensiveSetupPressure + rs.DefensiveSetupPressure;
        int sustain = rs.SelfSustain + rs.DamageAbsorbing;
        int fieldControl = rs.HazardPressure + rs.BattlefieldControl;

        //-----------------------
        //------Role Biases------
        //-----------------------

        //--Mixed Attacker
        if( offenseGap <= 15 )
        {
            rp.Biases.Add( RoleBias.Mixed );
        }
        //--Physical Attacker
        else if( rs.PhysicalOffense > rs.SpecialOffense + 20 )
        {
            rp.Biases.Add( RoleBias.Physical );
        }
        //--Special Attacker
        else if( rs.SpecialOffense > rs.PhysicalOffense + 20 )
        {
            rp.Biases.Add( RoleBias.Special );
        }
            

        //--Physically Bulky
        if( rs.PhysicalBulk > 50 )
            rp.Biases.Add( RoleBias.PhysicallyBulky );

        //--Specially Bulky
        if( rs.SpecialBulk > 50 )
            rp.Biases.Add( RoleBias.SpeciallyBulky );

        //--Fast speed tier
        if( speed >= 100 )
            rp.Biases.Add( RoleBias.FastSpeed );
        else if( speed >= 90 )
            rp.Biases.Add( RoleBias.MiddlingSpeed );
        else if( speed >= 70 )
            rp.Biases.Add( RoleBias.AwkwardSpeed );
        else if( speed < 70 )
            rp.Biases.Add( RoleBias.TrickRoomSpeed );

        //--Setup Focused
        if( rs.SetupPressure >= 35 )
            rp.Biases.Add( RoleBias.SetupFocused );

        //--Support Focused
        if( rs.SupportUtility + rs.TeamSupport > 60 )
            rp.Biases.Add( RoleBias.SupportFocused );

        //--Disruptive
        if( rs.Disruption > 40 )
            rp.Biases.Add( RoleBias.Disruptive );

        //--Passive Pressure
        if( rs.PassivePressure > 20 )
            rp.Biases.Add( RoleBias.PassivePressure );

        //--Weather Control
        if( _us.PokemonHasWeatherSetter_Ability( adapter.Pokemon ) || _us.PokemonHasWeatherSetter_Move( adapter.Pokemon ) )
            rp.Biases.Add( RoleBias.WeatherControl );

        //--Speed Control
        if( _us.PokemonHas_SpeedControl( adapter.Pokemon ) )
            rp.Biases.Add( RoleBias.SpeedControl );

        //--Lead
        //--Handled after traits are assigned

        //--Bulky Offense
        if( offense >= 45 && bulk >= 45 )
            rp.Biases.Add( RoleBias.BulkyOffense );

        //--Glass Cannon
        if( offense >= 55 && bulk <= 25 && rs.SelfSustain <= 10 )
            rp.Biases.Add( RoleBias.GlassCannon );

        //--Attrition Focused
        if( rs.PassivePressure + rs.SelfSustain + rs.Disruption >= 70 )
            rp.Biases.Add( RoleBias.AttritionFocused );
    }

    private void AssignRoleTraits( BattleAI_PokemonAdapter adapter, ref RoleSignals rs, ref RoleProfile rp )
    {
        //-----------------------
        //---Base Comparisons----
        //-----------------------
        int offense = Mathf.Max( rs.PhysicalOffense, rs.SpecialOffense );
        int bulk = Mathf.Max( rs.PhysicalBulk, rs.SpecialBulk );
        int baseSpeed = adapter.Pokemon.PokeSO.Speed;
        int utility = rs.SupportUtility + rs.OffensiveUtility;
        int setup = rs.SetupPressure + rs.OffensiveSetupPressure + rs.DefensiveSetupPressure;
        int sustain = rs.SelfSustain + rs.DamageAbsorbing;
        int fieldControl = rs.HazardPressure + rs.BattlefieldControl;
        var moves = adapter.ActiveMoves;
        
        //-----------------------
        //------Role Traits------
        //-----------------------

        //--Recovery
        if( _us.PokemonHasMove_Recovery( adapter.Pokemon ) )
            rp.Traits.Add( RoleTrait.RecoveryMove );

        if( adapter.Item == BattleItemEffectID.Leftovers || adapter.Item == BattleItemEffectID.SitrusBerry )
            rp.Traits.Add( RoleTrait.RecoveryItem );

        if( _us.PokemonHasAbility_Recovery( adapter.Pokemon ) )
            rp.Traits.Add( RoleTrait.RecoveryAbility );

        if( adapter.Ability == AbilityID.Regenerator )
            rp.Traits.Add( RoleTrait.Regenerator );

        //--Hazards
        if( _us.PokemonHasMove_HazardSet( adapter.Pokemon ) )
            rp.Traits.Add( RoleTrait.HazardSetter );

        if( _us.PokemonHasMove_HazardRemoval( adapter.Pokemon ) )
            rp.Traits.Add( RoleTrait.HazardRemover );

        //--Status Effects
        int statusMoveCount = 0;
        foreach( var move in moves )
        {
            var effects = move.MoveSO.MoveEffects;
            var severe = effects.SevereStatus;
            var vol = effects.VolatileStatus;

            if( severe == SevereConditionID.PSN )
            {
                rp.Traits.Add( RoleTrait.PoisonPressure );
                statusMoveCount++;
            }

            if( severe == SevereConditionID.TOX )
            {
                rp.Traits.Add( RoleTrait.ToxicPressure );
                statusMoveCount++;
            }

            if( severe == SevereConditionID.BRN )
            {
                rp.Traits.Add( RoleTrait.BurnPressure );
                statusMoveCount++;
            }

            if( severe == SevereConditionID.FBT )
            {
                rp.Traits.Add( RoleTrait.FrostbitePressure );
                statusMoveCount++;
            }

            if( severe == SevereConditionID.PAR )
            {
                rp.Traits.Add( RoleTrait.ParalysisPressure );
                statusMoveCount++;
            }

            if( severe == SevereConditionID.SLP )
            {
                rp.Traits.Add( RoleTrait.SleepPressure );
                statusMoveCount++;
            }

            if( vol == VolatileConditionID.Confusion || vol == VolatileConditionID.Cursed || vol == VolatileConditionID.Infatuation )
                statusMoveCount++;
        }

        if( statusMoveCount > 0 )
            rp.Traits.Add( RoleTrait.StatusSpreader );

        //--Pivoting
        if( _us.PokemonHasMove_Pivot( adapter.Pokemon ) )
        {
            rp.Traits.Add( RoleTrait.PivotMove );

            if( baseSpeed >= 105 )
                rp.Traits.Add( RoleTrait.FastPivot );

            if( baseSpeed <= 75 )
                rp.Traits.Add( RoleTrait.SlowPivot );
        }

        //--Setup && Stat Debuffs
        foreach( var move in moves )
        {
            var moveIsSetup = _us.MoveIsSetup( move );
            var moveIsDebuff = _us.MoveIsDebuff( move );

            if( !moveIsSetup && !moveIsDebuff )
                continue;

            var statChanges = move.MoveSO.MoveEffects.StatChangeList;
            if( statChanges != null && statChanges.Count > 0 )
            {
                foreach( var sc in statChanges )
                {
                    if( sc.Change > 0 )
                    {
                        if( sc.Stat == Stat.Attack )
                            rp.Traits.Add( RoleTrait.PhysicallyOffensiveSetup );

                        if( sc.Stat == Stat.SpAttack )
                            rp.Traits.Add( RoleTrait.SpeciallyOffensiveSetup );

                        if( sc.Stat == Stat.Defense )
                            rp.Traits.Add( RoleTrait.PhysicallyDefensiveSetup );

                        if( sc.Stat == Stat.SpDefense )
                            rp.Traits.Add( RoleTrait.SpeciallyDefensiveSetup );
                    }

                    if( sc.Change < 0 && moveIsDebuff )
                    {
                        if( sc.Stat == Stat.Attack )
                            rp.Traits.Add( RoleTrait.AttackDebuffer );

                        if( sc.Stat == Stat.Defense )
                            rp.Traits.Add( RoleTrait.DefenseDebuffer );

                        if( sc.Stat == Stat.SpAttack )
                            rp.Traits.Add( RoleTrait.SpAttackDebuffer );

                        if( sc.Stat == Stat.SpDefense )
                            rp.Traits.Add( RoleTrait.SpDefenseDebuffer );

                        if( sc.Stat == Stat.Speed )
                            rp.Traits.Add( RoleTrait.SpeedDebuffer );
                    }
                }
            }
        }

        //--Battlefield Control
        //--Weather
        if( _us.PokemonHasWeatherSetter_Ability( adapter.Pokemon ) || _us.PokemonHasWeatherSetter_Move( adapter.Pokemon ) )
            rp.Traits.Add( RoleTrait.WeatherSetter );

        //--Terrain
        if( _us.PokemonHasTerrainSetter_Ability( adapter.Pokemon ) || _us.PokemonHasTerrainSetter_Move( adapter.Pokemon ) )
            rp.Traits.Add( RoleTrait.TerrainSetter );

        //--Screens
        if( _us.PokemonHasMove_Screens( adapter.Pokemon ) )
            rp.Traits.Add( RoleTrait.ScreenSetter );

        //--Tailwind
        if( adapter.Pokemon.CheckHasActiveMove( "Tailwind" ) )
            rp.Traits.Add( RoleTrait.TailwindSetter );

        //--Trick Room
        if( adapter.Pokemon.CheckHasActiveMove( "Trick Room" ) )
            rp.Traits.Add( RoleTrait.TrickRoomSetter );

        //--Disruption
        if( _us.PokemonHasMove_Phaze( adapter.Pokemon ) )
            rp.Traits.Add( RoleTrait.Phazes );

        if( adapter.Pokemon.CheckHasActiveMove( "Encore" ) )
            rp.Traits.Add( RoleTrait.Encore );

        if( adapter.Pokemon.CheckHasActiveMove( "Taunt" ) )
            rp.Traits.Add( RoleTrait.Taunt );

        if( adapter.Pokemon.CheckHasActiveMove( "Haze" ) )
            rp.Traits.Add( RoleTrait.Haze );

        if( adapter.Pokemon.CheckHasActiveMove( "Trick" ) || adapter.Pokemon.CheckHasActiveMove( "Knock Off" ) || adapter.Pokemon.CheckHasActiveMove( "Covet" ) || adapter.Pokemon.CheckHasActiveMove( "Embargo" ) )
            rp.Traits.Add( RoleTrait.ItemDisruption );

        //--Defensive Utility
        if( adapter.Ability == AbilityID.Intimidate )
            rp.Traits.Add( RoleTrait.IntimidateSupport );

        if( adapter.Ability == AbilityID.Demoralize )
            rp.Traits.Add( RoleTrait.DemoralizeSupport );

        if( adapter.Pokemon.CheckHasActiveMove( "Follow Me" ) || adapter.Pokemon.CheckHasActiveMove( "Rage Powder" ) || adapter.Ability == AbilityID.LightningRod || adapter.Ability == AbilityID.StormDrain )
            rp.Traits.Add( RoleTrait.Redirection );

        if( adapter.Ability == AbilityID.MarvelScale || adapter.Ability == AbilityID.ThickFat || adapter.Ability == AbilityID.LightningRod || adapter.Ability == AbilityID.StormDrain || adapter.Ability == AbilityID.FriendGuard )
            rp.Traits.Add( RoleTrait.DamageMitigation );

        if( adapter.Pokemon.CheckHasActiveMove( "Wide Guard" ) || adapter.Pokemon.CheckHasActiveMove( "Quick Guard" ) )
            rp.Traits.Add( RoleTrait.DamageMitigation );

        //--Offensive Utility
        if( _us.PokemonHasMove_Priority( adapter.Pokemon ) )
            rp.Traits.Add( RoleTrait.Priority );

        if( _us.PokemonHas_SpeedControl( adapter.Pokemon ) )
            rp.Traits.Add( RoleTrait.SpeedControl );

        if( adapter.Ability == AbilityID.ShadowTag )
            rp.Traits.Add( RoleTrait.ShadowTag );

        if( _us.PokemonHasMove_Trapping( adapter.Pokemon ) )
            rp.Traits.Add( RoleTrait.TrappingMove );

        if( adapter.Pokemon.CheckHasActiveMove( "Perish Song" ) )
            rp.Traits.Add( RoleTrait.PerishSong );

        if( adapter.Pokemon.CheckHasActiveMove( "Fake Out" ) )
            rp.Traits.Add( RoleTrait.FakeOut );

        //--Misc
        //--Suicide Lead
        if( rp.Traits.Contains( RoleTrait.HazardSetter ) && ( adapter.Item == BattleItemEffectID.FocusSash || baseSpeed >= 110 ) && rs.SelfSustain <= 25 && ( rs.SpeedControl >= 45 || rs.SpeedPressure >= 45 || rs.Disruption >= 45 ) )
            rp.Traits.Add( RoleTrait.SuicideLead );
        
        //--Anti-Lead
        int antiLead = 0;
        if( adapter.Pokemon.CheckHasActiveMove( "Fake Out" ) )
            antiLead++;

        if( adapter.Pokemon.CheckHasActiveMove( "Taunt" ) )
            antiLead++;

        if( adapter.Pokemon.CheckHasActiveMove( "Encore" ) )
            antiLead++;

        if( adapter.Ability == AbilityID.MagicBounce )
            antiLead++;

        if( baseSpeed >= 110 && rs.Disruption >= 40 )
            antiLead++;

        if( antiLead >= 2 )
            rp.Traits.Add( RoleTrait.AntiLead );

        //--Stall Breaker
        int stallBreaker = 0;
        if( adapter.Pokemon.CheckHasActiveMove( "Taunt" ) )
            stallBreaker++;

        if( rp.Traits.Contains( RoleTrait.TrappingMove ) )
            stallBreaker++;

        if( rp.Traits.Contains( RoleTrait.RecoveryMove ) )
            stallBreaker++;

        if( adapter.Pokemon.CheckHasActiveMove( "Toxic" ) )
            stallBreaker++;

        if( rs.Wallbreaking >= 45 )
            stallBreaker++;

        //--Status immunity is good, however checking for it is pretty conditional, with only Good as Gold being the only guaranteed status immunity.
        //--We could check for things like a fire type or poison/steel type for burn and poison immunities, insomnia for sleep, limber, etc.
        //--but are those pokemon actually "status immune" in a way that contributes to breaking a stalling scenario?

        if( stallBreaker >= 2 )
            rp.Traits.Add( RoleTrait.StallBreaker );

        //--Cleric
        if( _us.PokemonHasMove_AllyHealing( adapter.Pokemon ) )
            rp.Traits.Add( RoleTrait.Cleric );

        //--Spin Blocker
        if( adapter.Type.One == PokemonType.Ghost || adapter.Type.Two == PokemonType.Ghost )
            rp.Traits.Add( RoleTrait.SpinBlocker );

        //--Coverage Trait
        int coverage = 0;
        Move lastMove = null;
        foreach( var move in moves )
        {
            if( move.MoveSO.MoveCategory == MoveCategory.Status )
                continue;

            if( moves.IndexOf( move ) == 0 )
            {
                coverage++;
                lastMove = move;
                continue;
            }

            if( move.MoveType != lastMove?.MoveType )
                coverage++;

            lastMove = move;
        }

        if( coverage >= 3 )
            rp.Traits.Add( RoleTrait.WideMoveCoverage );

        //--Sound Moves
        foreach( var move in moves )
        {
            if( move.MoveSO.Flags.Contains( MoveFlags.Sound ) )
            {
                rp.Traits.Add( RoleTrait.SoundMoves );
                break;
            }
        }

        //--Status Immunities
        bool benefitsFromSevereStatus = _ai.UnitSim.PokemonBenefitsFromSevereStatus( adapter.Pokemon );
        if( _ai.UnitSim.CheckTypes( PokemonType.Fire, adapter ) || adapter.Ability == AbilityID.FlashFire || benefitsFromSevereStatus )
            rp.Traits.Add( RoleTrait.BurnImmune );

        if( _ai.UnitSim.CheckTypes( PokemonType.Ice, adapter ) || benefitsFromSevereStatus )
            rp.Traits.Add( RoleTrait.FrostImmune );

        if( _ai.UnitSim.CheckTypes( PokemonType.Poison, adapter ) || _ai.UnitSim.CheckTypes( PokemonType.Steel, adapter ) || adapter.Ability == AbilityID.PoisonHeal || benefitsFromSevereStatus )
            rp.Traits.Add( RoleTrait.PoisonToxImmune );

        if( _ai.UnitSim.CheckTypes( PokemonType.Ground, adapter ) || _ai.UnitSim.CheckTypes( PokemonType.Electric, adapter ) || adapter.Ability == AbilityID.LightningRod || adapter.Ability == AbilityID.VoltAbsorb )
            rp.Traits.Add( RoleTrait.ThunderWaveImmune );

        if( _ai.UnitSim.CheckTypes( PokemonType.Grass, adapter ) || adapter.Item == BattleItemEffectID.SafetyGoggles )
            rp.Traits.Add( RoleTrait.PowderImmune );

        if( adapter.Ability == AbilityID.Insomnia || adapter.Ability == AbilityID.VitalSpirit )
            rp.Traits.Add( RoleTrait.SleepImmune );

        if( adapter.Ability == AbilityID.GoodAsGold )
        {
            rp.Traits.Add( RoleTrait.StatusMoveImmune );
            rp.Traits.Add( RoleTrait.TauntImmune );
            rp.Traits.Add( RoleTrait.ThunderWaveImmune );
        }

        if( _ai.UnitSim.CheckTypes( PokemonType.Dark, adapter ) )
            rp.Traits.Add( RoleTrait.PranksterImmune );

        if( adapter.Ability == AbilityID.Oblivious )
            rp.Traits.Add( RoleTrait.TauntImmune );

        if( _ai.UnitSim.CheckTypes( PokemonType.Ghost, adapter ) || adapter.Ability == AbilityID.InnerFocus )
            rp.Traits.Add( RoleTrait.FakeOutImmune );

        //--Status Weaknesses
        if( rp.Biases.Contains( RoleBias.Physical ) && !rp.Traits.Contains( RoleTrait.BurnImmune ) )
            rp.Traits.Add( RoleTrait.BurnWeak );

        if( rp.Biases.Contains( RoleBias.Special ) && !rp.Traits.Contains( RoleTrait.FrostImmune ) )
            rp.Traits.Add( RoleTrait.FrostWeak );

        if( ( rp.PrimaryRole == RoleClass.Wall || rp.Biases.Contains( RoleBias.PhysicallyBulky ) || rp.Biases.Contains( RoleBias.SpeciallyBulky ) ) && !rp.Traits.Contains( RoleTrait.PoisonToxImmune ) )
            rp.Traits.Add( RoleTrait.ToxicWeak );

        if( ( rp.Biases.Contains( RoleBias.FastSpeed ) || rp.Biases.Contains( RoleBias.MiddlingSpeed ) ) && !rp.Traits.Contains( RoleTrait.ThunderWaveImmune ) && !rp.Traits.Contains( RoleTrait.PowderImmune ) )
            rp.Traits.Add( RoleTrait.ParalysisWeak );

        bool statusMoveHeavy = rp.PrimaryRole == RoleClass.DefensiveSetup || rp.PrimaryRole == RoleClass.SetupSweeper || rp.PrimaryRole == RoleClass.UtilitySupport || rp.PrimaryRole == RoleClass.Disrupter;
        bool wantsToUseStatusMoves = rp.Traits.Contains( RoleTrait.PhysicallyOffensiveSetup ) || rp.Traits.Contains( RoleTrait.SpeciallyOffensiveSetup ) ||
            rp.Traits.Contains( RoleTrait.PhysicallyDefensiveSetup ) || rp.Traits.Contains( RoleTrait.SpeciallyDefensiveSetup ) || rp.Traits.Contains( RoleTrait.StatusSpreader ) || rp.Traits.Contains( RoleTrait.RecoveryMove ) ||
            rp.Traits.Contains( RoleTrait.Taunt ) || rp.Traits.Contains( RoleTrait.Encore );

        if( statusMoveHeavy || wantsToUseStatusMoves )
            rp.Traits.Add( RoleTrait.TauntWeak );

        if( statusMoveHeavy )
        {
            rp.Traits.Add( RoleTrait.EncoreWeak );
            rp.Traits.Add( RoleTrait.FakeOutWeak );
        }
    }

}

public enum MoveRoleFlag
{
    PureOffense,
    OffensiveHybrid,
    OffensiveUtility,
    SupportiveUtility,
    Setup,
    Hazard,
    Pivot,
    Disruption,
    Recovery,
    BattlefieldControl,
}

public enum RoleClass
{
    None,
    
    //--Offensive Roles
    Sweeper,
    BulkyAttacker,
    RevengeKiller,
    WallBreaker,

    //--Defensive Roles
    Wall,
    DefensiveSetup,
    
    //--Utility Roles
    UtilitySupport,
    Disrupter,
    Pivot,
    
    //--Team Archetype Roles
    HazardControl,
    FieldControl,
    
    //--Strategy Roles
    SetupSweeper,
    TrickRoomAbuser,
    
    AllRounder
}

public enum RoleBias
{
    //--Offensive Orientation
    Physical,
    Special,
    Mixed,
    
    //--Defensive Orientation
    PhysicallyBulky,
    SpeciallyBulky,

    //--Speed Tier
    FastSpeed,
    MiddlingSpeed,
    AwkwardSpeed,
    SlowSpeed,
    TrickRoomSpeed,
    
    //--Strategic Orientation
    SetupFocused,
    SupportFocused,
    Disruptive,
    PassivePressure,

    //--Team Field Role Orientation
    WeatherControl,
    SpeedControl,

    //--Behavioral Tendancy
    Lead,
    BulkyOffense,
    GlassCannon,
    AttritionFocused,
    UtilityFocused,
}

public enum RoleTrait
{
    //--Recovery
    RecoveryMove,
    RecoveryItem,
    RecoveryAbility,
    Regenerator,
    
    //--Hazards
    HazardSetter,
    HazardRemover,

    //--Status
    StatusSpreader,
    PoisonPressure,
    ToxicPressure,
    BurnPressure,
    FrostbitePressure,
    ParalysisPressure,
    SleepPressure,

    //--Pivoting
    PivotMove,
    FastPivot,
    SlowPivot,

    //--Setup
    PhysicallyOffensiveSetup,
    SpeciallyOffensiveSetup,
    PhysicallyDefensiveSetup,
    SpeciallyDefensiveSetup,

    //--Battlefield Control
    WeatherSetter,
    TerrainSetter,
    ScreenSetter,
    TrickRoomSetter,
    TailwindSetter,

    //--Disruption
    Phazes,
    Encore,
    Taunt,
    Haze,
    ItemDisruption,

    //--Defensive Utility
    IntimidateSupport,
    DemoralizeSupport,
    Redirection,
    DamageMitigation,

    //--Offensive Utility
    Priority,
    SpeedControl,
    ShadowTag,
    TrappingMove,
    PerishSong,
    FakeOut,

    //--Misc
    SuicideLead,
    AntiLead,
    StallBreaker,
    Cleric,
    SpinBlocker,
    WideMoveCoverage,
    Frail,
    FocusSash,
    SoundMoves,

    //--Status Immunities
    BurnImmune,
    FrostImmune,
    PoisonToxImmune,
    ThunderWaveImmune,
    PowderImmune,
    SleepImmune,
    StatusMoveImmune,
    PranksterImmune,
    TauntImmune,
    FakeOutImmune,

    //--Status Weaknesses
    BurnWeak,
    FrostWeak,
    ToxicWeak,
    ParalysisWeak,
    TauntWeak,
    EncoreWeak,
    FakeOutWeak,

    //--Stat Debuffs
    StatDebuffer,
    AttackDebuffer,
    DefenseDebuffer,
    SpAttackDebuffer,
    SpDefenseDebuffer,
    SpeedDebuffer,
}

public struct RoleProfile
{
    //--Primary Role, Secondary Roles, Role Biases, Role Traits, all RoleClass Scores
    public RoleClass PrimaryRole;
    public HashSet<RoleClass> SecondaryRoles;
    public HashSet<RoleBias> Biases;
    public HashSet<RoleTrait> Traits;
    public Dictionary<RoleClass, int> RoleScores;
    public RoleSignals Signals;
}

public struct RoleSignals
{
    //--------------------------------------------------
    //--Offense
    //--------------------------------------------------

    public int PhysicalOffense;
    public int SpecialOffense;
    public bool PhysicalOffenseLeaning;
    public bool SpecialOffenseLeaning;
    public bool MixedOffenseLeaning;

    public int BurstDamage;
    public int SustainedDamage;
    public int PassivePressure;
    public int SpreadDamagePressure;

    //--------------------------------------------------
    //--Bulk
    //--------------------------------------------------

    public int PhysicalBulk;
    public int SpecialBulk;
    public bool PhysicalDefenseLeaning;
    public bool SpecialDefenseLeaning;
    public bool MixedDefenseLeaning;


    public int SelfSustain;

    //--------------------------------------------------
    //--Tempo
    //--------------------------------------------------

    public int SpeedPressure;
    public int SpeedControl;
    public int TrickRoomBenefits;

    public int Pivoting;

    //--------------------------------------------------
    //--Utility
    //--------------------------------------------------

    public int OffensiveUtility;
    public int SupportUtility;

    public int Disruption;

    public int HazardPressure;

    public int BattlefieldControl;

    //--------------------------------------------------
    //--Setup Intent
    //--------------------------------------------------
    public int SetupPressure;
    public int OffensiveSetupPressure;
    public int DefensiveSetupPressure;
    public int PhysicalSetup;
    public int SpecialSetup;

    //--------------------------------------------------
    //--Strategic Intent
    //--------------------------------------------------
    public int Wallbreaking;
    public int RevengeKilling;
    public int TeamSupport;
    public int DamageAbsorbing;

    //--------------------------------------------------
    //--Move Counts
    //--------------------------------------------------
    public int PhysicalAttackCount;
    public int SpecialAttackCount;
    public int StatusMoveCount;
    public int SetupMoveCount;

    public bool HybridRoleLeaning;
}
