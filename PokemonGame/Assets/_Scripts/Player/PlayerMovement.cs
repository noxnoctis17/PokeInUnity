using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private CharacterController _characterController;
    [SerializeField] private CharacterAnimator _animator;
    [SerializeField] private float _speed;
    [SerializeField] private float _runMultiplier;
    public PlayerInput PlayerInput { get; private set; }
    private Transform _playerTransform;
    private Vector2 _currentMovementInput;
    private Vector3 _currentMovement;
    private Vector3 _currentRunMovement;
    private bool _isMovementPressed;
    private bool _isRunPressed;
    public bool AllowMovement { get; set; }
    private float _rotationPerFrame = 1.0f;

    private void OnEnable(){
        _playerTransform = transform;
        _cameraTransform = PlayerReferences.MainCameraTransform;
        PlayerInput = new PlayerInput();
        GetComponent<PlayerController>().SetPlayerInput( PlayerInput );
        GetComponent<PlayerController>().SetPlayerMovement( this );

        PlayerInput.CharacterControls.Enable();
        PlayerInput.CharacterControls.Walk.started += OnMovementInput;
        PlayerInput.CharacterControls.Walk.canceled += OnMovementInput;
        PlayerInput.CharacterControls.Walk.performed += OnMovementInput;
        PlayerInput.CharacterControls.Run.started += OnRunInput;
        PlayerInput.CharacterControls.Run.canceled += OnRunInput;
        PlayerInput.CharacterControls.Run.performed += OnRunInput;
    }

    private void OnDisable(){
        PlayerInput.CharacterControls.Disable();
        PlayerInput.CharacterControls.Walk.started -= OnMovementInput;
        PlayerInput.CharacterControls.Walk.canceled -= OnMovementInput;
        PlayerInput.CharacterControls.Walk.performed -= OnMovementInput;
        PlayerInput.CharacterControls.Run.started -= OnRunInput;
        PlayerInput.CharacterControls.Run.canceled -= OnRunInput;
        PlayerInput.CharacterControls.Run.performed -= OnRunInput;
    }

    private void Update(){
        HandleAnimation();
        // HandleRotation();
        // HandleGravity();

        // if( AllowMovement ){
        //     if( _isRunPressed ){
        //         _characterController.Move( _currentRunMovement.MovementAxisCorrection( PlayerReferences.MainCameraTransform ) * ( Time.deltaTime * _speed ) );
        //     } else {
        //         _characterController.Move( _currentMovement.MovementAxisCorrection( PlayerReferences.MainCameraTransform ) * ( Time.deltaTime * _speed ) );
        //     }
        // }
    }

    private void FixedUpdate(){
        HandleRotation();
        HandleGravity();

        if( AllowMovement ){
            if( _isRunPressed ){
                _characterController.Move( _currentRunMovement.MovementAxisCorrection( PlayerReferences.MainCameraTransform ) * ( Time.deltaTime * _speed ) );
            } else {
                _characterController.Move( _currentMovement.MovementAxisCorrection( PlayerReferences.MainCameraTransform ) * ( Time.deltaTime * _speed ) );
            }
        }
    }
    
    private void HandleAnimation(){
        bool isWalking = _animator.IsWalking;
        bool isRunning = _animator.IsRunning; //--Implement running animations lol
        
        if( _isMovementPressed && !isWalking ){
            _animator.IsWalking = true;
            _animator.MoveX = _currentMovementInput.x;
            _animator.MoveY = _currentMovementInput.y;
        }
        else if( !_isMovementPressed && isWalking ){
            _animator.IsWalking = false;
        }
        else if( _isMovementPressed && isWalking ){
            _animator.MoveX = _currentMovementInput.x;
            _animator.MoveY = _currentMovementInput.y;
        }
    }

    private void HandleRotation(){
        //--Position update
        Vector3 nextMoveDirection;
        nextMoveDirection.x = _currentMovement.x;
        nextMoveDirection.y = 0.0f;
        nextMoveDirection.z = _currentMovement.z;

        //--Current rotation
        Quaternion currentRotation = transform.rotation;

        if( _isMovementPressed ){
            //--If moving, creates new rotation in new movement direction
            Quaternion targetRotation = Quaternion.LookRotation( nextMoveDirection.MovementAxisCorrection( PlayerReferences.MainCameraTransform ) );
            
            //--* time.DeltaTime to not auto-snap rotation, but this is not good for the 8 direction sprite rotation
            transform.rotation = Quaternion.Slerp( currentRotation, targetRotation, _rotationPerFrame );
        }
    }

    private void HandleGravity(){
        if( _characterController.isGrounded ){
            float groundedGravity = -0.5f;
            _currentMovement.y = groundedGravity;
            _currentRunMovement.y = groundedGravity;
        } else {
            float gravity = -5.8f;
            _currentMovement.y += gravity;
            _currentRunMovement.y += gravity;
        }
    }

    private void OnMovementInput( InputAction.CallbackContext context ){
        _currentMovementInput = context.ReadValue<Vector2>();
        _currentMovement.x = _currentMovementInput.x;
        _currentMovement.z = _currentMovementInput.y;
        _currentRunMovement.x = _currentMovementInput.x * _runMultiplier;
        _currentRunMovement.z = _currentMovementInput.y * _runMultiplier;
        _isMovementPressed = _currentMovementInput.x != 0 || _currentMovementInput.y != 0;
    }

    private void OnRunInput( InputAction.CallbackContext context ){
        _isRunPressed = context.ReadValueAsButton();
    }

    public IEnumerator MovePlayerIntoBattlePosition( Transform battlePosition ){
        PlayerInput.CharacterControls.Disable();
        yield return new WaitUntil( () => !PlayerInput.CharacterControls.enabled );
        yield return _animator.JumpToBattlePosition( transform, battlePosition );
    }

    public IEnumerator MovePlayerToSceneSpawnPoint( Transform spawnPoint ){
        PlayerInput.CharacterControls.Disable();
        yield return new WaitUntil( () => !PlayerInput.CharacterControls.enabled );
        yield return MovePlayerPosition( spawnPoint.position );
        PlayerInput.CharacterControls.Enable();
    }

    //--We disable and enable the character controller because it forces a position update after we move the player
    //--The position it forces is the last position the player was at before we manually update it here
    //--Waiting for FixedUpdate nor end of frame helped the situation. I'm sure there's a better way, but for now, this is fine.
    //--That is, of course, until it isn't. --04/27/24
    public IEnumerator MovePlayerPosition( Vector3 position ){
        // Debug.Log( "PlayerMovement.MovePlayerPosition()" );
        // Debug.Log( transform.position );
        yield return new WaitForFixedUpdate();
        AllowMovement = false; //--This will prevent movement calls from being made in FixedUpdate after the controller has been disabled
        _characterController.enabled = false; //--Disable and Enable Character Controller component because of above --04/27/24
        transform.position = position;
        yield return new WaitForEndOfFrame();
        _characterController.enabled = true; //--Disable and Enable Character Controller component because of above --04/27/24
        AllowMovement = true; //--And then we re-allow movement after enabling the controller
        // Debug.Log( transform.position );
    }
}