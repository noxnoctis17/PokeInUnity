using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "PokemonGame/TrainerSO")]
public class TrainerSO : ScriptableObject
{
    [SerializeField] private string _name;
    [SerializeField] private TrainerClasses _trainerClass;
    [SerializeField] private Sprite _portrait;
    
    public TrainerClasses TrainerClass => _trainerClass;
    public string TrainerName => _name;
    public Sprite Portrait => _portrait;
}

public enum TrainerClasses{
    AceTrainer,
    Hiker,
    Lass,
    Youngster,
    Swimmer,
    BugCatcher,
    GymLeader,
    EliteFour,
    Champion,
    Trainer,
    None,
}

[Serializable]
public class TrainerPokemon
{
    [SerializeField] private PokemonSO _pokemon;
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
    [SerializeField] private List<MoveSO> _moves;
}
