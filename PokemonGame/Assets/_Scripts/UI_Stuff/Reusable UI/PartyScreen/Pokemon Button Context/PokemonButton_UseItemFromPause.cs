using System;
using System.Collections;
using UnityEngine;

public class PokemonButton_UseItemFromPause : MonoBehaviour, IPokemonButtonContext
{
    private PartyDisplay _partyDisplay;
    private Pokemon _pokemon;
    private PokemonButton _pkmnButton;
    private BagScreen_Pause _bagScreen;
    private bool _busy;

    public void Init( PartyDisplay partyScreen, PokemonButton button, IPartyScreen bagScreen ){
        _partyDisplay = partyScreen;
        _pkmnButton = button;
        _bagScreen = (BagScreen_Pause)bagScreen;
        _pokemon = _pkmnButton.Pokemon;
    }

    public void ContextSubmit(){
        if( _busy )
            return;
        Debug.Log( $"Use {_bagScreen.BagDisplay.ItemSelected.ItemSO.ItemName}, Count: {_bagScreen.BagDisplay.ItemSelected.ItemCount}, on {_pokemon.NickName}" );

        var itemCategory = _bagScreen.BagDisplay.ItemSelected.ItemSO.ItemCategory;

        switch( itemCategory )
        {
            case ItemCategory.TM:
                var tm = (TMItemSO)_bagScreen.BagDisplay.ItemSelected.ItemSO;
                StartCoroutine( UseTM( tm ) );
            break;

            case ItemCategory.Training:
                if( _bagScreen.BagDisplay.ItemSelected.ItemSO is EvolutionItemsSO ){
                    var evoItem = (EvolutionItemsSO)_bagScreen.BagDisplay.ItemSelected.ItemSO;
                    StartCoroutine( UseEvolutionItem( evoItem ) );
                }
            break;

            default:
                StartCoroutine( UseItem() );
            break;
        }
    }

    public void ContextSelected()
    {
        AudioController.Instance.PlaySFX( SoundEffect.ButtonSelect );
        int i = _partyDisplay.GetIndex( _pkmnButton );
        Vector3 rotate = new( 0f, 0f, 0f );
        _partyDisplay.MemberSlots[i].AnimateBall( rotate );
    }

    public void ContextDeSelected()
    {
        int i = _partyDisplay.GetIndex( _pkmnButton );
        Vector3 rotate = new( 0f, 0f, 45f );
        _partyDisplay.MemberSlots[i].AnimateBall( rotate );
    }

    public void ContextCancel(){
        _bagScreen.PauseMenuStateMachine.PopState();
    }

    public void CloseContextMenu(){
        Debug.Log( "ItemContext CloseContextMenu()" );
        _bagScreen.PauseMenuStateMachine.PopState();
    }

    //--Check SV to see if using a TM keeps the player in the party field or forces them to use it again
    //--USE A TM
    private IEnumerator UseTM( TMItemSO tm ){
        Debug.Log( "UseTM()" );
        if( !tm.CheckIfUsable( _pokemon ) ){
            _busy = true;
            Debug.Log( "CheckCanLearnMove" );
            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{_pokemon.NickName} can't learn {tm.MoveSO.Name}! They're incompatible!" );
            _busy = false;
            yield break;
        }

        if( _pokemon.CheckHasMove( tm.MoveSO ) ){
            _busy = true;
            Debug.Log( "CheckHasMove" );
            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{_pokemon.NickName} already knows {tm.MoveSO.Name}!" );
            _busy = false;
            yield break;
        }

        if( _pokemon.TryLearnMove( tm.MoveSO ) ){
            _busy = true;
            Debug.Log( "TryLearnMove" );
            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{_pokemon.NickName} learned {tm.MoveSO.Name}!" );
            _busy = false;
        }
        else{

            _busy = true;

            bool moveLearnOver = false;
            bool learnedNewMove = false;

            Action<bool> onMoveLearnComplete = ( bool pokemonLearnedNewMove ) =>
            {
                //--This callback is retrieving two things:
                //--It's telling us that the move learn state is over, for the coroutine to continue
                //--and the bool it takes is receiving whether or not the move was actually learned
                moveLearnOver = true;
                learnedNewMove = pokemonLearnedNewMove;
            };

            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"Which move will you set aside?" );

            _pokemon.TryReplaceMove( tm.MoveSO, _bagScreen.LearnMoveMenu, onMoveLearnComplete, _bagScreen );
            yield return new WaitUntil( () => moveLearnOver );

            if( learnedNewMove ){
                yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{_pokemon.NickName} learned {tm.MoveSO.Name}!" );
                yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"It was added to its Current Moves!" );
            }
            else{
                yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{_pokemon.NickName} learned {tm.MoveSO.Name}!" );
                yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"It was added to its Learned Moves!" );
            }
        }

        yield return new WaitForEndOfFrame();
        _busy = false;
    }

    //--USE AN EVOLUTION ITEM
    private IEnumerator UseEvolutionItem( EvolutionItemsSO evoItem ){
        //--Check for evolution. if it's null, we can't use this item to evolve!
        var evolution = _pokemon.CheckForEvolution( evoItem );

        if( evolution != null ){
            _busy = true;

            //--Evolve
            GameStateController.Instance.PushGameState( EvolutionManager.Instance );
            yield return EvolutionManager.Instance.Evolve( _pokemon, evolution );
            PlayerReferences.Instance.PlayerInventory.UseItem( _bagScreen.BagDisplay.ItemSelected, _pokemon );

            //--Check if we used the last item, and if so pop use item on pokemon state back to item selection
            if( _bagScreen.BagDisplay.ItemSelected == null || _bagScreen.BagDisplay.ItemSelected.ItemCount == 0 ){
                yield return new WaitForEndOfFrame();
                _bagScreen.PauseMenuStateMachine.PopState();
            }

            _busy = false;
        }
        else{
            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( "It won't have any effect!" );
            yield break;
        }

    }

    //--USE A FUCKING ITEM
    private IEnumerator UseItem(){
        var itemUsed = PlayerReferences.Instance.PlayerInventory.UseItem( _bagScreen.BagDisplay.ItemSelected, _pokemon );

        if( itemUsed != null )
        {
            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( itemUsed.UseText( _pokemon ) );

            if( _bagScreen.BagDisplay.ItemSelected == null || _bagScreen.BagDisplay.ItemSelected.ItemCount == 0 )
            {
                yield return new WaitForEndOfFrame();
                _bagScreen.PauseMenuStateMachine.PopState();
            }

        }
        else{
            yield return DialogueManager.Instance.PlaySystemMessageCoroutine( "It won't have any effect!" );
        }
        
        yield return null;
    }

}
