using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PartyGiver : MonoBehaviour, IInteractable
{
    [SerializeField] private List<Pokemon> _partyToGive;

    private void Start(){
        Init();
    }

    public void Init(){
        foreach( Pokemon pokemon in _partyToGive ){
            pokemon.Init();
            pokemon.SetAsPlayerUnit();
        }
    }

    public void Interact()
    {
        var party = PlayerReferences.Instance.PlayerParty;

        party.GiveParty( _partyToGive );
        DialogueManager.Instance.PlaySystemMessage( $"You received a new party!" );
    }
}
