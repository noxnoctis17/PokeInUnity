using UnityEngine;

public class BattleController : MonoBehaviour
{
    public static BattleController Instance;
    [SerializeField] private BattleSystem _battleSystem;

    private void OnEnable(){
        WildPokemonEvents.OnPlayerEncounter += InitWildBattle;
    }

    private void Awake(){
        Instance = this;
    }

    private void InitWildBattle( WildPokemon encounteredMon ){
        //--Set Battle Type
        BattleType battleType = BattleType.WildBattle_1v1;

        //--Push Game State
        GameStateController.Instance.GameStateMachine.Push( BattleState.Instance );
        
        //--Make Player BattleTrainer
        var playerTrainer = PlayerReferences.Instance.PlayerTrainer;
        var playerBattleTrainer = playerTrainer.MakeBattleTrainer();
        
        //--Assign Encountered Pokemon, Initialize Wild Battle
        _battleSystem.AssignWildPokemon( encounteredMon );
        _battleSystem.InitializeWildBattle( playerBattleTrainer, battleType );
    }

    public void InitTrainerBattle( BattleTrainer player, BattleTrainer cpu, BattleType battleType ){
        //--Push Game State
        GameStateController.Instance.GameStateMachine.Push( BattleState.Instance );

        //--Initialize Trainer Battle
        if( battleType == BattleType.TrainerSingles )
            _battleSystem.InitializeTrainerSingles( player, cpu );

        if( battleType == BattleType.TrainerDoubles )
            _battleSystem.InitializeTrainerDoubles( player, cpu );
    }

    public void InitAITrainerBattle( BattleType battleType, BattleTrainer topTrainer, BattleTrainer bottomTrainer )
    {
        //--Push Game State
        GameStateController.Instance.GameStateMachine.Push( BattleState.Instance );
        
        if( battleType == BattleType.AI_Singles )
            _battleSystem.InitializeAISingles( topTrainer, bottomTrainer );
    }
}
