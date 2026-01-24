using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;
using DG.Tweening;

public class PostBattleSummary : MonoBehaviour
{
    [SerializeField] private PostBattleEvent _eventPrefab;
    [SerializeField] private Transform _eventContainer;
    [SerializeField] private Transform _inactivePoolContainer;
    private ObjectPool<PostBattleEvent> _eventPool;
    private Queue<PostBattleEvent> _summaryEvents;
    private List<PostBattleEvent> _activeSummaryEvents;
    private string _eventText;
    private Sprite _portrait;
    private int ExpGain;
    private int EffortGain;
    private PokemonParty _playerParty;
    private const float MOVE_EVENT_X_IN = 0f;
    private const float MOVE_EVENT_X_STARTPOS = -575f;

    private PostBattleEvent EventPoolCreate(){
        var PostBattleEvent = Instantiate( _eventPrefab, _inactivePoolContainer );
        PostBattleEvent.gameObject.SetActive( false );
        return PostBattleEvent;
    }

    private void EventPoolRelease( PostBattleEvent eventObj )
    {
        eventObj.gameObject.transform.DOMoveX( MOVE_EVENT_X_STARTPOS, 0 );
        eventObj.gameObject.SetActive( false );
        eventObj.gameObject.transform.SetParent( _inactivePoolContainer );
    }

    private void EventPoolDestroy( PostBattleEvent eventObj )
    {
        Destroy( eventObj.gameObject );
    }

    public void RunBattleSummary( int exp, int ep )
    {
        ExpGain = exp;
        EffortGain = ep;

        //--Initialize a new ObjectPool of Buttons for each Item
        _eventPool = new( () => { return EventPoolCreate(); },
        eventObject => { /*EventPoolGet( eventObject, _eventText, _portrait );*/ },
        eventObject => { EventPoolRelease( eventObject ); },
        eventObject => { EventPoolDestroy( eventObject ); },
        //--Handle Dupes, Starting Amount in Pool, Max Amount in Pool------------------------
        false, 5, 10 );

        _summaryEvents = new();
        _activeSummaryEvents = new();

        _playerParty = PlayerReferences.Instance.PlayerParty;
        gameObject.SetActive( true );

        StartCoroutine( GivePartyExp() );
    }

    private PostBattleEvent CreateSummaryEvent( string text, Sprite portrait = null )
    {
        var summary = _eventPool.Get();
        summary.Setup( this, text, portrait );
        summary.gameObject.transform.SetParent( _eventContainer );
        return summary;
    }

    public void ReleaseSummaryEvent( PostBattleEvent summaryEvent )
    {
        _eventPool.Release( summaryEvent );
        _activeSummaryEvents.Remove( summaryEvent );
    }

    private IEnumerator RunSummaryEventQueue()
    {
        WaitForSeconds delay = new( 0.5f );

        while( _summaryEvents.Count > 0 )
        {
            if( _activeSummaryEvents.Count < 5 )
            {
                var summaryEvent = _summaryEvents.Peek();
                summaryEvent.gameObject.SetActive( true );
                yield return AnimateSummaryEventIn( summaryEvent );
                _activeSummaryEvents.Add( summaryEvent );
                _summaryEvents.Dequeue();

                yield return delay;
            }
            else
            {
                yield return delay;
            }
        }

        yield return new WaitUntil( () => _activeSummaryEvents.Count == 0 );
    }

    private IEnumerator GivePartyExp(){
        //--Gain Exp Dialogue
        PostBattleEvent summary;
        summary = CreateSummaryEvent( $"All Pokemon received {ExpGain} Exp!" );
        _summaryEvents.Enqueue( summary );

        summary = CreateSummaryEvent( $"All Pokemon received {EffortGain} Effort Points!" );
        _summaryEvents.Enqueue( summary );

        //--Give Exp to each Pokemon in player's party directly
        foreach( Pokemon pokemon in _playerParty.Party )
        {
            //--Gain EXP
            pokemon.GainExp( ExpGain );
            pokemon.GainEP( EffortGain );

            //--Check for Level up
            while( pokemon.CheckForLevelUpBattle() )
            {
                summary = CreateSummaryEvent( $"{pokemon.NickName} grew to level {pokemon.Level}!", pokemon.PokeSO.Portrait_Normal );
                _summaryEvents.Enqueue( summary );

                //--Try Learn Moves
                if( pokemon.GetNextLearnableMove() != null )
                {
                    var newMove = pokemon.GetNextLearnableMove();
                    if( !pokemon.CheckHasMove( newMove.MoveSO ) )
                    {
                        pokemon.LearnLevelUpMove( newMove.MoveSO );
                        summary = CreateSummaryEvent( $"{pokemon.NickName} has learned {newMove.MoveSO.Name}!", pokemon.PokeSO.Portrait_Normal );
                        _summaryEvents.Enqueue( summary );
                    }
                }

                var evolution = pokemon.CheckForEvolution();
                if( evolution != null && !pokemon.CanEvolveByLevelUp )
                {
                    pokemon.SetCanEvolveByLevelUp( true );
                    summary = CreateSummaryEvent( $"{pokemon.NickName} can now evolve!", pokemon.PokeSO.Portrait_Normal );
                    _summaryEvents.Enqueue( summary );
                }
            }
        }

        yield return null;
        yield return RunSummaryEventQueue();

        gameObject.SetActive( false );
        _playerParty = null;
        ExpGain = 0;
        EffortGain = 0;
        _summaryEvents.Clear();
    }

    private IEnumerator AnimateSummaryEventIn( PostBattleEvent summary )
    {
        summary.AnimateThis.DOLocalMoveX( MOVE_EVENT_X_IN, 0.25f );
        yield return null;
    }
}
