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

    //--let's make an inferface for all battle menus maybe? that way i can ensure they all have
    //--the same basic setup. initial button, cursor memory/last selected thing, animations n shit
    //--this will help me get into the flow again and figure out where to go from here, i think lol --03/14/24

    private void OnEnable(){
        _partyScreen.Init();
        _partyScreen.SetParty( _battleSystem.PlayerParty.PartyPokemon );
        BattleUIActions.OnPkmnMenuOpened?.Invoke();
    }

    private void OnDisable() {
        BattleUIActions.OnPkmnMenuClosed?.Invoke();
    }
    
}
