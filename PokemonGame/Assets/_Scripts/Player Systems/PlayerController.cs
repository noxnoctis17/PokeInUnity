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
    private PlayerInput _playerInput;

    private void OnEnable(){
        EnableInput();
        _playerInput.CharacterControls.Interact.performed += OnInteract;
        
    }
    
    private void OnDisable(){
        DisableInput();
        _playerInput.CharacterControls.Interact.performed -= OnInteract;
    }

    public void SetPlayerInput( PlayerInput playerInput ){
        _playerInput = playerInput;
        //--this is temporary, i need to reorganize my player controls scripts. Perhaps i'll have a "setup" script that runs and assigns things
    }

    // private void OnTriggerEnter( Collider col ){
    //     if( col.gameObject.GetComponent<IInteractable>() != null ){
    //         // Debug.Log( "interactable in range" );
    //     }
    // }
    
    private void OnInteract( InputAction.CallbackContext context ){
        // Debug.Log( "interact pressed" );
        RaycastHit raymond;
        
        if(    Physics.Raycast( _playerCenter.position, transform.forward.MovementAxisCorrection( PlayerReferences.MainCameraTransform ), out raymond, _interactableRayLength )
            || Physics.Raycast( _playerCenter.position + new Vector3( 1f, 0f, 0f ), transform.forward.MovementAxisCorrection( PlayerReferences.MainCameraTransform ), out raymond, _interactableRayLength )
            || Physics.Raycast( _playerCenter.position + new Vector3( -1f, 0f, 0f ), transform.forward.MovementAxisCorrection( PlayerReferences.MainCameraTransform ), out raymond, _interactableRayLength ) ){
            raymond.transform.GetComponent<IInteractable>()?.Interact();
        }
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
        Debug.DrawRay( _playerCenter.position + new Vector3( 1f, 0f, 0f ), transform.forward * _interactableRayLength, Color.red );
        Debug.DrawRay( _playerCenter.position + new Vector3( -1f, 0f, 0f ), transform.forward * _interactableRayLength, Color.red );
        Debug.DrawRay( _playerCenter.position, transform.forward * _interactableRayLength, Color.red );
    }

#endif
}
