using UnityEngine;

[CreateAssetMenu( menuName = "Items/TM/TM Item" )]
public class TMItemSO : ItemSO
{
    [SerializeField] private MoveSO _moveSO;

    public MoveSO MoveSO => _moveSO;

    public override bool Use( Pokemon pokemon ){
        return true;
    }
}
