using UnityEngine;

public class StateMachineDisplays : MonoBehaviour
{
    [SerializeField] private bool _show_GameStateStateStack;   
    [SerializeField] private bool _show_PlayerBattleMenuStateStack;
    [SerializeField] private bool _show_BattleSystemStateStack;
    [SerializeField] private bool _show_PauseScreenStateStack;
    [SerializeField] private bool _show_WildPokemonStateStack;
    [SerializeField] private bool _show_PlayerAnimatorStack;

    public static bool Show_GameStateStateStack;
    public static bool Show_PlayerBattleMenuStateStack;
    public static bool Show_BattleSystemStateStack;
    public static bool Show_PauseScreenStateStack;
    public static bool Show_WildPokemonStateStack;
    public static bool Show_PlayerAnimatorStack;

    private void Update(){
        Show_GameStateStateStack            = _show_GameStateStateStack;
        Show_PlayerBattleMenuStateStack     = _show_PlayerBattleMenuStateStack;
        Show_BattleSystemStateStack         = _show_BattleSystemStateStack;
        Show_PauseScreenStateStack          = _show_PauseScreenStateStack;
        Show_WildPokemonStateStack          = _show_WildPokemonStateStack;
        Show_PlayerAnimatorStack            = _show_PlayerAnimatorStack;
    }
}
