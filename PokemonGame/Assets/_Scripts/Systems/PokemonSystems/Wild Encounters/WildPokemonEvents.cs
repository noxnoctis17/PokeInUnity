using UnityEngine;
using System;

public class WildPokemonEvents : MonoBehaviour
{
    public static Action<WildPokemon> OnPlayerEncounter;
    public static Action<WildPokemon> OnPokeSpawned;
    public static Action<WildPokemon> OnPokeDespawned;
}
