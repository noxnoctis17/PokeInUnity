using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CourtConditionDB
{
    public static Dictionary<CourtConditionID, CourtCondition> CourtConditions = new()
    {
        {
          CourtConditionID.Tailwind, new( 4, 0 ) //--Duration + modifier get set in constructor. OnStart sets TimeLeft via public function. TimeLeft is what is actually ticked down.
          {
            ID = CourtConditionID.Tailwind,
            ConType = ConditionType.AllySide_Buff,
            StartMessage = "A tailwind blows from behind your team!",
            EndMessage = "The tailwind subsided.",
            //--Duration = 4,
            //--DurationModifier = 0,

            OnStart = ( BattleSystem bs, Battlefield field, CourtLocation location, BattleUnit user ) =>
            {
                Debug.Log( "Tailwild OnStart" );
                var court = field.ActiveCourts[location];
                int duration = court.Conditions[CourtConditionID.Tailwind].Duration;
                //--For Posterity when implementing screens + lightclay, this will be the extension lightclay adds to the default duration
                int modifier = court.Conditions[CourtConditionID.Tailwind].DurationModifier;

                Debug.Log( $"{location}'s Tailwind duration is: {duration}" );
                //--Set/Reset TimeLeft OnStart. For conditions with modifiers, we'll have to probably add the unit using the move and check for their held item or ability or w/e for modifier influence
                court.Conditions[CourtConditionID.Tailwind].SetTimeLeft( duration );
                Debug.Log( $"{location}'s Tailwind duration is: {duration}" );
                // bs.SetBattleFlag( BattleFlag.SpeedChange, true );
            },

            OnEnterCourt = ( BattleUnit unit, Battlefield field ) =>
            {
                Debug.Log( $"{unit.Pokemon.NickName} is about to be in Tailwind! Current Speed: {unit.Pokemon.Speed}" );
                unit.Pokemon.ApplyDirectStatModifier( Stat.Speed, DirectModifierCause.Tailwind, 2f );
                Debug.Log( $"{unit.Pokemon.NickName} is now in Tailwind! Current Speed: {unit.Pokemon.Speed}" );
            },

            OnExitCourt = ( BattleUnit unit, Battlefield field ) =>
            {
                Debug.Log( $"{unit.Pokemon.NickName} is about to leave Tailwind! Current Speed: {unit.Pokemon.Speed}" );
                unit.Pokemon.RemoveDirectStatModifier( Stat.Speed, DirectModifierCause.Tailwind );
                Debug.Log( $"{unit.Pokemon.NickName} is no longer in Tailwind! Current Speed: {unit.Pokemon.Speed}" );
            }
          }  
        },
        {
          CourtConditionID.TrickRoom, new( 5, 0 ) //--Duration + modifier get set in constructor. OnStart sets TimeLeft via public function. TimeLeft is what is actually ticked down.
          {
            ID = CourtConditionID.TrickRoom,
            TrickRoomStartMessage = ( BattleSystem bs, Pokemon pokemon ) =>
            {
                return $"{pokemon.NickName} has twisted the dimensions of speed!";
            },

            TrickRoomAlreadyActiveMessage = ( BattleSystem bs, Pokemon pokemon ) =>
            {
                return $"{pokemon.NickName} has returned the dimensions of speed back to normal!";
            },

            EndMessage = "The dimensions of speed have returned to normal.",
            //--Duration = 5,
            //--DurationModifier = 0,

            OnStart = ( BattleSystem bs, Battlefield field, CourtLocation location, BattleUnit user ) =>
            {
                Debug.Log( "Trick Room OnStart" );
                if( bs.BattleFlags[BattleFlag.TrickRoom] )
                {
                    Debug.Log( "Trick Room is already up! Reversing Trick Room!" );
                    field.ActiveCourts[location].Conditions[CourtConditionID.TrickRoom].OnEnd?.Invoke( bs, field );
                }
                else
                {
                    Debug.Log( "Trick Room Begins Baybeeeeeeeeeeee" );
                    var court = field.ActiveCourts[location];
                    int duration = court.Conditions[CourtConditionID.TrickRoom].Duration;
                    int modifier = court.Conditions[CourtConditionID.TrickRoom].DurationModifier; //--For Posterity when implementing screens + lightclay, this will be the extension lightclay adds to the default duration

                    //--Set/Reset TimeLeft OnStart. For conditions with modifiers, we'll have to probably add the unit using the move and check for their held item or ability or w/e for modifier influence
                    court.Conditions[CourtConditionID.TrickRoom].SetTimeLeft( duration );
                    Debug.Log( $"{location}'s Trick Room's duration is: {duration}" );
                    bs.SetBattleFlag( BattleFlag.TrickRoom, true );
                }
            },

            OnEnd = ( BattleSystem bs, Battlefield field ) =>
            {
                Debug.Log( "Trick Room OnEnd" );
                bs.SetBattleFlag( BattleFlag.TrickRoom, false );
            }
          }  
        },
        {
          CourtConditionID.Reflect, new( 5, 3 ) //--Duration + modifier get set in constructor. OnStart sets TimeLeft via public function. TimeLeft is what is actually ticked down.
          {
            ID = CourtConditionID.Reflect,
            ConType = ConditionType.AllySide_Buff,
            StartMessage = "A reflective screen will reduce incoming physical damage!",
            EndMessage = "The effects of Reflect have disappeared.",
            //--Duration = 4,
            //--DurationModifier = 0,

            OnStart = ( BattleSystem bs, Battlefield field, CourtLocation location, BattleUnit user ) =>
            {
                Debug.Log( "Reflect OnStart" );
                var court = field.ActiveCourts[location];
                int duration = court.Conditions[CourtConditionID.Reflect].Duration;
                //--For Posterity when implementing screens + lightclay, this will be the extension lightclay adds to the default duration
                int modifier = court.Conditions[CourtConditionID.Reflect].DurationModifier;

                Debug.Log( $"{location}'s Reflect duration is: {duration}" );
                //--Set/Reset TimeLeft OnStart. For conditions with modifiers, we'll have to probably add the unit using the move and check for their held item or ability or w/e for modifier influence
                court.Conditions[CourtConditionID.Reflect].SetTimeLeft( duration );
                Debug.Log( $"{location}'s Reflect duration is: {duration}" );
            },

            OnEnterCourt = ( BattleUnit unit, Battlefield field ) =>
            {
                unit.SetFlagActive( UnitFlags.Reflect, true );
                Debug.Log( $"{unit.Pokemon.NickName} is now in Reflect! Incoming physical damage should theoretically be reduced by 1/3" );
            },

            OnExitCourt = ( BattleUnit unit, Battlefield field ) =>
            {
                unit.SetFlagActive( UnitFlags.Reflect, false );
                Debug.Log( $"{unit.Pokemon.NickName} is no longer in Reflect!" );
            }
          }  
        },
        {
          CourtConditionID.LightScreen, new( 5, 3 ) //--Duration + modifier get set in constructor. OnStart sets TimeLeft via public function. TimeLeft is what is actually ticked down.
          {
            ID = CourtConditionID.LightScreen,
            ConType = ConditionType.AllySide_Buff,
            StartMessage = "A screen of light will reduce incoming special damage!",
            EndMessage = "The effects of Light Screen have disappeared.",
            //--Duration = 4,
            //--DurationModifier = 0,

            OnStart = ( BattleSystem bs, Battlefield field, CourtLocation location, BattleUnit user ) =>
            {
                Debug.Log( "Light Screen OnStart" );
                var court = field.ActiveCourts[location];
                int duration = court.Conditions[CourtConditionID.LightScreen].Duration;
                //--For Posterity when implementing screens + lightclay, this will be the extension lightclay adds to the default duration
                int modifier = court.Conditions[CourtConditionID.LightScreen].DurationModifier;

                Debug.Log( $"{location}'s Light Screen duration is: {duration}" );
                //--Set/Reset TimeLeft OnStart. For conditions with modifiers, we'll have to probably add the unit using the move and check for their held item or ability or w/e for modifier influence
                court.Conditions[CourtConditionID.LightScreen].SetTimeLeft( duration );
                Debug.Log( $"{location}'s Light Screen duration is: {duration}" );
            },

            OnEnterCourt = ( BattleUnit unit, Battlefield field ) =>
            {
                unit.SetFlagActive( UnitFlags.LightScreen, true );
                Debug.Log( $"{unit.Pokemon.NickName} is now in Light Screen! Incoming special damage should theoretically be reduced by 1/3" );
            },

            OnExitCourt = ( BattleUnit unit, Battlefield field ) =>
            {
                unit.SetFlagActive( UnitFlags.LightScreen, false );
                Debug.Log( $"{unit.Pokemon.NickName} is no longer in Light Screen!" );
            }
          }  
        },
        {
          CourtConditionID.LeechSeed, new( 0, 0 ) //--Duration + modifier get set in constructor. OnStart sets TimeLeft via public function. TimeLeft is what is actually ticked down.
          {
            ID = CourtConditionID.LeechSeed,
            ConType = ConditionType.OpposingSide_Hazard,
            StartMessage = "The opposing side has been seeded!",
            EffectMessage = "Your opponents' health has been drained!",
            EndMessage = "The seeds have been cleared from the field.",

            OnStart = ( BattleSystem bs, Battlefield field, CourtLocation location, BattleUnit user ) =>
            {
                //--This is an entry hazard, and must be placed in the opposing court of the user!
                var court = field.ActiveCourts[location];
                court.Conditions[CourtConditionID.LeechSeed].IsInfinite = true;
            },

            OnEnterCourt = ( BattleUnit unit, Battlefield field ) =>
            {
                //--Leech seed is a round-end phase life steal. It does not activate on entry, nor on exit.
                //--Instead, I will check to see if the entering Pokemon is a Fire type, and if so, remove the hazard
                //--Similar to how poison types will remove toxic spikes
            },

            OnCourtEffect = ( BattleUnit unit, Battlefield field, CourtLocation location ) =>
            {
                Debug.Log( "OnCourtEffect: Leech Seed" );
                BattleSystem battleSystem = BattleSystem.Instance;
                BattleUnit drainedUnit;
                BattleUnit healedUnit;
                int stolenHP = 0;

                //--Leech seed drains hp from the effected court! it's correctly applied to the opposing court when used.
                //--Here we check to see if the currently effected unit is in the top court. if it is, we have to drain its hp and give it to the
                //--opposing unit on the bottom court.
                if( field.GetUnitCourt( unit ).Location == CourtLocation.TopCourt )
                {
                    int unitIndex = field.ActiveCourts[CourtLocation.TopCourt].GetUnitIndex( unit );
                    drainedUnit = field.ActiveCourts[CourtLocation.TopCourt].Units[unitIndex];
                    stolenHP = drainedUnit.Pokemon.MaxHP / 8;

                    if( unitIndex == 0 )
                    {
                        if( field.ActiveCourts[CourtLocation.BottomCourt].Units[1] != null )
                            healedUnit = field.ActiveCourts[CourtLocation.BottomCourt].Units[1];
                        
                        else if( field.ActiveCourts[CourtLocation.BottomCourt].Units[1] == null && field.ActiveCourts[CourtLocation.BottomCourt].Units[0] != null )
                            healedUnit = field.ActiveCourts[CourtLocation.BottomCourt].Units[0];

                        else
                            healedUnit = null;
                    }
                    else
                    {
                        if( field.ActiveCourts[CourtLocation.BottomCourt].Units[0] != null )
                            healedUnit = field.ActiveCourts[CourtLocation.BottomCourt].Units[0];

                        else if( field.ActiveCourts[CourtLocation.BottomCourt].Units[0] == null && field.ActiveCourts[CourtLocation.BottomCourt].Units[1] != null )
                            healedUnit = field.ActiveCourts[CourtLocation.BottomCourt].Units[1];

                        else
                            healedUnit = null;
                    }
                }
                else
                {
                    int unitIndex = field.ActiveCourts[CourtLocation.BottomCourt].GetUnitIndex( unit );
                    drainedUnit = field.ActiveCourts[CourtLocation.BottomCourt].Units[unitIndex];
                    stolenHP = drainedUnit.Pokemon.MaxHP / 8;

                    if( unitIndex == 0 )
                    {
                        if( field.ActiveCourts[CourtLocation.TopCourt].Units[1] != null )
                            healedUnit = field.ActiveCourts[CourtLocation.TopCourt].Units[1];
                        
                        else if( field.ActiveCourts[CourtLocation.TopCourt].Units[1] == null && field.ActiveCourts[CourtLocation.TopCourt].Units[0] != null )
                            healedUnit = field.ActiveCourts[CourtLocation.TopCourt].Units[0];

                        else
                            healedUnit = null;
                    }
                    else
                    {
                        if( field.ActiveCourts[CourtLocation.TopCourt].Units[0] != null )
                            healedUnit = field.ActiveCourts[CourtLocation.TopCourt].Units[0];

                        else if( field.ActiveCourts[CourtLocation.TopCourt].Units[0] == null && field.ActiveCourts[CourtLocation.TopCourt].Units[1] != null )
                            healedUnit = field.ActiveCourts[CourtLocation.TopCourt].Units[1];

                        else
                            healedUnit = null;
                    }
                }

                if( healedUnit != null )
                {
                    // drainedUnit.DecreaseHP( stolenHP );
                    // drainedUnit.AddStatusEvent( StatusEventType.Damage, $"{drainedUnit.NickName} had its HP stolen by leech seed!" );
                    // healedUnit.IncreaseHP( stolenHP );
                    // healedUnit.AddStatusEvent( StatusEventType.Heal, $"{healedUnit.NickName}'s HP was restored by leech seed!" );
                    battleSystem.AddToEventQueue( () => battleSystem.CreateLifeStealEvent( drainedUnit, healedUnit, stolenHP ) );
                }
            },

          }  
        },
        {
          CourtConditionID.StealthRock, new( 0, 0 ) //--Duration + modifier get set in constructor. OnStart sets TimeLeft via public function. TimeLeft is what is actually ticked down.
          {
            ID = CourtConditionID.StealthRock,
            ConType = ConditionType.OpposingSide_Hazard,
            StartMessage = "Sharp stones have been scattered on the opposing side of the field!",
            EndMessage = "The sharp stones have been cleared from the field.",

            OnStart = ( BattleSystem bs, Battlefield field, CourtLocation location, BattleUnit user ) =>
            {
                //--This is an entry hazard, and must be placed in the opposing court of the user!
                var court = field.ActiveCourts[location];
                court.Conditions[CourtConditionID.StealthRock].IsInfinite = true;
            },

            OnEnterCourt = ( BattleUnit unit, Battlefield field ) =>
            {
                float effectiveness = TypeChart.GetEffectiveness( PokemonType.Rock, unit.Pokemon.PokeSO.Type1 ) * TypeChart.GetEffectiveness( PokemonType.Rock, unit.Pokemon.PokeSO.Type2 );
                float damage = ( unit.Pokemon.MaxHP / 8 ) * effectiveness;
                unit.Pokemon.DecreaseHP( Mathf.FloorToInt( damage ) );
                unit.Pokemon.AddStatusEvent( StatusEventType.Damage, $"Pointed stones dig into {unit.Pokemon.NickName}!" );
            },
          }  
        },
    };
}

public enum CourtConditionID
{
    NONE,
    Tailwind,
    TrickRoom,
    Reflect,
    LightScreen,
    LeechSeed,
    StealthRock,
    Spikes,
    ToxicSpikes,
    StickyWeb,

    
}
