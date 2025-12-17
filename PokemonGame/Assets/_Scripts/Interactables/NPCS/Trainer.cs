using System;
using System.Collections;
using System.Linq;
using UnityEngine;

public class Trainer : MonoBehaviour, IInteractable, ISavable
{  
    //--Actions
    public static Action<Trainer, BattleType> OnTrainerEncounter;

    //--Private
    [SerializeField] private TrainerSO _trainerSO;
    [SerializeField] private string _trainerName;
    [SerializeField] private int _trainerSkillLevel;
    [SerializeField] private PokemonParty _trainerParty;
    [SerializeField] private BattleType _battleType;
    [SerializeField] private DialogueSO _dialogueSO;
    [SerializeField] private DialogueSO _postBattleDialogueSO;
    [SerializeField] private GameObject _trainerCenter;
    [SerializeField] private bool _isDefeated;
    [SerializeField] private bool _isRematchable;
    [SerializeField] private bool _allow2v1;

    //--Public
    public TrainerSO TrainerSO => _trainerSO;
    public string TrainerName => _trainerName;
    public int TrainerSkillLevel => _trainerSkillLevel;
    public PokemonParty TrainerParty => _trainerParty;
    public BattleType BattleType => _battleType;
    public DialogueSO DialogueSO => _dialogueSO;
    public GameObject TrainerCenter => _trainerCenter;
    public bool IsDefeated => _isDefeated;
    public bool IsRematchable => _isRematchable;

    private void OnEnable()
    {
        _trainerName = _trainerSO.TrainerName;
    }

    //--You need to create a callback that returns whether the trainer or player won the battle
    //--and there you can set _isDefeated. Right now this class doesn't actually work very well lol --04/29/24
    //--We interact, start a dialogue, dialogue triggers response events, choosing a battle-initiating response
    //--triggers a callback, and the response event is a Unity Event that lets us choose a function directly in the inspector
    //--Actually, that does mean that this class works, sort of. i neeed to create a way for a battle to trigger directly after
    //--dialogue when there's no response event to trigger one.
    //--11/30/25 you can probably just set a bool lol. "auto trigger battle?" and if it's true you trigger a battle.
    //--i assume this is going to be for forced battles after dialogue, mostly against the evil team
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

    public void StartTrainerBattleCoroutine()
    {
        if( CheckIfBattlePossible() )
        {
            UpdateDialogueObject( _postBattleDialogueSO );
            StartCoroutine( InitializeTrainerBattle() );
        }
        else
        {
            DialogueManager.Instance.PlaySystemMessage( "You don't have enough healthy Pokemon! Revive some first!" );
        }
    }

    public IEnumerator InitializeTrainerBattle(){
        // yield return new WaitForSeconds( 0.25f );
        yield return new WaitForEndOfFrame();
        yield return DialogueManager.Instance.DialogueUI.ActiveDialogueCoroutine;
        yield return null;
        BattleController.Instance.InitTrainerBattle( this );
    }

    private bool CheckIfBattlePossible()
    {
        var availablePlayerPokemon = PlayerReferences.Instance.PlayerParty.PartyPokemon.Select( p => p ).Where( p => p.CurrentHP > 0 ).ToList();

        if( availablePlayerPokemon.Count == 0 )
        {
            //--Then force-warp to last PokeCenter
            Debug.LogError( "You have no available Pokemon! This shouldn't've happened!" );
            return false;
        }
        else if( _battleType == BattleType.TrainerDoubles && availablePlayerPokemon.Count < 2 )
        {
            if( _allow2v1 )
                return true;
            else
                return false;
        }
        else
            return true;
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
