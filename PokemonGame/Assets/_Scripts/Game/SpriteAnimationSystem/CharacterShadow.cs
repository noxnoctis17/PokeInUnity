using System.Collections.Generic;
using UnityEngine;

public class CharacterShadow : MonoBehaviour
{
    private Transform _sunTransform;
    private SpriteAnimator _spriteAnimator;
    [SerializeField] private CharacterAnimator _characterAnimator;
    private SpriteRenderer _spriteRenderer;

    private List<Sprite> _currentAnimSheet;
    private List<Sprite> _defaultAnimSheet;

    private float _moveX;
    private float _moveY;
    private bool _wasWalking;

    public enum FacingDirection {
        Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight,
    }

    private FacingDirection _facingDirection;

    private void Start(){
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _spriteAnimator = new SpriteAnimator( _spriteRenderer );
        _sunTransform = PlayerReferences.Instance.SunTransform;
        _defaultAnimSheet = _characterAnimator.IdleDownSprites;
        _currentAnimSheet = _defaultAnimSheet;
    }

    private void Update(){
        var previousAnimSheet = _currentAnimSheet;
        UpdateMovement();
        CalcShadowFacingDirection();
        SetIdleShadowSprites();

        if( _currentAnimSheet != previousAnimSheet || _characterAnimator.IsWalking != _wasWalking )
            _spriteAnimator.Start();

        if( _characterAnimator.IsWalking ){
            _spriteAnimator.HandleUpdate();
        }else{
            // SetIdleSprites();
            AssignAnimations( _currentAnimSheet );
            _spriteAnimator.Start();
            _spriteAnimator?.HandleUpdate();
        }

        _wasWalking = _characterAnimator.IsWalking;
    }

    private void LateUpdate(){
        Billboard( transform, _sunTransform );
    }

    private void Billboard( Transform shadow, Transform directionalLight ){
        shadow.forward = directionalLight.forward;
    }

    private void UpdateMovement(){
        _moveX = _characterAnimator.MoveX;
        _moveY = _characterAnimator.MoveY;
    }

    private void AssignAnimations( List<Sprite> animation ){
        if( animation.Count > 0 || animation != null )
            _spriteAnimator.AnimationFrames = animation;
        else
            _spriteAnimator.AnimationFrames = _defaultAnimSheet;
    }

    private void CalcShadowFacingDirection(){
        //--Position Update
        Vector3 moveDirection;
        moveDirection.x = _characterAnimator.MoveX;
        moveDirection.y = 0.0f;
        moveDirection.z = _characterAnimator.MoveY;

        //--Current rotation
        Quaternion currentRotation = transform.rotation;

        if( _characterAnimator.IsWalking ){
            Quaternion targetRotation = Quaternion.LookRotation( moveDirection.MovementAxisCorrection( PlayerReferences.MainCameraTransform ) );

            transform.rotation = Quaternion.Slerp( currentRotation, targetRotation, 1f ); //--1f is rotations per frame
        }
    }

    private void SetIdleShadowSprites(){
        //--Assign Shadow sprites based on last movement direction to keep appropriate shape perspective
        if( _moveX == 0 && _moveY == 1 )
            _currentAnimSheet = _characterAnimator.IdleUpSprites;
        else if( _moveX == 0 && _moveY == -1 )
            _currentAnimSheet = _characterAnimator.IdleDownSprites;
        else if( _moveX == -1 && _moveY == 0 )
            _currentAnimSheet = _characterAnimator.IdleLeftSprites;
        else if( _moveX == 1 && _moveY == 0 )
            _currentAnimSheet = _characterAnimator.IdleRightSprites;

        else if( _moveX == -0.1f && _moveY == 0.1f )
            _currentAnimSheet = _characterAnimator.IdleUpLeftSprites;
        else if( _moveX == 0.1f && _moveY == 0.1f )
            _currentAnimSheet = _characterAnimator.IdleUpRightSprites;
        else if( _moveX == -0.1f && _moveY == -0.1f )
            _currentAnimSheet = _characterAnimator.IdleDownLeftSprites;
        else if( _moveX == 0.1f && _moveY == -0.1f )
            _currentAnimSheet = _characterAnimator.IdleDownRightSprites;
        else
            _currentAnimSheet = _defaultAnimSheet;

        AssignAnimations( _currentAnimSheet );
    }

}
