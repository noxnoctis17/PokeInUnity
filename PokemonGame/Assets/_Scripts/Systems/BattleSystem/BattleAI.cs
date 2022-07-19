using UnityEngine;

public class BattleAI : MonoBehaviour
{
    [SerializeField] private BattleSystem _battleSystem;
    private BattleUnit _battleUnit;
    private PokemonClass _pokemon;
    private MoveClass _move;

    private void Start()
    {
        _battleUnit = GetComponent<BattleUnit>();
        BattleSystem.OnPlayerCommandSelect += SendCommand;

    }

    private void SendCommand()
    {
        ChooseCommand();
    }

    private void ChooseCommand()
    {
        //blah blah logic to decide whether it should use an item, switch pokemon, or choose a move
        ChooseMoveCommand();

    }

    private void ChooseMoveCommand()
    {
        _pokemon = _battleUnit.Pokemon;
        _move = _battleUnit.Pokemon.GetRandomMove();
        _battleSystem.SetEnemyMoveCommand(_battleUnit, _move);

    }

}
