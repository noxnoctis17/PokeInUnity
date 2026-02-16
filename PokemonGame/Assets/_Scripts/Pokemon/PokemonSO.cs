using System.Collections.Generic;
using UnityEngine;
using System;
using System.Linq;

[CreateAssetMenu( menuName = "Pokemon/New Pokemon" )]
public class PokemonSO : ScriptableObject
{
    [SerializeField] int _dexNO;
    [SerializeField] string _species;
    [SerializeField] int _form;
    [SerializeField] private WildType _wildType;
    public int DexNO => _dexNO;
    public string Species => _species;
    public int Form => _form;
    public WildType WildType => _wildType;

    [TextArea(10, 20)]
    [SerializeField] private string _description;
    public string Description => _description;

#region Base Stats

    [Header("Base Stats")]
    [SerializeField] int _maxHP;
    [SerializeField] int _attack;
    [SerializeField] int _defense;
    [SerializeField] int _spAttack;
    [SerializeField] int _spDefense;
    [SerializeField] int _speed;
    [SerializeField] int _catchRate = 255;
    [SerializeField] int _expYield;
    [SerializeField] int _effortPointsYield;
    [SerializeField] GrowthRate _growthRate;
    [SerializeField] private int _baseFriendship;
    [SerializeField] private int _height;
    [SerializeField] private int _weight;
    [SerializeField] private int _maleRatio;
    [SerializeField] private int _femaleRatio;


    //--Properties
    public int MaxHP                => _maxHP;
    public int Attack               => _attack;
    public int Defense              => _defense;
    public int SpAttack             => _spAttack;
    public int SpDefense            => _spDefense;
    public int Speed                => _speed;
    public int CatchRate            => _catchRate;
    public int ExpYield             => _expYield;
    public int EffortYield          => _effortPointsYield;
    public GrowthRate GrowthRate    => _growthRate;
    public int BaseFriendship => _baseFriendship;
    public int Height => _height;
    public int Weight => _weight;
    public int MaleRatio => _maleRatio;
    public int FemaleRatio => _femaleRatio;

#endregion

    //--------------------------------
#region Sprites
    [Header("Sprites")]
    //--Idle
    [SerializeField] private List<Sprite> _idleUpSprites;
    [SerializeField] private List<Sprite> _idleDownSprites;
    [SerializeField] private List<Sprite> _idleLeftSprites;
    [SerializeField] private List<Sprite> _idleRightSprites;
    [SerializeField] private List<Sprite> _idleUpLeftSprites;
    [SerializeField] private List<Sprite> _idleUpRightSprites;
    [SerializeField] private List<Sprite> _idleDownLeftSprites;
    [SerializeField] private List<Sprite> _idleDownRightSprites;

    //--Idle Getters for Shadows
    public List<Sprite> IdleUpSprites => _idleUpSprites;
    public List<Sprite> IdleDownSprites => _idleDownSprites;
    public List<Sprite> IdleLeftSprites => _idleLeftSprites;
    public List<Sprite> IdleRightSprites => _idleRightSprites;
    public List<Sprite> IdleUpLeftSprites => _idleUpLeftSprites;
    public List<Sprite> IdleUpRightSprites => _idleUpRightSprites;
    public List<Sprite> IdleDownLeftSprites => _idleDownLeftSprites;
    public List<Sprite> IdleDownRightSprites => _idleDownRightSprites;

    //--Walking
    [SerializeField] private List<Sprite> _walkUpSprites;
    [SerializeField] private List<Sprite> _walkDownSprites;

    //--Walking Getters for Shadows
    public List<Sprite> WalkUpSprites => _walkUpSprites;
    public List<Sprite> WalkDownSprites => _walkDownSprites;

    //--Special Attack Sprites

    //--Physical Attack Sprites

    //--Get Hit Sprites

    //--Enter Battle Sprites

    //--Faint Sprites
#endregion

    //--Portraits
    [SerializeField] private Sprite _portraitNormal;
    [SerializeField] private Sprite _portraitHappy;
    [SerializeField] private Sprite _portraitAngry;
    [SerializeField] private Sprite _portraitHurt;
    public Sprite Portrait_Normal => _portraitNormal;
    public Sprite Portrait_Happy => _portraitHappy;
    public Sprite Portrait_Angry => _portraitAngry;
    public Sprite Portrait_Hurt => _portraitHurt;

    //----------------------------------
    
    [Header("Type Assignments")]
    [SerializeField] private PokemonType _type1;
    [SerializeField] private PokemonType _type2;
    public PokemonType Type1 => _type1;
    public PokemonType Type2 => _type2;

    //-----------------------------------

    [Header( "Abilities" )]
    [SerializeField] private List<AbilityID> _abilities;
    public List<AbilityID> Abilities => _abilities;

    //-----------------------------------

    [Header( "Evolutions" )]
    [SerializeField] private List<Evolutions> _evolutions;
    public List<Evolutions> Evolutions => _evolutions;

    [Header( "Learnable Moves" )]
    [SerializeField] private List<LearnableMoves> _learnableMoves;
    [Header( "Teachable Moves" )]
    [SerializeField] private TMDB _tmDB;
    [SerializeField] private List<MoveSO> _tmKeys;
    [SerializeField] private List<bool> _tmValues;
    public List<LearnableMoves> LearnableMoves => _learnableMoves;
    public Dictionary<MoveSO, bool> TeachableMoves { get; private set; }

    public static int MAXLEVEL { get; set; } = 100;
    public static int MAX_ACTIVE_MOVES { get; set; } = 4;

    public bool CanLearn( MoveSO move )
    {
        for( int i = 0; i < _learnableMoves.Count; i++ )
        {
            var levelUpMove = _learnableMoves[i];
            if( levelUpMove.MoveSO == move )
                return true;
        }

        for( int i = 0; i < TeachableMoves.Keys.Count; i++ )
        {
            var tmMove = TeachableMoves.Keys.ElementAt( i );
            if( tmMove == move )
                return true;
        }

        return false;
    }

    public int GetExpForLevel( int level ){

        //--Fast
        if( _growthRate == GrowthRate.Fast ){
            return Mathf.FloorToInt( 4 * Mathf.Pow( level, 3 ) / 5 );
        }

        //--Medium Fast
        else if( _growthRate == GrowthRate.MediumFast ){
            return Mathf.FloorToInt( Mathf.Pow( level, 3 ) );
        }

        //--Medium Slow
        else if( _growthRate == GrowthRate.MediumSlow ){
            if( level == 1 )
                return 0;
            else
                return Mathf.FloorToInt( 6 * Mathf.Pow( level, 3 ) / 5 - 15 * ( level * level ) + 100 * level - 140 );
        }

        //--Slow
        else if( _growthRate == GrowthRate.Slow ){
            return Mathf.FloorToInt( 5 * Mathf.Pow( level, 3 ) / 4 );
        }

        //--Fluctuating
        else if( _growthRate == GrowthRate.Fluctuating ){
            return GetFluctuating( level );
        }

        //--Erratic
        else if ( _growthRate == GrowthRate.Erratic ){
            if ( level < 50 )
                return Mathf.FloorToInt( Mathf.Pow( level, 3f ) * ( 100f - level )  / 50f );
                
            else if (level >= 50 && level < 68)
                return Mathf.FloorToInt( Mathf.Pow( level, 3f ) * ( 150f - level )  / 100f );
                
            else if (level >= 68 && level < 98)
                return Mathf.FloorToInt( Mathf.Pow( level, 3f ) * ( ( 1911f - ( 10f * level ) ) / 3f )  / 500f );
                
            else
                return Mathf.FloorToInt( Mathf.Pow( level, 3f ) * ( 160f - level )  / 100f );
        }

        return -1;
    }

    public int GetFluctuating( int level ){
        if ( level < 15 ){
            return Mathf.FloorToInt( Mathf.Pow( level, 3 ) * ( ( Mathf.Floor( ( level + 1 ) / 3 ) + 24 ) / 50 ) );
        }
        else if ( level >= 15 && level < 36 ){
            return Mathf.FloorToInt( Mathf.Pow( level, 3 ) * ( ( level + 14 ) / 50 ) );
        }
        else{
            return Mathf.FloorToInt( Mathf.Pow( level, 3 ) * ( ( Mathf.Floor( level / 2 ) + 32 ) / 50 ) );
        }
    }

//================================Editor Tool=========================================
#if UNITY_EDITOR
    public void OnEnable()
    {
        if( _tmKeys == null )
            _tmKeys = new();

        if( _tmValues == null )
            _tmValues = new();

        if( _tmDB != null )
            BuildTMDB();
    }

    public void InitFromEditor()
    {
        _species = name;
        _learnableMoves = new();
        _idleUpSprites = new();
        _idleDownSprites = new();
        _idleLeftSprites = new();
        _idleRightSprites = new();
        _idleUpLeftSprites = new();
        _idleUpRightSprites = new();
        _idleDownLeftSprites = new();
        _idleDownRightSprites = new();
    }

    //--Dex Number
    public void SetDexNO( int dexNO )
    {
        _dexNO = dexNO;
    }

    //--Species
    public void SetSpecies( string species )
    {
        _species = species;
    }

    //--Form Index
    public void SetFormIndex( int formIndex )
    {
        _form = formIndex;
    }

    //--Wild Type
    public void SetWildType( WildType type )
    {
        _wildType = type;
    }

    //--Dex Description
    public void SetDexDescription( string desc )
    {
        _description = desc;
    }

    //--Types
    public void SetType1( PokemonType type1 )
    {
        _type1 = type1;
    }

    public void SetType2( PokemonType type2 )
    {
        _type2 = type2;
    }

    //--Base Stats
    public void SetHP( int value )
    {
        _maxHP = value;
    }

    public void SetAttack( int value )
    {
        _attack = value;
    }

    public void SetDefense( int value )
    {
        _defense = value;
    }

    public void SetSpAttack( int value )
    {
        _spAttack = value;
    }

    public void SetSpDefense( int value )
    {
        _spDefense = value;
    }

    public void SetSpeed( int value )
    {
        _speed = value;
    }

    public void SetCatchRate( int value )
    {
        _catchRate = value;
    }

    public void SetEXPYield( int value )
    {
        _expYield = value;
    }

    public void SetEffortYield( int value )
    {
        _effortPointsYield = value;
    }

    public void SetGrowthRate( GrowthRate value )
    {
        _growthRate = value;
    }

    public void SetBaseFriendship( int friend )
    {
        _baseFriendship = friend;
    }

    public void SetHeight( int height )
    {
        _height = height;
    }

    public void SetWeight( int weight )
    {
        _weight = weight;
    }

    public void SetMaleRatio( int male )
    {
        _maleRatio = male;
    }

    public void SetFemaleRatio( int fem )
    {
        _femaleRatio = fem;
    }

    //--Abilities
    public void AddAbility( AbilityID ability )
    {
        _abilities.Add( ability );
    }

    public void SetAbility( int index, AbilityID ability )
    {
        _abilities[index] = ability;
    }

    public void RemoveAbility( int index )
    {
        if ( index < 0 || index >= _abilities.Count )
            return;

        _abilities.RemoveAt( index );
    }

    //--Evolutions
    public void AddEvolution()
    {
        _evolutions.Add( new() );
    }

    public void SetEvolutionPokemon( int index, PokemonSO evo )
    {
        _evolutions[index].SetEvolutionPokemon( evo );
    }

    public void SetEvolutionLevel( int index, int level )
    {
        _evolutions[index].SetEvolutionLevel( level );
    }

    public void SetEvolutionItem( int index, EvolutionItemsSO item )
    {
        _evolutions[index].SetEvolutionItem( item );
    }

    public void SetEvolutionFriendship( int index, int friend )
    {
        _evolutions[index].SetEvolutionFriendship( friend );
    }

    public void SetEvolutionTime( int index, TimeOfDay time )
    {
        _evolutions[index].SetEvolutionTime( time );
    }

    public void RemoveEvolution( int index )
    {
        if ( index < 0 || index >= _evolutions.Count )
            return;

        _evolutions.RemoveAt( index );
    }

    //--Learnable Moves
    public void AddLevelUpMove()
    {
        _learnableMoves.Add( new() );
    }

    public void AddLevelUpMove( LearnableMoves move )
    {
        if( _learnableMoves == null || _learnableMoves.Count == 0 )
            _learnableMoves = new();
            
        _learnableMoves.Add( move );
    }

    public void SetLevelUpMove( int index, MoveSO move )
    {
        _learnableMoves[index].SetMove( move );
    }

    public void SetLevelUpMoveLevel( int index, int level )
    {
        _learnableMoves[index].SetLevelLearned( level );
    }

    public void RemoveLevelUpMove( int index )
    {
        if ( index < 0 || index >= _learnableMoves.Count )
            return;

        _learnableMoves.RemoveAt( index );
    }

    public void SortLevelUpMovesByLevel()
    {
        _learnableMoves.Sort( ( a, b ) =>
        {
            int levelComapre = a.LevelLearned.CompareTo( b.LevelLearned );
            if( levelComapre != 0 )
                return levelComapre;

            if( a.MoveSO == null || b.MoveSO == null )
                return 0;

            return string.Compare( a.MoveSO.Name, b.MoveSO.Name, StringComparison.Ordinal );
        });
    }

    //--Teachable Moves
    public void BuildTMDB()
    {
        TeachableMoves = new();

        for( int i = 0; i < _tmKeys.Count; i++ )
        {
            TeachableMoves[_tmKeys[i]] = _tmValues[i];
        }
    }

    public void SyncTMs( List<MoveSO> tmList, bool allowRemoval = false )
    {
        //--Add new TMs
        foreach( var tm in tmList )
        {
            if( !_tmKeys.Contains( tm ) )
            {
                _tmKeys.Add( tm );
                _tmValues.Add( false );
            }
        }

        BuildTMDB();

        if( !allowRemoval )
            return;

        //--Remove TMs that were removed from the DB
        for( int i = _tmKeys.Count - 1; i >= 0; i-- )
        {
            if( !tmList.Contains( _tmKeys[i] ) )
            {
                _tmKeys.RemoveAt( i );
                _tmValues.RemoveAt( i );
            }
        }

        BuildTMDB();

    }

    public void SetTM( MoveSO tm, bool canLearn )
    {
        int index = _tmKeys.IndexOf( tm );
        if( index == -1 )
            return;

        Debug.Log( $"Setting {tm.Name} Learnable from {TeachableMoves[tm]} to {canLearn} for {Species}" );
        if( TeachableMoves.ContainsKey( tm ) )
        {
            _tmValues[index] = canLearn;
            TeachableMoves[tm] = canLearn;
        }
        else
            Debug.LogError( $"TM not found in database!" );

        Debug.Log( $"{tm.Name} is: {TeachableMoves[tm]} for {Species}" );
    }

    public void SetNormalPortrait( Sprite portrait )
    {
        _portraitNormal = null;
        _portraitNormal = portrait;
    }

    public void SetHappyPortrait( Sprite portrait )
    {
        _portraitHappy = null;
        _portraitHappy = portrait;
    }

    public void SetAngryPortrait( Sprite portrait )
    {
        _portraitAngry = null;
        _portraitAngry = portrait;
    }

    public void SetHurtPortrait( Sprite portrait )
    {
        _portraitHurt = null;
        _portraitHurt = portrait;
    }

    private const int DIRECTION_COUNT = 8;
    //--Sprites are generated counter clockwise from "Down" (6:00)
    private void AddSpriteToDirection( int direction, Sprite sprite, FacingDirection sheetDirection = default )
    {
        switch( direction )
        {
            case 0: _idleDownSprites.Add( sprite );         break;
            case 1: _idleDownRightSprites.Add( sprite );    break;
            case 2: _idleRightSprites.Add( sprite );        break;
            case 3: _idleUpRightSprites.Add( sprite );      break;
            case 4: _idleUpSprites.Add( sprite );           break;
            case 5: _idleUpLeftSprites.Add( sprite );       break;
            case 6: _idleLeftSprites.Add( sprite );         break;
            case 7: _idleDownLeftSprites.Add( sprite );     break;
        }
    }

    public void SetIdleSprites( List<List<Sprite>> sprites )
    {
        ClearIdleSprites();
        
        for( int dir = 0; dir < DIRECTION_COUNT; dir++ )
        {
            foreach( var sprite in sprites[dir] )
            {
                AddSpriteToDirection( dir, sprite );
            }
        }
    }

    private void ClearIdleSprites()
    {
        _idleDownSprites.Clear();
        _idleDownRightSprites.Clear();
        _idleRightSprites.Clear();
        _idleUpRightSprites.Clear();
        _idleUpSprites.Clear();
        _idleUpLeftSprites.Clear();
        _idleLeftSprites.Clear();
        _idleDownLeftSprites.Clear();
    }

#endif

}

[Serializable]
public class LearnableMoves
{
    [Header("Move Pool")]
    [SerializeField] private MoveSO _moveSO;
    [SerializeField] private int _levelLearned;
    public MoveSO MoveSO => _moveSO;
    public int LevelLearned => _levelLearned;

    public LearnableMoves()
    {
        
    }

    public LearnableMoves( LearnableMoves copyMove )
    {
        _moveSO = copyMove.MoveSO;
        _levelLearned = copyMove.LevelLearned;
    }

#if UNITY_EDITOR

    public void SetMove( MoveSO move )
    {
        _moveSO = move;
    }

    public void SetLevelLearned( int level )
    {
        _levelLearned = level;
    }

#endif
}

[Serializable]
public class Evolutions
{
    [SerializeField] private PokemonSO _evolution;
    [SerializeField] private int _evolutionLevel;
    [SerializeField] private EvolutionItemsSO _evolutionItem;
    [SerializeField] private int _friendship;
    [SerializeField] private TimeOfDay _timeOfDay;
    public PokemonSO Pokemon => _evolution;
    public int EvolutionLevel => _evolutionLevel;
    public EvolutionItemsSO EvolutionItem => _evolutionItem;
    public int Friendship => _friendship;
    public TimeOfDay TimeOfDay => _timeOfDay;

#if UNITY_EDITOR

    public void SetEvolutionPokemon( PokemonSO evo )
    {
        _evolution = evo;
    }

    public void SetEvolutionLevel( int level )
    {
        _evolutionLevel = level;
    }

    public void SetEvolutionItem( EvolutionItemsSO item )
    {
        _evolutionItem = item;
    }

    public void SetEvolutionFriendship( int friend )
    {
        _friendship = friend;
    }

    public void SetEvolutionTime( TimeOfDay time )
    {
        _timeOfDay = time;
    }

#endif
}

public enum PokemonAnimationType
{
    Idle,
    Walk,
    Strike,
    Shoot,
    Faint,
}

public enum PokemonType
{
    None,       //--0
    Normal,     //--1
    Fire,       //--2
    Water,      //--3
    Electric,   //--4
    Grass,      //--5
    Ice,        //--6
    Fighting,   //--7
    Poison,     //--8
    Ground,     //--9
    Flying,     //--10
    Psychic,    //--11
    Bug,        //--12
    Rock,       //--13
    Ghost,      //--14
    Dragon,     //--15
    Dark,       //--16
    Steel,      //--17
    Fairy       //--18
}

public enum WildType
{
    Uninterested,
    Curious,
    Aggressive,
    Scared,
    Static,

}

public enum Stat
{
    HP,
    Attack,
    Defense,
    SpAttack,
    SpDefense,
    Speed,

    Accuracy,
    Evasion
}

public enum GrowthRate
{
    Fast,
    MediumFast,
    MediumSlow,
    Slow,
    Fluctuating,
    Erratic,

}

public class TypeChart
{
    static float[][] chart = 
    {
        //--Types                    NOR FIR WAT ELE GRA ICE FIG POI GRO FLY PSY BUG ROC GHO DRA DAR STE FAI

        /*NORMAL*/     new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 0.5f, 0f, 1f, 1f, 0.5f, 1f },
        /*FIRE*/       new float[] { 1f, 0.5f, 0.5f, 1f, 2f, 2f, 1f, 1f, 1f, 1f, 1f, 2f, 0.5f, 1f, 0.5f, 1f, 2f, 1f },
        /*WATER*/      new float[] { 1f, 2f, 0.5f, 1f, 0.5f, 1f, 1f, 1f, 2f, 1f, 1f, 1f, 2f, 1f, 0.5f, 1f, 1f, 1f },
        /*ELECTRIC*/   new float[] { 1f, 1f, 2f, 0.5f, 0.5f, 1f, 1f, 1f, 0f, 2f, 1f, 1f, 1f, 1f, 0.5f, 1f, 1f, 1f },
        /*GRASS*/      new float[] { 1f, 0.5f, 2f, 1f, 0.5f, 1f, 1f, 0.5f, 2f, 0.5f, 1f, 0.5f, 2f, 1f, 0.5f, 1f, 0.5f, 1f },
        /*ICE*/        new float[] { 1f, 0.5f, 0.5f, 1f, 2f, 0.5f, 1f, 1f, 2f, 2f, 1f, 1f, 1f, 1f, 2f, 1f, 0.5f, 1f },
        /*FIGHTING*/   new float[] { 2f, 1f, 1f, 1f, 1f, 2f, 1f, 0.5f, 1f, 0.5f, 0.5f, 0.5f, 2f, 0f, 1f, 2f, 2f, 0.5f },
        /*POISON*/     new float[] { 1f, 1f, 1f, 1f, 2f, 1f, 1f, 0.5f, 0.5f, 1f, 1f, 1f, 0.5f, 0.5f, 1f, 1f, 0f, 2f },
        /*GROUND*/     new float[] { 1f, 2f, 1f, 2f, 0.5f, 1f, 1f, 2f, 1f, 0f, 1f, 0.5f, 2f, 1f, 1f, 1f, 2f, 1f },
        /*FLYING*/     new float[] { 1f, 1f, 1f, 0.5f, 2f, 1f, 2f, 1f, 1f, 1f, 1f, 2f, 0.5f, 1f, 1f, 1f, 0.5f, 1f },
        /*PSYCHIC*/    new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 2f, 2f, 1f, 1f, 0.5f, 1f, 1f, 1f, 1f, 0f, 0.5f, 1f },
        /*BUG*/        new float[] { 1f, 0.5f, 1f, 1f, 2f, 1f, 0.5f, 0.5f, 1f, 0.5f, 2f, 1f, 1f, 0.5f, 1f, 2f, 0.5f, 0.5f },
        /*ROCK*/       new float[] { 1f, 2f, 1f, 1f, 1f, 2f, 0.5f, 1f, 0.5f, 2f, 1f, 2f, 1f, 1f, 1f, 1f, 0.5f, 1f },
        /*GHOST*/      new float[] { 0f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 2f, 1f, 1f, 2f, 1f, 0.5f, 1f, 1f },
        /*DRAGON*/     new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 1f, 2f, 1f, 0.5f, 0f },
        /*DARK*/       new float[] { 1f, 1f, 1f, 1f, 1f, 1f, 0.5f, 1f, 1f, 1f, 2f, 1f, 1f, 2f, 1f, 0.5f, 1f, 0.5f },
        /*STEEL*/      new float[] { 1f, 0.5f, 0.5f, 0.5f, 1f, 2f, 1f, 1f, 1f, 1f, 1f, 1f, 2f, 1f, 1f, 1f, 0.5f, 2f },
        /*FAIRY*/      new float[] { 1f, 0.5f, 1f, 1f, 1f, 1f, 2f, 0.5f, 1f, 1f, 1f, 1f, 1f, 1f, 2f, 2f, 0.5f, 1f },

        //--FUCK

    };

    public static float GetEffectiveness( PokemonType attackType, PokemonType defenseType ){
        if ( attackType == PokemonType.None || defenseType == PokemonType.None )
         return 1;

        int row = (int)attackType - 1;
        int col = (int)defenseType - 1;

        return chart[row][col];
    }
}
