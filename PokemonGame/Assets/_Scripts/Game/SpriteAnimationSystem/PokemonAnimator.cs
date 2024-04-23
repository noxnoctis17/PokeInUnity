using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UIElements;

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
    private bool _isAnimating;

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

        _spriteRenderer = GetComponent<SpriteRenderer>();
        _spriteAnimator = new SpriteAnimator( _spriteRenderer, 0.08f );
        _camera = PlayerReferences.MainCameraTransform;
    }

    private void OnDisable(){
        OnAnimationStateChange -= ChangeAnimationState;
    }

    public void Initialize( PokemonSO pokeSO ){
        //--Default/Initial Animation
        _defaultAnimSheet = _idleDownSprites;
        _currentAnimSheet = _defaultAnimSheet;

        SetAllSpriteSheets( pokeSO );
        // ResetAnimations();
        _initialized = true;
    }

    public void Clear(){
        ResetAnimations();
        _spriteRenderer.sprite = null;
        _spriteRenderer = null;
        _spriteAnimator = null;
        _camera = null;
        _initialized = false;
    }

    private void Update(){
        if( _prevAnimSheet != _currentAnimSheet )
            _spriteAnimator.Start();

        if( _initialized ){
            SetFacingDirection();
            PlayAnimations();
            _prevAnimSheet = _currentAnimSheet;
            _spriteAnimator.HandleUpdate();
        }
    }

    private void LateUpdate(){
        if( _initialized )
            if( !_isAnimating )
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
        //--Sets facing direction (up or down) based on the Pokemon's parent transform forward
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

    //==============================================================================================
    //==================================[ ANIMATIONS ]==============================================
    //==============================================================================================

    public IEnumerator PlayCaptureAnimation( Transform ballTransform ){
        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOFade( 0, 0.5f ) );
        sequence.Join( transform.DOMove( ballTransform.position, 0.5f ) );
        sequence.Join( transform.DOScale( Vector3.zero, 0.5f ) );

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayBreakoutAnimation( Transform originalPos ){
        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOFade( 1, 0.5f ) );
        sequence.Join( transform.DOMove( originalPos.position, 0.5f ) );
        sequence.Join( transform.DOScale( Vector3.one, 0.5f ) );

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayPhysicalAttackAnimation( Transform originalPos, Transform targetPos ){
        Vector3 direction = targetPos.position - transform.position;
        direction.Normalize();
        Vector3 readyPos = targetPos.position - direction;
        Vector3 attackPos = targetPos.position - new Vector3( 0.01f, 0, 0.01f );

        var sequence = DOTween.Sequence();
        sequence.Append( transform.DOMove( readyPos, 0.4f ) ); //--Move to close range Attack position
        sequence.AppendInterval( 0.25f );
        sequence.Append( transform.DOMove( attackPos, 0.15f ) ); //--Move into target's position
        sequence.Append( transform.DOMove( readyPos, 0.15f) ); //--Move back to close range Attack position
        sequence.AppendInterval( 0.1f );
        sequence.Append( transform.DOMove( originalPos.position, 0.25f ) ); //--Move back to default position

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlaySpecialAttackAnimation(){
        yield return transform.DOShakePosition( 0.5f, 1f, 1 ).WaitForCompletion();
    }

    public IEnumerator PlayStatusAttackAnimation(){
        Transform spin180 = transform;
        spin180.rotation = transform.rotation * Quaternion.Euler( 1, 180, 1 );

        _isAnimating = true;
        var sequence = DOTween.Sequence();
        sequence.Append( transform.DOLocalRotate( spin180.position, 0.5f ) );
        sequence.Append( transform.DOLocalRotate( spin180.position, 0.5f ) );
        sequence.Append( transform.DOShakePosition( 0.5f, 0.5f, 5 ) );

        yield return sequence.WaitForCompletion();
        _isAnimating = false;
    }

    public IEnumerator PlayEnterBattleAnimation( Transform originalPos, Transform player ){
        transform.position = player.position;
        _spriteRenderer.DOFade( 0, 0f );
        transform.DOScale( Vector3.zero, 0f );
        
        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOFade( 1, 0.5f ) );
        sequence.Join( transform.DOMove( originalPos.position, 0.5f ) );
        sequence.Join( transform.DOScale( Vector3.one, 0.5f ) );

        yield return sequence.WaitForCompletion();;
    }

    public IEnumerator PlayExitBattleAnimation( Transform player ){
        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOFade( 0, 0.5f ) );
        sequence.Join( transform.DOMove( player.position, 0.5f ) );
        sequence.Join( transform.DOScale( Vector3.zero, 0.5f ) );

        yield return sequence.WaitForCompletion();;
    }

    public IEnumerator PlayFaintAnimation(){
        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOFade( 0, 0.5f ) );
        sequence.Join( transform.DOScale( Vector3.zero, 0.5f ) );

        yield return sequence.WaitForCompletion();
    }

    public void ResetAnimations(){
        transform.localPosition = Vector3.zero;
        _spriteRenderer.DOFade( 1, 0f );
        transform.DOScale( Vector3.one, 0f );
    }
}
