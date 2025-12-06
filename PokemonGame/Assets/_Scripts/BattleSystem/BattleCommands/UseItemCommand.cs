using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UseItemCommand : IBattleCommand
{
    private int _commandPriority;
    public BattleUnit User => _user;
    public int CommandPriority => _commandPriority;
    public int AttackPriority => CommandPriority;
    public int UnitAgility => CommandPriority;
    private BattleSystem _battleSystem;
    private BattleUnit _user;
    private Pokemon _pokemon;
    private Item _item;

    public UseItemCommand( BattleSystem battleSystem, BattleUnit user, Pokemon pokemon, Item item ){
        _battleSystem = battleSystem;
        _commandPriority = (int)CommandPriorityEnum.Item;
        _user = user;
        _pokemon = pokemon;
        _item = item;
    }

    public IEnumerator ExecuteBattleCommand(){
        if( _item.ItemSO.ItemCategory == ItemCategory.PokeBall )
            yield return _battleSystem.ThrowPokeball( _item );
        else
            yield return _battleSystem.PerformUseItemCommand( _pokemon, _item );
        
    }

    public void ChangeTarget( BattleUnit target )
    {
        _user = target;
    }    
}
