using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Cinemachine;
using UnityEngine.Rendering.Universal;
using DG.Tweening;

public class BattleArena : MonoBehaviour
{
    private const float DEFAULT_POKEMON_SIZE = 3f;
    private const float DEFAULT_CIRCLE_SIZE = 18f;
    private const int DOUBLES_COUNT = 2;
    private Vector3 _defaultPokemonSize = new( DEFAULT_POKEMON_SIZE, DEFAULT_POKEMON_SIZE, DEFAULT_POKEMON_SIZE );
    private Vector3 _defaultCircleSize = new( DEFAULT_CIRCLE_SIZE, DEFAULT_CIRCLE_SIZE, DEFAULT_CIRCLE_SIZE );
    private BattleSystem _battleSystem;
    private BattleType _battleType;
    private CinemachineBrain _cmBrain;
    public CinemachineBrain CMBrain => _cmBrain;
    [SerializeField] private CinemachineVirtualCamera _1v1_EnemyIntroCamera;
    [SerializeField] private CinemachineFreeLook _2v2_EnemyIntroCamera;
    [SerializeField] private CinemachineFreeLook _singlesMainCamera;
    [SerializeField] private CinemachineFreeLook _doublesMainCamera;
    [SerializeField] private DecalProjector _arenaDecal;
    [SerializeField] private GameObject _2v2_Encounter;
    private Camera _mainCamera;
    private WildPokemon _wildEncounter;
    private GameObject _enemyTrainer1/*, _enemyTrainer2*/;
    [SerializeField] private GameObject _arenaContainer; //--parent object of the entire Battle Arena. Should always be at the same world position as the _stagePivot, which gets placed according to BattleType
    [SerializeField] private GameObject _arenaPivot; //--pivot will be placed according to encounter ex: same position as wild pokemon
    [SerializeField] private float _singlesArenaSize; //--wire disc radius for singles
    [SerializeField] private float _doublesArenaSize; //--wire disc radius for doubles
    [SerializeField] private GameObject _arenaGizmoCenter; //--wire disc center
    [SerializeField] private GameObject _spectator1;
    [SerializeField] private GameObject _singlesTrainer1, _singlesTrainer2; //--well, whatever
    [SerializeField] private GameObject _doublesTrainer1, _doublesTrainer2, _doublesTrainer3, _doublesTrainer4; //--player side odds vs evens
    [SerializeField] private GameObject _singlesUnit1, _singlesUnit2; //--again, whatever
    [SerializeField] private GameObject _doublesUnit1, _doublesUnit2, _doublesUnit3, _doublesUnit4; //--player 1 & 2, opponent 3 & 4
    [SerializeField] private GameObject _singlesCircle1, _singlesCircle2;
    [SerializeField] private GameObject _doublesCircle1, _doublesCircle2, _doublesCircle3, _doublesCircle4; //--player 1 & 2, opponent 3 & 4
    [SerializeField] private GameObject _singlesPokemon1, _singlesPokemon2;
    [SerializeField] private GameObject _doublesPokemon1, _doublesPokemon2, _doublesPokemon3, _doublesPokemon4;
    private List<GameObject> _activePositionsList = new();
    public List<GameObject> ActivePositionsList => _activePositionsList;
    private bool _animatingEnemyPositionsIn;
    public Transform ArenaCenterTransform => _arenaGizmoCenter.transform;

    private void OnEnable(){
        _mainCamera = PlayerReferences.MainCameraTransform.GetComponent<Camera>();
        _cmBrain = _mainCamera.GetComponent<CinemachineBrain>();
    }

    public IEnumerator PrepareArena( BattleSystem battleSystem ){
        _battleSystem = battleSystem;
        _battleType = _battleSystem.BattleType;

        switch( _battleType ){

            case BattleType.WildBattle_1v1:

                yield return WildBattle_1v1(); //--Wild Battle, 1v1

            break;

            case BattleType.WildBattle_2v2:

                //--I'm not sure i'll ever have one of these but who knows. i think the Arcanine Guardian encounters will be the only ones

            break;

            case BattleType.TrainerSingles:

                yield return TrainerSingles(); //--Trainer Battle, Singles

            break;

            case BattleType.TrainerDoubles:

                yield return TrainerDoubles_1v1(); //--Trainer Battle, Doubles 1v1

            break;

            case BattleType.TrainerMulti_2v1:

            break;

            case BattleType.TrainerMulti_2v2:

            break;

            case BattleType.AI_Singles:

                yield return AI_Singles(); //--Trainer Battle, CPU vs CPU, Singles

            break;

            case BattleType.AI_Doubles:

            break;

            
        }

        yield return null;
    }

    private IEnumerator SetPivot( Vector3 targetPosition, GameObject pivotObj ){
        Vector3 pivotOffset = _arenaContainer.transform.position - pivotObj.transform.position;
        _arenaPivot.transform.position = targetPosition;
        _arenaContainer.transform.position = _arenaPivot.transform.position + pivotOffset;

        yield return null;
    }

    private IEnumerator SetCameras( CinemachineFreeLook camera, GameObject target = null )
    {
        if( target != null )
        {
            camera.LookAt = target.transform;
            camera.Follow = target.transform;
        }

        camera.gameObject.SetActive( true );
        yield return null;

        _battleSystem.BattleComposer.Init( _cmBrain );

        yield return new WaitUntil( () => !_animatingEnemyPositionsIn );
        yield return new WaitUntil( () => !_cmBrain.IsBlending );

        if( _battleSystem.BattleType == BattleType.WildBattle_1v1 || _battleSystem.BattleType == BattleType.TrainerSingles )
            _singlesMainCamera.gameObject.SetActive( true );
        else if( _battleSystem.BattleType == BattleType.TrainerDoubles )
            _doublesMainCamera.gameObject.SetActive( true );

        camera.gameObject.SetActive( false );
    }

    private IEnumerator SetCameras( CinemachineVirtualCamera camera, GameObject target = null )
    {
        if( target != null )
        {
            camera.LookAt = target.transform;
            camera.Follow = target.transform;
        }

        camera.gameObject.SetActive( true );
        yield return null;

        _battleSystem.BattleComposer.Init( _cmBrain );

        yield return new WaitUntil( () => !_animatingEnemyPositionsIn );
        yield return new WaitUntil( () => !_cmBrain.IsBlending );

        if( _battleSystem.BattleType == BattleType.WildBattle_1v1 || _battleSystem.BattleType == BattleType.TrainerSingles || _battleSystem.BattleType == BattleType.AI_Singles )
            _singlesMainCamera.gameObject.SetActive( true );
        else if( _battleSystem.BattleType == BattleType.TrainerDoubles )
            _doublesMainCamera.gameObject.SetActive( true );

        camera.gameObject.SetActive( false );
    }

    private void ClearCameras(){
        _1v1_EnemyIntroCamera.LookAt = null;
        _1v1_EnemyIntroCamera.gameObject.SetActive( false );
        _singlesMainCamera.gameObject.SetActive( false );
        _doublesMainCamera.gameObject.SetActive( false );
    }

    private IEnumerator MovePlayerIntoPosition( Transform position ){
        // Debug.Log( "move player into position from battle arena" );
        yield return new WaitForSeconds( 0.25f );
        yield return PlayerReferences.Instance.PlayerMovement.MovePlayerIntoBattlePosition( position );
    }

    private IEnumerator MoveCPUIntoPosition( Transform trainer, Transform position )
    {
        PlayerReferences.Instance.PlayerInput.CharacterControls.Disable();
        yield return new WaitUntil( () => !PlayerReferences.Instance.PlayerInput.CharacterControls.enabled );
        yield return trainer.DOJump( position.position, 1, 1, 0.5f ).WaitForCompletion();
    }

    private GameObject GrabWildEncounter(){
        _wildEncounter = _battleSystem.EncounteredPokemon;
        return _wildEncounter.gameObject;
    }

    private GameObject GrabEnemyTrainer1(){
        _enemyTrainer1 = _battleSystem.TopTrainer1.TrainerCenter.transform.parent.gameObject;
        return _enemyTrainer1;
    }

    private IEnumerator LookAtArenaCenter( GameObject actor, GameObject target ){
        var dir = ( actor.transform.position - target.transform.position ).normalized;
        actor.transform.forward = -dir;
        yield return null;
    }

    private void ClearSprites( GameObject obj ){
        obj.GetComponentsInChildren<SpriteRenderer>()[0].sprite = null;
    }

    private void ClearActivePositionsList(){
        for( int i = _activePositionsList.Count - 1; i >= 0; i-- ){
            _activePositionsList[i].SetActive( false );
            _activePositionsList.RemoveAt( i );
        }
    }

    public void AfterBattleCleanup(){
        ClearActivePositionsList();
        ClearCameras();
        // _dof.active = false;

        _wildEncounter = null;
        _enemyTrainer1 = null;
        // _enemyTrainer2 = null;
    }

    private void SetCirclesToZero()
    {
        _singlesCircle1.transform.localScale = Vector3.zero;
        _singlesCircle2.transform.localScale = Vector3.zero;
        _doublesCircle1.transform.localScale = Vector3.zero;
        _doublesCircle2.transform.localScale = Vector3.zero;
        _doublesCircle3.transform.localScale = Vector3.zero;
        _doublesCircle4.transform.localScale = Vector3.zero;
    }

    private IEnumerator AnimateCircle_In( GameObject battlePos, float duration )
    {
        yield return null;
        yield return battlePos.transform.DOScale( _defaultCircleSize, duration ).WaitForCompletion();
    }

    private IEnumerator AnimatePokemon_In( GameObject pokeObj )
    {
        yield return pokeObj.transform.DOScale( _defaultPokemonSize, 0.5f ).WaitForCompletion();
    }

    private IEnumerator WildBattle_1v1(){
        SetCirclesToZero();
        //--Attempt! to set the pivot of the arena to the wild encounter
        var targetPosition = GrabWildEncounter().transform.position;
        yield return SetPivot( targetPosition, _singlesUnit2 );

        //--Move Player into position
        StartCoroutine( MovePlayerIntoPosition( _singlesTrainer1.transform ) );
        // yield return MovePlayerIntoPosition( _singlesTrainer1.transform );

        //--Handle Cameras by passing the initial single target camera's target unit
        StartCoroutine( SetCameras( _1v1_EnemyIntroCamera, _singlesUnit2 ) );

        //--Activate relevant positions
        _singlesPokemon1.transform.localScale = Vector3.zero;
        _singlesTrainer1.SetActive( true ); //--Player
        _singlesUnit1.SetActive( true );    //--Player Unit
        _singlesUnit2.SetActive( true );    //--Wild Pokemon
        yield return null;

        //--Set BattleUnit component appropriately
        _singlesUnit1.GetComponent<BattleUnit>().enabled = false;
        _singlesUnit2.GetComponent<BattleUnit>().enabled = false;
        yield return null;

        //--Clear relevant sprites that will be replaced by an in-world sprite instead
        ClearSprites( _singlesTrainer1 );   //--trainer 1 will almost always be the player, who will almost always be on-map
        ClearSprites( _singlesUnit2 );      //--clear unit2's sprite because the on-map encounter will take unit2's position
        _singlesUnit2.GetComponentInChildren<PokemonAnimator>().enabled = false; //--disable the animator for the battle unit, we use the wild pokemon's animator instead
        _singlesUnit2.GetComponentInChildren<PokemonShadow>().enabled = false; //--disable the animator for the battle unit, we use the wild pokemon's animator instead
        yield return null;

        //--Add relevant positions to the active positions list
        _activePositionsList.Add( _singlesTrainer1 );
        _activePositionsList.Add( _singlesUnit1 );
        _activePositionsList.Add( _singlesUnit2 );
        yield return null;

        //--Setup relevant Battle Units
        var playerUnit = _singlesUnit1.GetComponent<BattleUnit>();
        playerUnit.Setup( _battleSystem.BottomTrainer1.GetHealthyPokemon(), _battleSystem.BottomTrainer1, _battleSystem.PlayerHUDs[0], _battleSystem );

        var enemyUnit = _battleSystem.EncounteredPokemon.gameObject.GetComponent<BattleUnit>();
        enemyUnit.SetAI( true ); //--enable AI for this unit
        enemyUnit.Setup( _battleSystem.WildPokemon, null, _battleSystem.WildPokemonHUD, _battleSystem ); //--REMEMBER!!!!! _singlesUnit2 is not actually being assigned to, it's only here for its position!!
        yield return null;

        //--Make everyone face the arena center
        yield return LookAtArenaCenter( PlayerReferences.Instance.gameObject, _singlesUnit2 ); //--Player Trainer
        yield return LookAtArenaCenter( _singlesPokemon1, _singlesUnit2 ); //--Player Pokemon
        yield return LookAtArenaCenter( GrabWildEncounter(), _singlesPokemon1 ); //--Wild Pokemon

        //--Animate positions in
        StartCoroutine( AnimateCircle_In( _singlesCircle2, 0.75f ) );
        yield return new WaitForSeconds( 0.5f );
        StartCoroutine( AnimatePokemon_In( _singlesPokemon1 ) );
        StartCoroutine( AnimateCircle_In( _singlesCircle1, 0.75f ) );

        //--Assign relevant Battle Units
        _battleSystem.AssignUnits_1v1( playerUnit, enemyUnit );
        yield return null;

        yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"You encountered a wild {_battleSystem.EnemyUnits[0].Pokemon.NickName}!" );
        yield return new WaitForSeconds( 0.1f );

        yield return null;
    }

    //--Trainer Battle, Singles
    private IEnumerator TrainerSingles(){
        SetCirclesToZero();
        //--Attempt! to set the pivot of the arena to the Enemy Trainer's location
        var targetPosition = GrabEnemyTrainer1().transform.position;
        yield return SetPivot( targetPosition, _singlesTrainer2 );

        //--Move Player into position
        StartCoroutine( MovePlayerIntoPosition( _singlesTrainer1.transform ) );

        //--Wild Trainer wants to fight! Add a trainer name variable that gets fed into the battle system or something --11/27/25
        var enemyTrainer = _battleSystem.TopTrainer1;
        yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{enemyTrainer.TrainerClass} {enemyTrainer.TrainerName} wants to battle!" );

        //--Activate Relevant Positions
        _singlesPokemon1.transform.localScale = Vector3.zero;
        _singlesPokemon2.transform.localScale = Vector3.zero;
        _singlesTrainer1.SetActive( true ); //--Player
        _singlesTrainer2.SetActive( true ); //--Enemy Trainer
        _singlesUnit1.SetActive( true ); //--Player Unit
        _singlesUnit2.SetActive( true ); //--Enemy Trainer Unit
        // yield return null;

        //--Set BattleUnit component appropriately ( This is for cases where the pokemon is on-map, such as a follower pokemon )
        _singlesUnit1.GetComponent<BattleUnit>().enabled = true;
        _singlesUnit2.GetComponent<BattleUnit>().enabled = true;
        // yield return null;

        //--Clear relevant sprites that will be replaced by an in-world sprite instead
        ClearSprites( _singlesTrainer1 );
        ClearSprites( _singlesTrainer2 );
        // yield return null;

        //--Add relevant positions to the active positions list
        _activePositionsList.Add( _singlesTrainer1 );
        _activePositionsList.Add( _singlesTrainer2 );
        _activePositionsList.Add( _singlesUnit1 );
        _activePositionsList.Add( _singlesUnit2 );
        yield return null;

        //--Re Enable the pokemon animators on unit2, in case they were disabled by a wild battle
        _singlesUnit2.GetComponentInChildren<PokemonAnimator>().enabled = true;
        _singlesUnit2.GetComponentInChildren<PokemonShadow>().enabled = true;

        //--Setup relevant Battle Units
        var playerUnit = _singlesUnit1.GetComponent<BattleUnit>();
        var enemyUnit  = _singlesUnit2.GetComponent<BattleUnit>();
        
        playerUnit.Setup( _battleSystem.BottomTrainer1.GetHealthyPokemon(), _battleSystem.BottomTrainer1, _battleSystem.PlayerHUDs[0], _battleSystem );
        
        enemyUnit.SetAI( true ); //--enable AI for this unit
        enemyUnit.Setup( _battleSystem.TopTrainer1.GetHealthyPokemon(), _battleSystem.TopTrainer1, _battleSystem.EnemyHUDs[0], _battleSystem );

        _animatingEnemyPositionsIn = true;
        //--Handle Cameras by passing the initial single target camera's target unit
        //--We don't yield for this so that the camera transition happens alongside moving the player
        StartCoroutine( SetCameras( _1v1_EnemyIntroCamera, _singlesUnit2 ) );
        yield return null;

        //--Make everyone face the arena center
        yield return LookAtArenaCenter( PlayerReferences.Instance.gameObject, _singlesTrainer2 );
        yield return LookAtArenaCenter( _singlesTrainer2, _singlesTrainer1 );
        yield return LookAtArenaCenter( _singlesPokemon1, _singlesUnit2 );
        yield return LookAtArenaCenter( _singlesPokemon2, _singlesUnit1 );

        // yield return AnimateArenaDecal_In(); //--doesn't work rn
        StartCoroutine( AnimatePokemon_In( _singlesPokemon2 ) );
        StartCoroutine( AnimateCircle_In( _singlesCircle2, 0.75f ) );

        _animatingEnemyPositionsIn = false;
        yield return new WaitForSeconds( 0.5f );

        StartCoroutine( AnimatePokemon_In( _singlesPokemon1 ) );
        StartCoroutine( AnimateCircle_In( _singlesCircle1, 0.75f ) );

        //--Assign relevant Battle Units
        _battleSystem.AssignUnits_1v1( playerUnit, enemyUnit );
        yield return null;

    }

    private IEnumerator TrainerDoubles_1v1(){
        SetCirclesToZero();
        var targetPosition = GrabEnemyTrainer1().transform.position;
        yield return SetPivot( targetPosition, _singlesTrainer2 );
        
        //--Move Player into position
        StartCoroutine( MovePlayerIntoPosition( _singlesTrainer1.transform ) );

        //--Wild Trainer wants to fight! Add a trainer name variable that gets fed into the battle system or something --11/27/25
        var enemyTrainer = _battleSystem.TopTrainer1;
        yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{enemyTrainer.TrainerClass} {enemyTrainer.TrainerName} wants to battle!" );

        //--Activate Relevant Positions
        //--We use the singles trainer positions for 1v1 doubles. we'll use the other positions for 2v2 multi-battles.
        _doublesPokemon1.transform.localScale = Vector3.zero;
        _doublesPokemon2.transform.localScale = Vector3.zero;
        _doublesPokemon3.transform.localScale = Vector3.zero;
        _doublesPokemon4.transform.localScale = Vector3.zero;
        _singlesTrainer1.SetActive( true ); //--Player
        _singlesTrainer2.SetActive( true );
        _doublesUnit1.SetActive( true ); //--Player Unit
        _doublesUnit2.SetActive( true ); //--Player Unit
        _doublesUnit3.SetActive( true );
        _doublesUnit4.SetActive( true );
        // yield return null;

        _animatingEnemyPositionsIn = true;

        //--Handle Double Battle Intro Camera
        //--We don't yield for this so that the camera transition happens alongside moving the player
        StartCoroutine( SetCameras( _2v2_EnemyIntroCamera, _2v2_Encounter ) );
        // yield return null;

        //--Set BattleUnit component appropriately
        _doublesUnit1.GetComponent<BattleUnit>().enabled = true;
        _doublesUnit2.GetComponent<BattleUnit>().enabled = true;
        _doublesUnit3.GetComponent<BattleUnit>().enabled = true;
        _doublesUnit4.GetComponent<BattleUnit>().enabled = true;
        // yield return null;

        //--Clear relevant sprites that will be replaced by an in-world sprite instead
        ClearSprites( _singlesTrainer1 );
        ClearSprites( _singlesTrainer2 );

        //--Add relevant positions to the active positions list. I don't remember what this was for but I guess we'll find out! --11/24/25
        _activePositionsList.Add( _singlesTrainer1 );
        _activePositionsList.Add( _singlesTrainer2 );
        _activePositionsList.Add( _doublesUnit1 );
        _activePositionsList.Add( _doublesUnit2 );
        _activePositionsList.Add( _doublesUnit3 );
        _activePositionsList.Add( _doublesUnit4 );
        // yield return null;

        //--
        //--We don't need to re-enable unit 2's animators as the doubles units aren't used for wild battles, currently. implement if you add wild double battles!
        //--

        //--Setup the list of Battle Units, unlike the individual units in singles
        List<BattleUnit> playerUnits = new()
        {
            _doublesUnit1.GetComponent<BattleUnit>(),
            _doublesUnit2.GetComponent<BattleUnit>()
        };

        List<BattleUnit> enemyUnits = new()
        {
            _doublesUnit3.GetComponent<BattleUnit>(),
            _doublesUnit4.GetComponent<BattleUnit>()
        };

        //--Get top 2 mons off each player's party.
        var playerMons = _battleSystem.BottomTrainer1.GetHealthyPokemon( DOUBLES_COUNT );
        var enemyMons = _battleSystem.TopTrainer1.GetHealthyPokemon( DOUBLES_COUNT );

        //--Setup each unit, all indicies should be the same! unit 0 should have hud 0!
        for( int i = 0; i < playerMons.Count; i++)
            playerUnits[i].Setup( playerMons[i], _battleSystem.BottomTrainer1, _battleSystem.PlayerHUDs[i], _battleSystem );

        for( int i = 0; i < enemyMons.Count; i++)
        {
            enemyUnits[i].SetAI( true );
            enemyUnits[i].Setup( enemyMons[i], _battleSystem.TopTrainer1, _battleSystem.EnemyHUDs[i], _battleSystem );
        }

        //--Make everyone face the arena center
        yield return LookAtArenaCenter( PlayerReferences.Instance.gameObject, _singlesTrainer2 );
        yield return LookAtArenaCenter( _singlesTrainer2, _singlesTrainer1 );
        yield return LookAtArenaCenter( _doublesPokemon1, _doublesUnit4 );
        yield return LookAtArenaCenter( _doublesPokemon4, _doublesUnit1 );
        yield return LookAtArenaCenter( _doublesPokemon2, _doublesUnit3 );
        yield return LookAtArenaCenter( _doublesPokemon3, _doublesUnit2 );

        // yield return AnimateArenaDecal_In(); //--doesn't work rn
        StartCoroutine( AnimatePokemon_In( _doublesPokemon3 ) );
        yield return new WaitForSeconds( 0.1f );
        StartCoroutine( AnimateCircle_In( _doublesCircle3, 0.75f ) );
        yield return new WaitForSeconds( 0.25f );
        StartCoroutine( AnimatePokemon_In( _doublesPokemon4 ) );
        yield return new WaitForSeconds( 0.1f );
        StartCoroutine( AnimateCircle_In( _doublesCircle4, 0.75f ) );
        yield return new WaitForSeconds( 0.25f );

        _animatingEnemyPositionsIn = false;

        StartCoroutine( AnimatePokemon_In( _doublesPokemon1 ) );
        yield return new WaitForSeconds( 0.1f );
        StartCoroutine( AnimateCircle_In( _doublesCircle1, 0.75f ) );
        yield return new WaitForSeconds( 0.25f );
        StartCoroutine( AnimatePokemon_In( _doublesPokemon2 ) );
        yield return new WaitForSeconds( 0.1f );
        StartCoroutine( AnimateCircle_In( _doublesCircle2, 0.75f ) );
        yield return new WaitForSeconds( 0.25f );

        //--Assign Battle Units in BattleSystem
        _battleSystem.AssignUnits_2v2( playerUnits, enemyUnits );
        yield return null;
    }

    private IEnumerator AI_Singles(){
        SetCirclesToZero();
        //--Attempt! to set the pivot of the arena to the Enemy Trainer's location
        var targetPosition = GrabEnemyTrainer1().transform.position;
        yield return SetPivot( targetPosition, _singlesTrainer2 );

        //--Move player into spectator position
        yield return MovePlayerIntoPosition( _spectator1.transform );
        yield return null;

        //--Move Bottom Trainer into position
        StartCoroutine( MoveCPUIntoPosition( _battleSystem.BottomTrainer1.TrainerCenter.transform.parent, _singlesTrainer1.transform ) );

        //--Wild Trainer wants to fight! Add a trainer name variable that gets fed into the battle system or something --11/27/25
        var topTrainer = _battleSystem.TopTrainer1;
        var bottomTrainer = _battleSystem.BottomTrainer1;
        yield return DialogueManager.Instance.PlaySystemMessageCoroutine( $"{topTrainer.TrainerClass} {topTrainer.TrainerName} is going to battle {bottomTrainer.TrainerClass} {bottomTrainer.TrainerName}!" );

        //--Activate Relevant Positions
        _singlesPokemon1.transform.localScale = Vector3.zero;
        _singlesPokemon2.transform.localScale = Vector3.zero;
        _singlesTrainer1.SetActive( true ); //--Player
        _singlesTrainer2.SetActive( true ); //--Enemy Trainer
        _singlesUnit1.SetActive( true ); //--Player Unit
        _singlesUnit2.SetActive( true ); //--Enemy Trainer Unit
        // yield return null;

        //--Set BattleUnit component appropriately ( This is for cases where the pokemon is on-map, such as a follower pokemon )
        _singlesUnit1.GetComponent<BattleUnit>().enabled = true;
        _singlesUnit2.GetComponent<BattleUnit>().enabled = true;
        // yield return null;

        //--Clear relevant sprites that will be replaced by an in-world sprite instead
        ClearSprites( _singlesTrainer1 );
        ClearSprites( _singlesTrainer2 );
        // yield return null;

        //--Add relevant positions to the active positions list
        _activePositionsList.Add( _singlesTrainer1 );
        _activePositionsList.Add( _singlesTrainer2 );
        _activePositionsList.Add( _singlesUnit1 );
        _activePositionsList.Add( _singlesUnit2 );
        yield return null;

        //--Re Enable the pokemon animators on unit2, in case they were disabled by a wild battle
        _singlesUnit2.GetComponentInChildren<PokemonAnimator>().enabled = true;
        _singlesUnit2.GetComponentInChildren<PokemonShadow>().enabled = true;

        //--Setup relevant Battle Units
        var playerUnit = _singlesUnit1.GetComponent<BattleUnit>();
        var enemyUnit  = _singlesUnit2.GetComponent<BattleUnit>();
        
        playerUnit.SetAI( true ); //--enable AI for this unit
        playerUnit.Setup( _battleSystem.BottomTrainer1.GetHealthyPokemon(), _battleSystem.BottomTrainer1, _battleSystem.PlayerHUDs[0], _battleSystem );
        
        enemyUnit.SetAI( true ); //--enable AI for this unit
        enemyUnit.Setup( _battleSystem.TopTrainer1.GetHealthyPokemon(), _battleSystem.TopTrainer1, _battleSystem.EnemyHUDs[0], _battleSystem );

        _animatingEnemyPositionsIn = true;
        //--Handle Cameras by passing the initial single target camera's target unit
        //--We don't yield for this so that the camera transition happens alongside moving the player
        StartCoroutine( SetCameras( _1v1_EnemyIntroCamera, _singlesUnit2 ) );
        yield return null;

        //--Make everyone face the arena center
        yield return LookAtArenaCenter( PlayerReferences.Instance.gameObject, _arenaGizmoCenter );
        yield return LookAtArenaCenter( _battleSystem.BottomTrainer1.TrainerCenter.transform.parent.gameObject, _singlesTrainer1 );
        yield return LookAtArenaCenter( _singlesTrainer2, _singlesTrainer1 );
        yield return LookAtArenaCenter( _singlesPokemon1, _singlesUnit2 );
        yield return LookAtArenaCenter( _singlesPokemon2, _singlesUnit1 );

        // yield return AnimateArenaDecal_In(); //--doesn't work rn
        StartCoroutine( AnimatePokemon_In( _singlesPokemon2 ) );
        StartCoroutine( AnimateCircle_In( _singlesCircle2, 0.75f ) );

        _animatingEnemyPositionsIn = false;
        yield return new WaitForSeconds( 0.5f );

        StartCoroutine( AnimatePokemon_In( _singlesPokemon1 ) );
        StartCoroutine( AnimateCircle_In( _singlesCircle1, 0.75f ) );

        //--Assign relevant Battle Units
        _battleSystem.AssignUnits_1v1( playerUnit, enemyUnit );
        yield return null;
    }


#if UNITY_EDITOR

    public void OnDrawGizmos(){
        //--Arena Rings
        Handles.DrawWireDisc( _arenaGizmoCenter.transform.position, Vector3.up , _singlesArenaSize ); //--singles
        Handles.DrawWireDisc( _arenaGizmoCenter.transform.position, Vector3.up , _doublesArenaSize ); //--doubles

    //--Spectator Positions

        //--Spectator 1
        Handles.color = Color.black;
        if( _spectator1 != null )
        Handles.DrawWireDisc( _singlesTrainer1.transform.position, Vector3.up, 1f );

    //--Singles Positions

        //--Trainer 1
        Handles.color = Color.cyan;
        if( _singlesTrainer1 != null )
        Handles.DrawWireDisc( _singlesTrainer1.transform.position, Vector3.up, 1f );

        //--Trainer 2
        Handles.color = Color.magenta;
        if( _singlesTrainer2 != null )
        Handles.DrawWireDisc( _singlesTrainer2.transform.position, Vector3.up, 1f );

        //--Unit 1
        Handles.color = Color.cyan;
        if( _singlesUnit1 != null )
        Handles.DrawWireDisc( _singlesUnit1.transform.position, Vector3.up, 1f );

        //--Unit 2
        Handles.color = Color.magenta;
        if( _singlesUnit2 != null )
        Handles.DrawWireDisc( _singlesUnit2.transform.position, Vector3.up, 1f );

    //--Doubles Positions

        //--Trainer 1
        Handles.color = Color.red;
        if( _doublesTrainer1 != null )
        Handles.DrawWireDisc( _doublesTrainer1.transform.position, Vector3.up, 1f );

        //--Trainer 2
        Handles.color = Color.blue;
        if( _doublesTrainer2 != null )
        Handles.DrawWireDisc( _doublesTrainer2.transform.position, Vector3.up, 1f );

        //--Trainer 3
        Handles.color = Color.yellow;
        if( _doublesTrainer3 != null )
        Handles.DrawWireDisc( _doublesTrainer3.transform.position, Vector3.up, 1f );

        //--Trainer 4
        Handles.color = Color.green;
        if( _doublesTrainer4 != null )
        Handles.DrawWireDisc( _doublesTrainer4.transform.position, Vector3.up, 1f );

        //--Unit 1
        Handles.color = Color.red;
        if( _doublesUnit1 != null )
        Handles.DrawWireDisc( _doublesUnit1.transform.position, Vector3.up, 1f );

        //--Unit 2
        Handles.color = Color.blue;
        if( _doublesUnit2 != null )
        Handles.DrawWireDisc( _doublesUnit2.transform.position, Vector3.up, 1f );

        //--Unit 3
        Handles.color = Color.red;
        if( _doublesUnit3 != null )
        Handles.DrawWireDisc( _doublesUnit3.transform.position, Vector3.up, 1f );

        //--Unit 4
        Handles.color = Color.blue;
        if( _doublesUnit4 != null )
        Handles.DrawWireDisc( _doublesUnit4.transform.position, Vector3.up, 1f );

    }


#endif

}
