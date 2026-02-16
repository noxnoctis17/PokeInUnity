using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class TrainerPokemon
{
    [SerializeField] private PokemonSO _pokemon;
    [SerializeField] private string _nickName;
    [SerializeField] private int _level;
    [SerializeField] private NatureID _nature;
    [SerializeField] private AbilityID _ability;
    [SerializeField] private ItemSO _heldItem;
    [SerializeField] private int _hpEVs;
    [SerializeField] private int _attackEVs;
    [SerializeField] private int _defenseEVs;
    [SerializeField] private int _spattackEVs;
    [SerializeField] private int _spdefenseEVs;
    [SerializeField] private int _speedEVs;
    [SerializeField] PokeBallType _ball = PokeBallType.PokeBall;
    [SerializeField] private List<MoveSO> _moves;

    public PokemonSO PokeSO => _pokemon;
    public string NickName => _nickName;
    public int Level => _level;
    public NatureID Nature => _nature;
    public AbilityID AbilityID => _ability;
    public ItemSO HeldItem => _heldItem;
    public int HP_EVs => _hpEVs;
    public int Atk_EVs =>_attackEVs;
    public int Def_EVs => _defenseEVs;
    public int SpAtk_EVs => _spattackEVs;
    public int SpDef_EVs => _spdefenseEVs;
    public int Spe_EVs => _speedEVs;
    public PokeBallType Ball => _ball;
    public List<MoveSO> Moves => _moves;

    public TrainerPokemon( PokemonSO pokeSO, string nickName, int level, NatureID nature, AbilityID abilityID, ItemSO heldItem, int hp, int atk, int def, int spatk, int spdef, int spe, PokeBallType ball, List<MoveSO> moves )
    {
        _pokemon        = pokeSO;
        _nickName       = nickName;
        _level          = level;
        _nature         = nature;
        _ability        = abilityID;
        _heldItem       = heldItem;
        _hpEVs          = hp;
        _attackEVs      = atk;
        _defenseEVs     = def;
        _spattackEVs    = spatk;
        _spdefenseEVs   = spdef;
        _speedEVs       = spe;
        _ball           = ball;
        _moves          = moves;
    }
}
