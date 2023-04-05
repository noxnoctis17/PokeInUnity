using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TrainerClass : NPC_Base
{  
    [SerializeField] private TrainerSO _trainerSO;
    [SerializeField] private PokemonParty _trainerParty;
    [SerializeField] private BattleType _battleType;

    public TrainerSO TrainerSO => _trainerSO;
    public PokemonParty TrainerParty => _trainerParty;



	public override void Interact(){
        Debug.Log( $"You've Interacted With {this}" );

        foreach( DialogueResponseEvents responseEvents in GetComponents<DialogueResponseEvents>() ){
            if( responseEvents.DialogueSO == _dialogueSO ){
                DialogueManager.Instance.OnHasResponseEvents?.Invoke( responseEvents );
                break;
            }
        }

        DialogueManager.Instance.OnDialogueEvent?.Invoke( DialogueSO );

    }

    public void InitializeTrainerBattle(){

    }
}
