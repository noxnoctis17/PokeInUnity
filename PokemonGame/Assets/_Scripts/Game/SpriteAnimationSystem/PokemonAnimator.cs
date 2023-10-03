using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PokemonAnimator : MonoBehaviour
{
    //==[REFERENCES]==
    private SpriteRenderer _spriteRenderer;
    private SpriteAnimator _spriteAnimator;
    private Transform _camera;
    private Transform _pokemonTransform; //--so you don't forget, this is the main transform that rotates. there should be objects for sprite and shadow too like Catch
    private PokemonSO _pokemonSO;

    //==[SPRITES]==
    //--Current Sheet
    private List<Sprite> _currentAnimSheet;
    //--Break Glass Incase of Emergency
    private List<Sprite> _defaultAnimSheet;

    //--Idle
    [SerializeField] private List<Sprite> _idleUpSprites;
    [SerializeField] private List<Sprite> _idleDownSprites;

    //--Idle Getters for Shadows
    public List<Sprite> IdleUpSprites => _idleUpSprites;
    public List<Sprite> IdleDownSprites => _idleDownSprites;

    //--Walking
    [SerializeField] private List<Sprite> _walkUpSprites;
    [SerializeField] private List<Sprite> _walkDownSprites;

    //--Walking Getters for Shadows
    public List<Sprite> WalkUpSprites => _walkUpSprites;
    public List<Sprite> WalkDownSprites => _walkDownSprites;

    //--Special Attack Sprites

    //--Physical Attack Sprites

    //--Get Hit Sprites

    //--Enter Battle Sprites

    //--Faint Sprites


    //==[PARAMETERS]==
    public float MoveX { get; set; }
    public float MoveY { get; set; }
    public bool IsWalking { get; set; }
    private bool _wasWalking;

    //==[ROTATION STEP CONSTANT]==
    private const float _rotationStep = 90f;

    public enum FacingDirection{
        Up, Down,
    }

    private FacingDirection _facingDirection;

    //==[STATES]==

    private void Start(){
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _spriteAnimator = new SpriteAnimator( _spriteRenderer );
        _camera = PlayerReferences.MainCameraTransform;
        _pokemonTransform = GetComponentInParent<Transform>();

        //--Assign Sprites from Pokemon Scriptable Object


        //--Default/Initial Animation
        _defaultAnimSheet = _idleDownSprites;
        _currentAnimSheet = _defaultAnimSheet;

        // AssignAnimations( _currentAnimSheet );
    }

    private void Update(){
        var previousAnimSheet = _currentAnimSheet;
        // SetFacingDirection();
        // SetWalkingSprites();

        if( _currentAnimSheet != previousAnimSheet || IsWalking != _wasWalking )
            _spriteAnimator?.Start();
        
        if( IsWalking ){
            _spriteAnimator?.HandleUpdate();
        }else{
            SetIdleSprites();
            AssignAnimations( _currentAnimSheet );
            _spriteAnimator?.Start();
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
        if( animation.Count >= 1 )
            _spriteAnimator.AnimationFrames = animation;
        else
            _spriteAnimator.AnimationFrames = _defaultAnimSheet;
    }

    private void SetFacingDirection(){
        //--Sets facing direction based on the Pokemon's parent transform forward
        var projection = Vector3.ProjectOnPlane( _camera.transform.forward, _pokemonTransform.up );
        var angle = Vector3.SignedAngle( projection, _pokemonTransform.forward, _pokemonTransform.up );
        var absAngle = Mathf.Abs( angle );

        if( absAngle <= _rotationStep )
            _facingDirection = FacingDirection.Up;
        else
            _facingDirection = FacingDirection.Down;
    }

    private void SetIdleSprites(){
        //--Assigns Idle Sprites based on facing direction/parent transform forward
        switch( _facingDirection ){
            case FacingDirection.Up:
                _currentAnimSheet = _idleUpSprites;
            
            break;

            case FacingDirection.Down:
                _currentAnimSheet = _idleDownSprites;

            break;
        }
    }

    private