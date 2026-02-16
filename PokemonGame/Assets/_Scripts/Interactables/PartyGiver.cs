using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PartyGiver : MonoBehaviour, IInteractable
{
    [SerializeField] private List<Pokemon> _partyToGive;

    private void Start()
    {
        for( int i = 0; i < _partyToGive.Count; i++ )
        {
            _partyToGive[i].Init();
        }
    }

    public void Interact()
    {
        var playerTrainer = PlayerReferences.Instance.PlayerTrainer;

        playerTrainer.GiveParty( _partyToGive );
        DialogueManager.Instance.PlaySystemMessage( $"You received a new party!" );
    }
}
