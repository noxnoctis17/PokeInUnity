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
                bs.SetBattleFlag( BattleFlag.SpeedChange, true );
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
          CourtConditionID.Reflect, new( 5, 3 ) //--Duration + modifier get set in constructor. OnStart sets TimeLeft via public function. TimeLeft is what is actually ticked down.
          {
            ID = CourtConditionID.Reflect,
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
                unit.SetReflect( true );
                Debug.Log( $"{unit.Pokemon.NickName} is now in Reflect! Incoming physical damage should theoretically be reduced by 1/3" );
            },

            OnExitCourt = ( BattleUnit unit, Battlefield field ) =>
            {
                unit.SetReflect( false );
                Debug.Log( $"{unit.Pokemon.NickName} is no longer in Reflect!" );
            }
          }  
        },
        {
          CourtConditionID.LightScreen, new( 5, 3 ) //--Duration + modifier get set in constructor. OnStart sets TimeLeft via public function. TimeLeft is what is actually ticked down.
          {
            ID = CourtConditionID.LightScreen,
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
                unit.SetLightScreen( true );
                Debug.Log( $"{unit.Pokemon.NickName} is now in Light Screen! Incoming special damage should theoretically be reduced by 1/3" );
            },

            OnExitCourt = ( BattleUnit unit, Battlefield field ) =>
            {
                unit.SetLightScreen( false );
                Debug.Log( $"{unit.Pokemon.NickName} is no longer in Light Screen!" );
            }
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
    LightScreen
    
}
