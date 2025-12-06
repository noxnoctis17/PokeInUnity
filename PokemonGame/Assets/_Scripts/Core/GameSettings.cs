using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameSettings : MonoBehaviour
{
    public static GameSettings Instance;
    [SerializeField] private bool _useBattleCameras;
    public bool UseBattleCameras => _useBattleCameras;

    private void OnEnable()
    {
        Instance = this;
    }
}
