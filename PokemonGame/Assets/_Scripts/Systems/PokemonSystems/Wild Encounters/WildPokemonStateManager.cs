using System.Collections;
using UnityEngine;
using Pathfinding;
using System;
using NoxNoctisDev.StateMachine;

public class WildPokemonStateManager : MonoBehaviour
{
    // public Action OnPlayerTooFar;
    // private WildPokemon _wildPokemon; //--Just to cache this for the state machine
    private PokemonSO _wildMonSO; //--We get this to have access to the species' wander type (ex. aggressive or scared)
    public PokemonAnimator PokemonAnimator { get; private set; }
    // public AIPath AgentMon { get; private set; }
    [SerializeField] private float _radius;

    // private void OnEnable(){
    //     OnPlayerTooFar += SetWanderState;
    // }

    // private void OnDisable() {
    //     OnPlayerTooFar -= SetWanderState;
    //     AgentMon.SetPath( null );
    //     StopAllCoroutines();
    // }
    
    // private void Start(){
    //     PokemonAnimator = GetComponentInChildren<PokemonAnimator>();
    //     AgentMon = GetComponent<AIPath>();
    //     _wildPokemon = GetComponent<WildPokemon>();
    //     _wildMonSO = _wildPokemon.Pokemon.PokeSO;
    //     _wildPokemon.WildPokemonStateMachine?.StartState( _wildPokemon.WanderState );
    // }

    

    // public void SetWanderState(){
    //     _wildPokemon.WildPokemonStateMachine?.OnQueueNextState?.Invoke( _wildPokemon.WanderState );
    // }

    // public IEnumerator WanderIdle(){
    //     yield return new WaitForSeconds( UnityEngine.Random.Range( 5f, 21f ) );
    //     if( _wildPokemon.WildPokemonStateMachine != null ){
    //         SetWanderState();
    //     }
    // }

    // #if UNITY_EDITOR
    // private void OnDrawGizmos(){
    //     Gizmos.DrawWireSphere( transform.position, _radius );
    // }
    // #endif
}
