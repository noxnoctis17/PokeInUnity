using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NoxNoctisDev.StateMachine{

public class StateMachine<T>
{
	public State<T> CurrentState { get; private set; }
    public Stack<State<T>> StateStack { get; private set; }
    private T _owner;

    public StateMachine( T owner ){
        _owner = owner;
        StateStack = new Stack<State<T>>();
    }

    public void Push( State<T> newState ){
        StateStack.Push( newState );
        CurrentState = newState;
        CurrentState.Enter( _owner );
    }

    public void Pop(){
        StateStack.Pop();
        CurrentState.Exit();
        CurrentState = StateStack.Peek();
        CurrentState.Return();
    }

    public void Execute(){
        CurrentState?.Execute();
    }

    public void ChangeState( State<T> newState ){
        if( CurrentState != null ){
            StateStack.Pop().Exit();
        }

        Push( newState ); //--if ChangeState doesn't seem to be working correctly at some point, copy Push code here instead of using Push()

    }



}

}
