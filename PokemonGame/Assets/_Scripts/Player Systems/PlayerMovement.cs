using System;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerMovement : MonoBehaviour
{
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private CharacterController _controller;
    [SerializeField] private float _speed;
    [SerializeField] private float _runMultiplier;
    private PlayerInput _playerInput;
    private Vector2 _currentMovementInput;
    private Vector3 _currentMovement;
    private Vector3 _currentRunMovement;
    private bool _isMovementPressed;
    private bool _isRunPressed;
    private float _rotationPerFrame = 1.0f;

    private void OnEnable(){
        _playerInput.CharacterControls.Enable();
        _playerInput.CharacterControls.Walk.started += OnMovementInput;
        _playerInput.CharacterControls.Walk.canceled += OnMovementInput;
        _playerInput.CharacterControls.Walk.performed += OnMovementInput;
        _playerInput.CharacterControls.Run.started += OnRun;
        _playerInput.CharacterControls.Run.canceled += OnRun;
        _playerInput.CharacterControls.Run.performed += OnRun;
    }

    private void OnDisnable(){
        _playerInput.CharacterControls.Disable();
        _playerInput.CharacterControls.Walk.started -= OnMovementInput;
        _playerInput.CharacterControls.Walk.canceled -= OnMovementInput;
        _playerInput.CharacterControls.Walk.performed -= OnMovementInput;
        _playerInput.CharacterControls.Run.started -= OnRun;
        _playerInput.CharacterControls.Run.canceled -= OnRun;
        _playerInput.CharacterControls.Run.performed -= OnRun;
    }

    private void Awake(){
        _playerInput = new PlayerInput();
        _cameraTransform = Camera.main.transform;
    }

    private void Update(){
        HandleRotation();
        HandleGravity();

        if( _isRunPressed ){
            _controller.Move( _currentRunMovement.MovementAxisCorrection( _cameraTransform ) * Time.deltaTime * _speed );
        } else {
            _controller.Move( _currentMovement.MovementAxisCorrection( _cameraTransform ) * Time.deltaTime * _speed );
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

        if(_isMovementPressed){
            //--If moving, creates new rotation in new movement direction
            Quaternion targetRotation = Quaternion.LookRotation( nextMoveDirection );
            //--* time.DeltaTime to not auto-snap rotation, but this is not good for the 8 direction sprite rotation
            transform.rotation = Quaternion.Slerp( currentRotation, targetRotation, _rotationPerFrame );
        }
    }

    private void HandleGravity(){
        if( _controller.isGrounded ){
            float groundedGravity = -0.5f;
            _currentMovement.y = groundedGravity;
        } else {
            float gravity = -5.8f;
            _currentMovement.y += gravity;
        }
    }

    private void OnMovementInput( InputAction.CallbackContext context ){
        _currentMovementInput = context.ReadValue<Vector2>();
        _currentMovement.x = _currentMovementInput.x;
        _currentMovement.z = _currentMovementInput.y;
        _currentRunMovement.x = _currentMovementInput.x * _runMultiplier;
        _currentRunMovement.z = _currentMovementInput.y * _runMultiplier;
        _isMovementPressed = _currentMovementInput.x != 0 || _currentMovementInput.y != 0;
        // _currentMovement = Quaternion.AngleAxis ( _cameraTransform.rotation.eulerAngles.y, Vector3.up ) * _currentMovement;
        // _currentRunMovement = Quaternion.AngleAxis ( _cameraTransform.rotation.eulerAngles.y, Vector3.up ) * _currentRunMovement;
    }

    private void OnRun( InputAction.CallbackContext context ){
        _isRunPressed = context.ReadValueAsButton();
    }
}