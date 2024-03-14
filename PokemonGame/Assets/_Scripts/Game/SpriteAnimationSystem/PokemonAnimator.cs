using System;
using System.Collections.Generic;
using UnityEngine;

public class PokemonAnimator : MonoBehaviour
{
    //==[REFERENCES]==
    private SpriteRenderer _spriteRenderer;
    private SpriteAnimator _spriteAnimator;
    public SpriteAnimator Animator => _spriteAnimator;
    private Transform _camera;
    [SerializeField] private Transform _pokemonTransform; //--Main Parent Transform
    private BattleSystem _battleSystem;
    public BattleSystem BattleSys => _battleSystem;

    //==[ACTIONS]==
    public Action<AnimationState> OnAnimationStateChange;

    //==[SPRITES]==
    //--Current Sheet
    private List<Sprite> _currentAnimSheet;
    //--Break Glass Incase of Emergency
    private List<Sprite> _defaultAnimSheet;
    private List<Sprite> _prevAnimSheet;

    //--Idle
    private List<Sprite> _idleUpSprites;
    private List<Sprite> _idleDownSprites;

    //--Idle Getters for Shadows
    public List<Sprite> IdleUpSprites => _idleUpSprites;
    public List<Sprite> IdleDownSprites => _idleDownSprites;

    //--Walking
    private List<Sprite> _walkUpSprites;
    private List<Sprite> _walkDownSprites;

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
    public bool WasWalking => _wasWalking;
    private bool _initialized;

    //==[ROTATION STEP CONSTANT]==
    private const float _rotationStep = 90f;

    public enum FacingDirection{
        Up, Down,
    }

    private FacingDirection _facingDirection;
    public FacingDirection FacingDir => _facingDirection;

    //==[STATES]==
    public enum AnimationState{
        Idle, Walking, Attack,
    }

    private AnimationState _animState;

    private void OnEnable(){
        OnAnimationStateChange += ChangeAnimationState;
    }

    private void OnDisable(){
        OnAnimationStateChange -= ChangeAnimationState;
    }

    private void Start(){
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _spriteAnimator = new SpriteAnimator( _spriteRenderer, 0.08f );
        _camera = PlayerReferences.MainCameraTransform;

        //--Default/Initial Animation
        _defaultAnimSheet = _idleDownSprites;
        _currentAnimSheet = _defaultAnimSheet;

        // AssignAnimations( _currentAnimSheet );
    }

    public void Initialize( PokemonSO pokeSO ){
        if( !_initialized ){
            SetAllSpriteSheets( pokeSO );
        }
    
        // _initialized = true;
    }

    private void Update(){
        if( _prevAnimSheet != _currentAnimSheet ){
            _spriteAnimator.Start();
        }

        SetFacingDirection();
        PlayAnimations();
        _prevAnimSheet = _currentAnimSheet;
        _spriteAnimator.HandleUpdate();
    }

    private void LateUpdate(){
        Billboard();
    }

    private void Billboard(){
        transform.forward = _camera.forward;
    }

    private void ChangeAnimationState( AnimationState state ){
        _animState = state;
    }

    private void PlayAnimations(){
        switch( _animState ){
            case AnimationState.Idle:
                SetIdleSprites();

            break;

            case AnimationState.Walking:
                SetIdleSprites(); //--Change to Walking eventually

            break;

            default:
                SetIdleSprites();

            break;

        }
    }

    private void SetAllSpriteSheets( PokemonSO pokeSO ){
        if( pokeSO != null ){
            //--Idle
            _idleUpSprites = pokeSO.IdleUpSprites;
            _idleDownSprites = pokeSO.IdleDownSprites;

            //--Walking
            _walkUpSprites = pokeSO.IdleUpSprites; //--make sure to set these to the walking sprites eventually lol
            _walkDownSprites = pokeSO.IdleDownSprites; //--make sure to set these to the walking sprites eventually lol

            //--Special Attack Sprites

            //--Physical Attack Sprites

            //--Get Hit Sprites

            //--Enter Battle Sprites

            //--Faint Sprites
        }
    }

    //--Temporary measure until battle state is made
    //--Battle State will likely end up being set the same way, however
    public void SetBattleSystem( BattleSystem battleSystem ){
        _battleSystem = battleSystem;
    }

    private void AssignAnimations( List<Sprite> animation ){
        if( animation?.Count > 0 ) //--for some reason entering a battle keeps breaking this, returning null despite...not being null?
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
        else if( absAngle >= _rotationStep )
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

            default:
                _currentAnimSheet = _defaultAnimSheet;

            break;
        }

        AssignAnimations( _currentAnimSheet );
    }

    private void SetWalkingSprites(){
        //--Vertical
        //--Up

        //--Down
    }
}
