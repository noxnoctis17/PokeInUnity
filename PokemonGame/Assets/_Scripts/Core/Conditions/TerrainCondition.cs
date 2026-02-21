using System;
using UnityEngine;

public class TerrainCondition
{
    public string Name { get; set; }
    public TerrainID ID { get; set; }
    public string Description { get; set; }
    public string StartMessage { get; set; }
    public string EffectMessage { get; set; }
    public string EndMessage { get; set; }
    public string StartByMoveMessage { get; set; }

    public Action<BattleUnit> OnStart { get; set; }
    public Action<BattleUnit> OnTerrainEffect { get; set; }
    public Action<BattleUnit> OnEnterTerrain { get; set; }
    public Action<BattleUnit> OnExitTerrain { get ; set;}
    public Action<BattleUnit, BattleUnit, Move> OnMoveModify { get; set; }
    public Func<BattleUnit, Pokemon, Move, float> OnDamageModify { get; set; }
}
