using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class PokemonAnimator_IdleState : State<PokemonAnimator>, IPokemonAnimator_SpriteSetter
{
    private PokemonAnimator _stateMachine;
    private SpritePerspective _spritePerspective;
    private List<Sprite> _currentAnimSheet;
    private List<Sprite> _idleUpSprites;
    private List<Sprite> _idleDownSprites;
    private List<Sprite> _idleLeftSprites;
    private List<Sprite> _idleRightSprites;
    private List<Sprite> _idleUpLeftSprites;
    private List<Sprite> _idleUpRightSprites;
    private List<Sprite> _idleDownLeftSprites;
    private List<Sprite> _idleDownRightSprites;

    public override void EnterState( PokemonAnimator sm ){
        _stateMachine = sm;
        // _stateMachine.OnSpritePerspectiveChanged += ChangePerspective;
        // _stateMachine.SpriteAnimator.Start();
    }

    public override void UpdateState(){
        ChangePerspective();
    }

    public override void ReturnToState(){
        // _stateMachine.OnSpritePerspectiveChanged += ChangePerspective;
        // _stateMachine.SpriteAnimator.Start();
        // ChangePerspective( _stateMachine.SpritePerspective );
    }

    public override void PauseState(){
        // _stateMachine.OnSpritePerspectiveChanged -= ChangePerspective;
    }

    public override void ExitState(){
        // _stateMachine.OnSpritePerspectiveChanged -= ChangePerspective;
    }

    public void SetSprites( PokemonSO pokeSO )
    {
        _idleUpSprites = pokeSO.IdleUpSprites;
        _idleDownSprites = pokeSO.IdleDownSprites;
        _idleLeftSprites = pokeSO.IdleLeftSprites;
        _idleRightSprites = pokeSO.IdleRightSprites;
        _idleUpLeftSprites = pokeSO.IdleUpLeftSprites;
        _idleUpRightSprites = pokeSO.IdleUpRightSprites;
        _idleDownLeftSprites = pokeSO.IdleDownLeftSprites;
        _idleDownRightSprites = pokeSO.IdleDownRightSprites;
    }

    private void ChangePerspective(){
        _spritePerspective = _stateMachine.SpritePerspective;

         //--Assigns idle sprites based on facing direction/transform forward
        switch( _spritePerspective ){
            case SpritePerspective.Up:
                _currentAnimSheet = _idleUpSprites;

            break;

            case SpritePerspective.Down:
                _currentAnimSheet = _idleDownSprites;

            break;

            case SpritePerspective.Left:
                _currentAnimSheet = _idleLeftSprites;

            break;

            case SpritePerspective.Right:
                _currentAnimSheet = _idleRightSprites;

            break;

            case SpritePerspective.UpLeft:
                _currentAnimSheet = _idleUpLeftSprites;

            break;

            case SpritePerspective.UpRight:
                _currentAnimSheet = _idleUpRightSprites;

            break;

            case SpritePerspective.DownLeft:
                _currentAnimSheet = _idleDownLeftSprites;

            break;

            case SpritePerspective.DownRight:
                _currentAnimSheet = _idleDownRightSprites;

            break;

        }

        _stateMachine.SetSpriteSheet( _currentAnimSheet );
    }
}
