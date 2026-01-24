using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleBoardState
{
    public FieldState FieldState { get; set; }
    public CourtState TopCourtState { get; set ; }
    public CourtState BottomCourtState { get; set ; }
}

public class FieldState
{
    
}

public class CourtState
{
    public List<PokemonState> PokemonStates { get; set; }
}

public class PokemonState
{
    
}
