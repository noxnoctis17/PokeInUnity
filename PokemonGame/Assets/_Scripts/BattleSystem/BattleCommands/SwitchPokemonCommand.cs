using System.Collections;

public class SwitchPokemonCommand : IBattleCommand
{
    private int _commandPriority;
    private int _attackPriority;
    private int _unitAgility;
    private bool _isAISwitch;
    private bool _afterYou;
    private bool _quash;
    private Pokemon _pokemon;
    private BattleSystem _battleSystem;
    private BattleUnit _battleUnit;
    public BattleUnit User => _battleUnit;
    public int CommandPriority => _commandPriority;
    public int AttackPriority => _attackPriority;
    public int UnitSpeed => _unitAgility;
    public bool AfterYou => _afterYou;
    public bool Quash => _quash;

    public SwitchPokemonCommand( Pokemon pokemon, BattleSystem battleSystem, BattleUnit battleUnit, bool aiSwitch )
    {
        _pokemon = pokemon;
        _battleSystem = battleSystem;
        _battleUnit = battleUnit;
        _commandPriority = (int)CommandPriorityEnum.Switch; //--i don't think i'll ever get over this implementation of this lol //--03/26/24 jokes on you you added a system for this
        _unitAgility = battleUnit.Pokemon.Speed;
        _isAISwitch = aiSwitch;
    }

    public IEnumerator ExecuteBattleCommand()
    {
        yield return _battleSystem.CommandCenter.PerformSwitchPokemonCommand( _pokemon, _battleUnit, _isAISwitch );
    }

    public Pokemon GetPokemon()
    {
        return _pokemon;
    }

    public void ChangeTarget( BattleUnit target )
    {
        //--This will likely be unused
    }
}
