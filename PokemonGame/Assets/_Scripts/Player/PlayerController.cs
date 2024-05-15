using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

public class PlayerController : MonoBehaviour
{
    [SerializeField] private float _interactableDetectionRadius;
    [SerializeField] private float _interactableRayLength;
    [SerializeField] private Transform _playerCenter;
    [SerializeField] private EventSystem _eventSystem;
    [SerializeField] private bool _disableMouse;
    private PlayerInput _playerInput;
    private PlayerMovement _playerMovement;
    public EventSystem EventSystem => _eventSystem;
    public static event Action OnPause;

    private void OnEnable(){
        // EnableInteract();
        _playerInput.CharacterControls.Interact.performed += OnInteract;
        _playerInput.CharacterControls.PauseMenu.performed += OnPausePressed;
        _playerInput.CharacterControls.Load.performed += OnLoadPressed;

        //--Lock and Hide mouse
        if( _disableMouse ){
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
    
    private void OnDisable(){
        // DisableInteract();
        _playerInput.CharacterControls.Interact.performed -= OnInteract;
        _playerInput.CharacterControls.PauseMenu.performed -= OnPausePressed;
        _playerInput.CharacterControls.Load.performed -= OnLoadPressed;
    }

    public void SetPlayerInput( PlayerInput playerInput ){
        _playerInput = playerInput;
        //--this is temporary, i need to reorganize my player controls scripts. Perhaps i'll have a "setup" script that runs and assigns things
    }

    public void SetPlayerMovement( PlayerMovement playerMovement ){
        _playerMovement = playerMovement;
    }
    
    private void OnInteract( InputAction.CallbackContext context ){
        if( Physics.Raycast( _playerCenter.position, transform.forward/*.MovementAxisCorrection( PlayerReferences.MainCameraTransform )*/, out RaycastHit raymond, _interactableRayLength ) ){
            raymond.transform.GetComponent<IInteractable>()?.Interact();
        }
    }

    private void OnPausePressed( InputAction.CallbackContext context ){
        // Debug.Log( "Pause Pressed" );
        OnPause?.Invoke();
    }

    private void OnLoadPressed( InputAction.CallbackContext context ){
        if( GameStateController.Instance.CurrentStateEnum == GameStateController.GameStateEnum.FreeRoamState ){
            Debug.Log( "Load Fired" );
            SavingSystem.Instance.Load( "SaveSlot_1" );
        }
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
        EnableUI();
        _playerInput.UIBattle.Enable();
    }

    public void DisableBattleControls(){
        _eventSystem.enabled = false;
        DisableUI();
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
