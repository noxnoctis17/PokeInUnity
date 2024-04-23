using System;
using System.Collections;
using UnityEngine;

public class TrainerClass : MonoBehaviour, IInteractable
{  
    public static Action<TrainerClass, BattleType> OnTrainerEncounter;
    [SerializeField] private TrainerSO _trainerSO;
    [SerializeField] private PokemonParty _trainerParty;
    [SerializeField] private BattleType _battleType;
    [SerializeField] private DialogueSO _dialogueSO;
    [SerializeField] private DialogueSO _postBattleDialogueSO;
    [SerializeField] private GameObject _trainerCenter;

    public TrainerSO TrainerSO => _trainerSO;
    public PokemonParty TrainerParty => _trainerParty;
    public BattleType BattleType => _battleType; //--Don't think i need this
    public DialogueSO DialogueSO => _dialogueSO;



	public void Interact(){
        Debug.Log( $"You've Interacted With Trainer {this}!" );

        foreach( DialogueResponseEvents responseEvents in GetComponents<DialogueResponseEvents>() ){
            if( responseEvents.DialogueSO == _dialogueSO ){
                DialogueManager.Instance.OnHasResponseEvents?.Invoke( responseEvents );
                break;
            }
        }

        DialogueManager.Instance.OnDialogueEvent?.Invoke( DialogueSO );

    }

    public void StartTrainerBattleCoroutine(){
        UpdateDialogueObject( _postBattleDialogueSO );
        StartCoroutine( InitializeTrainerBattle() );
    }

    public IEnumerator InitializeTrainerBattle(){
        yield return new WaitForSeconds( 0.25f );
        yield return DialogueManager.Instance.DialogueUI.ActiveDialogueCoroutine;
        yield return null;
        BattleController.Instance.InitTrainerBattle( _trainerCenter, _trainerParty, _battleType );
    }

    public void UpdateDialogueObject( DialogueSO dialogueSO ){
        _dialogueSO = dialogueSO;
    }
}
