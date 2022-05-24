using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PokemonParty : MonoBehaviour
{
    [SerializeField] private List<PokemonClass> _partyPokemon;
    public List<PokemonClass> PartyPokemon => _partyPokemon;

    private void Start()
    {
        foreach(PokemonClass pokemon in _partyPokemon)
        {
            pokemon.Init();
        }
    }

    public PokemonClass GetHealthyPokemon()
    {
        return _partyPokemon.Where(x => x.currentHP > 0).FirstOrDefault();
    }
    
}
