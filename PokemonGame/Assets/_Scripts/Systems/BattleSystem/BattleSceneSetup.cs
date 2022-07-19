using System.Collections;
using Unity.Mathematics;
using UnityEngine;

public class BattleSceneSetup : MonoBehaviour
{
    [SerializeField] private ActiveBattlePokemon _playerActiveBattlePokemon;
    [SerializeField] private GameObject _battleUnitPrefab;

    public IEnumerator Setup( int unitAmount, PokemonParty pokemonParty ){
        yield return _playerActiveBattlePokemon.EnableUnits( unitAmount );

        yield return null;
    }
    
    public BattleUnit InstantiateBattleUnits( Vector3 location ){
        var obj = Instantiate( _battleUnitPrefab, location, quaternion.identity );
        var unit = obj.GetComponent<BattleUnit>();
        return unit;
    }
}
