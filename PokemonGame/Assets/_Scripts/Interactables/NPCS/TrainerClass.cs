using System;
using System.Collections;
using UnityEngine;

public class TrainerClass : MonoBehaviour, IInteractable, ISavable
{  
    //--Actions
    public static Action<TrainerClass, BattleType> OnTrainerEncounter;

    //--Private
    [SerializeField] private TrainerSO _trainerSO;
    [SerializeField] private PokemonParty _trainerParty;
    [SerializeField] private BattleType _battleType;
    [SerializeField] private DialogueSO _dialogueSO;
    [SerializeField] private DialogueSO _postBattleDialogueSO;
    [SerializeField] private GameObject _trainerCenter;
    [SerializeField] private bool _isDefeated;
     [SerializeField] private bool _isRematchable;

    //--Public
    public TrainerSO TrainerSO => _trainerSO;
    public PokemonParty TrainerParty => _trainerParty;
    public BattleType BattleType => _battleType;
    public DialogueSO DialogueSO => _dialogueSO;
    public GameObject TrainerCenter => _trainerCenter;
    public bool IsDefeated => _isDefeated;
    public bool IsRematchable => _isRematchable;

    //--You need to create a callback that returns whether the trainer or player won the battle
    //--and there you can set _isDefeated. Right now this class doesn't actually work very well lol --04/29/24
    //--We interact, start a dialogue, dialogue triggers response events, choosing a battle-initiating response
    //--triggers a callback, and the response event is a Unity Event that lets us choose a function directly in the inspector
    //--Actually, that does mean that this class works, sort of. i neeed to create a way for a battle to trigger directly after
    //--dialogue when there's no response event to trigger one.
	public void Interact(){
        Debug.Log( $"You've Interacted With Trainer {this}!" );
        if( !_isDefeated || _isRematchable ){
            foreach( DialogueResponseEvents responseEvents in GetComponents<DialogueResponseEvents>() ){
                if( responseEvents.DialogueSO == _dialogueSO ){
                    DialogueManager.Instance.OnHasResponseEvents?.Invoke( responseEvents );
                    break;
                }
            }

            DialogueManager.Instance.OnDialogueEvent?.Invoke( _dialogueSO );
        }
        else{
            DialogueManager.Instance.OnDialogueEvent?.Invoke( _postBattleDialogueSO );
        }
    }

    public void StartTrainerBattleCoroutine(){
        UpdateDialogueObject( _postBattleDialogueSO );
        StartCoroutine( InitializeTrainerBattle() );
    }

    public IEnumerator InitializeTrainerBattle(){
        yield return new WaitForSeconds( 0.25f );
        yield return DialogueManager.Instance.DialogueUI.ActiveDialogueCoroutine;
        yield return null;
        BattleController.Instance.InitTrainerBattle( this );
    }

    public void UpdateDialogueObject( DialogueSO dialogueSO ){
        _dialogueSO = dialogueSO;
    }

    public void SetDefeated(){
        _isDefeated = true;
    }

    public void SetRematchable( bool rematchable ){
        _isRematchable = rematchable;
    }

    public object CaptureState(){
        return _isDefeated;
    }

    public void RestoreState( object state ){
        _isDefeated = (bool)state;
    }
    
}
