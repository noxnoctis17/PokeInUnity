using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleSystem_Opener : MonoBehaviour
{
    public static BattleSystem_Opener Instance;
    [SerializeField] private GameObject _battleSystemContainer;

    private void Awake()
    {
        Instance = this;
    }

    public void OpenBattleSystem()
    {
        _battleSystemContainer.SetActive( true );
    }

    public void CloseBattleSystem()
    {
        _battleSystemContainer.SetActive( false );
    }
}
