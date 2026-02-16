using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

[CreateAssetMenu(menuName = "Pokemon/New Move")]
public class MoveSO : ScriptableObject
{
    [SerializeField] private string _moveName;
    public string Name => _moveName;
    [SerializeField] private bool _hasTM;
    public bool HasTM => _hasTM;

    [TextArea(10, 20)]
    [SerializeField] private string _description;
    public string Description => _description;

    ///
    /// ///////////////////////////////////////////////////////
    ///
    [SerializeField] private MoveCategory _moveCategory;
    [SerializeField] private MoveTarget _moveTarget;
    [SerializeField] PokemonType _moveType;
    [SerializeField] MoveAnimationType _animationType;
    [SerializeField] bool _statOverride;
    [SerializeField] private int _power;
    [SerializeField] private int _accuracy;
    [SerializeField] private AccuracyType _accuracyType;
    [SerializeField] private int _pp = 10;
    [SerializeField] private MovePriority _movePriority = MovePriority.Zero;
    [SerializeField] private CritBehavior _critBehavior;
    [SerializeField] private RecoilMoveEffect _recoil = new();
    [SerializeField] private int _drainPercentage;
    [SerializeField] private HealType _healType;
    [SerializeField] private int _healAmount;
    [SerializeField] private Vector2Int _hitRange;
    [SerializeField] private List<MoveFlags> _flags;
    [SerializeField] private MoveEffects _moveEffects;
    [SerializeField] private List<SecondaryMoveEffects> _secondaryMoveEffects;

    public MoveCategory MoveCategory => _moveCategory;
    public MoveTarget MoveTarget => _moveTarget;
    public PokemonType Type => _moveType;
    public MoveAnimationType AnimationType => _animationType;
    public bool OverrideAttackStat => _statOverride;
    public int Power => _power;
    public int Accuracy => _accuracy;
    public AccuracyType AccuracyType => _accuracyType;
    public int PP => _pp;
    public MovePriority MovePriority => _movePriority;
    public CritBehavior CritBehavior => _critBehavior;
    public RecoilMoveEffect Recoil => _recoil;
    public int DrainPercentage => _drainPercentage;
    public HealType HealType => _healType;
    public int HealAmount => _healAmount;
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

#if UNITY_EDITOR

    public void SetName( string name )
    {
        _moveName = name;
    }

    public void SetPP( int pp )
    {
        _pp = pp;
    }

    public void SetPower( int power )
    {
        _power = power;
    }

    public void SetAccuracy( int acc )
    {
        _accuracy = acc;
    }

    public void SetAccuracyType( AccuracyType type )
    {
        _accuracyType = type;
    }

    public void SetHasTM( bool value )
    {
        _hasTM = value;
    }

    public void SetStatOverride( bool value )
    {
        _statOverride = value;
    }

    public void SetTarget( MoveTarget target )
    {
        _moveTarget = target;
    }

    public void SetType( PokemonType type )
    {
        _moveType = type;
    }

    public void SetCateogry( MoveCategory cat )
    {
        _moveCategory = cat;
    }

    public void SetCriticals( CritBehavior crit )
    {
        _critBehavior = crit;
    }

    public void SetPriority( MovePriority priority )
    {
        _movePriority = priority;
    }

    public void SetAnimationType( MoveAnimationType anim )
    {
        _animationType = anim;
    }

    public void SetRecoilType( RecoilType type )
    {
        _recoil.RecoilType = type;
    }

    public void SetRecoilDamage( int damage )
    {
        _recoil.RecoilDamage = damage;
    }

    public void SetDrainAmount( int drain )
    {
        _drainPercentage = drain;
    }

    public void SetHealType( HealType type )
    {
        _healType = type;
    }

    public void SetHealAmount( int amount )
    {
        _healAmount = amount;
    }

    public void SetHitRange( Vector2Int hits )
    {
        _hitRange = hits;
    }

    public void SetDescription( string desc )
    {
        _description = desc;
    }

    public void AddFlag( MoveFlags flag )
    {
        if( _flags == null || _flags.Count == 0 )
            _flags = new();
            
        _flags.Add( flag );
    }

    public void SetFlag( int index, MoveFlags flag )
    {
        _flags[index] = flag;
    }

    public void RemoveFlag( int index )
    {
        if ( index < 0 || index >= _flags.Count )
            return;

        Flags.RemoveAt( index );
    }

#endif

}

public enum MoveCategory { Physical, Special, Status, Other };
public enum MoveEffectTrigger { PerHit, LastHit }
public enum EffectTarget { Enemy, Self, OpposingSide, AllySide }
public enum AccuracyType { Once, PerHit, AlwaysHits }

[Serializable]
public class MoveEffects
{
    [SerializeField] private EffectTarget _target;
    [SerializeField] private MoveEffectTrigger _trigger;
    //--Stat Modifiers
    [SerializeField] private List<StatStage> _statChangeList;

    //--Severe Status Conditions (PSN, BRN, PAR, SLP, FRZ)
    [SerializeField] private SevereConditionID _severeStatus;
    //--Volatile Status Conditions (Confusion, Affection, etc. )
    [SerializeField] private VolatileConditionID _volatileStatus;
    //--Transient Status Conditions (Flinch, Protect, Endure)
    [SerializeField] private TransientConditionID _transientStatus;
    //--Extra Status Conditions (Sand Tomb, Whirlpool, Fire Spin, who knows )
    [SerializeField] private BindingConditionID _bindingStatus;
    //-Weather Conditions (Harsh Sunlight, Rainfall, Sandstorm, Snowfall)
    [SerializeField] private WeatherConditionID _weather;
    [SerializeField] private TerrainID _terrain;
    //--Court Conditions (Tailwind, Entry Hazards, Screens, etc.)
    [SerializeField] private CourtConditionID _courtCondition;
    [SerializeField] private SwitchEffectType _switchType;

    public EffectTarget Target => _target;
    public MoveEffectTrigger Trigger => _trigger;
    public List<StatStage> StatChangeList => _statChangeList;
    public SevereConditionID SevereStatus => _severeStatus;
    public VolatileConditionID VolatileStatus => _volatileStatus;
    public TransientConditionID TransientStatus => _transientStatus;
    public BindingConditionID BindingStatus => _bindingStatus;
    public WeatherConditionID Weather => _weather;
    public TerrainID Terrain => _terrain;
    public CourtConditionID CourtCondition => _courtCondition;
    public SwitchEffectType SwitchType => _switchType;

    public MoveEffects()
    {
        _statChangeList = new();
    }

    public MoveEffects( EffectTarget target, MoveEffectTrigger trigger, List<StatStage> changeList,
    SevereConditionID severe, VolatileConditionID vol, TransientConditionID trans, BindingConditionID bind,
    WeatherConditionID weather, TerrainID terrain, CourtConditionID court, SwitchEffectType switchEffect )
    {
        _target             = target;
        _trigger            = trigger;
        _statChangeList     = changeList;
        _severeStatus       = severe;
        _volatileStatus     = vol;
        _transientStatus    = trans;
        _bindingStatus      = bind;
        _weather            = weather;
        _terrain            = terrain;
        _courtCondition     = court;
        _switchType         = switchEffect;
    }
}

[Serializable]
public class SecondaryMoveEffects : MoveEffects
{
    [SerializeField] private int _chance;
    public int Chance => _chance;

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

[Serializable]
public class SwitchEffect
{
    public SwitchEffectType SwitchType;
}

public enum MoveTarget { Enemy, Self, OpposingSide, AllAdjacent, Ally, AllySide, All, AllField }

public enum CritBehavior { None, HighCritRatio, AlwaysCrits, NeverCrits, }

public enum RecoilType { None, RecoilByMaxHP, RecoilByCurrentHP, RecoilByDamage, }
public enum HealType { None, PercentOfMaxHP }
public enum SwitchEffectType { None, SelfPivot, ForceOpponentOut, }

public enum MovePriority { Neg_7, Neg_6, Neg_5, Neg_4, Neg_3, Neg_2, Neg_1, Zero, One, Two, Three, Four, Five, Six, Seven, Eight, Nine }

public enum MoveFlags
{
    Authentic, //--Ignore's Substitute
    Charge, //--Move must draw in power on turn 1, turn 2 it is used
    Contact, //--Makes contact
    Dance, //--Can be copied by Dancer
    Defrost, //--Thaws a frozen user if it doesn't fail. May never use since i use Frostbite instead of freeze
    Gravity, //--Gravity being on the field prevents its use
    Heal, //--Prevented from taking effect during Heal Block
    Jaw, //--For abilities like Strong Jaw and Biting moves
    Mirror, //--Can be copied by Mirror Move. I probably won't implement this nor MM
    ProtectIgnore, //--Official marks moves blocked by protect with protect. May save myself the time and reverse this so that moves with this flag ignore protect instead. --12/14/25
    Punch, //--For abilities like Iron Fist or items like the Punching Glove
    Recharge, //--User must recharge during their next turn on move success. Hyper Beam
    Reflectable, //--Can be reflected back by Magic Coat or Magic Bounce
    Sound, //--Sound proof
    Bullet,
    Powder,
    Cutting,
    Wind,
    TwoTurnMove, //--Dig, Fly, Dive, Phantom Force, etc.
}

public enum MoveAnimationType
{
    None,
    Strike,
    Shoot,
    Status,
    Dance,
    Earthquake,
    FakeOut,
    Fast,
    Pivot,
    
}
