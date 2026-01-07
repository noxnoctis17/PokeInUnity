using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class Character_LedgeHopState : State<CharacterAnimator>
{
    private CharacterAnimator _stateMachine;
    private SpritePerspective _spritePerspective;
    private List<Sprite> _currentAnimSheet;
    [SerializeField] private List<Sprite> _ledgeHopDownSprites;
    [SerializeField] private List<Sprite> _ledgeHopUpSprites;
    [SerializeField] private List<Sprite> _ledgeHopLeftSprites;
    [SerializeField] private List<Sprite> _ledgeHopRightSprites;
    [SerializeField] private List<Sprite> _ledgeHopUpLeftSprites;
    [SerializeField] private List<Sprite> _ledgeHopUpRightSprites;
    [SerializeField] private List<Sprite> _ledgeHopDownLeftSprites;
    [SerializeField] private List<Sprite> _ledgeHopDownRightSprites;

    public override void EnterState( CharacterAnimator sm ){
        _stateMachine = sm;
        _stateMachine.SpriteAnimator.Start();
        ChangePerspective();
    }

    public override void UpdateState(){
        
    }

    public override void ReturnToState(){
        _stateMachine.SpriteAnimator.Start();
        ChangePerspective();
    }

    public override void PauseState(){

    }

    public override void ExitState(){

    }

    private void ChangePerspective(){
        _spritePerspective = _stateMachine.SpritePerspective;

         //--Assigns idle sprites based on facing direction/transform forward
        switch( _spritePerspective ){
            case SpritePerspective.Up:
                _currentAnimSheet = _ledgeHopUpSprites;

            break;

            case SpritePerspective.Down:
                _currentAnimSheet = _ledgeHopDownSprites;

            break;

            case SpritePerspective.Left:
                _currentAnimSheet = _ledgeHopLeftSprites;

            break;

            case SpritePerspective.Right:
                _currentAnimSheet = _ledgeHopRightSprites;

            break;

            case SpritePerspective.UpLeft:
                _currentAnimSheet = _ledgeHopUpLeftSprites;

            break;

            case SpritePerspective.UpRight:
                _currentAnimSheet = _ledgeHopUpRightSprites;

            break;

            case SpritePerspective.DownLeft:
                _currentAnimSheet = _ledgeHopDownLeftSprites;

            break;

            case SpritePerspective.DownRight:
                _currentAnimSheet = _ledgeHopDownRightSprites;

            break;

        }

        if( _currentAnimSheet.Count == 0 )
            _currentAnimSheet = _ledgeHopDownSprites;

        _stateMachine.SetSpriteSheet( _currentAnimSheet );
    }
}
