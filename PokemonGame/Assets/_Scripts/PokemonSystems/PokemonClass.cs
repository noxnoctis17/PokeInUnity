using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

[System.Serializable]
public class PokemonClass
{
    [SerializeField] PokemonSO _pokeSO;
    [SerializeField] int _level;
    public PokemonSO PokeSO => _pokeSO;
    public int Level => _level;
    public int currentHP {get; set;}
    public int currentPP {get; set;}
    public List<MoveClass> Moves {get; set;}
    public Dictionary<Stat, int> Stats { get; private set; }
    public Dictionary<Stat, int> StatBoosts { get; private set; }
    public ConditionClass SevereStatus { get; private set; }
    public ConditionClass VolatileStatus { get; private set; }
    public Action OnStatusChanged;
    public int SevereStatusTime { get; set; }
    public int VolatileStatusTime { get; set; }
    public bool HPChanged;

//--------------------------------------------------------------------------------------------
//------------------------------------POKEMON STATS-------------------------------------------
//--------------------------------------------------------------------------------------------

    public int MaxHP { get; private set; }
    public int MaxPP { get; private set; }
    public int Attack => GetStat(Stat.Attack);
    public int Defense => GetStat(Stat.Defense);
    public int SpAttack => GetStat(Stat.SpAttack);
    public int SpDefense => GetStat(Stat.SpDefense);
    public int Speed => GetStat(Stat.Speed);

//--------------------------------------------------------------------------------------------
//---------------------------------------FUNCTIONS--------------------------------------------
//--------------------------------------------------------------------------------------------

    private void OnEnable()
    {
        BattleSystem.OnBattleEnded += ResetStatBoost;
        BattleSystem.OnBattleEnded += CureVolatileStatus;
    }
    
    public void Init()
    {
        Moves = new List<MoveClass>();

        //--------GENERATE MOVES-----------

        foreach(var move in PokeSO.LearnableMoves)
        {
            if(move.LevelLearned <= Level)
            {
                Moves.Add(new MoveClass(move.MoveBase));
            }

            if(Moves.Count >= 4)
                break;
        }

        CalculateStats();
        currentHP = MaxHP;
        currentPP = MaxPP;

        ResetStatBoost();
        SevereStatus = null;
        VolatileStatus = null;
    }

    private void CalculateStats()
    {
        Stats = new Dictionary<Stat, int>();

        Stats.Add(Stat.Attack, Mathf.FloorToInt(( 2 * PokeSO.Attack * Level ) / 100f ) + 5);
        Stats.Add(Stat.Defense, Mathf.FloorToInt(( 2 * PokeSO.Attack * Level ) / 100f ) + 5);
        Stats.Add(Stat.SpAttack, Mathf.FloorToInt(( 2 * PokeSO.Attack * Level ) / 100f ) + 5);
        Stats.Add(Stat.SpDefense, Mathf.FloorToInt(( 2 * PokeSO.Attack * Level ) / 100f ) + 5);
        Stats.Add(Stat.Speed, Mathf.FloorToInt(( 2 * PokeSO.Attack * Level ) / 100f ) + 5);

        MaxHP = Mathf.FloorToInt(( 2 * PokeSO.MaxHP * Level ) / 100 ) + Level + 10;
        MaxPP = Mathf.FloorToInt(( 2 * PokeSO.MaxPP * Level ) / 200 ) + Level + 10;
    }

    private int GetStat(Stat stat)
    {
        int statValue = Stats[stat];

        int boost = StatBoosts[stat];
        var boostmodifier = new float[] { 1f, 1.5f, 2f, 2.5f, 3f, 3.5f, 4f };

        if(boost >= 0)
            statValue = Mathf.FloorToInt(statValue * boostmodifier[boost]);
        else
            statValue = Mathf.FloorToInt(statValue / boostmodifier[-boost]);

        return statValue;
    }

    public void ApplyStatBoost(List<StatBoost> statBoosts)
    {
        foreach(var statBoost in statBoosts)
        {
            var stat = statBoost.Stat;
            var boost = statBoost.Boost;

            StatBoosts[stat] = Mathf.Clamp(StatBoosts[stat] + boost, -6, 6);

            Debug.Log($"{stat} has been boosted to: {StatBoosts[stat]}");
        }
    }

    private void ResetStatBoost()
    {
        StatBoosts = new Dictionary<Stat, int>()
        {
            {Stat.Attack,    0},
            {Stat.Defense,   0},
            {Stat.SpAttack,  0},
            {Stat.SpDefense, 0},
            {Stat.Speed,     0},
            {Stat.Accuracy,  0},
            {Stat.Evasion,   0},
        };
    }

    public void UpdateHP(int damage)
    {
        currentHP = Mathf.Clamp(currentHP - damage, 0, MaxHP);
        HPChanged = true;
    }

    public void SetSevereStatus(ConditionID conditionID)
    {
        if(SevereStatus != null) return;

        SevereStatus = ConditionsDB.Conditions[conditionID];
        SevereStatus?.OnRoundStart?.Invoke(this);
        Debug.Log($"{_pokeSO.pName} has been afflicted with: {ConditionsDB.Conditions[conditionID].ConditionName}");
        OnStatusChanged?.Invoke();
    }

    public void CureSevereStatus()
    {
        SevereStatus = null;
        OnStatusChanged?.Invoke();
    }

    public void SetVolatileStatus(ConditionID conditionID)
    {
        if(VolatileStatus != null) return;

        VolatileStatus = ConditionsDB.Conditions[conditionID];
        VolatileStatus?.OnRoundStart?.Invoke(this);
        Debug.Log($"{_pokeSO.pName} has been afflicted with: {ConditionsDB.Conditions[conditionID].ConditionName}");
        // OnStatusChanged?.Invoke(); -------will add some visual effect for volatile statuses eventually
    }

    public void CureVolatileStatus()
    {
        VolatileStatus = null;
        // OnStatusChanged?.Invoke(); -------will add some visual effect for volatile statuses eventually
    }

    public MoveClass GetRandomMove()
    {
        int r = UnityEngine.Random.Range(0, Moves.Count);
        return Moves[r];
    }

    public bool OnBeforeTurn()
    {
        bool canPerformMove = true;
        if(SevereStatus?.OnBeforeTurn != null)
        {
            if(!SevereStatus.OnBeforeTurn(this))
                canPerformMove = false;
        }

        if(VolatileStatus?.OnBeforeTurn != null)
        {
            if(!VolatileStatus.OnBeforeTurn(this))
                canPerformMove = false;
        }

        return canPerformMove;
    }

    public void OnAfterTurn()
    {
        SevereStatus?.OnAfterTurn?.Invoke(this);
        VolatileStatus?.OnAfterTurn?.Invoke(this);
    }

}

public class DamageDetails
{
    public bool Fainted {get; set;}
    public float Critical {get; set;}
    public float TypeEffectiveness {get; set;}
}
