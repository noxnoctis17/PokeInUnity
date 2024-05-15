using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class StateMachineDisplays : MonoBehaviour
{
    [SerializeField] private bool _show_PlayerBattleMenuStateStack;
    [SerializeField] private bool _show_PauseScreenStateStack;
    [SerializeField] private bool _show_GameStateStateStack;

    public static bool Show_WildPokemonStateStack;
    public static bool Show_PlayerBattleMenuStateStack;
    public static bool Show_PauseScreenStateStack;
    public static bool Show_GameStateStateStack;

    private void Update(){
        Show_PlayerBattleMenuStateStack = _show_PlayerBattleMenuStateStack;
        Show_PauseScreenStateStack      = _show_PauseScreenStateStack;
        Show_GameStateStateStack     = _show_GameStateStateStack;
    }
}
