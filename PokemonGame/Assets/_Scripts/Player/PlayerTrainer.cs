using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class PlayerTrainer : MonoBehaviour
{
    public string TrainerName = "Catch";
    [SerializeField] private TrainerClasses _trainerClass;
    [SerializeField] private Sprite _portrait;
    [SerializeField] private DialogueColorSO _dialogueColor;
    [SerializeField] private List<Pokemon> _activeParty;
    [SerializeField] private RentalTeamSO _rentalTeam;
    [SerializeField] private bool _useRentalTeam;
    private List<Pokemon> _storedParty;
    public int TrainerID { get; private set; }
    public Sprite Portrait => _portrait;
    public DialogueColorSO DialogueColor => _dialogueColor;
    public List<Pokemon> ActiveParty { get { return _activeParty; } set { PartySetter( value ); } }
    public TrainerClasses TrainerClass => _trainerClass;
    public GameObject TrainerCenter => PlayerReferences.Instance.PlayerCenter.gameObject;
    public Dictionary<TrainerClasses, string> TrainerClassDB { get; private set; }
    public event Action<List<Pokemon>> OnPartyUpdated;

    private void Start()
    {
        //--Eventually generate this once on new game start, not here.
        if( TrainerID == 0 || TrainerID < 1 )
            TrainerID = UnityEngine.Random.Range( 1, 9999 );

        if( _storedParty == null )
            _storedParty = new();

        for( int i = 0; i < _activeParty.Count; i++ )
        {
            _activeParty[i].Init();
        }

        if( _rentalTeam != null && _useRentalTeam )
        {
            _storedParty = _activeParty;
            _activeParty = _rentalTeam.BuildParty();
        }
    }

    public BattleTrainer MakeBattleTrainer()
    {
        return BattleTrainerFactory.FromPlayer( this );
    }

    private void PartySetter( List<Pokemon> party )
    {
        _activeParty = party;
        OnPartyUpdated?.Invoke( _activeParty );
    }

    public Pokemon GetHealthyPokemon( List<Pokemon> dontInclude = null )
    {
        var healthyPokemon = _activeParty.Where( x => x.CurrentHP > 0 ).ToList();
        
        if( dontInclude != null )
            healthyPokemon = healthyPokemon.Where( p => !dontInclude.Contains( p ) ).ToList();

        return healthyPokemon.FirstOrDefault();
    }

    public List<Pokemon> GetHealthyPokemon( int unitCount )
    {
        return _activeParty.Where( x => x.CurrentHP > 0 ).Take( unitCount ).ToList();
    }

    public void AddPokemon( Pokemon pokemon, PokeBallType ball )
    {
        Pokemon copyPokemon = new ( pokemon.PokeSO, pokemon.Level );
        copyPokemon.Init();
        copyPokemon.CurrentHP = pokemon.CurrentHP;
        copyPokemon.ChangeCurrentBall( ball );

        if( _activeParty.Count < 6 ){
            ActiveParty.Add( copyPokemon );
            OnPartyUpdated?.Invoke( _activeParty );

            if( pokemon.SevereStatus != null )
                copyPokemon.SyncSevereStatus( pokemon.SevereStatus.ID );
        }
        else{
            Debug.Log( "Your Party is Full" );
            //--Add to PC
        }
    }

    public void UpdateParty()
    {
        OnPartyUpdated?.Invoke( _activeParty );
    }

    public void GiveParty( List<Pokemon> givenParty )
    {
        ActiveParty = givenParty;
    }

    public void RestoreSavedParty( List<Pokemon> restoredParty )
    {
        ActiveParty = restoredParty;
    }

    public void SwitchPokemonPosition( Pokemon a, Pokemon b )
    {
        int indexA = _activeParty.IndexOf( a );
        int indexB = _activeParty.IndexOf( b );

        if( indexA < 0 || indexB < 0 )
            return;
        
        ( _activeParty[indexA], _activeParty[indexB] ) = ( _activeParty[indexB], _activeParty[indexA] );

        OnPartyUpdated?.Invoke( _activeParty );
    }

    public void PostBattleSync( BattleTrainer battleTrainer )
    {
        Debug.Log( $"[Battle System] Fired Post Battle Sync!" );
        
        for( int i = 0; i < battleTrainer.Party.Count; i++ )
        {
            var battleMon = battleTrainer.Party[i];
            var realMon = ActiveParty.FirstOrDefault( p => p.PID == battleMon.PID );

            if( realMon == null )
                continue;

            realMon.CurrentHP = battleMon.CurrentHP;

            if( battleMon.SevereStatus != null )
                realMon.SyncSevereStatus( battleMon.SevereStatus.ID );
            else
                realMon.CureSevereStatus();

            for( int m = 0; m < realMon.ActiveMoves.Count; m++ )
            {
                realMon.ActiveMoves[m].PP = battleMon.ActiveMoves[m].PP;
            }
        }

        ActiveParty = battleTrainer.Party.Select( b => ActiveParty.First( p => p.PID == b.PID ) ).ToList();
        OnPartyUpdated?.Invoke( _activeParty );
    }
}
