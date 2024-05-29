using System.Collections.Generic;
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
    [SerializeField] private MoveEffects _moveEffects;
    [SerializeField] private List<SecondaryMoveEffects> _secondaryMoveEffects;
    [SerializeField] private MovePriority _movePriority;
    [SerializeField] private Vector2Int _hitRange;
    [SerializeField] private int _power;
    [SerializeField] private int _accuracy;
    [SerializeField] private bool _alwaysHits;
    [SerializeField] private int _pp;

    public MoveCategory MoveCategory => _moveCategory;
    public MoveTarget MoveTarget => _moveTarget;
    public PokemonType Type => _moveType;
    public MoveEffects MoveEffects => _moveEffects;
    public List<SecondaryMoveEffects> SecondaryMoveEffects => _secondaryMoveEffects;
    public MovePriority MovePriority => _movePriority;
    public Vector2Int HitRange => _hitRange;
    public int Power => _power;
    public int Accuracy => _accuracy;
    public bool Alwayshits => _alwaysHits;
    public int PP => _pp;

    public int GetHitAmount(){
        if( _hitRange == Vector2.zero )
            return 1;

        int hitCount;

        if( _hitRange.y == 0 )
            hitCount = _hitRange.x;
        else
            hitCount = Random.Range( _hitRange.x, _hitRange.y + 1 );

        return hitCount;
    }

}

[System.Serializable]
public enum MoveCategory { Physical, Special, Status, Other };

[System.Serializable]
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
    [Range(-6, 6)]
    public int Change;
}

public enum MoveTarget { enemy, self }

public enum MovePriority { zero, one, two, three, four, five, six, seven, eight, nine, ten }