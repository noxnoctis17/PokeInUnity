using System.Collections;
using UnityEngine;
using Pathfinding;

public class WildPokemonWander : MonoBehaviour
{
    private WildPokemon _wildPokemon; //--Just to cache this for the state machine
    private PokemonSO _wildMonSO; //--We get this to have access to the species' wander type (ex. aggressive or scared)
    public AIPath AgentMon { get; private set; }
    [SerializeField] private float _radius;

    private void OnEnable(){
    }

    private void OnDisable() {
        AgentMon.SetPath( null );
        StopAllCoroutines();
    }
    
    private void Awake(){
        AgentMon = GetComponent<AIPath>();
        _wildPokemon = GetComponent<WildPokemon>();
        _wildMonSO = _wildPokemon.Pokemon.PokeSO;
        _wildPokemon.WildPokemonStateMachine?.Push( WildMon_WanderState.Instance );
    }

    private void Update(){
        _wildPokemon.WildPokemonStateMachine?.Execute();
    }

    public void PopState(){
        _wildPokemon.WildPokemonStateMachine?.Pop();
    }

    public void SetWanderState(){
        _wildPokemon.WildPokemonStateMachine?.ChangeState( WildMon_WanderState.Instance );
    }

    public void SetIdleState(){
        _wildPokemon.WildPokemonStateMachine?.ChangeState( WildMon_IdleState.Instance );
    }

    public void SetCuriousState(){
        _wildPokemon.WildPokemonStateMachine?.Push( WildMon_CuriousState.Instance );
    }

    public void SetAggressiveState(){
        _wildPokemon.WildPokemonStateMachine?.Push( WildMon_AggressiveState.Instance );
    }

    public void SetPausedState(){
        _wildPokemon.WildPokemonStateMachine?.Push( WildMon_PausedState.Instance );
    }

    public IEnumerator WanderIdle(){
        yield return new WaitForSeconds( Random.Range( 5f, 21f ) );
        if( _wildPokemon.WildPokemonStateMachine != null ){
            SetWanderState();
        }
    }

    // #if UNITY_EDITOR
    // private void OnDrawGizmos(){
    //     Gizmos.DrawWireSphere( transform.position, _radius );
    // }
    // #endif
}
