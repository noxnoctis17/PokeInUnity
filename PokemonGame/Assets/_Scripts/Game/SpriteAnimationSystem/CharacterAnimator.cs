using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public enum FacingDirection { Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight, }
public enum SpritePerspective { Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight, }

public class CharacterAnimator : MonoBehaviour
{
    //==[REFERENCES]==
    private SpriteRenderer _spriteRenderer;
    public SpriteAnimator SpriteAnimator { get; private set; }
    private Transform _camera;
    [SerializeField] private Transform _parentTransform;

    //==[STATE MACHINE]==
    public StateStackMachine<CharacterAnimator> StateMachine;

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

    //==[PARAMETERS]==
    public float MoveX { get; set; }
    public float MoveY { get; set; }
    public bool IsWalking { get; set; }
    public bool IsRunning { get; set; } //--Implement running lol

    //==[ROTATION STEP CONSTANT]==
    private const float _rotationStep = 22.5f;

    public SpritePerspective SpritePerspective { get; private set; }
    public Action<SpritePerspective> OnSpritePerspectiveChanged;

    public FacingDirection FacingDirection { get; private set; }
    public Action<FacingDirection> OnFacingDirectionChanged;


    //==[STATES]==
    [SerializeField] private State<CharacterAnimator> _idleState;
    [SerializeField] private State<CharacterAnimator> _walkingState;
    [SerializeField] private State<CharacterAnimator> _runningState;
    [SerializeField] private State<CharacterAnimator> _ledgeHopState;


    private void Start(){
        _spriteRenderer = GetComponent<SpriteRenderer>();
        SpriteAnimator = new SpriteAnimator( _spriteRenderer );
        _camera = PlayerReferences.MainCameraTransform;

        StateMachine = new( this );
        StateMachine.Push( _idleState );

        _defaultAnimSheet = _idleDownSprites;
    }

    private void Update(){
        DetectWalking();
        StateMachine.Update();
        SpriteAnimator.HandleUpdate();
        SetSpritePerspective();
        Billboard();
    }

    private void Billboard(){
        transform.forward = _camera.forward;
    }

    private void AssignAnimations( List<Sprite> animation ){
        if( animation.Count > 0 )
            SpriteAnimator.AnimationFrames = animation;
        else
            SpriteAnimator.AnimationFrames = _defaultAnimSheet;
    }

    //--Somehow this Just Worked™
    private void SetSpritePerspective(){
        //--Sets facing direction based on the player transform forward
        var projection = Vector3.ProjectOnPlane( _camera.transform.forward, _parentTransform.up );
        var angle = Vector3.SignedAngle( projection, _parentTransform.forward, _parentTransform.up );
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

        // OnSpritePerspectiveChanged?.Invoke( SpritePerspective );
    }

    //--This is to set the shadow facing direction based on the character's forward angle. i don't know math tho lol
    private void GetCharacterForwardDirection(){
        var absAngle = Mathf.Abs( _parentTransform.forward.y );

        if( absAngle > _rotationStep )
            FacingDirection = FacingDirection.Up;
        else if( absAngle <= _rotationStep * 3 )
            FacingDirection = Mathf.Sign( absAngle ) < 0 ? FacingDirection.UpLeft      : FacingDirection = FacingDirection.UpRight;
        else if( absAngle <= _rotationStep * 5 )
            FacingDirection = Mathf.Sign( absAngle ) < 0 ? FacingDirection.Left        : FacingDirection = FacingDirection.Right;
        else if( absAngle <= _rotationStep * 7 )
            FacingDirection = Mathf.Sign( absAngle ) < 0 ? FacingDirection.DownLeft    : FacingDirection = FacingDirection.DownRight;
    }

    private void DetectWalking(){
        if( StateMachine.CurrentState == _walkingState )
            return;

        if( MoveY != 0 || MoveX != 0 )
            StateMachine.Push( _walkingState );
    }

    public void SetSpriteSheet( List<Sprite> sprites ){
        _currentAnimSheet = sprites;

        AssignAnimations( _currentAnimSheet );
    }

    //=======================================================================================
    //===========================[ ANIMATIONS & THINGS ]=====================================
    //=======================================================================================

    public IEnumerator JumpToBattlePosition( Transform player, Transform battlePosition ){
        yield return player.DOJump( battlePosition.position, 1, 1, 0.5f ).WaitForCompletion();
    }

    public IEnumerator JumpFromLedge( Transform player, Vector3 destination ){
        Debug.Log( $"JumpFromLedge()" );
        StateMachine.Push( _ledgeHopState );

        yield return SpriteAnimator.PauseAtFrameUntil( 2, 0.05f );
        AudioController.Instance.PlaySFX( SoundEffect.LedgeHop );

        yield return SpriteAnimator.PauseAtFrame( 3 );
        yield return player.DOJump( destination, 0.75f, 1, 0.35f ).WaitForCompletion();

        SpriteAnimator.UnpauseAnimator();
        yield return SpriteAnimator.PauseAtFrameUntil( 4, 0.05f );

        SpriteAnimator.UnpauseAnimator();
        StateMachine.Pop();
    }

    #if UNITY_EDITOR
    private void OnGUI(){
        if( !StateMachineDisplays.Show_PlayerAnimatorStack )
            return; 

        var style = new GUIStyle();
        style.font = Resources.Load<Font>( "Fonts/Gotham Bold Outlined" );
        style.fontSize = 30;
        style.fontStyle = FontStyle.Bold;
        style.normal.textColor = Color.white;
        style.richText = true;

        GUILayout.BeginArea( new Rect( 0, 0, 600, 500 ) );
        GUILayout.Label( "CHARACTER ANIMATOR STATE", style );
        foreach( var state in StateMachine.StateStack ){
            GUILayout.Label( state.GetType().ToString(), style );
        }
        GUILayout.EndArea();
    }
#endif

}
