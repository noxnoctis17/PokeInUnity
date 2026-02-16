using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu( menuName = "Pokemon/New Rental Team" )]
public class RentalTeamSO : ScriptableObject
{
    [SerializeField] private List<TrainerPokemon> _rentalTeam;
    public List<TrainerPokemon> RentalTeam => _rentalTeam;

    public List<Pokemon> BuildParty()
    {
        List<Pokemon> party = new();

        for( int i = 0; i < _rentalTeam.Count; i++ )
        {
            Pokemon pokemon = new( _rentalTeam[i] );
            party.Add( pokemon );
        }

        return party;
    }

#if UNITY_EDITOR

    public void InitTeam()
    {
        _rentalTeam = new();
    }

#endif
}
