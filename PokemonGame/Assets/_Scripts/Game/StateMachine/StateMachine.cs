using System.Collections.Generic;
using System;

namespace NoxNoctisDev.StateMachine{
//////////////////////////////////////////////////////////namespace
public class StateMachine<T>
{
    public Action<State<T>> OnQueueNextState;
    public State<T> DefaultState { get; private set; }
	public State<T> CurrentState { get; private set; }
    private State<T> _queuedState;
    public Stack<State<T>> StateStack { get; private set; }
    private T _owner;

    public StateMachine( T owner, State<T> defaultState ){
        _owner = owner;
        DefaultState = defaultState;
    }

    private void SetActions(){
        OnQueueNextState += QueueNextState;
    }

    public void ClearActions(){
        OnQueueNextState -= QueueNextState;
    }

    private void QueueNextState( State<T> nextState ){
        _queuedState = nextState;
    }

    public void Initialize(){
        SetActions();
        CurrentState = DefaultState;
        CurrentState.EnterState( _owner );
    }

    public void StartState( State<T> newState ){
        if( newState == null || newState == DefaultState ){
            CurrentState = DefaultState;
        }else{
            CurrentState = newState;
        }

        CurrentState.EnterState( _owner );
    }

    public void Update(){
        if( CurrentState != _queuedState && _queuedState != null ){
            ChangeState( _queuedState );
        }
        CurrentState.UpdateState();
    }

    public void ChangeState( State<T> newState ){
        CurrentState.ExitState();
        CurrentState = newState;
        StartState( newState );
    }

}




//////////////////////////////////////////////////////////namespace
}
