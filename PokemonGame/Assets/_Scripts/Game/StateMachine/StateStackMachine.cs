using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class StateStackMachine<T>
{
    public State<T> CurrentState { get; private set; }
    public Stack<State<T>> StateStack { get; private set; }
    private T _owner;

    public StateStackMachine( T owner ){
        _owner = owner;
        StateStack = new Stack<State<T>>();
    }

    public void Push( State<T> newState ){
        Debug.Log( $"S{_owner} Push(): {newState}" );
        StateStack.Push( newState );

        if( CurrentState != null )
            CurrentState.PauseState();

        CurrentState = newState;
        CurrentState.EnterState( _owner );
    }

    public void Pop(){
        Debug.Log( $"StateStack Machine Pop() by: {_owner}" );
        StateStack.Pop();
        CurrentState.ExitState();

        if( StateStack.Peek() != null){
            CurrentState = StateStack.Peek();
            CurrentState.ReturnToState();
        }
    }

    public void ChangeState( State<T> newState ){
        Debug.Log( $"S{_owner} ChangeState(): {newState}" );
        StateStack.Pop().ExitState();
        CurrentState = newState;
        Push( CurrentState );
    }

    public void Update(){
        CurrentState.UpdateState();
    }

    public void ClearStack(){
        StateStack.Clear();
    }
}
