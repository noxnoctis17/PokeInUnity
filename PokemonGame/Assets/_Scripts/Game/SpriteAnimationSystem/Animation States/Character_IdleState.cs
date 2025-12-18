using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class Character_IdleState : State<CharacterAnimator>
{
    private CharacterAnimator _stateMachine;
    private SpritePerspective _spritePerspective;
    private List<Sprite> _currentAnimSheet;
    [SerializeField] private List<Sprite> _idleUpSprites;
    [SerializeField] private List<Sprite> _idleDownSprites;
    [SerializeField] private List<Sprite> _idleLeftSprites;
    [SerializeField] private List<Sprite> _idleRightSprites;
    [SerializeField] private List<Sprite> _idleUpLeftSprites;
    [SerializeField] private List<Sprite> _idleUpRightSprites;
    [SerializeField] private List<Sprite> _idleDownLeftSprites;
    [SerializeField] private List<Sprite> _idleDownRightSprites;

    public override void EnterState( CharacterAnimator sm ){
        _stateMachine = sm;
        _stateMachine.OnSpritePerspectiveChanged += ChangePerspective;
        _stateMachine.SpriteAnimator.Start();
    }

    public override void UpdateState(){
        
    }

    public override void ReturnToState(){
        _stateMachine.OnSpritePerspectiveChanged += ChangePerspective;
        _stateMachine.SpriteAnimator.Start();
        ChangePerspective( _stateMachine.SpritePerspective );
    }

    public override void PauseState(){
        _stateMachine.OnSpritePerspectiveChanged -= ChangePerspective;
    }

    public override void ExitState(){
        _stateMachine.OnSpritePerspectiveChanged -= ChangePerspective;
    }

    private void ChangePerspective( SpritePerspective perspective ){
        _spritePerspective = perspective;

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
