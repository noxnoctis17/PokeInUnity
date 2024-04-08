using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;

public class CharacterAnimator : MonoBehaviour
{
    //==[REFERENCES]==
    private SpriteRenderer _spriteRenderer;
    private SpriteAnimator _spriteAnimator;
    private Transform _camera;
    private Transform _playerTransform;


    //==[SPRITES]==
    //--Current Sheet
    private List<Sprite> _currentAnimSheet;
    //--Break Glass Incase of Emergency
    private List<Sprite> _defaultAnimSheet;

    //--Idle
    [SerializeField] private List<Sprite> _idleUpSprites;
    [SerializeField] private List<Sprite> _idleDownSprites;
    [SerializeField] private List<Sprite> _idleLeftSprites;
    [SerializeField] private List<Sprite> _idleRightSprites;
    [SerializeField] private List<Sprite> _idleUpLeftSprites;
    [SerializeField] private List<Sprite> _idleUpRightSprites;
    [SerializeField] private List<Sprite> _idleDownLeftSprites;
    [SerializeField] private List<Sprite> _idleDownRightSprites;

    //--Idle Getters for Shadows
    public List<Sprite> IdleUpSprites => _idleUpSprites;
    public List<Sprite> IdleDownSprites => _idleDownSprites;
    public List<Sprite> IdleLeftSprites => _idleLeftSprites;
    public List<Sprite> IdleRightSprites => _idleRightSprites;
    public List<Sprite> IdleUpLeftSprites => _idleUpLeftSprites;
    public List<Sprite> IdleUpRightSprites => _idleUpRightSprites;
    public List<Sprite> IdleDownLeftSprites => _idleDownLeftSprites;
    public List<Sprite> IdleDownRightSprites => _idleDownRightSprites;

    //--Walking
    [SerializeField] private List<Sprite> _walkDownSprites;
    [SerializeField] private List<Sprite> _walkUpSprites;
    [SerializeField] private List<Sprite> _walkLeftSprites;
    [SerializeField] private List<Sprite> _walkRightSprites;
    [SerializeField] private List<Sprite> _walkUpLeftSprites;
    [SerializeField] private List<Sprite> _walkUpRightSprites;
    [SerializeField] private List<Sprite> _walkDownLeftSprites;
    [SerializeField] private List<Sprite> _walkDownRightSprites;

    //--Running
    // [SerializeField] private List<Sprite> _runDownSprites;
    // [SerializeField] private List<Sprite> _runUpSprites;
    // [SerializeField] private List<Sprite> _runLeftSprites;
    // [SerializeField] private List<Sprite> _runRightSprites;
    // [SerializeField] private List<Sprite> _runDownLeftSprites;
    // [SerializeField] private List<Sprite> _runDownRightSprites;
    // [SerializeField] private List<Sprite> _runUpLeftSprites;
    // [SerializeField] private List<Sprite> _runUpRightSprites;


    //==[PARAMETERS]==
    public float MoveX { get; set; }
    public float MoveY { get; set; }
    public bool IsWalking { get; set; }
    public bool IsRunning { get; set; } //--Implement running lol
    private bool _wasWalking;

    //==[ROTATION STEP CONSTANT]==
    private const float _rotationStep = 22.5f;

    public enum SpritePerspective {
        Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight,
    }

    private SpritePerspective _spritePerspective;

    public enum FacingDirection {
        Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight,
    }

    private FacingDirection _facingDirection;


    //==[STATES]==
    /*
    I think I should be able to further iterate on this by adding proper States (via behavior tree)
    that further automates the sheet that gets passed to the SpriteAnimator instance, making this much
    more "modular" and more easily expandable
    */


    private void Start(){
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _spriteAnimator = new SpriteAnimator( _spriteRenderer );
        _camera = PlayerReferences.MainCameraTransform;
        _playerTransform = PlayerReferences.Instance.PlayerTransform;

        //--Default/Initial Animation
        _defaultAnimSheet = _idleDownSprites;
        _currentAnimSheet = _defaultAnimSheet;

        AssignAnimations( _currentAnimSheet );
    }

    private void Update(){
        var previousAnimSheet = _currentAnimSheet;
        SetSpritePerspective();
        SetWalkingSprites();

        if( _currentAnimSheet != previousAnimSheet || IsWalking != _wasWalking )
            _spriteAnimator.Start();

        if( IsWalking ){
            _spriteAnimator.HandleUpdate();
        }else{
            SetIdleSprites();
            AssignAnimations( _currentAnimSheet );
            _spriteAnimator.Start();
            _spriteAnimator.HandleUpdate();
        }

        _wasWalking = IsWalking;
    }

    private void LateUpdate(){
        Billboard();
    }

    private void Billboard(){
        transform.forward = _camera.forward;
    }

    private void AssignAnimations( List<Sprite> animation ){
        if( animation.Count > 0 )
            _spriteAnimator.AnimationFrames = animation;
        else
            _spriteAnimator.AnimationFrames = _defaultAnimSheet;
    }

    //--Somehow this Just Worked™
    private void SetSpritePerspective(){
        //--Sets facing direction based on the player transform forward
        var projection = Vector3.ProjectOnPlane( _camera.transform.forward, _playerTransform.up );
        var angle = Vector3.SignedAngle( projection, _playerTransform.forward, _playerTransform.up );
        var absAngle = Mathf.Abs( angle );

        if( absAngle <= _rotationStep )
            _spritePerspective = SpritePerspective.Up;
        else if( absAngle <= _rotationStep * 3 )
            _spritePerspective = Mathf.Sign( angle ) < 0 ? SpritePerspective.UpLeft     : _spritePerspective = SpritePerspective.UpRight;
        else if( absAngle <= _rotationStep * 5 )
            _spritePerspective = Mathf.Sign( angle ) < 0 ? SpritePerspective.Left       : _spritePerspective = SpritePerspective.Right;
        else if( absAngle <= _rotationStep * 7 )
            _spritePerspective = Mathf.Sign( angle ) < 0 ? SpritePerspective.DownLeft   : _spritePerspective = SpritePerspective.DownRight;
        else
            _spritePerspective = SpritePerspective.Down;
    }

    //--This is to set the shadow facing direction based on the character's forward angle. i don't know math tho lol
    private void GetCharacterForwardDirection(){
        var absAngle = Mathf.Abs( _playerTransform.forward.y );

        if( absAngle > _rotationStep )
            _facingDirection = FacingDirection.Up;
        else if( absAngle <= _rotationStep * 3 )
            _facingDirection = Mathf.Sign( absAngle ) < 0 ? FacingDirection.UpLeft : _facingDirection = FacingDirection.UpRight;
        else if( absAngle <= _rotationStep * 5 )
            _facingDirection = Mathf.Sign( absAngle ) < 0 ? FacingDirection.Left : _facingDirection = FacingDirection.Right;
        else if( absAngle <= _rotationStep * 7 )
            _facingDirection = Mathf.Sign( absAngle ) < 0 ? FacingDirection.DownLeft : _facingDirection = FacingDirection.DownRight;
    }

    private void SetIdleSprites(){
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
    }

    private void SetWalkingSprites(){
        //--Vertical--
        //--Up
        if( MoveY > 0f && MoveX == 0f )
            _currentAnimSheet = _walkUpSprites;
        //--Down
        else if( MoveY < 0f && MoveX == 0f )
            _currentAnimSheet = _walkDownSprites;
        
        //--Horizontal--
        //--Left
        if( MoveX < 0f && MoveY == 0f )
            _currentAnimSheet = _walkLeftSprites;
        //--Right
        else if ( MoveX > 0f && MoveY == 0f )
            _currentAnimSheet = _walkRightSprites;

        //--Diagonals--
        //--Up Left
        if( MoveY >= 0.05f && MoveX <= -0.05f )
            _currentAnimSheet = _walkUpLeftSprites;
        //--Up Right
        else if( MoveY >= 0.05f && MoveX >= 0.05f )
            _currentAnimSheet = _walkUpRightSprites;
        //--Down Left
        else if( MoveY <= -0.05f && MoveX <= -0.05f )
            _currentAnimSheet = _walkDownLeftSprites;
        //--Down Right
        else if( MoveY <= -0.05f && MoveX >= 0.05f )
            _currentAnimSheet = _walkDownRightSprites;

        AssignAnimations( _currentAnimSheet );
    }

    //=======================================================================================
    //===========================[ ANIMATIONS & THINGS ]=====================================
    //=======================================================================================

    public IEnumerator JumpToBattlePosition( Transform player, Transform battlePosition ){
        yield return player.DOJump( battlePosition.position, 1, 1, 0.5f ).WaitForCompletion();
    }
}
