using System.Collections;
using System;
using System.Linq;
using UnityEngine;
using System.Collections.Generic;
using DG.Tweening;
using Cinemachine;
using UnityEngine.EventSystems;

public enum BattleStateEnum { PlayerAction, Busy, SelectingNextPokemon }

public enum BattleType {
    WildBattle_1v1,
    WildBattle_2v2,
    TrainerSingles,
    TrainerDoubles,
    TrainerMulti_2v1,
    TrainerMulti_2v2,

    }

[Serializable]
public class BattleSystem : BattleStateMachine
{
    //================================[ REFERENCES ]===========================================
    //--Serialized Fields/private-----------------------------------------
    [SerializeField] private BattleArena _battleArena;
    [SerializeField] private GameObject _battleUnitPrefab;
    [SerializeField] private EventSystem _eventSystem;
    [SerializeField] private BattleHUD _playerHUD;
    [SerializeField] private BattleHUD _enemyHUD;
    [SerializeField] private BattleDialogueBox _dialogueBox;
    [SerializeField] private Transform _damageTakenPopupPrefab;
    [SerializeField] private PlayerBattleMenu _battleMenu;
    [SerializeField] private FightMenu _fightMenu;
    [SerializeField] private PKMNMenu _pkmnMenu;
    [SerializeField] private PartyScreen _partyScreen;
    [SerializeField] private LearnMoveMenu _learnMoveMenu;
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private GameObject _thrownPokeBall;
    [SerializeField] private CinemachineVirtualCamera _singleTargetCamera;
    
    //--Private-----------------------------------------------------------
    private BattleType _battleType;
    private BattleUnit _playerUnit;
    private BattleUnit _enemyUnit;

    //--public/getters/properties----------------------------------------------
    public static BattleSystem Instance;
    public static bool BattleIsActive { get; private set; }
    public EventSystem EventSystem => _eventSystem;
    public BattleType BattleType => _battleType;
    public BattleArena BattleArena => _battleArena;
    //--Units
    public GameObject Trainer1Center { get; private set; }
    public GameObject BattleUnitPrefab => _battleUnitPrefab;
    public BattleUnit PlayerUnit => _playerUnit;
    public BattleUnit EnemyUnit => _enemyUnit;
    //--HUD
    public BattleHUD PlayerHUD => _playerHUD;
    public BattleHUD EnemyHUD => _enemyHUD;
    //--Dialogue/Damage Text
    public BattleDialogueBox DialogueBox => _dialogueBox;
    public Queue<BattleDialogueBox> DialogueBoxeUpdates { get; set; }
    public static Transform DamageTakenPopupPrefab;
    //--Menus and Screens
    public PlayerBattleMenu PlayerBattleMenu => _battleMenu;
    public FightMenu FightMenu => _fightMenu;
    public PKMNMenu PKMNMenu => _pkmnMenu;
    public PartyScreen PartyScreen => _partyScreen;
    public LearnMoveMenu LearnMoveMenu => _learnMoveMenu;
    //--EXP
    public int TotalPartyExpGain { get; private set; }
    public int TotalPartyEffortGain { get; private set; }
    //--Audio
    public AudioSource AudioSource => _audioSource;

//=============================[ STATE MACHINE ]===============================================================

    private BattleStateEnum _battleStateEnum;
    public BattleStateEnum BattleStateEnum => _battleStateEnum;
    public StateStackMachine<BattleSystem> BattleSystemStateMachine { get; private set; }

//================================[ EVENTS ]===================================================================
    public static Action OnBattleStarted;
    public static Action OnBattleEnded;
    public static Action OnPlayerPokemonFainted;
    public static Action OnPlayerChoseNextPokemon;

//----------------------------------------------------------------------------
    private int _turnsLeft;
    private bool _isFaintedSwitch;
    private bool _isSinglesTrainerBattle;
    private bool _isDoublesTrainerBattle;
    private bool _levelUpCompleted;
    public bool IsSinglesTrainerBattle => _isSinglesTrainerBattle;
    public bool IsDoublesTrainerBattle => _isDoublesTrainerBattle;

//=========================[ POKEMON AND PLAYER/TRAINER PARTIES ]================================================
    //--private
    private TrainerClass _enemyTrainer1;
    private PokemonParty _playerParty;
    private PokemonParty _enemyTrainerParty;
    private int _playerUnitAmount;
    private PokemonClass _wildPokemon;
    private WildPokemon _encounteredPokemon; //--wild pokemon object that you ran into
    private PokemonClass _caughtPokemon; //--Pokemon you caught
    //--public/getters/properties
    public PokemonParty PlayerParty => _playerParty;
    public PokemonParty EnemyTrainerParty => _enemyTrainerParty;
    public int PlayerUnitAmount => _playerUnitAmount;
    public PokemonClass WildPokemon => _wildPokemon;
    public WildPokemon EncounteredPokemon => _encounteredPokemon;

//====================================[ COMMAND SYSTEM ]=======================================================

    private List<IBattleCommand> _commandList;
    private Queue<IBattleCommand> _commandQueue;
    private UseMoveCommand _useMoveCommand;
    private UseItemCommand _useItemCommand;
    private SwitchPokemonCommand _switchPokemonCommand;
    private RunFromBattleCommand _runFromBattleCommand;

//============================================================================================================
//============================================================================================================
//============================================================================================================

    private void OnEnable(){
        Instance = this;
        DamageTakenPopupPrefab = _damageTakenPopupPrefab;
        // BattleSystemStateMachine = new( this );

        DialogueManager.Instance.OnSystemDialogueComplete += SetLevelUpCompleted;

        _commandQueue = new Queue<IBattleCommand>();
        _commandList = new List<IBattleCommand>();
        DialogueBoxeUpdates = new Queue<BattleDialogueBox>(); //--?? was this for my passive dialogue box? --3/26/24 probably lol
        TotalPartyExpGain = 0;
        BattleIsActive = true;
    }

    private void OnDisable(){
        Instance = null;

        DialogueManager.Instance.OnSystemDialogueComplete += SetLevelUpCompleted;
    }

    //--Battle System State Functions. The Current BattleSystem doesn't necessarily progress through states
    //--In a way that is significant. Currently, the flow of the battle system is mostly hard-coded, and
    //--depending on the current point in the flow, the state will change between busy, playeraction, and selecting a pokemon
    //--if the user's pokemon has been fainted (or eventually if forced to switch by shit like self u-turn or enemy roar)
    //--therefore, based on the actual code executed for each particular state, i don't think it's necessary to manage a state machine
    //--the one upside of using a state stack machine is being able to literally stack and pop each "state" in any order we might need
    //--eliminating any manual management of the states beyond pushing or popping the current state. buut...
    //--we're literally ONLY managing the player's controls in each state. as long as we call the appropriate "state"
    //--function, the correct set of controls should be set at any given time.

    private void SetBusyState(){
        Debug.Log( "SetBusyState" );
        if( PlayerBattleMenu.BattleMenuStateMachine.CurrentState != PlayerBattleMenu.PausedState )
            PlayerBattleMenu.OnPauseState?.Invoke();
    }

    private void SetPlayerActionState(){
        Debug.Log( "SetPlayerActionState" );
        if( PlayerBattleMenu.BattleMenuStateMachine.CurrentState == PlayerBattleMenu.PausedState )
            PlayerBattleMenu.OnUnpauseState?.Invoke();
    }

    private void SetForceSelectPokemonState(){
        if( PlayerBattleMenu.BattleMenuStateMachine.CurrentState == PlayerBattleMenu.PausedState )
            PlayerBattleMenu.OnChangeState?.Invoke( _pkmnMenu );
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

    public void InitializeTrainerSingles( TrainerClass trainer ){
        _isSinglesTrainerBattle = true;
        _enemyTrainer1 = trainer;

        //--Grab refs
        Trainer1Center = trainer.TrainerCenter;
        _battleType = trainer.BattleType;
        _playerParty = PlayerReferences.Instance.PlayerParty;
        _enemyTrainerParty = trainer.TrainerParty;

        SetState( new BattleState_Setup( this ) );
    }

    public void InitializeTrainerDoubles( TrainerClass trainer ){
        _isDoublesTrainerBattle = true;
        _enemyTrainer1 = trainer;
    }

    public void AssignUnits_1v1( BattleUnit playerUnit, BattleUnit enemyUnit ){
        _playerUnit = playerUnit;
        _enemyUnit = enemyUnit;
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
        Debug.Log( "PlayerAction" );
        _battleStateEnum = BattleStateEnum.PlayerAction;
        SetPlayerActionState();
    }

    private void FaintedSwitchPartyMenu(){
        _isFaintedSwitch = true;
        SetForceSelectPokemonState();
        OnPlayerPokemonFainted?.Invoke();
        BattleUIActions.OnSubMenuOpened?.Invoke();
        BattleUIActions.OnPkmnMenuOpened?.Invoke();
    }
    
    public void SetFaintedSwitchMon( PokemonClass switchedMon ){
        //--The pkmn menu pops its own state before this function is called, therefore we need to
        //--Set ourselves back to the busy state.
        SetBusyState();
        StartCoroutine( PerformSwitchPokemonCommand( switchedMon ) );
    }

    private IEnumerator SetForcedSwitchMon( PokemonClass switchedMon ){
        //--for when something like u-turn or roar happens and we need
        //--to include it as part of the battle phase coroutine chain
        yield return PerformSwitchPokemonCommand( switchedMon );
    }

    //--Will call this where necessary in HandleFaintedPokemon()
    //--We won't be applying exp directly to pokemon immediately during battle. instead,
    //--The entire party will gain the combined EXP after battle is over. mons that participated
    //--at all will gain full exp, mons that did not will gain, for now, 75%
    //--I may actually need to place a method that actually applies the exp into the PokemonParty script
    //--since it will occur after battle. or, just have a "post battle" state before the battle system
    //--ends. that sounds more appropriate
    private IEnumerator HandleExpGain( BattleUnit faintedUnit ){
        //--Exp Gain
        int expYield = faintedUnit.Pokemon.PokeSO.ExpYield;
        int unitLevel = faintedUnit.Pokemon.Level;
        float trainerBonus = ( _isSinglesTrainerBattle || _isDoublesTrainerBattle ) ? 1.5f : 1f;

        int expGain = Mathf.FloorToInt( expYield * unitLevel * trainerBonus ) / 7;

        //--Effort Points Gain
        int effortYield = faintedUnit.Pokemon.PokeSO.EffortYield;

        //--Add to totals
        TotalPartyExpGain += expGain;
        TotalPartyEffortGain += effortYield;

        yield return null; //--temp so unity shuts up
    }

    public void SetLevelUpCompleted( bool isComplete ){
        _levelUpCompleted = isComplete;
        Debug.Log( "is level up completed: " + _levelUpCompleted );
    }

    private IEnumerator PostBattleScreen( PokemonClass caughtPokemon = null ){
        _battleStateEnum = BattleStateEnum.Busy;
        SetBusyState();
        WaitForSeconds wait = new( 1f );

        //--Add Total Gained Exp. Eventually account for battle participation
        if( TotalPartyExpGain > 0 ){
            yield return GivePartyExp( wait );
        }

        //--Add Caught Pokemon to Party here AFTER exp calculations, so it doesn't gain EXP from itself being caught LOL
        if( caughtPokemon != null){
            _playerParty.AddPokemon( caughtPokemon );
        }

        //--Post Trainer Battle
        if( _isSinglesTrainerBattle ){
            _enemyTrainer1.SetDefeated();
        }

        yield return wait;
        EndBattle();
    }

    private IEnumerator GivePartyExp( WaitForSeconds wait ){
        //--Gain Exp Dialogue
        yield return _dialogueBox.TypeDialogue( $"All Pokemon received {TotalPartyExpGain} Exp!" );
        yield return _dialogueBox.TypeDialogue( $"All Pokemon received {TotalPartyEffortGain} Effort Points!" );

        //--Give Exp to each Pokemon in player's party directly
        foreach( PokemonClass pokemon in PlayerReferences.Instance.PlayerParty.PartyPokemon ){

            //--Gain EXP
            pokemon.GainExp( TotalPartyExpGain, TotalPartyEffortGain );

            //--If the current Pokemon is the Active Pokemon, refresh the BattleHUD
            yield return RefreshHUD( pokemon );

            //--Check for Level up
            while( pokemon.CheckForLevelUpBattle() ){
                yield return _dialogueBox.TypeDialogue( $"{pokemon.PokeSO.pName} grew to level {pokemon.Level}!" );

                //--Try Learn Moves
                if( pokemon.GetNextLearnableMove() != null ){
                    if( pokemon.Moves.Count == PokemonSO.MAXMOVES ){
                        yield return _dialogueBox.TypeDialogue( $"{pokemon.PokeSO.pName} is trying to learn {pokemon.GetNextLearnableMove().MoveSO.MoveName}," );
                        yield return _dialogueBox.TypeDialogue( $"But it can't use more than four moves during battle." );
                        yield return _dialogueBox.TypeDialogue( $"Which move will you set aside?" );

                        pokemon.TryLearnMove( pokemon.GetNextLearnableMove(), _learnMoveMenu, this );

                        PlayerBattleMenu.OnPushNewState?.Invoke( PlayerBattleMenu.MoveLearnSelectionState );
                        yield return new WaitUntil( () => !_learnMoveMenu.gameObject.activeSelf );

                        if( _learnMoveMenu.ReplacedMove )
                            yield return _dialogueBox.TypeDialogue( $"{pokemon.PokeSO.pName} learned {pokemon.GetNextLearnableMove().MoveSO.MoveName}!" );
                        else
                            yield return _dialogueBox.TypeDialogue( $"{pokemon.PokeSO.pName} didn't learn {pokemon.GetNextLearnableMove().MoveSO.MoveName}!" );
                        
                    }
                    else{
                        pokemon.TryLearnMove( pokemon.GetNextLearnableMove(), _learnMoveMenu, this );
                        yield return _dialogueBox.TypeDialogue( $"{pokemon.PokeSO.pName} learned {pokemon.GetNextLearnableMove().MoveSO.MoveName}!" );
                    }
                }

                //--If the current Pokemon is the Active Pokemon, refresh the BattleHUD
                yield return RefreshHUD( pokemon );

                yield return wait;

            }
        }
    }

    private IEnumerator RefreshHUD( PokemonClass pokemon ){
        //--If the current Pokemon is the Active Pokemon, refresh the BattleHUD
        if( pokemon == _playerUnit.Pokemon ){
            _playerUnit.BattleHUD.RefreshHUD();
            yield return _playerHUD.SetExpSmooth( true );
        }
    }

    private void EndBattle(){
        Debug.Log( "BattleSystem EndBattle()" );

        if( !_isSinglesTrainerBattle ){
            _encounteredPokemon.Despawn();
            _encounteredPokemon = null;
        }

        if( _isSinglesTrainerBattle || _isDoublesTrainerBattle )
            _enemyTrainerParty = null;

        _pkmnMenu.PartyScreen.ClearParty();
        _playerParty = null;

        _isSinglesTrainerBattle = false;
        // _isFainted = false;

        _commandQueue.Clear();
        _commandList = null;

        _battleArena.AfterBattleCleanup();

        TotalPartyExpGain = 0;
        TotalPartyEffortGain = 0;

        ConditionsDB.Clear();
        TypeColorsDB.Clear();
        
        OnBattleEnded?.Invoke();
        BattleUIActions.OnAttackPhaseCompleted?.Invoke();

        BattleIsActive = false;
        GameStateController.Instance.GameStateMachine.Pop();
        // GameStateController.Instance.GameStateMachine.ChangeState( FreeRoamState.Instance );
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

        var moveEffects = move.MoveSO.MoveEffects; //--Secondary effects on moves such as burn, para, stat down, etc.
        // attacker.Pokemon.CurrentPP = attacker.Pokemon.CurrentPP - move.PP; //--Reduces the PP bar by the move's PP //--REDUCE MOVE PP HERE WHEN YOU REIMPLEMENT IT
        // yield return attacker.BattleHUD.UpdatePP(); //--Updates the PP on the hud

        //--doing it this way instead of yielding should make it not slow battles down. eventually i will make a queue
        //--that takes these in instead, and not only runs them onto a smaller update bar somewhere in the UI so that
        //--the player can still get the text play-by-play, it will add the full strings to a turn log that will come up
        //--when the player opens the "battle status" window 
        // StartCoroutine( _dialogueBox.TypeDialogue( $"{attacker.Pokemon.PokeSO.pName} used {move.moveBase.MoveName}!" ) );
        yield return _dialogueBox.TypeDialogue( $"{attacker.Pokemon.PokeSO.pName} used {move.MoveSO.MoveName}!" );

        // BattleUIActions.OnCommandUsed?.Invoke(); //--hide UI
        // BattleUIActions.OnCommandAnimationsCompleted?.Invoke(); //--restore ui
        // yield return new WaitForSeconds(1f); //--wait for ui to be on screen lol

        if( CheckMoveAccuracy( move, attacker, target ) ){
            if( move.MoveSO.MoveCategory == MoveCategory.Status ){
                yield return attacker.PokeAnimator.PlayStatusAttackAnimation();
                yield return RunMoveEffects( move.MoveSO.MoveEffects, move.MoveSO.MoveTarget, attacker.Pokemon, target.Pokemon );
            }
            else{
                if( move.MoveSO.MoveCategory == MoveCategory.Physical )
                    yield return attacker.PokeAnimator.PlayPhysicalAttackAnimation( attacker.transform, target.transform );

                if( move.MoveSO.MoveCategory == MoveCategory.Special )
                    yield return attacker.PokeAnimator.PlaySpecialAttackAnimation();
                
                var damageDetails = target.TakeDamage( move, attacker.Pokemon );
                yield return target.BattleHUD.UpdateHP();
                yield return ShowDamageDetails( damageDetails );
            }

            if( move.MoveSO.SecondaryMoveEffects != null && move.MoveSO.SecondaryMoveEffects.Count > 0 && target.Pokemon.CurrentHP > 0 ){
                foreach( var secondary in move.MoveSO.SecondaryMoveEffects ){
                    var rand = UnityEngine.Random.Range( 1, 101 );
                    if( rand <= secondary.Chance )
                        yield return RunMoveEffects( secondary, secondary.Target, attacker.Pokemon, target.Pokemon );
                }
            }
        }
        else{
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
            // Debug.Log( _playerUnit.Pokemon.CurrentHP );
            _playerUnit.Pokemon.OnAfterTurn();
            yield return new WaitForSeconds( 0.25f );
            yield return _playerUnit.BattleHUD.UpdateHP();
            // Debug.Log( _playerUnit.Pokemon.CurrentHP );
            yield return CheckForFaint( _playerUnit );
        }

        if( _enemyUnit.Pokemon.CurrentHP > 0 ){
            // _enemyUnit.Pokemon.OnAfterTurn();
            yield return new WaitForSeconds( 0.25f );
            yield return _enemyHUD.UpdateHP();
            yield return CheckForFaint( _enemyUnit );
        }

        yield return null;
    }

    //--Check a Move's accuracy and determine if it hits or misses
    private bool CheckMoveAccuracy( MoveClass move, BattleUnit attacker, BattleUnit target ){
        if( move.MoveSO.Alwayshits )
            return true;

        float moveAccuracy = move.MoveSO.Accuracy;

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
    //--respect to priority and unit speed, and then the board state will update
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
        yield return checkUnit.PokeAnimator.PlayFaintAnimation(); //--fainted animation placeholder

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
            // Debug.Log( "HandleFaintedPokemon() Player Unit has fainted" );
            //--For singles BattleTypes, we immediately clear the queue and let the player change pokemon
            //--if they have any more remaining in their party. if not, the battle ends, the player should be
            //--brought back to the last visited poke center
            if( BattleType == BattleType.WildBattle_1v1 || BattleType == BattleType.TrainerSingles ){
                var nextPokemon = _playerParty.GetHealthyPokemon();

                if( nextPokemon != null ){
                    _commandQueue.Clear();
                    FaintedSwitchPartyMenu();
                }
                else{
                    yield return PostBattleScreen();
                }
            }
        }

        //--If Enemy Unit has fainted
        if( _enemyUnit.Pokemon.SevereStatus?.ID == ConditionID.FNT ){
            // Debug.Log( "HandleFaintedPokemon() Enemy Unit has fainted" );
            //--For singles BattleTypes, we immediately clear the queue. In the case of an enemy trainer,
            //--we send out their next available pokemon, and if not, the battle is ended because the player won
            if( _enemyUnit.Pokemon == _wildPokemon ){
                yield return HandleExpGain( _enemyUnit );
                yield return PostBattleScreen();
            }
            else if( _isSinglesTrainerBattle ){
                yield return HandleExpGain( _enemyUnit );
                var nextEnemyPokemon = _enemyTrainerParty.GetHealthyPokemon();

                if( nextEnemyPokemon != null ){
                    _commandQueue.Clear();
                    StartCoroutine( PerformSwitchEnemyTrainerPokemonCommand( nextEnemyPokemon ) );
                }
                else{
                    yield return PostBattleScreen();
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
        SetBusyState();

        yield return _enemyUnit.PokeAnimator.PlayExitBattleAnimation( Trainer1Center.transform );
        _enemyUnit.Pokemon.CureVolatileStatus(); //--Cure the volatile status of the previous pokemon
        _enemyUnit.Setup( pokemon, _enemyHUD, this ); //--Assign and setup the new pokemon

        yield return _dialogueBox.TypeDialogue( $"Go, {pokemon.PokeSO.pName}!" );
        yield return _enemyUnit.PokeAnimator.PlayEnterBattleAnimation( _enemyUnit.transform, Trainer1Center.transform );

        PlayerAction();
    }

    //--When the player's pokemon faints, this is called explicitly, rather than as a command added to the command queue
    public IEnumerator PerformSwitchPokemonCommand( PokemonClass pokemon ){
        //--for the future if i want to target the previous pokemon with a move (pursuit), specificy the previous pokemon in this class? //--yes 4/12/2023
        // _battleStateEnum = BattleStateEnum.Busy;
        // SetBusyState();
        _playerUnit.Pokemon.CureVolatileStatus(); //--Cure the volatile status of the previous pokemon. Will need to set a previous pokemon soon

        if( !_isFaintedSwitch )
            yield return _dialogueBox.TypeDialogue( $"{_playerUnit.Pokemon.PokeSO.pName}, come back!" );

        yield return _playerUnit.PokeAnimator.PlayExitBattleAnimation( PlayerReferences.Instance.PlayerCenter );
        // _playerUnit.PokeAnimator.ResetAnimations(); //--Clear's the animator which resets the animations before initialization of the incoming mon
        
        _playerUnit.Setup( pokemon, _playerHUD, this );
        // _fightMenu.SetUpMoves( pokemon.Moves );

        yield return _dialogueBox.TypeDialogue( $"Go, {pokemon.PokeSO.pName}!" );
        yield return _playerUnit.PokeAnimator.PlayEnterBattleAnimation( _playerUnit.transform, PlayerReferences.Instance.PlayerCenter );
        yield return new WaitForSeconds( 0.25f );
        // BattleUIActions.OnCommandAnimationsCompleted?.Invoke();

        if( _isFaintedSwitch ){
            //--During a fainted switch, the menu gets paused, but because fainted
            //--switch happens after the command queue, there's never an opportunity for
            //--the menu to become unpaused, therefore it needs to happen here in this
            //--fainted switch conditional area
            PlayerAction();
            _isFaintedSwitch = false;
        }

        yield return new WaitForSeconds( 0.5f );
        
    }

    public IEnumerator PerformRunFromBattleCommand(){
        // Debug.Log( "You got away!" );
        yield return new WaitForSeconds( 1f );
        EndBattle();
    }

    public IEnumerator ThrowPokeball(){
        // Debug.Log( "threw pokeball" );
        // _battleStateEnum = BattleStateEnum.Busy;
        // SetBusyState();

        var playerBallPosition = PlayerReferences.Instance.PlayerCenter.position;

        yield return _dialogueBox.TypeDialogue( $"Catch threw a Pokeball!" );

        var thrownBall = Instantiate( _thrownPokeBall, playerBallPosition, Quaternion.identity );
        var originalPos = _enemyUnit.transform;
        Vector3 originalScale = _enemyUnit.transform.localScale;
        Vector3 ballBouncePos = new Vector3( 0.5f, 0.5f, 3f );

        var sequence = DOTween.Sequence();
        sequence.Append( thrownBall.transform.DOJump( _enemyUnit.transform.position, 3f, 1, 0.75f ) );
        sequence.Append( thrownBall.transform.DOJump( _enemyUnit.transform.position + ballBouncePos, 1f, 1, 0.75f ) );

        yield return sequence.WaitForCompletion();
        yield return _enemyUnit.PokeAnimator.PlayCaptureAnimation( thrownBall.transform );
        yield return thrownBall.transform.DOMoveY( originalPos.position.y, 0.5f ).WaitForCompletion();

        _singleTargetCamera.LookAt = thrownBall.transform;
        _singleTargetCamera.gameObject.SetActive( true );

        int shakeCount = TryToCatchPokemon( _enemyUnit.Pokemon );
        Debug.Log( _enemyUnit.PokeSO.CatchRate );
        Debug.Log( shakeCount );

        for( int i = 0; i < Mathf.Min( shakeCount, 3 ); i++ ){
            yield return new WaitForSeconds( 0.5f );
            yield return thrownBall.GetComponentInChildren<PokeballAnimator>().TryCaptureShake();
        }
        if( shakeCount == 4 ){
            //--Pokemon is Caught
            yield return _dialogueBox.TypeDialogue( $"{_enemyUnit.Pokemon.PokeSO.pName} was caught!" );
            yield return thrownBall.GetComponentInChildren<PokeballAnimator>().Fadeout( 1.5f, true );
            
            _singleTargetCamera.gameObject.SetActive( false );
            Destroy( thrownBall );
            yield return HandleExpGain( _enemyUnit );
            //--Pokemon is added to party post battle, so it doesn't gain exp from itself
            yield return PostBattleScreen( _enemyUnit.Pokemon );
        }
        else{
            //--Pokemon eats your ass
            yield return new WaitForSeconds( 1f );
            yield return thrownBall.GetComponentInChildren<PokeballAnimator>().Fadeout( 0.25f, false );
            yield return _enemyUnit.PokeAnimator.PlayBreakoutAnimation( originalPos );
            _singleTargetCamera.gameObject.SetActive( false );

            Destroy( thrownBall );

            if( shakeCount == 0 )
                yield return _dialogueBox.TypeDialogue( $"It broke free!" );
            if( shakeCount == 1 )
                yield return _dialogueBox.TypeDialogue( $"Argh, almost caught!" );
            if( shakeCount == 2 )
                yield return _dialogueBox.TypeDialogue( $"Shoot, it was so close!" );
            if( shakeCount == 3 )
                yield return _dialogueBox.TypeDialogue( $"FUCK     !" );
        }
    }

    private int TryToCatchPokemon( PokemonClass pokemon ){
        float a = ( 3 * pokemon.MaxHP - 2 * pokemon.CurrentHP ) * pokemon.PokeSO.CatchRate * ConditionsDB.GetStatusBonus( pokemon.SevereStatus ) / ( 3 * pokemon.MaxHP );

        if( a >= 255 )
            return 4;

        float b = 1048560 / Mathf.Sqrt( Mathf.Sqrt( 16711680 / a) );

        int shakeCount = 0;

        while( shakeCount < 4 ){
            if( UnityEngine.Random.Range( 0, 65535 ) >= b )
                break;

            shakeCount++;
        }

        return shakeCount;
    }

//--------------------------------------------------------------------------------------------------------
//-------------------------------------[ COMMAND SYSTEM METHODS ]-----------------------------------------
//--------------------------------------------------------------------------------------------------------

    public void DetermineCommandOrder(){
        SetBusyState();
        Debug.Log( "DetermineCommandOrder()" );

        _commandList = _commandList.OrderBy( prio => prio.CommandPriority ).ThenBy( prio => prio.AttackPriority).ThenBy( prio => prio.UnitAgility ).ToList();

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

    //--Currently all player options lead to this, where the ai simply chooses a random move
    //--which then gets added to the command list, and then the priority sorting -> queue execution happens
    public void SetEnemyMoveCommand( BattleUnit attacker, MoveClass move ){
        _useMoveCommand = new UseMoveCommand( move, attacker, _playerUnit, this );
        _commandList.Add( _useMoveCommand );
        DetermineCommandOrder();
    }

    public void SetUseItemCommand(){
        _useItemCommand = new UseItemCommand( this );
        _commandList.Add( _useItemCommand );
        _enemyUnit.BattleAI.OnPlayerCommandSelect?.Invoke();
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

        //--In Doubles, the fainted pokemon are handled after all possible turns are completed.
        if( _isDoublesTrainerBattle ){
            yield return HandleFaintedPokemon();
        }
        
        BattleUIActions.OnAttackPhaseCompleted?.Invoke();
        yield return new WaitForSeconds( 0.25f );
        PlayerAction();
        yield return null;
    }

}
