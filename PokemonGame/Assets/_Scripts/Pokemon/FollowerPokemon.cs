using UnityEngine;
using UnityEngine.AI;

public class FollowerPokemon : MonoBehaviour
{
    [SerializeField] private PokemonSO _pokeSO;
    [SerializeField] private PokemonAnimator _pokeAnimator;
    [SerializeField] private Transform _followerTarget;
    [SerializeField] private NavMeshAgent _agentMon;
    private Transform _previousLocation;

    private void Start(){
        //--If there is already a pokemon assigned, setup the animator
        if( _pokeSO != null)
        {
            SetFollowerPokemon();
        }
    }

    private void Update(){
        if( !_agentMon.enabled )
            return;

        if( !_agentMon.hasPath || _followerTarget != _previousLocation )
            SetDestination();
        else if( _agentMon.hasPath || _followerTarget != _previousLocation )
            SetDestination();

        _previousLocation = _followerTarget;
    }

    private void SetDestination()
    {
        _agentMon.ResetPath();
        _agentMon.SetDestination( _followerTarget.position );
        _pokeAnimator.MoveX = _agentMon.desiredVelocity.x;
        _pokeAnimator.MoveY = _agentMon.desiredVelocity.y;
    }

    private void SetFollowerPokemon()
    {
        SetupNavmeshAgent();
        _pokeAnimator.Initialize( _pokeSO );
        _pokeAnimator.enabled = true;
        GetComponentInChildren<PokemonShadow>( true ).enabled = true;
    }

    public void SetFollowerPokemon( Pokemon pokemon ){
        gameObject.SetActive( true );
        SetupNavmeshAgent();
        _pokeSO = pokemon.PokeSO;
        _pokeAnimator.Initialize( _pokeSO );
        _pokeAnimator.enabled = true;
        GetComponentInChildren<PokemonShadow>( true ).enabled = true;
    }

    private void SetupNavmeshAgent()
    {
        transform.position = _followerTarget.position;

        //--Navmesh Agent
        if( NavMesh.SamplePosition( transform.position, out NavMeshHit hit, 2f, NavMesh.AllAreas ))
        {
            transform.position = hit.position;
            _agentMon.enabled = true;
        }
    }
}
