using UnityEngine;

[CreateAssetMenu(menuName = "PokemonGame/TrainerSO")]
public class TrainerSO : ScriptableObject
{
    [SerializeField] private string _name;
    [SerializeField] private TrainerClassEnum _trainerClassEnum;
    
    public TrainerClassEnum TrainerClassEnum => _trainerClassEnum;
    public string TrainerName;
}

public enum TrainerClassEnum{
    AceTrainer,
    Hiker,
    Lass,
    Youngster,
    Swimmer,
    BugCatcher,

}
