using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TerrainDB : MonoBehaviour
{
    public static Dictionary<TerrainID, TerrainCondition> Terrains { get; private set; }

    public static void Init()
    {
        SetDictionary();

        foreach( var kvp in Terrains )
        {
            var conditionID = kvp.Key;
            var condition = kvp.Value;

            condition.ID = conditionID;
        }
    }

    private static void SetDictionary()
    {
        Terrains = new Dictionary<TerrainID, TerrainCondition>()
        {
            {
                TerrainID.None, new()
                {
                    Name = "None",
                    ID = TerrainID.None,
                }
            },
            {
                TerrainID.Grassy, new()
                {
                    Name = "Grassy Terrain",
                    ID = TerrainID.Grassy,
                    Description = "The user turns the ground into Grassy Terrain for five turns. This restores the HP of Pokemon on the ground a little every turn and powers up Grass-type moves.",
                    StartMessage = "Grass sprouts all over the battlefield!",
                    StartByMoveMessage = "Grass sprouts all over the battlefield!",
                    EndMessage = "The grassy terrain has disappeared!",

                    OnTerrainEffect = ( BattleUnit unit ) =>
                    {
                        if( unit.Flags[UnitFlags.Grounded].IsActive )
                        {
                            int healBy = Mathf.FloorToInt( unit.Pokemon.MaxHP / 16 );
                            unit.Pokemon.IncreaseHP( healBy );
                            unit.Pokemon.AddStatusEvent( StatusEventType.Heal, $"{unit.Pokemon.NickName} was healed by the Grassy Terrain!" );
                        }
                    },

                    OnDamageModify = ( BattleUnit source, BattleUnit target, Move move ) =>
                    {
                        if( source.Flags[UnitFlags.Grounded].IsActive && move.MoveSO.Type == PokemonType.Grass )
                            return 1.3f;
                        else
                            return 1f;
                    },
                }
            },
            {
                TerrainID.Psychic, new()
                {
                    Name = "Psychic Terrain",
                    ID = TerrainID.Psychic,
                    Description = "This protects Pokemon on the ground from priority moves and powers up Psychic-type moves for five turns.",
                    StartMessage = "The battlefield got weird!",
                    StartByMoveMessage = "The battlefield got weird!",
                    EndMessage = "The strange terrain disappeared!",

                    OnTerrainEffect = ( BattleUnit unit ) =>
                    {
                        if( unit.Flags[UnitFlags.Grounded].IsActive )
                        {
                            int healBy = Mathf.FloorToInt( unit.Pokemon.MaxHP / 16 );
                            unit.Pokemon.IncreaseHP( healBy );
                            unit.Pokemon.AddStatusEvent( StatusEventType.Heal, $"{unit.Pokemon.NickName} was healed by the Grassy Terrain!" );
                        }
                    },

                    OnMoveModify = ( BattleUnit source, BattleUnit target, Move move ) =>
                    {
                        if( move.MoveSO.Name == "Expanding Force" )
                        {
                            int newPower = Mathf.FloorToInt( move.MovePower * 1.5f );
                            //--move.OverrideTargets( Targets.SpreadMove );
                            move.OverrideMovePower( newPower );
                        }
                    },

                    OnDamageModify = ( BattleUnit source, BattleUnit target, Move move ) =>
                    {
                        if( source.Flags[UnitFlags.Grounded].IsActive && move.MoveSO.Type == PokemonType.Psychic )
                            return 1.3f;
                        else
                            return 1f;
                    },
                }
            },
        };
    }
}

public enum TerrainID { None, Grassy, Psychic, Electric, Misty, }
