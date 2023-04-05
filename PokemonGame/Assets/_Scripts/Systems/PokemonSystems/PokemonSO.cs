using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Pokemon/New Pokemon")]
public class PokemonSO : ScriptableObject
{
    [SerializeField] string _name;
    public string pName => _name;
    [SerializeField] private WildType _wildType;
    public WildType WildType => _wildType;

    [TextArea(10, 20)]
    [SerializeField] private string _description;
    public string Description => _description;

    //--------------------------------

    [Header("Sprites")]
    [SerializeField] private Sprite _frontSprite;
    [SerializeField] private Sprite _backSprite;
    [SerializeField] private Sprite _battlePortrait;
    public Sprite FrontSprite => _frontSprite;
    public Sprite BackSprite => _backSprite;
    public Sprite BattlePortrait => _battlePortrait;

    //----------------------------------
    
    [Header("Type Assignments")]
    [SerializeField] PokemonType _type1;
    [SerializeField] PokemonType _type2;
    public PokemonType Type1 => _type1;
    public PokemonType Type2 => _type2;

    //-----------------------------------

    //Base Stats baaaaaaybbbeeeeeeeeeeeee
    [Header("Base Stats")]
    [SerializeField] int _maxHP;
    [SerializeField] int _maxPP;
    [SerializeField] int _attack;
    [SerializeField] int _defense;
    [SerializeField] int _spAttack;
    [SerializeField] int _spDefense;
    [SerializeField] int _speed;

    //Base Stat Getters
    public int MaxHP => _maxHP;
    public int MaxPP => _maxPP;
    public int Attack => _attack;
    public int Defense => _defense;
    public int SpAttack => _spAttack;
    public int SpDefense => _spDefense;
    public int Speed => _speed;

    [SerializeField] private List<LearnableMoves> _learnableMoves;
    public List<LearnableMoves> LearnableMoves => _learnableMoves;

}

//-------------------------------------------------------------------------
//-------------------------------MOVE POOL---------------------------------
//-------------------------------------------------------------------------

[System.Serializable]
public class LearnableMoves
{
    

    [Header("Move Pool")]
    [SerializeField] private MoveBaseSO _moveBase;
    [SerializeField] private int _levelLearned;
    public MoveBaseSO MoveBase => _moveBase;
    public int LevelLearned => _levelLearned;

}

public enum PokemonType
{
    None,
    Normal,
    Fire,
    Water,
    Electric,
    Grass,
    Ice,
    Fighting,
    Poison,
    Ground,
    Flying,
    Psychic,
    Bug,
    Rock,
    Ghost,
    Dragon,
    Dark,
    Steel,
    Fairy
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
