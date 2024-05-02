using System.Collections.Generic;
using System.Runtime.Remoting.Messaging;
using UnityEngine;

[CreateAssetMenu(menuName = "Pokemon/New Pokemon")]
public class PokemonSO : ScriptableObject
{
    [SerializeField] private PokemonSpecies _species;
    public PokemonSpecies Species => _species;
    [SerializeField] string _name;
    public string pName => _name;
    [SerializeField] private WildType _wildType;
    public WildType WildType => _wildType;

    [TextArea(10, 20)]
    [SerializeField] private string _description;
    public string Description => _description;

    //--------------------------------

    [Header("Sprites")]
    //--Idle
    [SerializeField] private List<Sprite> _idleUpSprites;
    [SerializeField] private List<Sprite> _idleDownSprites;

    //--Idle Getters for Shadows
    public List<Sprite> IdleUpSprites => _idleUpSprites;
    public List<Sprite> IdleDownSprites => _idleDownSprites;

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

    //--Battle Portrait
    [SerializeField] private Sprite _battlePortrait;
    public Sprite BattlePortrait => _battlePortrait;

    //----------------------------------
    
    [Header("Type Assignments")]
    [SerializeField] PokemonType _type1;
    [SerializeField] PokemonType _type2;
    public PokemonType Type1 => _type1;
    public PokemonType Type2 => _type2;

    //-----------------------------------

    //--Base Stats baaaaaaybbbeeeeeeeeeeeee
    [Header("Base Stats")]
    [SerializeField] int _maxHP;
    [SerializeField] int _attack;
    [SerializeField] int _defense;
    [SerializeField] int _spAttack;
    [SerializeField] int _spDefense;
    [SerializeField] int _speed;
    [SerializeField] int _catchRate = 255;
    [SerializeField] int _expYield;
    [SerializeField] GrowthRate _growthRate;
    [SerializeField] int _effortPointsYield;

    //--Base Stat Getters
    public int MaxHP => _maxHP;
    public int Attack => _attack;
    public int Defense => _defense;
    public int SpAttack => _spAttack;
    public int SpDefense => _spDefense;
    public int Speed => _speed;
    public int CatchRate => _catchRate;
    public int ExpYield => _expYield;
    public GrowthRate GrowthRate => _growthRate;
    public int EffortYield => _effortPointsYield;

    [SerializeField] private List<LearnableMoves> _learnableMoves;
    public List<LearnableMoves> LearnableMoves => _learnableMoves;

    public static int MAXLEVEL { get; set; } = 100;
    public static int MAXMOVES { get; set; } = 4;

    public int GetExpForLevel( int level ){
        if( _growthRate == GrowthRate.Fast ){
            return Mathf.FloorToInt( 4 * Mathf.Pow( level, 3 ) / 5 );
        }
        else if( _growthRate == GrowthRate.MediumFast ){
            return Mathf.FloorToInt( Mathf.Pow( level, 3 ) );
        }

        return -1;
    }

}

//-------------------------------------------------------------------------
//-----------------------------[ MOVE POOL ]-------------------------------
//-------------------------------------------------------------------------

[System.Serializable]
public class LearnableMoves
{
    [Header("Move Pool")]
    [SerializeField] private MoveBaseSO _moveSO;
    [SerializeField] private int _levelLearned;
    public MoveBaseSO MoveSO => _moveSO;
    public int LevelLearned => _levelLearned;
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

public class TypeCardColors
{
    public Color PrimaryColor { get; set; }
    public Color SecondaryColor { get; set; }

    public TypeCardColors( Color primaryColor, Color secondaryColor){
        PrimaryColor = primaryColor;
        SecondaryColor = secondaryColor;
    }
}

public enum PokemonSpecies
{
    Snasee,
    Rebellinum,
    Knighinum,
    Vulpix,
    Eevee,
    Wingull,
    Roserade,
    Lilligant,
    Meouchie,
    Meomber,
    Meormor,
}
