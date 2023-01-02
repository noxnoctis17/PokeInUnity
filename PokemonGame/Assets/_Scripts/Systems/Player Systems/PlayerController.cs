using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _interactableDetectionRadius;
    [SerializeField] private float _interactableRayLength;
    [SerializeField] private InputActionProperty _interactButton;

    private void OnEnable(){
        DialogueManager.OnDialogueStarted += DisableInput;
        DialogueManager.OnDialogueFinished += EnableInput;
        _interactButton.action.performed += OnInteract;
        EnableInput();
    }
    
    private void OnDisable(){
        DialogueManager.OnDialogueStarted -= DisableInput;
        DialogueManager.OnDialogueFinished -= EnableInput;
        _interactButton.action.performed -= OnInteract;
        DisableInput();
    }

    private void OnTriggerEnter( Collider col ){
        if( col.gameObject.GetComponent<IInteractable>() != null ){
            Debug.Log( "interactable in range" );
        }
    }
    
    private void OnInteract( InputAction.CallbackContext context ){
        // Debug.Log( "interact pressed" );
        RaycastHit raymond;
        
        if( Physics.Raycast( transform.position, transform.forward.MovementAxisCorrection( PlayerReferences.MainCameraTransform ), out raymond, _interactableRayLength )
            || Physics.Raycast( transform.position + new Vector3( 1f, 0f, 0f ), transform.forward.MovementAxisCorrection( PlayerReferences.MainCameraTransform ), out raymond, _interactableRayLength )
            || Physics.Raycast( transform.position + new Vector3( -1f, 0f, 0f ), transform.forward.MovementAxisCorrection( PlayerReferences.MainCameraTransform ), out raymond, _interactableRayLength ) ){
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
        Debug.DrawRay( transform.position + new Vector3( 1f, 0f, 0f ), transform.forward * _interactableRayLength, Color.red );
        Debug.DrawRay( transform.position + new Vector3( -1f, 0f, 0f ), transform.forward * _interactableRayLength, Color.red );
        Debug.DrawRay( transform.position, transform.forward * _interactableRayLength, Color.red );
    }

#endif
}
