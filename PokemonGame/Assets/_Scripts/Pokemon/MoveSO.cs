using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Pokemon/New Move")]
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
    [SerializeField] private int _pp = 10;
    [SerializeField] private MovePriority _movePriority = MovePriority.Zero;
    [SerializeField] private CritBehavior _critBehavior;
    [SerializeField] private RecoilMoveEffect _recoil = new();
    [SerializeField] private int _drainPercentage;
    [SerializeField] private Vector2Int _hitRange;
    [SerializeField] private List<MoveFlags> _flags;
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
    public List<MoveFlags> Flags => _flags;
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

    public bool HasFlag( MoveFlags flag )
    {
        return _flags.Contains( flag );
    }

}

[Serializable]
public enum MoveCategory { Physical, Special, Status, Other };
public enum EffectSource { Move, Ability, Item, }

[Serializable]
public class MoveEffects
{
    //--Stat Modifiers
    [SerializeField] private List<StatStage> _statChangeList;
    public List<StatStage> StatChangeList => _statChangeList;

    //--Severe Status Conditions (PSN, BRN, PAR, SLP, FRZ)
    [SerializeField] private StatusConditionID _severeStatus;
    //--Volatile Status Conditions (Confusion, Affection, etc. )
    [SerializeField] private StatusConditionID _volatileStatus;
    //--Transient Status Conditions (Flinch, Protect, Endure)
    [SerializeField] private StatusConditionID _transientStatus;
    //-Weather Conditions (Harsh Sunlight, Rainfall, Sandstorm, Snowfall)
    [SerializeField] private WeatherConditionID _weather;
    //--Court Conditions (Tailwind, Entry Hazards, Screens, etc.)
    [SerializeField] private CourtConditionID _courtCondition;

    public StatusConditionID SevereStatus => _severeStatus;
    public StatusConditionID VolatileStatus => _volatileStatus;
    public StatusConditionID TransientStatus => _transientStatus;
    public WeatherConditionID Weather => _weather;
    public CourtConditionID CourtCondition => _courtCondition;

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
public class StatStage
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

public enum MoveTarget { Enemy, Self, OpposingSide, AllAdjacent, Ally, }

public enum CritBehavior { none, HighCritRatio, AlwaysCrits, NeverCrits, }

public enum RecoilType { none, RecoilByMaxHP, RecoilByCurrentHP, RecoilByDamage, }

public enum MovePriority { Neg_7, Neg_6, Neg_5, Neg_4, Neg_3, Neg_2, Neg_1, Zero, One, Two, Three, Four, Five, Six, Seven, Eight, Nine }

public enum MoveFlags
{
    Authentic, //--Ignore's Substitute
    Charge, //--User is unable to move between turns
    Contact, //--Makes contact
    Dance, //--Can be copied by Dancer
    Defrost, //--Thaws a frozen user if it doesn't fail. May never use since i use Frostbite instead of freeze
    Gravity, //--Gravity being on the field prevents its use
    Heal, //--Prevented from taking effect during Heal Block
    Jaw, //--For abilities like Strong Jaw and Biting moves
    Mirror, //--Can be copied by Mirror Move. I probably won't implement this nor MM
    Protect, //--Official marks moves blocked by protect with protect. May save myself the time and reverse this so that moves with this flag ignore protect instead. --12/14/25
    Punch, //--For abilities like Iron Fist or items like the Punching Glove
    Recharge, //--User must recharge during their next turn on move success. Hyper Beam
    Reflectable, //--Can be reflected back by Magic Coat or Magic Bounce
    Sound, //--Sound proof

}