using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "New TrainerSO")]
public class TrainerSO : ScriptableObject
{
    [SerializeField] private string _name;
    [SerializeField] private TrainerClasses _trainerClass;
    [SerializeField] private int _skillLevel;
    [SerializeField] private Sprite _portrait;
    [SerializeField] private DialogueColorSO _dialogueColor;
    [SerializeField] private MusicTheme _battleTheme;
    [SerializeField] private List<TrainerPokemon> _party;
    
    public string TrainerName => _name;
    public TrainerClasses TrainerClass => _trainerClass;
    public int SkillLevel => _skillLevel;
    public Sprite Portrait => _portrait;
    public DialogueColorSO DialogueColor => _dialogueColor;
    public MusicTheme BattleTheme => _battleTheme;
    public List<TrainerPokemon> Party => _party;

#if UNITY_EDITOR

    public void InitFromEditor()
    {
        _party = new();
    }

    public void InitParty()
    {
        _party = new();
    }

    public void SetTrainerName( string name )
    {
        _name = name;
    }

    public void SetTrainerClass( TrainerClasses trainerClass )
    {
        _trainerClass = trainerClass;
    }

    public void SetTrainerSkillLevel( int skillLevel )
    {
        _skillLevel = skillLevel;
    }

    public void SetDialogueColor( DialogueColorSO dialogueColor )
    {
        _dialogueColor = dialogueColor;
    }

    public void SetBattleTheme( MusicTheme theme )
    {
        _battleTheme = theme;
    }

#endif
}

public enum TrainerAnimationType { Idle, Walk, Throw, Jump, }

public enum TrainerClasses
{
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
