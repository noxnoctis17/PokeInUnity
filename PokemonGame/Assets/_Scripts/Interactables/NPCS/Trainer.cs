using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class Trainer : MonoBehaviour, IInteractable, ISavable
{  
    //--Actions
    public static Action<Trainer, BattleType> OnTrainerEncounter;

    //--Private
    [SerializeField] private TrainerSO _trainerSO;
    [SerializeField] private BattleType _battleType;
    [SerializeField] private DialogueSO _dialogueSO;
    [SerializeField] private DialogueSO _postBattleDialogueSO;
    [SerializeField] private GameObject _trainerCenter;
    [SerializeField] private Trainer _partnerTrainer;
    [SerializeField] private Trainer _opposingTrainer; //--Should be Bottom Trainer
    [SerializeField] private bool _isDefeated;
    [SerializeField] private bool _isRematchable;
    [SerializeField] private bool _allow2v1;

    //--Public
    public TrainerSO TrainerSO => _trainerSO;
    public string TrainerName => _trainerSO.TrainerName;
    public Sprite Portrait => _trainerSO.Portrait;
    public DialogueColorSO DialogueColor => _trainerSO.DialogueColor;
    public TrainerClasses TrainerClass => _trainerSO.TrainerClass;
    public int TrainerSkillLevel => _trainerSO.SkillLevel;
    public BattleType BattleType => _battleType;
    public MusicTheme TrainerMusic => _trainerSO.BattleTheme;
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
    //--11/30/25 you can probably just set a bool lol. "auto trigger battle?" and if it's true you trigger a battle.
    //--i assume this is going to be for forced battles after dialogue, mostly against the evil team
	public void Interact(){
        Debug.Log( $"You've Interacted With Trainer {this}!" );
        if( !_isDefeated || _isRematchable )
        {
            foreach( DialogueResponseEvents responseEvents in GetComponents<DialogueResponseEvents>() )
            {
                if( responseEvents.DialogueSO == _dialogueSO )
                {
                    DialogueManager.Instance.OnHasResponseEvents?.Invoke( responseEvents );
                    break;
                }
            }

            DialogueManager.Instance.OnDialogueEvent?.Invoke( _dialogueSO );
        }
        else
        {
            DialogueManager.Instance.OnDialogueEvent?.Invoke( _postBattleDialogueSO );
        }
    }

    public void StartTrainerBattleCoroutine()
    {
        if( CheckIfBattlePossible() )
        {
            UpdateDialogueObject( _postBattleDialogueSO );

            if( _battleType == BattleType.AI_Singles || _battleType == BattleType.AI_Doubles )
                StartCoroutine( InitAITrainerBattle() );
            else
                StartCoroutine( InitializeTrainerBattle() );
        }
        else
        {
            DialogueManager.Instance.PlaySystemMessage( "You don't have enough healthy Pokemon! Revive some first!" );
        }
    }

    public List<Pokemon> BuildTrainerParty()
    {
        List<Pokemon> battleParty = new();

        for( int i = 0; i < _trainerSO.Party.Count; i++ )
        {
            Pokemon pokemon = new( _trainerSO.Party[i] );
            battleParty.Add( pokemon );
        }

        return battleParty;
    }

    public IEnumerator InitializeTrainerBattle()
    {
        Debug.Log( $"[Trainer] Initialize Trainer Battle" );
        yield return new WaitForEndOfFrame();
        yield return DialogueManager.Instance.DialogueUI.ActiveDialogueCoroutine;
        yield return null;

        //--Build CPU BattleTrainer
        // var party = BuildTrainerParty();
        BattleTrainer cpuBattleTrainer = BattleTrainerFactory.FromNPC( this );

        //--Build Player BattleTrainer
        var playerTrainer = PlayerReferences.Instance.PlayerTrainer;
        var playerBattleTrainer = playerTrainer.MakeBattleTrainer();
        BattleController.Instance.InitTrainerBattle( playerBattleTrainer, cpuBattleTrainer, BattleType ); //--We pass BattleTrainer here
    }

    public IEnumerator InitAITrainerBattle()
    {
        Debug.Log( "InitAITrainerBattle()" );
        yield return new WaitForEndOfFrame();
        yield return DialogueManager.Instance.DialogueUI.ActiveDialogueCoroutine;
        yield return null;

        //--Build This Trainer
        // var thisParty = BuildTrainerParty();
        BattleTrainer thisBattleTrainer = BattleTrainerFactory.FromNPC( this );

        //--Build Opposing Trainer
        // var opposingParty = _opposingTrainer.BuildTrainerParty();
        BattleTrainer opposingBattleTrainer = BattleTrainerFactory.FromNPC( _opposingTrainer );

        BattleController.Instance.InitAITrainerBattle( BattleType, thisBattleTrainer, opposingBattleTrainer );
    }

    private bool CheckIfBattlePossible()
    {
        var playerTrainer = PlayerReferences.Instance.PlayerTrainer;
        var availablePlayerPokemon = playerTrainer.ActiveParty.Select( p => p ).Where( p => p.CurrentHP > 0 ).ToList();

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
