using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UseItemCommand : IBattleCommand
{
    private int _commandPriority;
    public int CommandPriority => _commandPriority;
    public int AttackPriority => CommandPriority;
    public int UnitAgility => CommandPriority;
    private BattleSystem _battleSystem;
    private Pokemon _pokemon;
    private Item _item;

    public UseItemCommand( BattleSystem battleSystem, Pokemon pokemon, Item item ){
        _battleSystem = battleSystem;
        _commandPriority = (int)CommandPriorityEnum.Item;
        _pokemon = pokemon;
        _item = item;
    }

    public IEnumerator ExecuteBattleCommand(){
        if( _item.ItemSO.ItemCategory == ItemCategory.PokeBall )
            yield return _battleSystem.ThrowPokeball( _item );
        else
            yield return _battleSystem.PerformUseItemCommand( _pokemon, _item );
        
    }
}
