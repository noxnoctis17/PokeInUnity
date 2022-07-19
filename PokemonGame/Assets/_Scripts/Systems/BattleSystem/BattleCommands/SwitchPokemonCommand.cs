using System.Collections;

public class SwitchPokemonCommand : IBattleCommand
{
    private int _commandPriority;
    public int CommandPriority => _commandPriority;
    private int _unitAgility;
    public int UnitAgility => _unitAgility;
    private PokemonClass _pokemon;
    private BattleSystem _battleSystem;

    public SwitchPokemonCommand(PokemonClass pokemon, BattleSystem battleSystem)
    {
        _pokemon = pokemon;
        _battleSystem = battleSystem;
        _commandPriority = 999999; //--i don't think i'll ever get over this implementation of this lol
        _unitAgility = _commandPriority;
    }

    public IEnumerator ExecuteBattleCommand()
    {
        yield return _battleSystem.PerformSwitchPokemonCommand(_pokemon);
    }
}
