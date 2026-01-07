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
