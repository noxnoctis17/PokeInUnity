using System.Collections;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;

public enum BattleStateEnum { Start, PlayerAction, Busy, NextTurn, SelectingNextPokemon, Over }

public enum BattleType {
    WildBattle_1v1,
    WildBattle_2v2,
    TrainerSingles,
    TrainerDoubles_1v1,
    TrainerDoubles_2v1,
    TrainerDoubles_2v2,

    }

[Serializable]
public class BattleSystem : BattleStateMachine
{
    public static BattleSystem Instance;
    private BattleType _battleType;
    public BattleType BattleType => _battleType;
    [SerializeField] private BattleArena _battleArena;
    public BattleArena BattleArena => _battleArena;
    [SerializeField] private GameObject _battleUnitPrefab;
    public GameObject BattleUnitPrefab => _battleUnitPrefab;
    private BattleUnit _playerUnit;
    public BattleUnit PlayerUnit => _playerUnit;
    private BattleUnit _enemyUnit;
    public BattleUnit EnemyUnit => _enemyUnit;
    [SerializeField] private BattleHUD _playerHUD;
    public BattleHUD PlayerHUD => _playerHUD;
    [SerializeField] private BattleHUD _enemyHUD;
    public BattleHUD EnemyHUD => _enemyHUD;
    [SerializeField] private BattleDialogueBox _dialogueBox;
    public BattleDialogueBox DialogueBox => _dialogueBox;
    public Queue<BattleDialogueBox> DialogueBoxeUpdates { get; set; }
    [SerializeField] private Transform _damageTakenPopupPrefab;
    public static Transform DamageTakenPopupPrefab;
    [SerializeField] private PlayerBattleMenu _battleMenu;
    [SerializeField] private FightMenu _fightMenu;
    public FightMenu FightMenu => _fightMenu;
    [SerializeField] private PKMNMenu _pkmnMenu;
    public PKMNMenu PKMNMenu => _pkmnMenu;
    [SerializeField] private PartyScreen _partyScreen;
    public PartyScreen PartyScreen => _partyScreen;
    [SerializeField] private EventSystem _eventSystem;
    public EventSystem EventSystem => _eventSystem;
    [SerializeField] private AudioSource _audioSource;
    public AudioSource AudioSource => _audioSource;

//----------------------------------------------------------------------------

    private BattleStateEnum _battleStateEnum;
    public BattleStateEnum BattleStateEnum => _battleStateEnum;
    public static Action OnBattleStarted;
    public static Action OnBattleEnded;
    public static Action<BattleSystem> OnPlayerCommandSelect;
    public static Action OnPlayerAction;
    public static Action OnPlayerPokemonFainted;
    public static Action OnPlayerChoseNextPokemon;

//----------------------------------------------------------------------------
    private int _turnsLeft;
    private bool _isFainted;
    private bool _isFaintedSwitch;
    private bool _isSinglesTrainerBattle;
    private bool _isDoublesTrainerBattle;

    private PokemonParty _playerParty;
    private PokemonParty _enemyTrainerParty;
    public PokemonParty PlayerParty => _playerParty;
    public PokemonParty EnemyTrainerParty => _enemyTrainerParty;
    private int _playerUnitAmount;
    public int PlayerUnitAmount => _playerUnitAmount;
    private PokemonClass _wildPokemon; //--wild pokemon you are battling. i need to clean these guys up and clean up my wild encounter setup
    public PokemonClass WildPokemon => _wildPokemon;
    private WildPokemon _encounteredPokemon; //--wild pokemon object that you ran into
    public WildPokemon EncounteredPokemon => _encounteredPokemon;

//----------------------------------Command System-----------------------------

    private List<IBattleCommand> _commandList;
    private Queue<IBattleCommand> _commandQueue;
    private UseMoveCommand _useMoveCommand;
    private SwitchPokemonCommand _switchPokemonCommand;
    private RunFromBattleCommand _runFromBattleCommand;

//----------------------------------------------------------------------
//----------------------------------------------------------------------
//----------------------------------------------------------------------

    private void OnEnable(){
        Instance = this;
        DamageTakenPopupPrefab = _damageTakenPopupPrefab;
    }

    private void Start()    {
        _commandQueue = new Queue<IBattleCommand>();
        _commandList = new List<IBattleCommand>();
        DialogueBoxeUpdates = new Queue<BattleDialogueBox>(); //--?? was this for my passive dialogue box?
    }

    private void Update(){
        switch( _battleStateEnum ){
            case BattleStateEnum.Busy :

                if( _eventSystem.enabled ){
                    _eventSystem.enabled = false;
                    BattleUIActions.OnBattleSystemBusy?.Invoke();
                } 
                break;

            case BattleStateEnum.PlayerAction :

                if( !_eventSystem.enabled ){
                    _eventSystem.enabled = true;
                    OnPlayerAction?.Invoke();
                }
                break;

            case BattleStateEnum.SelectingNextPokemon :

                if( !_eventSystem.enabled ){
                    _eventSystem.enabled = true;
                }
                break;
        }
    }

    //--Start setting up a battle. Anything that starts a battle needs to set the Battle Type. The Battle Type is responsible
    //--for HOW the Battle Stage will set itself up. From there, it will add all necessary unit positions to a list
    //--SOMEHOW, we will then assign all necessary Battle Unit objects from the Stage to their correct references here
    //--in the Battle System
    public void InitializeWildBattle( BattleType battleType ){
        _battleType = battleType;
        _playerParty = PlayerReferences.Instance.PlayerParty;

        SetState( new BattleState_Setup( this ) );
    }

    public void InitializeTrainerSingles( PokemonParty enemyTrainerParty, BattleType battleType ){
        _isSinglesTrainerBattle = true;
        _battleType = battleType;
        _playerParty = PlayerReferences.Instance.PlayerParty;
        _enemyTrainerParty = enemyTrainerParty;

        SetState( new BattleState_Setup( this ) );
    }

    public void InitializeTrainerDoubles(){
        _isDoublesTrainerBattle = true;
    }

    //--Hopefully this can correctly assign units. The BattleArena passes the gameObject containing the BattleUnit
    //--that needs to be assigned, as well as a reference to the BattleSystem's appropriate BattleUnit variable.
    //--The BattleArena class currently also handles the Wild Encounter's BattleUnit case, by passing in the Encounter's
    //--gameObject as the obj, and the EnemyUnit as the battle unit, instead of an arena gameObject like it would in
    //--a Trainer Battle, or the Player's sent-out Pokemon

    public void AssignUnits_1v1( BattleUnit playerUnit, BattleUnit enemyUnit ){
        _playerUnit = playerUnit;
        Debug.Log( _playerUnit._pokeSO.pName );
        
        _enemyUnit = enemyUnit;
        Debug.Log( _enemyUnit._pokeSO.pName + " obj name: " + name );
    }

    public void AssignPlayerUnit( BattleUnit playerUnit ){
        _playerUnit = playerUnit;
        Debug.Log( _playerUnit._pokeSO.pName );
    }

    public void AssignEnemyUnit( BattleUnit enemyUnit ){
        _enemyUnit = enemyUnit;
        Debug.Log( _enemyUnit._pokeSO.pName + " obj name: " + name );
    }

    //--When a Wild Battle is triggered by WildPokemonEvents.OnPlayerEncounter (a player runs into a wild mon),
    //--the BattleController's InitWildBattle() is called. That method eventually passes the Encounter's reference
    //--to the BattleSystem so that BattleState_Setup can properly call Setup() on the EnemyUnit to properly add the
    //--Wild Encounter as the EnemyUnit's Pokemon
    public void AssignWildPokemon( WildPokemon wildPokemon ){
        _wildPokemon = wildPokemon.Pokemon;
        _encounteredPokemon = wildPokemon;
        _wildPokemon.SetAsEnemyUnit();
    }

    public void PlayerAction(){
        _battleStateEnum = BattleStateEnum.PlayerAction;
    }

    private void OpenPartyMenu(){
        Debug.Log( "OpenPartyMenu Called" );
        _isFaintedSwitch = true;
        _pkmnMenu.gameObject.SetActive( enabled );
        _battleStateEnum = BattleStateEnum.SelectingNextPokemon;
        OnPlayerPokemonFainted?.Invoke();
        BattleUIActions.OnSubMenuOpened?.Invoke();
        BattleUIActions.OnPkmnMenuOpened?.Invoke();
    }
    
    public void ClosePartyMenu( PokemonClass pokemon ){
        var switchedTo = pokemon;
        _pkmnMenu.gameObject.SetActive( !enabled );
        BattleUIActions.OnSubMenuClosed?.Invoke();
        StartCoroutine( PerformSwitchPokemonCommand( switchedTo ) );
    }

    private void EndBattle(){
        Debug.Log( "BattleSystem EndBattle()" );

        if( !_isSinglesTrainerBattle ){
            _encounteredPokemon?.Despawn();
            _encounteredPokemon = null;
        }

        _isSinglesTrainerBattle = false;
        _isFainted = false;

        _battleArena.AfterBattleCleanup();
        _commandQueue.Clear();
        
        OnBattleEnded?.Invoke();
        BattleUIActions.OnAttackPhaseCompleted?.Invoke();
        _battleStateEnum = BattleStateEnum.Over;

        //--Should just have to pop the state. battles SHOULD never happen above another state, but we'll see
        //--in case of strange behavior after battle state pop, examine the state that's being returned to and use
        //--your added Return() function to the state, to attempt to correct those issues
        // GameStateController.Instance.GameStateMachine.Pop();
        GameStateController.Instance.GameStateMachine.ChangeState( FreeRoamState.Instance );
    }

//-------------------------------------------------------------------------------------------------------
//----------------------------------------------COMMANDS-------------------------------------------------
//-------------------------------------------------------------------------------------------------------

    //--Perform any Move
    public IEnumerator PerformMoveCommand( MoveClass move, BattleUnit attacker, BattleUnit target ){
        bool canAttack = attacker.Pokemon.OnBeforeTurn(); //--Checks if there's something impeding the pokemon from using a move this turn
        // Debug.Log( this + " canAttack is: " + canAttack ); //--canAttack log

        if( !canAttack ){
            yield return attacker.BattleHUD.UpdateHP(); //--if it cannot attack we...update its hp? i wonder why lol. i will need to go back
            yield break; //--o this might be like exclusively added for confusion lol wtf
        }

        var moveEffects = move.moveBase.MoveEffects; //--Secondary effects on moves such as burn, para, stat down, etc.
        attacker.Pokemon.CurrentPP = attacker.Pokemon.CurrentPP - move.PP; //--Reduces the PP bar by the move's PP
        yield return attacker.BattleHUD.UpdatePP(); //--Updates the PP on the hud

        //--doing it this way instead of yielding should make it not slow battles down. eventually i will make a queue
        //--that takes these in instead, and not only runs them onto a smaller update bar somewhere in the UI so that
        //--the player can still get the text play-by-play, it will add the full strings to a turn log that will come up
        //--when the player opens the "battle status" window 
        StartCoroutine( _dialogueBox.TypeDialogue( $"{attacker.Pokemon.PokeSO.pName} used {move.moveBase.MoveName}!" ) );

        // BattleUIActions.OnCommandUsed?.Invoke(); //--hide UI
        yield return new WaitForSeconds(0.5f); //--attack animation placeholder
        // BattleUIActions.OnCommandAnimationsCompleted?.Invoke(); //--restore ui
        // yield return new WaitForSeconds(1f); //--wait for ui to be on screen lol

        if( CheckMoveAccuracy( move, attacker, target ) ){
            if( move.moveBase.MoveCategory == MoveCategory.Status ){
                yield return RunMoveEffects( move.moveBase.MoveEffects, move.moveBase.MoveTarget, attacker.Pokemon, target.Pokemon );
            } else {
                var damageDetails = target.TakeDamage( move, attacker.Pokemon );
                yield return target.BattleHUD.UpdateHP();
                yield return ShowDamageDetails( damageDetails );
            }

            if( move.moveBase.SecondaryMoveEffects != null && move.moveBase.SecondaryMoveEffects.Count > 0 && target.Pokemon.CurrentHP > 0 ){
                foreach( var secondary in move.moveBase.SecondaryMoveEffects ){
                    var rand = UnityEngine.Random.Range( 1, 101 );
                    if( rand <= secondary.Chance )
                        yield return RunMoveEffects( secondary, secondary.Target, attacker.Pokemon, target.Pokemon );
                }
            }
        } else {
            yield return _dialogueBox.TypeDialogue( $"{attacker.Pokemon.PokeSO.pName}'s attack missed!" );
        }

        //--Check for faint after a move is used on the target.
        //--I need to simplify this to take in a target, and then inside compare whether it was the
        //--player's unit or enemy unit. Add in "IsPlayerUnit" and "IsEnemyUnit" bools to the PokemonClass so that you can always
        //--quickly check a unit? IsPlayerUnit sounds like it should be the case, because you want to always be able to know
        //--whether a pokemon belongs to the player or not in and out of battle i think
        yield return CheckForFaint( target ); 
    }

    //--Will eventually be adjusted, and expanded to include item, weather, field effect, and other necessary post-turn ticks
    //--MAKE A TURN MANAGER INSTEAD HE HE !
    private IEnumerator AfterTurnUpdate(){
        if( _playerUnit.Pokemon.CurrentHP > 0 ){
            Debug.Log( _playerUnit.Pokemon.CurrentHP );
            _playerUnit.Pokemon.OnAfterTurn();
            yield return new WaitForSeconds( 0.25f );
            yield return _playerUnit.BattleHUD.UpdateHP();
            Debug.Log( _playerUnit.Pokemon.CurrentHP );
            yield return CheckForFaint( _playerUnit );
        }

        if( _enemyUnit.Pokemon.CurrentHP > 0 ){
            _enemyUnit.Pokemon.OnAfterTurn();
            yield return new WaitForSeconds( 0.25f );
            yield return _enemyHUD.UpdateHP();
            yield return CheckForFaint( _enemyUnit );
        }

        yield return null;
    }

    //--Check a Move's accuracy and determine if it hits or misses
    private bool CheckMoveAccuracy( MoveClass move, BattleUnit attacker, BattleUnit target ){
        if( move.moveBase.Alwayshits )
            return true;

        float moveAccuracy = move.moveBase.Accuracy;

        int accuracy = attacker.Pokemon.StatChange[ Stat.Accuracy ];
        int evasion = target.Pokemon.StatChange[ Stat.Evasion ];

        var modifierValue = new float[] { 1f, 4f / 3f, 5f / 3f, 2f, 7f / 3f, 8f / 3f, 3f };

        if( accuracy > 0 )
            moveAccuracy *= modifierValue[accuracy];
        else
            moveAccuracy /= modifierValue[-accuracy];

        if( evasion < 0 )
            moveAccuracy /= modifierValue[evasion];
        else
            moveAccuracy *= modifierValue[-evasion];

        return UnityEngine.Random.Range( 1, 101 ) <= moveAccuracy;
    }

    //--If a Move has secondary effects, apply them appropriately
    private IEnumerator RunMoveEffects( MoveEffects effects, MoveTarget moveTarget, PokemonClass attacker, PokemonClass target ){
        //--Modify Stats
        if( effects.StatChangeList != null ){
            if( moveTarget == MoveTarget.self )
                attacker.ApplyStatChange( effects.StatChangeList ); //--Apply stat change to self, like in ddance or swords dance
            else
                target.ApplyStatChange( effects.StatChangeList );
        }

        //--Apply Severe Status Effects
        if( effects.SevereStatus != ConditionID.NONE ){
            target.SetSevereStatus( effects.SevereStatus ); //--Severe status like BRN, FRZ, PSN
        }

        //--Apply Volatile Status Effects
        if( effects.VolatileStatus != ConditionID.NONE ){
            target.SetVolatileStatus( effects.VolatileStatus ); //--Volatile status like CONFUSION
        }

        yield return null;
    }

    //--Display text update based on damage done
    private IEnumerator ShowDamageDetails( DamageDetails damageDetails ){
        //--critical hit dialogue
        if( damageDetails.Critical > 1 )
            yield return _dialogueBox.TypeDialogue( "It was a critical hit!" );

        //--super effective dialogue
        if( damageDetails.TypeEffectiveness > 1 )
            yield return _dialogueBox.TypeDialogue( "It's super effective!" );

        //--not very effective dialogue
        else if ( damageDetails.TypeEffectiveness < 1 )
            yield return _dialogueBox.TypeDialogue( "It wasn't very effective..." );
    }

    public void AfterTurnDialogue( string afterTurnDialogue ){
        StartCoroutine( _dialogueBox.TypeDialogue( afterTurnDialogue ) );
    }

    //--Check to see if a pokemon is fainted
    //--I think i should...oh. I did add FNT as a status condition
    //--I should utilize that instead of adding a random bool to each pokemon.
    //--FNT should probably be applied immediately in the case that a pokemon's HP
    //--reahces <= 0. I need to then figure out a way to manage the Command Queue
    //--in case of a faint. In singles, all input commands are cleared after a pokemon
    //--faints. this works because the priority and status updates function in a way that
    //--unit switching and items will always go first, then moves will be run with
    //--respect to priority and/or agility, and then the board state will update
    //--with field effects or status effects ticking appropriately as the final thing
    //--CheckForFainted() should perhaps therefore be called in two places.
    //--Once after each move used by a pokemon currently out (total of 4 assuming doubles)
    //--or in the case of a move's recoil, or things like life orb, rough skin, other abilities, etc
    //--and then once after game board update in the case of statuses like BRN, FRST, PSN, TOX
    public IEnumerator CheckForFaint( BattleUnit checkUnit ){

        if( checkUnit.Pokemon.CurrentHP > 0 )
            yield break; //--if the pokemon's hp is above 0 we simply leave, it hasn't fainted yet

        checkUnit.Pokemon.CureSevereStatus(); //--Clear any potential Severe Status, which would prevent FNT from being assigned
        checkUnit.Pokemon.CureVolatileStatus(); //--This also happens on faint, so it should be taken care of. Reminder to do so on switch too

        checkUnit.Pokemon.SetSevereStatus( ConditionID.FNT ); //--Set fainted status condition
        yield return new WaitForSeconds( 1f ); //--fainted animation placeholder

        if( checkUnit.Pokemon.IsPlayerUnit == true ){
            yield return _dialogueBox.TypeDialogue( $"Your {_playerUnit.Pokemon.PokeSO.pName} fainted!" );
            yield return HandleFaintedPokemon();
        }
        else if( checkUnit.Pokemon.IsEnemyUnit == true || checkUnit.Pokemon == _enemyUnit.Pokemon ){
            if( checkUnit.Pokemon == _wildPokemon ){
                yield return _dialogueBox.TypeDialogue( $"The wild {_enemyUnit.Pokemon.PokeSO.pName} fainted!" );
                yield return HandleFaintedPokemon();
            }
            else if( _isSinglesTrainerBattle ){
                yield return _dialogueBox.TypeDialogue( $"The Enemy {_enemyUnit.Pokemon.PokeSO.pName} fainted!" );
                yield return HandleFaintedPokemon();
            }
        }

        yield return null;
    }

    private IEnumerator HandleFaintedPokemon(){

        //--If Player's unit has fainted
        if( _playerUnit.Pokemon.SevereStatus?.ID == ConditionID.FNT ){
            Debug.Log( "HandleFaintedPokemon() Player Unit has fainted" );
            //--For singles BattleTypes, we immediately clear the queue and let the player change pokemon
            //--if they have any more remaining in their party. if not, the battle ends, the player should be
            //--brought back to the last visited poke center
            if( BattleType == BattleType.WildBattle_1v1 || BattleType == BattleType.TrainerSingles ){
                var nextPokemon = _playerParty.GetHealthyPokemon();

                if( nextPokemon != null ){
                    _commandQueue.Clear();
                    OpenPartyMenu();
                }
                else{
                    EndBattle();
                }
            }
        }

        //--If Enemy Unit has fainted
        if( _enemyUnit.Pokemon.SevereStatus?.ID == ConditionID.FNT ){
            Debug.Log( "HandleFaintedPokemon() Enemy Unit has fainted" );
            //--For singles BattleTypes, we immediately clear the queue. In the case of an enemy trainer,
            //--we send out their next available pokemon, and if not, the battle is ended because the player won
            if( _enemyUnit.Pokemon == _wildPokemon ){
                EndBattle();
            }
            else if( _isSinglesTrainerBattle ){
                var nextEnemyPokemon = _enemyTrainerParty.GetHealthyPokemon();

                if( nextEnemyPokemon != null ){
                    _commandQueue.Clear();
                    StartCoroutine( PerformSwitchEnemyTrainerPokemonCommand( nextEnemyPokemon ) );
                }
                else{
                    EndBattle();
                }
            }
        }

        yield return null;
    }

    public IEnumerator PerformSwitchEnemyTrainerPokemonCommand( PokemonClass pokemon ){
        //--This is currently only happening when the enemy trainer's pokemon faints and they have
        //--more left in their party. AI is eventually going to be expanded on to choose smarter
        //--pokemon instead of just the next one in their party, especially in the case of doubles.
        //--they will also be able to make smart switch calls during battle.
        //--I need to see how this functions when it's added as a BattleCommand, rather than just hard
        //--executed, but i can save that for another time, for now.
        _battleStateEnum = BattleStateEnum.Busy;

        _enemyUnit.Pokemon.CureVolatileStatus(); //--Cure the volatile status of the previous pokemon
        _enemyUnit.Setup( pokemon, _enemyHUD, this ); //--Assign and setup the new pokemon

        yield return _dialogueBox.TypeDialogue( $"Go, {pokemon.PokeSO.pName}!" );

        PlayerAction();
    }

    //--When the player's pokemon faints, this is called explicitly, rather than as a command added to the command queue
    public IEnumerator PerformSwitchPokemonCommand( PokemonClass pokemon ){
        //--for the future if i want to target the previous pokemon with a move (pursuit), specificy the previous pokemon in this class? //--yes 4/12/2023
        _battleStateEnum = BattleStateEnum.Busy;
        _playerUnit.Pokemon.CureVolatileStatus(); //--Cure the volatile status of the previous pokemon. Will need to set a previous pokemon soon

        if( !_isFaintedSwitch ){
            yield return _dialogueBox.TypeDialogue( $"{_playerUnit.Pokemon.PokeSO.pName}, come back!" );
        }
        
        _playerUnit.Setup( pokemon, _playerHUD, this );
        // _fightMenu.SetUpMoves( pokemon.Moves );

        yield return _dialogueBox.TypeDialogue( $"Go, {pokemon.PokeSO.pName}!" );
        yield return new WaitForSeconds( 1f );
        // BattleUIActions.OnCommandAnimationsCompleted?.Invoke();

        if( _isFaintedSwitch )
            _isFaintedSwitch = false;
        yield return new WaitForSeconds( 0.5f );
        PlayerAction();
    }

    public IEnumerator PerformRunFromBattleCommand(){
        Debug.Log( "You got away!" );
        EndBattle();
        yield return new WaitForSeconds( 1f );
    }

//--------------------------------------------------------------------------------------------------------
//---------------------------------------COMMAND SYSTEM METHODS-------------------------------------------
//--------------------------------------------------------------------------------------------------------

    public void DetermineCommandOrder(){
        _commandList = _commandList.OrderBy( prio => prio.CommandPriority ).ThenBy( prio => prio.UnitAgility ).ToList();

        for( int i = _commandList.Count - 1; i >= 0; i-- ){
            AddCommand( _commandList[i] );
            _commandList.RemoveAt( i );
        }

        StartCoroutine( ExecuteCommandQueue() );
    }

    public void SetPlayerMoveCommand( BattleUnit attacker, MoveClass move ){
        _useMoveCommand = new UseMoveCommand( move, attacker, _enemyUnit, this );
        _commandList.Add( _useMoveCommand );
        _enemyUnit.BattleAI.OnPlayerCommandSelect?.Invoke();
    }

    public void SetEnemyMoveCommand( BattleUnit attacker, MoveClass move ){
        _useMoveCommand = new UseMoveCommand( move, attacker, _playerUnit, this );
        _commandList.Add( _useMoveCommand );
        DetermineCommandOrder();
    }

    public void SetSwitchPokemonCommand( PokemonClass pokemon ){
        _switchPokemonCommand = new SwitchPokemonCommand( pokemon, this );
        _commandList.Add( _switchPokemonCommand );
        _enemyUnit.BattleAI.OnPlayerCommandSelect?.Invoke();
    }

    public void SetRunFromBattleCommand(){
        _runFromBattleCommand = new RunFromBattleCommand( this );
        _commandList.Add( _runFromBattleCommand );
        _enemyUnit.BattleAI.OnPlayerCommandSelect?.Invoke();
    }

    public void AddCommand( IBattleCommand command ){
        _commandQueue.Enqueue( command );
    }

    public IEnumerator ExecuteCommandQueue(){
        while( _commandQueue.Count > 0 ){
            _battleStateEnum = BattleStateEnum.Busy;
            yield return _commandQueue.Dequeue().ExecuteBattleCommand();
            _turnsLeft = _commandQueue.Count; //--i have no idea what i was going to use this for

            // //--In Singles, Faints are handled immediately
            // if( _isSinglesTrainerBattle ){
            //     yield return HandleFaintedPokemon();
            // }
        }

        yield return new WaitForSeconds( 0.25f );
        //--This should handle all board state updates like leftovers, status, weather, and field effects
        //--It gets called after all turns are completed and the command queue is empty
        yield return AfterTurnUpdate();

        //--In Doubles, faints are handled after all possible turns are completed.
        if( _isDoublesTrainerBattle ){
            yield return HandleFaintedPokemon();
        }
        
        BattleUIActions.OnAttackPhaseCompleted?.Invoke();
        yield return new WaitForSeconds( 0.25f );
        PlayerAction();
        yield return null;
    }

    // private void OnGUI() {
    //     var style = new GUIStyle();
    //     style.fontSize = 24;
    //     style.fontStyle = FontStyle.Bold;
    //     style.normal.textColor = Color.white;

    //     GUILayout.BeginArea( new Rect( 710, 500, 500, 500 ) );
    //     GUILayout.Label( "ENEMY MOVES", style );
    //     if( _enemyUnit?.Pokemon.Moves.Count > 0 ){
    //         foreach( var move in _enemyUnit.Pokemon.Moves ){
    //             GUILayout.Label( move?.moveBase.MoveName, style );
    //         }
    //     }
    //     GUILayout.EndArea();

    // }

}
