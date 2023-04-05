using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class PKMNMenu : MonoBehaviour
{
    [SerializeField] private BattleSystem _battleSystem;
    public BattleSystem BattleSystem => _battleSystem;
    [SerializeField] private EventSystem _eventSystem;
    [SerializeField] private PartyScreen _partyScreen;
    public Button Lastbutton;

    private void OnEnable(){
        _partyScreen.Init();
        _partyScreen.SetParty( _battleSystem.PlayerParty.PartyPokemon );
        BattleUIActions.OnPkmnMenuOpened?.Invoke();
    }

    private void OnDisable() {
        BattleUIActions.OnPkmnMenuClosed?.Invoke();
    }
    
}
