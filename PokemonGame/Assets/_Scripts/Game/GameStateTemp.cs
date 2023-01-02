using UnityEngine;
using UnityEngine.EventSystems;
using System;

public enum GameState { Overworld, Battle, Dialogue }

public class GameStateTemp : MonoBehaviour
{
    [SerializeField] private PlayerMovement _playerMovement;
    private PlayerInput _playerInput;
    [SerializeField] private BattleSystem _battleSystem;
    [SerializeField] private GameObject _massEnableParent;
    [SerializeField] private EventSystem _eventSystem;
    public static GameState GameState { get; set; }
    public static Action OnGameStateChanged;
    bool _overworldState, _battleState, _dialogueState;

    private void OnEnable(){
        OnGameStateChanged += ChangeGameState;
    }

    private void OnDisable(){
        OnGameStateChanged += ChangeGameState;
    }

    private void Start(){
        _playerInput = PlayerMovement.PlayerInput;
        GameState = GameState.Overworld;
        GameStateTemp.OnGameStateChanged?.Invoke();
    }

    // private void Update(){
    //     switch( GameState ){
    //         case GameState.Overworld :

    //             if( _overworldState ) return;
    //             _overworldState = true;
    //             _massEnableParent.SetActive( !enabled );
    //             _massEnableParent.SetActive( false );
    //             _eventSystem.enabled = false;
    //             _playerInput.CharacterControls.Enable();
    //             _battleState = false;
    //             _dialogueState = false;
    //             Debug.Log( GameState + " exploration baaybeee" );
    //             Debug.Log( _playerInput.CharacterControls.enabled );
    //             Debug.Log( _playerInput.UI.enabled );

    //         break;

    //         case GameState.Battle : 

    //             if( _battleState ) return;
    //             _battleState = true;
    //             _massEnableParent.SetActive( enabled );
    //             _massEnableParent.SetActive( true );
    //             _eventSystem.enabled = true;
    //             _playerInput.CharacterControls.Disable();
    //             _overworldState = false;
    //             _dialogueState = false;
    //             Debug.Log( "A " + GameState + " has started!" );

    //         break;
            
    //         case GameState.Dialogue :
                
    //             if( _dialogueState ) return;
    //             _dialogueState = true;
    //             _eventSystem.enabled = true;
    //             _playerInput.CharacterControls.Disable();
    //             _playerInput.UI.Enable();
    //             _overworldState = false;
    //             _battleState = false;
    //             Debug.Log( GameState + " is running!" );
    //             Debug.Log( _playerInput.CharacterControls.enabled );
    //             Debug.Log( _playerInput.UI.enabled );

    //             break;

    //     }
    // }

    private void ChangeGameState(){

        switch( GameState ){
            case GameState.Overworld :

                _overworldState = true;
                _massEnableParent.SetActive( !enabled );
                _massEnableParent.SetActive( false );
                _eventSystem.enabled = false;
                _playerInput.CharacterControls.Enable();
                _playerInput.UI.Disable();
                _battleState = false;
                _dialogueState = false;
                Debug.Log( GameState + " exploration baaybeee" );

            break;

            case GameState.Battle : 

                _battleState = true;
                _massEnableParent.SetActive( enabled );
                _massEnableParent.SetActive( true );
                _eventSystem.enabled = true;
                _playerInput.CharacterControls.Disable();
                _overworldState = false;
                _dialogueState = false;
                Debug.Log( "A " + GameState + " has started!" );

            break;
            
            case GameState.Dialogue :
                
                _dialogueState = true;
                _eventSystem.enabled = true;
                _playerInput.CharacterControls.Disable();
                _playerInput.UI.Enable();
                _overworldState = false;
                _battleState = false;
                Debug.Log( GameState + " is running!" );

                break;
        }
    }
}
