using UnityEngine;

[System.Serializable]
public class WildEncounter
{
    [SerializeField] private PokemonSO _pokeSO;
    [SerializeField] private int _level;
    public PokemonSO PokeSO => _pokeSO;
    public int Level => _level;

}
