using System.Collections.Generic;
using UnityEngine;

public class PokemonShadow : MonoBehaviour
{
    private Transform _sunTransform;
    private SpriteAnimator _spriteAnimator;
    [SerializeField] private PokemonAnimator _pokemonAnimator;
    private SpriteRenderer _spriteRenderer;

    private List<Sprite> _currentAnimSheet;
    private List<Sprite> _defaultAnimSheet;

    private float _moveX;
    private float _moveY;
    private bool _wasWalking;

    public enum FacingDirection {
        Up, Down,
    }

    private FacingDirection _facingDirection;

    private void Start(){
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _spriteAnimator = new SpriteAnimator( _spriteRenderer );
        _sunTransform = PlayerReferences.Instance.SunTransform;
        _defaultAnimSheet = _pokemonAnimator.IdleDownSprites;
        _currentAnimSheet = _defaultAnimSheet;

        AssignAnimations( _currentAnimSheet );
    }

    private void Update(){
        var previousAnimSheet = _currentAnimSheet;
        UpdateMovement();
        CalcShadowFacingDirection(); //--need to get movement information from character controller or w/e for these
        SetIdleShadowSprites();

        if( _currentAnimSheet != previousAnimSheet || _pokemonAnimator.IsWalking != _wasWalking )
            _spriteAnimator.Start();

        if( _pokemonAnimator.IsWalking ){
            _spriteAnimator.HandleUpdate();
        }else{
            // SetIdleSprites();
            AssignAnimations( _currentAnimSheet );
            _spriteAnimator.Start();
            _spriteAnimator?.HandleUpdate();
        }

        _wasWalking = _pokemonAnimator.IsWalking;
    }

    private void LateUpdate(){
        Billboard();
    }

    private void Billboard(){
        transform.forward = _sunTransform.forward;
    }

    private void UpdateMovement(){
        if( _pokemonAnimator.MoveX > 0 )
            _moveX = _pokemonAnimator.MoveX;

        if( _pokemonAnimator.MoveY > 0 )
            _moveY = _pokemonAnimator.MoveY;
    }

    public void SetBattleShadows(){
        _currentAnimSheet = _pokemonAnimator.IdleDownSprites; //--Temporary measure until a battle state is made for the animators
    }

    private void AssignAnimations( List<Sprite> animation ){
        if( animation?.Count > 0 )
            _spriteAnimator.AnimationFrames = animation;
        else
            _spriteAnimator.AnimationFrames = _defaultAnimSheet;
    }

    private void CalcShadowFacingDirection(){
        //--Position Update
        Vector3 moveDirection;
        moveDirection.x = _pokemonAnimator.MoveX;
        moveDirection.y = 0.0f;
        moveDirection.z = _pokemonAnimator.MoveY;

        //--Current rotation
        Quaternion currentRotation = transform.rotation;

        if( _pokemonAnimator.IsWalking ){
            Quaternion targetRotation = Quaternion.LookRotation( moveDirection.MovementAxisCorrection( PlayerReferences.MainCameraTransform ) );

            transform.rotation = Quaternion.Slerp( currentRotation, targetRotation, 1f ); //--1f is rotations per frame
        }
    }

    private void SetIdleShadowSprites(){
        //--Assign Shadow sprites based on look direction to keep appropriate shape
        switch( _facingDirection ){
            case FacingDirection.Up:
                _currentAnimSheet = _pokemonAnimator.IdleUpSprites;
            
            break;

            case FacingDirection.Down:
                _currentAnimSheet = _pokemonAnimator.IdleDownSprites;

            break;

            default:
                _currentAnimSheet = _pokemonAnimator.IdleDownSprites;

            break;
        }

        AssignAnimations( _currentAnimSheet );
    }

}
