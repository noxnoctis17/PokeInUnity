using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NoxNoctisDev.StateMachine;
using Unity.VisualScripting;
using UnityEngine;

public class PokemonAnimator : MonoBehaviour
{
    //==[STATE MACHINE]==
    public StateStackMachine<PokemonAnimator> StateMachine;
    [SerializeField] private State<PokemonAnimator> _idleState;
    [SerializeField] private State<PokemonAnimator> _walkingState;
    [SerializeField] private PokemonAnimator_EvolutionState _evolutionState;
    private Vector3 _originalPos;
    private SpriteRenderer _spriteRenderer;
    private SpriteAnimator _spriteAnimator;
    public SpriteAnimator SpriteAnimator => _spriteAnimator;
    private Transform _camera;
    [SerializeField] private Transform _pokemonTransform; //--Main Parent Transform
    [SerializeField] private bool _disableBillboarding;
    private BattleSystem _battleSystem;
    public BattleSystem BattleSys => _battleSystem;
    public SpriteRenderer SpriteRenderer => _spriteRenderer;
    public Transform PokemonTransform => _pokemonTransform;
    public PokemonSO PokeSO { get; private set; }

    //==[MATERIAL]==
    private MaterialPropertyBlock _materialPB;

    //==[SPRITES]==
    //--Current Sheet
    private List<Sprite> _currentAnimSheet;
    //--Break Glass Incase of Emergency
    private List<Sprite> _defaultAnimSheet;

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
    private const float _rotationStep = 22.5f;

    public SpritePerspective SpritePerspective { get; private set; }

    private void OnEnable(){

        _spriteRenderer = GetComponent<SpriteRenderer>();
        _spriteAnimator = new SpriteAnimator( _spriteRenderer, 0.16f ); //--0.08f
        _camera = PlayerReferences.MainCameraTransform;
    }

    public void Initialize( PokemonSO pokeSO ){
        _materialPB = new();
        _spriteRenderer.GetPropertyBlock( _materialPB );

        _originalPos = _pokemonTransform.position;

        PokeSO = pokeSO;
        SetAllSpriteSheets( PokeSO );

        StateMachine = new( this );
        StateMachine.Push( _idleState );

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
        if( _initialized )
        {
            StateMachine.Update();
            SpriteAnimator.HandleUpdate();
            // DetectWalking();
            SetSpritePerspective();
            Billboard();
        }
    }

    private void Billboard(){
        transform.forward = _camera.forward;
    }

    private void SetAllSpriteSheets( PokemonSO pokeSO ){
        if( pokeSO != null ){
            //--Idle Sprites
            var idleState = _idleState as IPokemonAnimator_SpriteSetter;
            idleState.SetSprites( pokeSO );

            //--Walking Sprites
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

    private void SetSpritePerspective(){
        //--Sets facing direction (up or down) based on the Pokemon's parent transform forward
        var projection = Vector3.ProjectOnPlane( _camera.transform.forward, _pokemonTransform.up );
        var angle = Vector3.SignedAngle( projection, _pokemonTransform.forward, _pokemonTransform.up );
        var absAngle = Mathf.Abs( angle );

        if( absAngle <= _rotationStep )
            SpritePerspective = SpritePerspective.Up;
        else if( absAngle <= _rotationStep * 3 )
            SpritePerspective = Mathf.Sign( angle ) < 0 ? SpritePerspective.UpLeft     : SpritePerspective = SpritePerspective.UpRight;
        else if( absAngle <= _rotationStep * 5 )
            SpritePerspective = Mathf.Sign( angle ) < 0 ? SpritePerspective.Left       : SpritePerspective = SpritePerspective.Right;
        else if( absAngle <= _rotationStep * 7 )
            SpritePerspective = Mathf.Sign( angle ) < 0 ? SpritePerspective.DownLeft   : SpritePerspective = SpritePerspective.DownRight;
        else
            SpritePerspective = SpritePerspective.Down;

        // OnSpritePerspectiveChanged?.Invoke( _facingDirection );
    }

    public void SetSpriteSheet( List<Sprite> sprites )
    {
        if( sprites == null )
            _currentAnimSheet = PokeSO.IdleDownSprites;
        else if( sprites != null && sprites.Count == 0 )
            _currentAnimSheet = PokeSO.IdleDownSprites;
        else
            _currentAnimSheet = sprites;

        AssignAnimations( _currentAnimSheet );
    }

    //==============================================================================================
    //==================================[ ANIMATIONS ]==============================================
    //==============================================================================================

    public void SetStatusColor( Color color ){
        _spriteRenderer.color = color;
    }

    public IEnumerator PlayBeginEvolutionAnimation( PokemonSO current, PokemonSO evolution )
    {
        StateMachine.Push( _evolutionState );
        _evolutionState.SetSprites( current, evolution );
        _evolutionState.CurrentMon();
        yield return null;
    }

    public IEnumerator PlayEvolutionAnimation()
    {
        int cycles = 10;
        float speed = 0.25f;

        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOColor( Color.black, 0.5f ) ).WaitForCompletion();

        for( int i = 1; i < cycles + 1; i++ ){
            sequence.Append( transform.DOScale( 0.5f, speed / i ) );

            if( i % 2 == 1 )
                sequence.AppendCallback( () => _evolutionState.Evolution() ).WaitForCompletion();
            else
                sequence.AppendCallback( () => _evolutionState.CurrentMon() ).WaitForCompletion();
            
            sequence.Append( transform.DOScale( 1f, speed / i ) );
        }

        sequence.AppendCallback( () => _evolutionState.Evolution() );
        sequence.Join( _spriteRenderer.DOColor( Color.white, 0.5f ) );

        yield return sequence.WaitForCompletion();

    }

    public IEnumerator PlayCaptureAnimation( Transform ballTransform )
    {
        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOFade( 0, 0.5f ) );
        sequence.Join( transform.DOMove( ballTransform.position, 0.5f ) );
        sequence.Join( transform.DOScale( Vector3.zero, 0.5f ) );

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayStatChangeAnimation( Texture2D texture, int direction )
    {
        yield return null;
        float speed = 0;
        float duration = 1f;
        float timer = 0f;

        _spriteRenderer.GetPropertyBlock( _materialPB );
        _materialPB.SetTexture( "_StatChangeTexture", texture );
        _materialPB.SetFloat( "_StatChangeVisibility", 1f );
        _materialPB.SetFloat( "_ScrollSpeed", speed );
        _spriteRenderer.SetPropertyBlock( _materialPB );

        if( direction > 0 )
            speed = -2f;
        else if( direction < 0 )
            speed = 2f;

        while( timer < duration )
        {
            _materialPB.SetFloat( "_ScrollSpeed", speed );
            _spriteRenderer.SetPropertyBlock( _materialPB );

            timer += Time.deltaTime;
            yield return null;
        }

        _spriteRenderer.GetPropertyBlock( _materialPB );
        _materialPB.SetFloat( "_StatChangeVisibility", 0f );
        _materialPB.SetFloat( "_ScrollSpeed", 0f );
        _spriteRenderer.SetPropertyBlock( _materialPB );
    }

    public IEnumerator PlayBreakoutAnimation( Transform originalPos )
    {
        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOFade( 1, 0.5f ) );
        sequence.Join( transform.DOMove( originalPos.position, 0.5f ) );
        sequence.Join( transform.DOScale( Vector3.one, 0.5f ) );

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayMoveIntoStrikePosition( Transform targetPos )
    {
        Vector3 direction = targetPos.position - _pokemonTransform.position;
        direction.Normalize();
        Vector3 readyPos = targetPos.position - direction;

        var sequence = DOTween.Sequence();
        _pokemonTransform.DOMove( readyPos, 0.75f ).WaitForCompletion();   //--Move to Strike position
        sequence.AppendInterval( 0.5f );
        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayMoveIntoShootPosition( Transform targetPos )
    {
        Vector3 direction = _pokemonTransform.position - targetPos.position;
        direction.Normalize();
        Vector3 readyPos = _pokemonTransform.position - direction;

        var sequence = DOTween.Sequence();
        _pokemonTransform.DOMove( readyPos, 0.75f ).WaitForCompletion();
        sequence.AppendInterval( 0.5f );
        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayMoveIntoEarthquakePosition( Transform targetPos )
    {
        var sequence = DOTween.Sequence();
        sequence.Append( _pokemonTransform.DOJump( targetPos.position, 3f, 1, 1f ) ).WaitForCompletion();
        sequence.AppendInterval( 0.25f ).WaitForCompletion();

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayReturnToDefaultPosition()
    {
        Debug.Log( $"{PokeSO.Species} is returning to default position" );
        if( _pokemonTransform.position == _originalPos || _originalPos == null )
            yield return null;
        else
            yield return _pokemonTransform.DOMove( _originalPos, 0.25f ).WaitForCompletion(); //--Move back to default position
    }

    public IEnumerator PlayStrikeAnimation( Transform targetPos, TweenCallback cameraCallback = null ){
        Vector3 direction = targetPos.position - _pokemonTransform.position;
        direction.Normalize();
        Vector3 readyPos = targetPos.position - direction;
        Vector3 attackPos = targetPos.position - new Vector3( 0.01f, 0, 0.01f );

        var sequence = DOTween.Sequence();
        sequence.AppendInterval( 0.25f );
        sequence.Append( _pokemonTransform.DOMove( attackPos, 0.15f ) );            //--"Hit" target

        if( cameraCallback != null )
            sequence.AppendCallback( cameraCallback );                              //--If camera stuff isn't null, trigger the callback to change cameras in the BattleComposer

        sequence.Append( _pokemonTransform.DOMove( readyPos, 0.15f) );              //--Move back to close range Attack position
        sequence.AppendInterval( 0.25f );
        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayShootAnimation( Transform targetPos, TweenCallback cameraCallback = null  )
    {
        Vector3 direction = _pokemonTransform.position - targetPos.position;
        direction.Normalize();
        Vector3 attackPos = _pokemonTransform.position - direction;

        var sequence = DOTween.Sequence();
        sequence.AppendInterval( 0.25f );
        sequence.Append( _pokemonTransform.DOMove( attackPos, 0.75f ) ).WaitForCompletion();
        sequence.AppendInterval( 0.25f );
        sequence.Append( _pokemonTransform.DOShakePosition( 0.5f, 0.25f, 8 ) ).WaitForCompletion();
        sequence.AppendCallback( cameraCallback ).WaitForCompletion();
        sequence.AppendInterval( 0.1f );
        sequence.Append( _pokemonTransform.DOMove( _originalPos, 0.25f ) ).WaitForCompletion();
        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayStatusAttackAnimation()
    {
        Vector3 front = _pokemonTransform.position + _pokemonTransform.forward * 1f;
        Vector3 behind = _pokemonTransform.position - _pokemonTransform.forward * 1f;

        var sequence = DOTween.Sequence();
        sequence.Append( _pokemonTransform.DOJump( _pokemonTransform.position, 1, 1, 0.5f ) );
        sequence.Join( _pokemonTransform.DODynamicLookAt( behind, 0.5f ) );
        sequence.Append( _pokemonTransform.DOJump( _pokemonTransform.position, 1, 1, 0.5f ) );
        sequence.Join( _pokemonTransform.DODynamicLookAt( front, 0.5f ) );

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayEarthquakeAnimation( Transform targetPos, TweenCallback cameraCallback = null  )
    {
        var cam = PlayerReferences.MainCameraTransform.GetComponent<Camera>();
        var sequence = DOTween.Sequence();

        sequence.AppendCallback( cameraCallback );
        sequence.Append( _pokemonTransform.DOJump( targetPos.position, 2, 1, 0.25f ) ).WaitForCompletion();
        sequence.Join( cam.DOShakePosition( 0.25f ) ).WaitForCompletion();
        sequence.Append( _pokemonTransform.DOJump( targetPos.position, 2, 1, 0.25f ) ).WaitForCompletion();
        sequence.Join( cam.DOShakePosition( 0.25f ) ).WaitForCompletion();
        sequence.Append( _pokemonTransform.DOJump( targetPos.position, 2, 1, 0.25f ) ).WaitForCompletion();
        sequence.Join( cam.DOShakePosition( 0.25f ) ).WaitForCompletion();
        sequence.AppendCallback( cameraCallback ).WaitForCompletion();

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayFastAnimation( Transform targetPos )
    {
        yield return null;
        Vector3 direction = targetPos.position - _pokemonTransform.position;
        direction.Normalize();
        Vector3 attackPos = targetPos.position + new Vector3( 0f, 0f, 1f );

        var sequence = DOTween.Sequence();
        sequence.AppendInterval( 0.25f );
        sequence.Append( _pokemonTransform.DOMove( attackPos, 0.1f ) ).WaitForCompletion();
        sequence.AppendInterval( 0.5f );
        sequence.Append( _pokemonTransform.DOMove( _originalPos, 0.01f ) ).WaitForCompletion();

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayFakeOutAnimation( Transform targetPos, TweenCallback cameraCallback = null  )
    {
        Vector3 direction = targetPos.position - _pokemonTransform.position;
        direction.Normalize();
        Vector3 readyPos = _pokemonTransform.position + new Vector3( direction.x + 0.5f, direction.y, direction.z + 0.5f );
        Vector3 fakeoutPos = readyPos + new Vector3( direction.x - 1f, direction.y, direction.z + 0.5f );
        Vector3 attackPos = targetPos.position - new Vector3( 0.01f, 0, 0.01f );

        var sequence = DOTween.Sequence();
        sequence.AppendInterval( 0.25f );
        sequence.Append( _pokemonTransform.DOMove( readyPos, 0.25f ) ).WaitForCompletion();
        sequence.AppendInterval( 0.25f );
        sequence.Append( _pokemonTransform.DOMove( fakeoutPos, 0.25f ) ).WaitForCompletion();
        sequence.AppendInterval( 0.25f );
        sequence.Append( _pokemonTransform.DOMove( attackPos, 0.25f ) ).WaitForCompletion();
        sequence.AppendInterval( 0.25f );
        sequence.Append( _pokemonTransform.DOMove( _originalPos, 0.01f ) ).WaitForCompletion();

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayPivotAnimation( Transform targetPos, TweenCallback cameraCallback = null  )
    {
        Vector3 attackPos = targetPos.position - new Vector3( 0.01f, 0, 0.01f );

        var sequence = DOTween.Sequence();
        sequence.AppendInterval( 0.25f );
        sequence.Append( _pokemonTransform.DOMove( attackPos, 0.25f ) ).WaitForCompletion();
        sequence.AppendInterval( 0.25f );
        sequence.Append( _pokemonTransform.DOJump( _originalPos, 1, 1, 0.5f ) ).WaitForCompletion();
        sequence.AppendInterval( 0.25f );

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayEnterBattleAnimation( Transform originalPos, Transform trainer ){
        transform.position = trainer.position;
        _spriteRenderer.DOFade( 0, 0f );
        transform.DOScale( Vector3.zero, 0f );
        
        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOFade( 1, 0.5f ) );
        sequence.Join( transform.DOMove( originalPos.position, 0.5f ) );
        sequence.Join( transform.DOScale( Vector3.one, 0.5f ) );

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayExitBattleAnimation( Transform trainer ){
        var sequence = DOTween.Sequence();
        sequence.Append( _spriteRenderer.DOFade( 0, 0.5f ) );
        sequence.Join( transform.DOMove( trainer.position, 0.5f ) );
        sequence.Join( transform.DOScale( Vector3.zero, 0.5f ) );

        yield return sequence.WaitForCompletion();
    }

    public IEnumerator PlayHealAnimation()
    {
        var currentColor = _spriteRenderer.color;
        yield return null;
        var sequence = DOTween.Sequence();

        sequence.Append( _spriteRenderer.DOColor( Color.green, 0.25f ) ).WaitForCompletion();
        sequence.AppendInterval( 0.1f );
        sequence.Append( _spriteRenderer.DOColor( currentColor, 0.25f ) ).WaitForCompletion();

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

#if UNITY_EDITOR

public void OnDrawGizmos(){
        Debug.DrawRay( new Vector3( _pokemonTransform.position.x, _pokemonTransform.position.y + 0.75f, _pokemonTransform.position.z ), transform.forward * 2f, Color.red );
    }

#endif
}
