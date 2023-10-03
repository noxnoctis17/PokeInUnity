using System.Collections;
using UnityEngine;

public class BattleState_Setup : InBattleStates
{
    //--Base Constructor
    public BattleState_Setup( BattleSystem battleSystem ) : base( battleSystem ){}

    public override IEnumerator Start(){
        BattleUIActions.OnBattleSystemBusy?.Invoke();
        BattleSystem.OnBattleStarted?.Invoke();
        yield return new WaitForSeconds(0.1f);
        yield return BattleSystem.BattleArena.PrepareArena( BattleSystem );
        yield return SetUpPlayer();
        BattleSystem.PlayerAction();
    }

    private IEnumerator SetUpPlayer(){
        //--eventually will have to account for follower mon shit
        // BattleSystem.PlayerUnit.Setup( BattleSystem.PlayerParty.GetHealthyPokemon(), BattleSystem.PlayerHUD );

        //--Set up Player UI
        BattleSystem.FightMenu.SetUpMoves( BattleSystem.PlayerUnit.Pokemon.Moves );
        BattleSystem.PartyScreen.Init();
        BattleSystem.PartyScreen.SetParty( BattleSystem.PlayerParty.PartyPokemon );
        yield return null;
    }

    private IEnumerator SetUpEnemy(){
        // BattleSystem.EnemyUnit.Setup( BattleSystem.WildPokemon, BattleSystem.EnemyHUD );
        Debug.Log( BattleSystem.WildPokemon.PokeSO.pName );
        yield return null;
    }
}
