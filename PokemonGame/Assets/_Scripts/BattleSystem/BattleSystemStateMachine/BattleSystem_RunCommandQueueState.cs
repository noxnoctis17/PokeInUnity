using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class BattleSystem_RunCommandQueueState : State<BattleSystem>
{
    private BattleSystem _battleSystem;

    public override void EnterState( BattleSystem bs )
    {
        _battleSystem = bs;

        _battleSystem.PlayerBattleMenu.OnPauseState?.Invoke();

        DetermineCommandOrder();
    }

    public override void ExitState()
    {
        if( _battleSystem.BattleType == BattleType.AI_Singles || _battleSystem.BattleType == BattleType.AI_Doubles )
        {
            _battleSystem.PushState( _battleSystem.AITurnState );
        }
        else
            _battleSystem.PushState( _battleSystem.ActionSelectState );
    }

    public void DetermineCommandOrder()
    {
        if( _battleSystem.BattleFlags[BattleFlag.TrickRoom] )
            _battleSystem.SetCommandList( _battleSystem.CommandList.OrderByDescending( prio => prio.CommandPriority ).ThenByDescending( prio => prio.AttackPriority ).ThenBy( prio => prio.UnitAgility ).ToList() );
        else
            _battleSystem.SetCommandList( _battleSystem.CommandList.OrderByDescending( prio => prio.CommandPriority ).ThenByDescending( prio => prio.AttackPriority ).ThenByDescending( prio => prio.UnitAgility ).ToList() );

        for( int i = 0; i < _battleSystem.CommandList.Count; i++ ){
            _battleSystem.AddCommand( _battleSystem.CommandList[i] );
        }

        _battleSystem.CommandList.Clear();

        StartCoroutine( ExecuteCommandQueue() );
    }

    public IEnumerator ReorderCommands()
    {
        if( _battleSystem.BattleFlags[BattleFlag.TrickRoom] )
            _battleSystem.SetCommandList( _battleSystem.CommandQueue.OrderByDescending( prio => prio.CommandPriority ).ThenByDescending( prio => prio.AttackPriority ).ThenBy( prio => prio.UnitAgility ).ToList() );
        else
            _battleSystem.SetCommandList( _battleSystem.CommandQueue.OrderByDescending( prio => prio.CommandPriority ).ThenByDescending( prio => prio.AttackPriority ).ThenByDescending( prio => prio.UnitAgility ).ToList() );

        _battleSystem.CommandQueue.Clear();

        for( int i = 0; i < _battleSystem.CommandList.Count; i++ ){
            _battleSystem.AddCommand( _battleSystem.CommandList[i] );
        }

        _battleSystem.CommandList.Clear();

        yield return null;
    }

    private void HandleDoublesTargetSwap()
    {
        if( _battleSystem.CommandQueue.Peek() is UseMoveCommand )
            {
                var moveCommand = _battleSystem.CommandQueue.Peek() as UseMoveCommand;
                if( moveCommand.SingleTarget.Pokemon.SevereStatus?.ID == StatusConditionID.FNT || moveCommand.SingleTarget.Pokemon == null )
                {
                    //--change target if current target is fainted. if the new target has also fainted, we remove the command from the queue.
                    if( moveCommand.SingleTarget == _battleSystem.EnemyUnits[0] )
                    {
                        moveCommand.ChangeTarget( _battleSystem.EnemyUnits[1] );
                        if( moveCommand.SingleTarget.Pokemon.SevereStatus?.ID == StatusConditionID.FNT || moveCommand.SingleTarget.Pokemon == null || moveCommand.SingleTarget.Pokemon.CurrentHP == 0 )
                            _battleSystem.CommandQueue.Dequeue();
                    }
                    else if( moveCommand.SingleTarget == _battleSystem.EnemyUnits[1] )
                    {
                        moveCommand.ChangeTarget( _battleSystem.EnemyUnits[0] );
                        if( moveCommand.SingleTarget.Pokemon.SevereStatus?.ID == StatusConditionID.FNT || moveCommand.SingleTarget.Pokemon == null || moveCommand.SingleTarget.Pokemon.CurrentHP == 0 )
                            _battleSystem.CommandQueue.Dequeue();
                    }
                    else if( moveCommand.SingleTarget == _battleSystem.PlayerUnits[0] )
                    {
                        moveCommand.ChangeTarget( _battleSystem.PlayerUnits[1] );
                        if( moveCommand.SingleTarget.Pokemon.SevereStatus?.ID == StatusConditionID.FNT || moveCommand.SingleTarget.Pokemon == null || moveCommand.SingleTarget.Pokemon.CurrentHP == 0 )
                            _battleSystem.CommandQueue.Dequeue();
                    }
                    else if( moveCommand.SingleTarget == _battleSystem.PlayerUnits[1] )
                    {
                        moveCommand.ChangeTarget( _battleSystem.PlayerUnits[0] );
                        if( moveCommand.SingleTarget.Pokemon.SevereStatus?.ID == StatusConditionID.FNT || moveCommand.SingleTarget.Pokemon == null || moveCommand.SingleTarget.Pokemon.CurrentHP == 0 )
                            _battleSystem.CommandQueue.Dequeue();
                    }
                }
            }
    }

    private IEnumerator AfterTurnUpdate( BattleUnit user )
    {
        user.Pokemon.BattleItemEffect?.OnItemAfterTurn?.Invoke( user );
        _battleSystem.AddToEventQueue( () => _battleSystem.ShowStatusChanges( user ) );
        yield return null;
        yield return _battleSystem.WaitForEventQueue();

        _battleSystem.AddToEventQueue( () => _battleSystem.CheckForFaint( user ) );
        yield return null;
        yield return _battleSystem.WaitForEventQueue();

        if( user != null && user.Pokemon.IsFainted() )
        {
            _battleSystem.SetHandleFaintCompleted( false );
            yield return _battleSystem.HandleFaintedPokemon( user );
            yield return null;
            yield return _battleSystem.WaitForEventQueue();
            yield return new WaitUntil( () => _battleSystem.HandleFaintCompleted );
        }

        yield return _battleSystem.WaitForUIQueue();
        yield return null;

        user.Pokemon.Ability?.OnAbilityAfterTurn?.Invoke( user, _battleSystem.Field );
        _battleSystem.AddToEventQueue( () => _battleSystem.ShowStatusChanges( user ) );
        yield return null;
        yield return _battleSystem.WaitForEventQueue();

        _battleSystem.AddToEventQueue( () => _battleSystem.CheckForFaint( user ) );
        yield return null;
        yield return _battleSystem.WaitForEventQueue();

        if( user != null && user.Pokemon.IsFainted() )
        {
            _battleSystem.SetHandleFaintCompleted( false );
            yield return _battleSystem.HandleFaintedPokemon( user );
            yield return null;
            yield return _battleSystem.WaitForEventQueue();
            yield return new WaitUntil( () => _battleSystem.HandleFaintCompleted );
        }

        yield return _battleSystem.WaitForUIQueue();
        yield return null;

        var activePokemon = _battleSystem.GetActivePokemon();

        foreach( var unit in activePokemon )
        {
            _battleSystem.AddToEventQueue( () => _battleSystem.ShowStatusChanges( user ) );
            yield return null;
            yield return _battleSystem.WaitForEventQueue();
        }

        yield return null;
    }

    private IEnumerator RoundEndUpdate()
    {
        //--Add all available units to the "After Turn List" so that it can perform all after turn functions such as weather damage, status damage, etc. in the appropriate speed order.
        List<BattleUnit> afterRoundList = new();
        var availPlayerUnits = _battleSystem.PlayerUnits.Select( u => u ).Where( u => u.Pokemon != null ).ToList();
        var availEnemyUnits = _battleSystem.EnemyUnits.Select( u => u ).Where( u => u.Pokemon != null ).ToList();

        for( int i = 0; i < availPlayerUnits.Count; i++)
            afterRoundList.Add( availPlayerUnits[i] );

        for( int i = 0; i < availEnemyUnits.Count; i++)
            afterRoundList.Add( availEnemyUnits[i] );

        //--Sort all units by speed
        afterRoundList = afterRoundList.OrderByDescending( unit => unit.Pokemon.Speed ).ToList();

        //--Go through each phase in the phase list, executing that phase on all pokemon in speed order
        foreach( RoundEndPhase endPhase in _battleSystem.RoundEndPhaseState.RoundEndPhases )
        {
            var phase = _battleSystem.RoundEndPhaseState.RoundEndPhaseDictionary[endPhase.Type];
            phase.OnPhaseTick( _battleSystem );

            foreach( var unit in afterRoundList )
            {
                if( unit.Pokemon.CurrentHP > 0 )
                {
                    phase.OnUnitTick( _battleSystem, unit );
                    _battleSystem.AddToEventQueue( () => _battleSystem.ShowStatusChanges( unit ) );
                    yield return null;
                    yield return _battleSystem.WaitForEventQueue();
                }

                yield return _battleSystem.CheckForFaint( unit );

                if( unit.Pokemon.CurrentHP == 0 )
                {
                    if( _battleSystem.BottomTrainerParty.Party.Contains( unit.Pokemon ) )
                    {
                        var activePokemon = _battleSystem.PlayerUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
                        var nextPokemon = _battleSystem.BottomTrainerParty.GetHealthyPokemon( dontInclude: activePokemon );

                        if( _battleSystem.CheckForBattleOver( activePokemon, nextPokemon ) )
                            break;
                    }

                    if( _battleSystem.BattleType != BattleType.WildBattle_1v1 )
                    {
                        if( _battleSystem.TopTrainerParty != null && _battleSystem.TopTrainerParty.Party.Contains( unit.Pokemon ) )
                        {
                            var activeEnemyPokemon = _battleSystem.EnemyUnits.Select( u => u.Pokemon ).Where( p => p.CurrentHP > 0 ).ToList();
                            var nextEnemyPokemon = _battleSystem.TopTrainerParty.GetHealthyPokemon( dontInclude: activeEnemyPokemon );

                            if( _battleSystem.CheckForBattleOver( activeEnemyPokemon, nextEnemyPokemon ) )
                                break;
                        }
                    }
                    else
                    {
                        //--In a wild battle, if the enemy unit faints the battle must just end and nothing else should calc or tick
                        _battleSystem.SetHandleFaintCompleted( false );
                        yield return _battleSystem.HandleFaintedPokemon( unit );
                        yield return new WaitUntil( () => _battleSystem.HandleFaintCompleted );
                        yield break;
                    }
                }
            }
        }

        //--Handle Unit Flags
        //--Handle fainted Pokemon after all other phases are complete
        foreach( var unit in afterRoundList )
        {
            unit.SetFlagActive( UnitFlags.DidDamage, false );
            unit.SetFlagActive( UnitFlags.Phased, false );
            unit.Pokemon.CureTransientStatus();

            if( unit.Pokemon.IsFainted() )
            {
                _battleSystem.SetHandleFaintCompleted( false );
                yield return _battleSystem.HandleFaintedPokemon( unit );
                yield return new WaitUntil( () => _battleSystem.HandleFaintCompleted );
            }

            unit.IncreaseTurnsTakenInBattle();
        }

        yield return null;
    }

    public IEnumerator ExecuteCommandQueue()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForSeconds( 0.25f );

        while( _battleSystem.CommandQueue.Count > 0 )
        {
            if( _battleSystem.CommandQueue.Count == 0 )
                break;
            
            var user = _battleSystem.CommandQueue.Peek().User;
            yield return _battleSystem.WaitForUIQueue();
            yield return null;

            UseMoveCommand moveCommand = null;
            Debug.Log( $"[Command Queue] Next Command is: {_battleSystem.CommandQueue.Peek()} by {user.Pokemon.NickName}. Their HP: {user.Pokemon.CurrentHP}, Status: {user.Pokemon.SevereStatus?.ID}" );
            if( _battleSystem.CommandQueue.Peek() is UseMoveCommand )
            {
                moveCommand = _battleSystem.CommandQueue.Peek() as UseMoveCommand;
                Debug.Log( $"[Command Queue] The next move command by {moveCommand.User.Pokemon.NickName} is {moveCommand.Move.MoveSO.Name}, targeting: {moveCommand.Targets}" );
            }

            yield return null;
            if( user.Pokemon.IsFainted() )
                yield return _battleSystem.CommandQueue.Dequeue();

            yield return null;
            if( _battleSystem.CommandQueue.Count == 0 )
                break;

            if(  moveCommand != null && _battleSystem.BattleType == BattleType.TrainerDoubles && moveCommand.SingleTarget != null && !moveCommand.SingleTarget.Pokemon.IsFainted() )
            {
                HandleDoublesTargetSwap();
                yield return null;
            }

            if( _battleSystem.CommandQueue.Count > 0 )
            {
                var command = _battleSystem.CommandQueue.Peek();

                //--This checks to see if the target we're attacking has fainted by the time the attack is run. First, we check if it's a move command at all, and if it is
                //--We make sure that it has a single target. If it does have a single target, we check to see if the target has fainted. if it has fainted, we want to
                //--discard the move command because we don't want to attack a fainted pokemon, or the pokemon that replaces it.
                //--We don't worry about this for multi target moves here, it will be handled in PerformMoveCommand, where we make a temporary list of all targets
                //--and make sure we don't call TakeDamage() on a pokemon that IsFainted() it.
                if( command is UseMoveCommand moveComm && moveCommand.SingleTarget != null && moveCommand.SingleTarget.Pokemon.IsFainted() )
                    yield return _battleSystem.CommandQueue.Dequeue();
                else
                    yield return _battleSystem.CommandQueue.Dequeue().ExecuteBattleCommand();
            }

            yield return new WaitUntil( () => !_battleSystem.BattleComposer.CMBrain.IsBlending );
            yield return null;

            yield return AfterTurnUpdate( user );
            yield return null;

            yield return _battleSystem.WaitForUIQueue();
            yield return new WaitForSeconds( 0.5f );

            //--We simply just reorder commands after every turn. with constant speed changes being fired off in an intense weather double battle, it's really not worth it to track a battle flag, or
            //--to give moves a flag to check for here. just do it anyway lol
            if( !_battleSystem.BattleOver )
                yield return ReorderCommands();

            yield return null;
        }

        _battleSystem.CommandQueue.Clear();
        yield return new WaitForSeconds( 0.25f );

        //--This should handle all board state updates like leftovers, status, weather, and field effects
        //--It gets called after all turns are completed and the command queue is empty
        yield return RoundEndUpdate();
        yield return null;
        
        yield return null;
        _battleSystem.SetUnitInSelectionState( 0 );

        yield return _battleSystem.WaitForUIQueue();
        yield return new WaitForSeconds( 0.1f );

        if( _battleSystem.StateMachine.CurrentState == this )
            _battleSystem.PopState();
    }

}
