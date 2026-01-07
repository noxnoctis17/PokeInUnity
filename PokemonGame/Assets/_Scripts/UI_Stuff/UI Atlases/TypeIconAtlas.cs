using System.Collections.Generic;
using UnityEngine;

public class TypeIconAtlas : MonoBehaviour
{
    [SerializeField] private Sprite _normal;
    [SerializeField] private Sprite _fire;
    [SerializeField] private Sprite _water;
    [SerializeField] private Sprite _electric;
    [SerializeField] private Sprite _grass;
    [SerializeField] private Sprite _ice;
    [SerializeField] private Sprite _fighting;
    [SerializeField] private Sprite _poison;
    [SerializeField] private Sprite _ground;
    [SerializeField] private Sprite _flying;
    [SerializeField] private Sprite _psychic;
    [SerializeField] private Sprite _bug;
    [SerializeField] private Sprite _rock;
    [SerializeField] private Sprite _ghost;
    [SerializeField] private Sprite _dragon;
    [SerializeField] private Sprite _dark;
    [SerializeField] private Sprite _steel;
    [SerializeField] private Sprite _fairy;

    public static Dictionary<PokemonType, Sprite> TypeIcons;

    private void OnEnable(){
        InitializeDictionary();
    }

    private void InitializeDictionary(){
        TypeIcons = new()
        {
            { PokemonType.None,         null },
            { PokemonType.Normal,       _normal },
            { PokemonType.Fire,         _fire },
            { PokemonType.Water,        _water },
            { PokemonType.Electric,     _electric },
            { PokemonType.Grass,        _grass },
            { PokemonType.Ice,          _ice },
            { PokemonType.Fighting,     _fighting },
            { PokemonType.Poison,       _poison },
            { PokemonType.Ground,       _ground },
            { PokemonType.Flying,       _flying },
            { PokemonType.Psychic,      _psychic },
            { PokemonType.Bug,          _bug },
            { PokemonType.Rock,         _rock },
            { PokemonType.Ghost,        _ghost },
            { PokemonType.Dragon,       _dragon },
            { PokemonType.Dark,         _dark },
            { PokemonType.Steel,        _steel },
            { PokemonType.Fairy,        _fairy },

        };
    }
}
