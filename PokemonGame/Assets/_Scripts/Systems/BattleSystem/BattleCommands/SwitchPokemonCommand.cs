using System.Collections;

public class SwitchPokemonCommand : IBattleCommand
{
    private int _commandPriority;
    private int _attackPriority;
    private int _unitAgility;
    public int CommandPriority => _commandPriority;
    public int AttackPriority => _attackPriority;
    public int UnitAgility => _unitAgility;
    private PokemonClass _pokemon;
    private BattleSystem _battleSystem;

    public SwitchPokemonCommand( PokemonClass pokemon, BattleSystem battleSystem ){
        _pokemon = pokemon;
        _battleSystem = battleSystem;
        _commandPriority = (int)CommandPriorityEnum.Switch; //--i don't think i'll ever get over this implementation of this lol //--03/26/24 jokes on you you added a system for this
        _unitAgility = _commandPriority;
    }

    public IEnumerator ExecuteBattleCommand(){
        yield return _battleSystem.PerformSwitchPokemonCommand( _pokemon );
    }
}
