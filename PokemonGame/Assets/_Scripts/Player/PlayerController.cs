using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _interactableDetectionRadius;
    [SerializeField] private float _interactableRayLength;
    [SerializeField] private Transform _playerCenter;
    [SerializeField] private InputActionProperty _interactButton;
    [SerializeField] private GameObject _pauseMenu;
    private PlayerInput _playerInput;

    private void OnEnable(){
        EnableInput();
        _playerInput.CharacterControls.Interact.performed += OnInteract;
        _playerInput.CharacterControls.PauseMenu.performed += OnPausePressed;
    }
    
    private void OnDisable(){
        DisableInput();
        _playerInput.CharacterControls.Interact.performed -= OnInteract;
    }

    public void SetPlayerInput( PlayerInput playerInput ){
        _playerInput = playerInput;
        //--this is temporary, i need to reorganize my player controls scripts. Perhaps i'll have a "setup" script that runs and assigns things
    }
    
    private void OnInteract( InputAction.CallbackContext context ){
        RaycastHit raymond;
        
        if( Physics.Raycast( _playerCenter.position, transform.forward/*.MovementAxisCorrection( PlayerReferences.MainCameraTransform )*/, out raymond, _interactableRayLength ) ){
            raymond.transform.GetComponent<IInteractable>()?.Interact();
        }
    }

    private void OnPausePressed( InputAction.CallbackContext context ){
        if( _pauseMenu.activeSelf )
            _pauseMenu.SetActive( false );
        else
            _pauseMenu.SetActive( true );
    }
    
    private void EnableInput(){
        _interactButton.action.Enable();
    }

    private void DisableInput(){
        _interactButton.action.Disable();
    }
    

#if UNITY_EDITOR
    
    public void OnDrawGizmos(){
        Gizmos.DrawWireSphere( transform.position, _interactableDetectionRadius );
        Debug.DrawRay( _playerCenter.position, transform.forward * _interactableRayLength, Color.red );
    }

#endif
}
