using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

[CreateAssetMenu(menuName = "Pokemon/Moves/MoveBaseSO")]
public class MoveSO : ScriptableObject
{
    [SerializeField] private string _moveName;
    public string Name => _moveName;

    [TextArea(10, 20)]
    [SerializeField] private string _description;
    public string Description => _description;

    ///
    /// ///////////////////////////////////////////////////////
    ///
    [SerializeField] private MoveCategory _moveCategory;
    [SerializeField] private MoveTarget _moveTarget;
    [SerializeField] PokemonType _moveType;
    [SerializeField] private int _power;
    [SerializeField] private int _accuracy;
    [SerializeField] private bool _alwaysHits;
    [SerializeField] private int _pp;
    [SerializeField] private MovePriority _movePriority;
    [SerializeField] private CritBehavior _critBehavior;
    [SerializeField] private RecoilMoveEffect _recoil = new();
    [SerializeField] private int _drainPercentage;
    [SerializeField] private Vector2Int _hitRange;
    [SerializeField] private MoveEffects _moveEffects;
    [SerializeField] private List<SecondaryMoveEffects> _secondaryMoveEffects;

    public MoveCategory MoveCategory => _moveCategory;
    public MoveTarget MoveTarget => _moveTarget;
    public PokemonType Type => _moveType;
    public int Power => _power;
    public int Accuracy => _accuracy;
    public bool Alwayshits => _alwaysHits;
    public int PP => _pp;
    public MovePriority MovePriority => _movePriority;
    public CritBehavior CritBehavior => _critBehavior;
    public RecoilMoveEffect Recoil => _recoil;
    public int DrainPercentage => _drainPercentage;
    public Vector2Int HitRange => _hitRange;
    public MoveEffects MoveEffects => _moveEffects;
    public List<SecondaryMoveEffects> SecondaryMoveEffects => _secondaryMoveEffects;

    public int GetHitAmount(){
        if( _hitRange == Vector2.zero )
            return 1;

        int hitCount;

        if( _hitRange.y == 0 )
            hitCount = _hitRange.x;
        else
            hitCount = UnityEngine.Random.Range( _hitRange.x, _hitRange.y + 1 );

        return hitCount;
    }

}

[Serializable]
public enum MoveCategory { Physical, Special, Status, Other };

[Serializable]
public class MoveEffects
{
    //--Stat Modifiers
    [SerializeField] private List<StatBoost> _statChangeList;
    public List<StatBoost> StatChangeList => _statChangeList;

    //--Severe Status Conditions (PSN, BRN, PAR, SLP, FRZ)
    [SerializeField] private ConditionID _severeStatus;
    [SerializeField] private ConditionID _volatileStatus;
    [SerializeField] private ConditionID _weather;

    public ConditionID SevereStatus => _severeStatus;
    public ConditionID VolatileStatus => _volatileStatus;
    public ConditionID Weather => _weather;

}

[Serializable]
public class SecondaryMoveEffects : MoveEffects
{
    [SerializeField] private int _chance;
    [SerializeField] MoveTarget _target;
    public int Chance => _chance;
    public MoveTarget Target => _target;
}

[Serializable]
public class StatBoost
{
    public Stat Stat;
    [Range(-6, 6)]
    public int Change;
}

[Serializable]
public class RecoilMoveEffect
{
    public RecoilType RecoilType;
    public int RecoilDamage = 0;
}

public enum MoveTarget { enemy, self }

public enum CritBehavior { none, HighCritRatio, AlwaysCrits, NeverCrits, }

public enum RecoilType { none, RecoilByMaxHP, RecoilByCurrentHP, RecoilByDamage, }

public enum MovePriority { zero, one, two, three, four, five, six, seven, eight, nine, ten }