using UnityEngine;

namespace NoxNoctisDev.StateMachine{
//////////////////////////////////////////////////////////namespace
public class State<T> : MonoBehaviour
{
    public virtual void EnterState( T owner ){}
    public virtual void UpdateState(){}
    public virtual void ReturnToState(){}
    public virtual void ExitState(){}
    public virtual void PauseState(){}

}







//////////////////////////////////////////////////////////namespace
}
