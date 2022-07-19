using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public enum GameState { Overworld, Battle }

public class GameStateTemp : MonoBehaviour
{
    [SerializeField] PlayerMovement _playerMovement;
    [SerializeField] BattleSystem _battleSystem;
    [SerializeField] GameObject _massEnableParent;
    [SerializeField] EventSystem _eventSystem;
    public static GameState GameState { get; set; }
    bool _overworldState, _battleState;

    private void Awake()
    {
        GameState = GameState.Overworld;
    }

    private void Update()
    {
        switch(GameState)
        {
            case GameState.Overworld :

                if(_overworldState) return;
                _massEnableParent.SetActive(!enabled);
                _massEnableParent.SetActive(false);
                _eventSystem.enabled = false;
                _playerMovement.enabled = true;
                _overworldState = true;
                _battleState = false;
                Debug.Log(GameState + " exploration baaybeee");

            break;

            case GameState.Battle : 

                if(_battleState) return;
                _massEnableParent.SetActive(enabled);
                _massEnableParent.SetActive(true);
                _eventSystem.enabled = true;
                _playerMovement.enabled = false;
                _overworldState = false;
                _battleState = true;
                Debug.Log("A " + GameState + " has started!");

            break;

        }
    }
}
