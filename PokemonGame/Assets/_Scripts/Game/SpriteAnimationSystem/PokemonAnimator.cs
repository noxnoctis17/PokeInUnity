using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using UnityEditor.Experimental.GraphView;
using UnityEngine;

    //==[STATES]==
public enum PokeAnimationState{
    Idle, Walking, Attack, Evolving,
}

public class PokemonAnimator : MonoBehaviour
{
    //==[REFERENCES]==
    private SpriteRenderer _spriteRenderer;
    private SpriteAnimator _spriteAnimator;
    public SpriteAnimator Animator => _spriteAnimator;
    private Transform _camera;
    [SerializeField] private Transform _pokemonTransform; //--Main Parent Transform
    [SerializeField] private bool _disableBillboarding;
    private BattleSystem _battleSystem;
    public BattleSystem BattleSys => _battleSystem;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;
    public Transform PokemonTransform => _pokemonTransform;

    //==[ACTIONS]==
    public Action<PokeAnimationState> OnAnimationStateChange;

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

    private PokeAnimationState _animState;

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
        SetAllSpriteSheets( pokeSO );

        //--Default/Initial Animation
        _defaultAnimSheet = _idleDownSprites;
        _currentAnimSheet = _defaultAnimSheet;
        _camera = PlayerReferences.MainCameraTransform;

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

        if( !_disableBillboarding )
            if( _initialized )
                if( !_isAnimating )
                    Billboard();
    }

    private void Billboard(){
        transform.forward = _camera.forward;
    }

    public void ChangeAnimationState( PokeAnimationState state ){
        _animState = state;
    }

    private void PlayAnimations(){
        switch( _animState ){
            case PokeAnimationState.Idle:
                SetIdleSprites();

            break;

            case PokeAnimationState.Walking:
                SetIdleSprites(); //--Change to Walking eventually

            break;

            case PokeAnimationState.Evolving:
                AssignAnimations( _currentAnimSheet );
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

    public void SetStatusColor( Color color ){
        _spriteRenderer.color = color;
    }

    public IEnumerator PlayEvolutionAnimation( PokemonSO pokemon, PokemonSO evolution ){
        yield return null;
        int cycles = 10;
        float speed = 0.25f;

        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOColor( Color.black, 0.5f ) );

        for( int i = 1; i < cycles + 1; i++ ){
            sequence.Append( transform.DOScale( 1f, speed / i ) );

            if( i % 2 == 1 )
                sequence.AppendCallback( () => _currentAnimSheet = evolution.IdleDownSprites );
            else
                sequence.AppendCallback( () => _currentAnimSheet = pokemon.IdleDownSprites );
            
            sequence.Append( transform.DOScale( 1f, speed / i ) );
        }

        sequence.AppendCallback( () => _currentAnimSheet = evolution.IdleDownSprites );
        sequence.Join( _spriteRenderer.DOColor( Color.white, 0.5f ) );

        yield return sequence.WaitForCompletion();

    }

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

    public IEnumerator PlayPhysicalAttackAnimation( Transform originalPos, Transform targetPos, TweenCallback cameraCallback = null ){
        Vector3 direction = targetPos.position - _pokemonTransform.position;
        direction.Normalize();
        Vector3 readyPos = targetPos.position - direction;
        Vector3 attackPos = targetPos.position - new Vector3( 0.01f, 0, 0.01f );

        var sequence = DOTween.Sequence();
        sequence.Append( _pokemonTransform.DOMove( readyPos, 0.75f ) );             //--Move to close range Attack position
        sequence.AppendInterval( 0.25f );                                           //--Wait
        sequence.Append( _pokemonTransform.DOMove( attackPos, 0.15f ) );            //--"Hit" target

        if( cameraCallback != null )
            sequence.AppendCallback( cameraCallback );                              //--If camera stuff isn't null, trigger the callback to change cameras in the BattleComposer

        sequence.Append( _pokemonTransform.DOMove( readyPos, 0.15f) );              //--Move back to close range Attack position
        sequence.AppendInterval( 0.1f );                                            //--Wait
        sequence.Append( _pokemonTransform.DOMove( originalPos.position, 0.25f ) ); //--Move back to default position

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlaySpecialAttackAnimation( Transform targetPos ){
        Vector3 originalPos = _pokemonTransform.position;
        Vector3 direction = _pokemonTransform.position - targetPos.position;
        direction.Normalize();
        Vector3 attackPos = _pokemonTransform.position - direction;

        var sequence = DOTween.Sequence();
        sequence.Append( _pokemonTransform.DOMove( attackPos, 0.75f ) ).WaitForCompletion();
        sequence.AppendInterval( 0.25f );
        sequence.Append( _pokemonTransform.DOShakePosition( 0.5f, 0.25f, 8 ) ).WaitForCompletion();
        sequence.AppendInterval( 0.1f );
        sequence.Append( _pokemonTransform.DOMove( originalPos, 0.25f ) ).WaitForCompletion();
        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayStatusAttackAnimation(){
        Vector3 front = _pokemonTransform.position + _pokemonTransform.forward * 1f;
        Vector3 behind = _pokemonTransform.position - _pokemonTransform.forward * 1f;

        var sequence = DOTween.Sequence();
        sequence.Append( _pokemonTransform.DOJump( _pokemonTransform.position, 1, 1, 0.5f ) );
        sequence.Join( _pokemonTransform.DODynamicLookAt( behind, 0.5f ) );
        sequence.Append( _pokemonTransform.DOJump( _pokemonTransform.position, 1, 1, 0.5f ) );
        sequence.Join( _pokemonTransform.DODynamicLookAt( front, 0.5f ) );

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayEnterBattleAnimation( Transform originalPos, Transform player ){
        transform.position = player.position;
        _spriteRenderer.DOFade( 0, 0f );
        transform.DOScale( Vector3.zero, 0f );
        
        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOFade( 1, 0.5f ) );
        sequence.Join( transform.DOMove( originalPos.position, 0.5f ) );
        sequence.Join( transform.DOScale( Vector3.one, 0.5f ) );

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayExitBattleAnimation( Transform player ){
        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOFade( 0, 0.5f ) );
        sequence.Join( transform.DOMove( player.position, 0.5f ) );
        sequence.Join( transform.DOScale( Vector3.zero, 0.5f ) );

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayTakeDamageAnimation()
    {
        WaitForSeconds flicker = new( 0.1f );
        _spriteRenderer.enabled = false;
        yield return flicker;
        _spriteRenderer.enabled = true;
        yield return flicker;
        _spriteRenderer.enabled = false;
        yield return flicker;
        _spriteRenderer.enabled = true;
        yield return flicker;

        yield return new WaitForSeconds( 0.25f );
    }

    public IEnumerator PlayFaintAnimation(){
        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOFade( 0, 0.5f ) );
        sequence.Join( transform.DOScale( Vector3.zero, 0.5f ) );

        yield return sequence.WaitForCompletion();
    }

    public void ResetAnimations(){
        transform.localPosition = Vector3.zero;
        _spriteRenderer.color = Color.white;
        _spriteRenderer.DOFade( 1, 0f );
        transform.DOScale( Vector3.one, 0f );
    }
}
