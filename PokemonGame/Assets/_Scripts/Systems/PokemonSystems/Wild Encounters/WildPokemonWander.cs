using System.Collections;
using UnityEngine;
using Pathfinding;

public class WildPokemonWander : MonoBehaviour
{
    private PokemonSO _wildPokemon;
    private AIPath _agentMon;
    [SerializeField] private float _radius;

    //---------------------------------------------

    private enum _WanderStates {
        Idle,
        Wander,
        Curious,
        Aggressive,
        Scared
    }

    [SerializeField] private _WanderStates _wanderState;
    private Transform _targetPosition;
    private Transform _previousRandomPosition;
    private bool _isWandering;
    private bool _isAggressive;
    private float _stopAggressiveDistance = 15f;
    private Vector3 _previousPosition;
    private bool _isIdle;
    
    //---------------------------------------------
    
    private void Awake(){
        _agentMon = GetComponent<AIPath>();
        _wildPokemon = GetComponent<WildPokemon>().wildPokemon.PokeSO;
        _wanderState = _WanderStates.Wander;
    }

    private void OnDisable() {
        _agentMon.SetPath(null);
    }

    private void Update()
    {
        //------------------------WANDER STATES----------------------------

        if( GameStateTemp.GameState != GameState.Overworld ) return;
        
        switch ( _wanderState ){

            case _WanderStates.Wander:

                //--set a point to wander to
                if(!_isWandering && !_agentMon.hasPath){
                    _isWandering = true;
                    _agentMon.SetPath(null);
                    SetWanderPoint();
                }

                //--switch to curious or aggressive states when player is within range
                if(_agentMon.hasPath){
                    FindPlayer();
                }

                //--once we are at our wander point we have a 33% chance on finding a new point, otherwise we idle
                if(_agentMon.reachedEndOfPath){
                    if(Random.Range(1,4) == 1){
                        SetWanderPoint();
                    }
                    else{
                        _agentMon.SetPath(null);
                        _wanderState = _WanderStates.Idle;
                    }
                }

            break;

            case _WanderStates.Idle:

                _isWandering = false;
                if(!_isIdle && !_agentMon.hasPath)
                _isIdle = true;
                StartCoroutine(WanderIdle());
                    
            break;

            case _WanderStates.Curious:

                _agentMon.SetPath(null);
                _agentMon.endReachedDistance = 5f;
                _agentMon.destination = PlayerReferences.PlayerTransform.position;

                float stopCuriousDistance = 11f;
                if(Vector3.Distance(transform.position, PlayerReferences.PlayerTransform.position) > stopCuriousDistance){
                    _agentMon.endReachedDistance = 0.5f;
                    _wanderState = _WanderStates.Wander;
                }

            break;

            case _WanderStates.Aggressive:

                if(!_isAggressive){
                    _isAggressive = true;
                    _agentMon.maxSpeed = 10f;
                    _agentMon.maxAcceleration = 10f;
                    _previousPosition = _agentMon.position;
                }

                _agentMon.destination = PlayerReferences.PlayerTransform.position;

                if(Vector3.Distance(transform.position, PlayerReferences.PlayerTransform.position) > _stopAggressiveDistance){
                    if(_agentMon.remainingDistance < 0.5f) //--should hopefully wait to return to previous position before wandering
                    _agentMon.destination = _previousPosition;

                    if(Vector3.Distance(transform.position, _previousPosition) < 0.5f)
                    _agentMon.maxSpeed = 3;
                    _agentMon.maxAcceleration = 3f;
                    _wanderState = _WanderStates.Wander;
                }

            break;
        }
        
    }



    private void SetWanderPoint(){
        var startNode = AstarPath.active.GetNearest(transform.position, NNConstraint.Default).node;
        var nodes = PathUtilities.BFS(startNode, 5);
        var singleRandomPoint = PathUtilities.GetPointsOnNodes(nodes, 1)[0];
        _agentMon.destination = singleRandomPoint;
    }

    private void FindPlayer(){
        float discoverRange = 10;
        if(Vector3.Distance(transform.position, PlayerReferences.PlayerTransform.position) < discoverRange){
            switch(_wildPokemon.WildType){
                case WildType.Curious:

                    _wanderState = _WanderStates.Curious;

                break;

                case WildType.Aggressive:

                    _wanderState = _WanderStates.Aggressive;

                break;
            }
        }
    }

    private IEnumerator WanderIdle(){
        yield return new WaitForSeconds(UnityEngine.Random.Range(5f, 21f));
        _wanderState = _WanderStates.Wander;
        _isIdle = false;
    }

    #if UNITY_EDITOR
    private void OnDrawGizmos(){
        Gizmos.DrawWireSphere(transform.position, _radius);
    }
    #endif
}
