using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class Character_WalkingState : State<CharacterAnimator>
{
    private CharacterAnimator _stateMachine;
    private List<Sprite> _currentAnimSheet;
    [SerializeField] private List<Sprite> _walkDownSprites;
    [SerializeField] private List<Sprite> _walkUpSprites;
    [SerializeField] private List<Sprite> _walkLeftSprites;
    [SerializeField] private List<Sprite> _walkRightSprites;
    [SerializeField] private List<Sprite> _walkUpLeftSprites;
    [SerializeField] private List<Sprite> _walkUpRightSprites;
    [SerializeField] private List<Sprite> _walkDownLeftSprites;
    [SerializeField] private List<Sprite> _walkDownRightSprites;

    public override void EnterState( CharacterAnimator sm ){
        _stateMachine = sm;
        _stateMachine.IsWalking = true;
        _stateMachine.SpriteAnimator.Start();
    }

    public override void UpdateState(){
        MonitorWalking();
    }

    public override void ReturnToState(){
        _stateMachine.IsWalking = true;
    }

    public override void PauseState(){
        _stateMachine.IsWalking = false;
    }

    public override void ExitState(){
        _stateMachine.IsWalking = false;
    }

    private void MonitorWalking(){
        //--Vertical--
        //--Up
        if( _stateMachine.MoveY > 0f && _stateMachine.MoveX == 0f )
            _currentAnimSheet = _walkUpSprites;
        //--Down
        else if( _stateMachine.MoveY < 0f && _stateMachine.MoveX == 0f )
            _currentAnimSheet = _walkDownSprites;
        
        //--Horizontal--
        //--Left
        if( _stateMachine.MoveX < 0f && _stateMachine.MoveY == 0f )
            _currentAnimSheet = _walkLeftSprites;
        //--Right
        else if ( _stateMachine.MoveX > 0f && _stateMachine.MoveY == 0f )
            _currentAnimSheet = _walkRightSprites;

        //--Diagonals--
        //--Up Left
        if( _stateMachine.MoveY >= 0.05f && _stateMachine.MoveX <= -0.05f )
            _currentAnimSheet = _walkUpLeftSprites;
        //--Up Right
        else if( _stateMachine.MoveY >= 0.05f && _stateMachine.MoveX >= 0.05f )
            _currentAnimSheet = _walkUpRightSprites;
        //--Down Left
        else if( _stateMachine.MoveY <= -0.05f && _stateMachine.MoveX <= -0.05f )
            _currentAnimSheet = _walkDownLeftSprites;
        //--Down Right
        else if( _stateMachine.MoveY <= -0.05f && _stateMachine.MoveX >= 0.05f )
            _currentAnimSheet = _walkDownRightSprites;
        //--We're Idle!
        else if( _stateMachine.MoveY == 0 && _stateMachine.MoveX == 0 ){
            _stateMachine.StateMachine.Pop();
        }

        _stateMachine.SetSpriteSheet( _currentAnimSheet );
    }
}
