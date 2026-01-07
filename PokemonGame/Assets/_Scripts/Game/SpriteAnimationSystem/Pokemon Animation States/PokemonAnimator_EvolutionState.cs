using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class PokemonAnimator_EvolutionState : State<PokemonAnimator>
{
    private PokemonAnimator _stateMachine;
    private List<Sprite> _currentAnimSheet;
    private List<Sprite> _currentMonSprites;
    private List<Sprite> _evolveIntoSprites;

    public override void EnterState( PokemonAnimator owner )
    {
        _stateMachine = owner;
    }

    public void SetSprites( PokemonSO currentMon, PokemonSO evolution )
    {
        _currentMonSprites = currentMon.IdleDownSprites;
        _evolveIntoSprites = evolution.IdleDownSprites;
    }

    public void CurrentMon()
    {
        _currentAnimSheet = _currentMonSprites;
        _stateMachine.SetSpriteSheet( _currentAnimSheet );
    }

    public void Evolution()
    {
        _currentAnimSheet = _evolveIntoSprites;
        _stateMachine.SetSpriteSheet( _currentAnimSheet );
    }
}
