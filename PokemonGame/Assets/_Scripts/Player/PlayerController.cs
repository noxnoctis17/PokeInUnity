using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _interactableDetectionRadius;
    [SerializeField] private float _interactableRayLength;
    [SerializeField] private Transform _playerCenter;
    [SerializeField] private InputActionProperty _interactButton;
    [SerializeField] private EventSystem _eventSystem;
    private PlayerInput _playerInput;
    private PlayerMovement _playerMovement;
    public static Action OnPause;

    private void OnEnable(){
        EnableInteract();
        _playerInput.CharacterControls.Interact.performed += OnInteract;
        _playerInput.CharacterControls.PauseMenu.performed += OnPausePressed;
        OnPause += OnPauseTest;
    }
    
    private void OnDisable(){
        DisableInteract();
        _playerInput.CharacterControls.Interact.performed -= OnInteract;
        _playerInput.CharacterControls.PauseMenu.performed -= OnPausePressed;
        OnPause -= OnPauseTest;
    }

    public void SetPlayerInput( PlayerInput playerInput ){
        _playerInput = playerInput;
        //--this is temporary, i need to reorganize my player controls scripts. Perhaps i'll have a "setup" script that runs and assigns things
    }

    public void SetPlayerMovement( PlayerMovement playerMovement ){
        _playerMovement = playerMovement;
    }
    
    private void OnInteract( InputAction.CallbackContext context ){
        RaycastHit raymond;
        
        if( Physics.Raycast( _playerCenter.position, transform.forward/*.MovementAxisCorrection( PlayerReferences.MainCameraTransform )*/, out raymond, _interactableRayLength ) ){
            raymond.transform.GetComponent<IInteractable>()?.Interact();
        }
    }

    private void OnPausePressed( InputAction.CallbackContext context ){
        Debug.Log( "Pause Pressed" );
        OnPause?.Invoke();
    }

    private void OnPauseTest(){
        Debug.Log( "yep pause was infact pressed" );
    }

    private void OnLoadPressed( InputAction.CallbackContext context ){
        Debug.Log( "Load Fired" );
        if( GameStateController.Instance.CurrentStateEnum == GameStateController.GameStateEnum.FreeRoamState )
            SavingSystem.Instance.Load( "SaveSlot_1" );
    }
    
    private void EnableInteract(){
        _interactButton.action.Enable();
    }

    private void DisableInteract(){
        _interactButton.action.Disable();
    }

    public void EnableCharacterControls(){
        _playerInput.CharacterControls.Enable();
        _playerMovement.AllowMovement = true;
    }

    public void DisableCharacterControls(){
        _playerMovement.AllowMovement = false;
        _playerInput.CharacterControls.Disable();
    }

    public void EnableBattleControls(){
        _eventSystem.enabled = true;
        _playerInput.UIBattle.Enable();
    }

    public void DisableBattleControls(){
        _eventSystem.enabled = false;
        _playerInput.UIBattle.Disable();
    }

    public void EnableUI(){
        _eventSystem.enabled = true;
        _playerInput.UI.Enable();
    }

    public void DisableUI(){
        _eventSystem.enabled = false;
        _playerInput.UI.Disable();
    }
    

#if UNITY_EDITOR
    
    public void OnDrawGizmos(){
        Gizmos.DrawWireSphere( transform.position, _interactableDetectionRadius );
        Debug.DrawRay( _playerCenter.position, transform.forward * _interactableRayLength, Color.red );
    }

#endif
}
