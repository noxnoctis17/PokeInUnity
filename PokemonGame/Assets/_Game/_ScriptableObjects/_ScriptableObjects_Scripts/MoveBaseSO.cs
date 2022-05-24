using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Pokemon/Moves/MoveBaseSO")]
public class MoveBaseSO : ScriptableObject
{
    [SerializeField] private string _moveName;
    public string MoveName => _moveName;

    [TextArea(10, 20)]
    [SerializeField] private string _description;
    public string Description => _description;

    ///
    /// ///////////////////////////////////////////////////////
    ///
    [SerializeField] private MoveCategory _moveCategory;
    public MoveCategory MoveCategory => _moveCategory;
    [SerializeField] private MoveTarget _moveTarget;
    public MoveTarget MoveTarget => _moveTarget;
    [SerializeField] PokemonType _moveType;
    public PokemonType MoveType => _moveType;
    [SerializeField] private MoveEffects _moveEffects;
    public MoveEffects MoveEffects => _moveEffects;
    [SerializeField] private List<SecondaryMoveEffects> _secondaryMoveEffects;
    public List<SecondaryMoveEffects> SecondaryMoveEffects => _secondaryMoveEffects;
    [SerializeField] private MovePriority _movePriority;
    public MovePriority MovePriority => _movePriority;

    ///
    /// //////////////////////////////////////////////////////
    ///

    [SerializeField] private int _power;
    [SerializeField] private int _accuracy;
    [SerializeField] private bool _alwaysHits;
    [SerializeField] private int _pp;
    public int Power => _power;
    public int Accuracy => _accuracy;
    public bool Alwayshits => _alwaysHits;
    public int PP => _pp;

}

[System.Serializable]
public enum MoveCategory { Physical, Special, Status, Other };

[System.Serializable]
public class MoveEffects
{
    //--Stat Modifiers
    [SerializeField] private List<StatBoost> _statBoostList;
    public List<StatBoost> StatBoostList => _statBoostList;

    //--Severe Status Conditions (PSN, BRN, PAR, SLP, FRZ)
    [SerializeField] private ConditionID _severeStatus;
    public ConditionID SevereStatus => _severeStatus;
    [SerializeField] private ConditionID _volatileStatus;
    public ConditionID VolatileStatus => _volatileStatus;

}

[System.Serializable]
public class SecondaryMoveEffects : MoveEffects
{
    [SerializeField] private int _chance;
    [SerializeField] MoveTarget _target;
    public int Chance => _chance;
    public MoveTarget Target => _target;
}

[System.Serializable]
public class StatBoost
{
    public Stat Stat;
    public int Boost;
}

public enum MoveTarget { enemy, self }

public enum MovePriority { zero, one, two, three, four, five, six, seven, eight, nine, ten }