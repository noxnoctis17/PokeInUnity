using System;
using UnityEngine;

public class PlayerBattleMenu_AnimEvents : MonoBehaviour
{
    public Action<Transform> OnBattleStart;
    public Action<Transform> OnSwapActiveButton;
    public Action<Transform> OnNewActiveButton;
    public Action<Transform> OnHideMenu;
    public Action<Transform> OnRestoreMenu;
}
