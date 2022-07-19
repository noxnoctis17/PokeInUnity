using System.Collections;
using UnityEngine;

public class BattleState_Setup : BattleState
{
    public BattleState_Setup( BattleSystem battleSystem ) : base( battleSystem ){
    }
    
    public override IEnumerator Start(){
        BattleSystem.EventSystem.enabled = false;
        BattleUIActions.OnBattleSystemBusy?.Invoke();
        yield return new WaitForSeconds(0.1f);

        yield return SetUpPlayer();
        yield return SetUpEnemyTrainer();
        BattleSystem.OnBattleStarted?.Invoke();
        //--let's also start separating the dialogue box from the battle. we won't be waiting for its updates, it'll just store them and spit them out as things progress.
        //--i could probably make a queue that takes in dialgoues and runs them as the go. that sounds like a good way to handle it so it doesn't break
        yield return BattleSystem.DialogueBox.TypeDialogue( $"A wild {BattleSystem.EnemyUnit.Pokemon.PokeSO.pName} appeared!" );
        yield return BattleSystem.BattleSceneSetup.Setup( BattleSystem.PlayerUnitAmount, BattleSystem.PlayerParty );
        //--animate all UI elements into place. can maybe even get fancy here eventually
        BattleSystem.PlayerAction();
    }

    private IEnumerator SetUpPlayer(){

        var unit1 = BattleSystem.BattleSceneSetup.InstantiateBattleUnits( PlayerReferences.Poke1.position );
        unit1.Setup( BattleSystem.PlayerParty.GetHealthyPokemon(), BattleSystem.PlayerHUD );
        BattleSystem.PlayerUnit = unit1;


        //--Set up Player UI
        BattleSystem.FightMenu.SetUpMoves( unit1.Pokemon.Moves );
        BattleSystem.PartyScreen.Init();
        BattleSystem.PartyScreen.SetParty( BattleSystem.PlayerParty.PartyPokemon );
        yield return null;
    }

    private IEnumerator SetUpEnemyTrainer(){
        BattleSystem.EnemyUnit.Setup( BattleSystem.WildPokemon, BattleSystem.EnemyHUD);
        Debug.Log( BattleSystem.WildPokemon.PokeSO.pName );
        yield return null;
    }
}
