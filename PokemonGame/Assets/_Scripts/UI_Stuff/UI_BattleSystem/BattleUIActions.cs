using UnityEngine;
using System;

public class BattleUIActions : MonoBehaviour
{
    public static Action OnCommandAnimationsCompleted;
    public static Action OnAttackPhaseCompleted;
    public static Action OnSubMenuOpened;
    public static Action OnSubMenuClosed;
    public static Action OnCommandUsed;
    public static Action OnBattleSystemBusy;
    public static Action OnFightMenuOpened;
    public static Action OnFightMenuClosed;
    public static Action OnPkmnMenuOpened;
    public static Action OnPkmnMenuClosed;
    public Action<GameObject> OnMenuOpened;
}
