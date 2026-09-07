using UnityEngine;

public class BattleController : MonoBehaviour
{
    public static BattleController Instance;
    [SerializeField] private BattleSystem _battleSystem;

    private void OnEnable()
    {
        WildPokemonEvents.OnPlayerEncounter += InitWildBattle;
    }

    private void Awake()
    {
        Instance = this;
    }

    private void InitWildBattle( WildPokemon encounteredMon )
    {
        //--Push Game State
        GameStateController.Instance.GameStateMachine.Push( BattleState.Instance );
        
        //--Make Player BattleTrainer
        var playerTrainer = PlayerReferences.Instance.PlayerTrainer;
        var playerBattleTrainer = playerTrainer.MakeBattleTrainer();

        //--Make Wild Pokemon BattleTrainer
        var wildTrainer = BattleTrainerFactory.FromWild( new() );
        
        //--Assign Encountered Pokemon, Initialize Wild Battle
        _battleSystem.AssignWildPokemon( encounteredMon );
        _battleSystem.InitializeWildBattle( playerBattleTrainer, wildTrainer );
    }

    public void InitTrainerBattle( BattleTrainer player, BattleTrainer cpu, BattleType battleType )
    {
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
        
        //--Initialize AI Trainer Battle
        if( battleType == BattleType.AI_Singles )
            _battleSystem.InitializeAISingles( topTrainer, bottomTrainer );
    }
}
