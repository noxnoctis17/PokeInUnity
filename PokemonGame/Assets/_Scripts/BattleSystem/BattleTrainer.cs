using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum ControlType { Player, CPU, }
public class BattleTrainer
{
    public ControlType ControlType { get; private set; }
    public string TrainerName { get; private set; }
    public string TrainerClass { get; private set; }
    public int TrainerSkillLevel { get; private set; }
    public GameObject TrainerCenter { get; private set; }
    public MusicTheme BattleTheme { get; private set; }
    public List<Pokemon> Party { get; private set; }
    public Sprite Portrait { get; private set; }
    public DialogueColorSO DialogueColor { get; private set; }
    public Dictionary<TrainerClasses, string> TrainerClassDB { get; private set; }
    public Action OnDefeated;

    //--CPU Constructor
    public BattleTrainer(
        ControlType controller,
        string name,
        TrainerClasses
        trainerClass,
        GameObject trainerCenter,
        Sprite portrait,
        DialogueColorSO dialogueColor,
        List<Pokemon> party,
        int skillLevel = 0,
        MusicTheme battleTheme = default,
        Action onDefeated = null
        )
    {
        SetClassDB();
        ControlType = controller;
        TrainerName = name;
        TrainerClass = TrainerClassDB[trainerClass];
        TrainerCenter = trainerCenter;
        TrainerSkillLevel = skillLevel;
        Portrait = portrait;
        DialogueColor = dialogueColor;
        BattleTheme = battleTheme;
        OnDefeated = onDefeated;
        Party = CloneParty( party );
        BattleSystem.OnBattlePartyUpdated?.Invoke( Party );
    }

    private void SetClassDB()
    {
        TrainerClassDB = new()
        {
            { TrainerClasses.None,          "" },
            { TrainerClasses.AceTrainer,    "Ace Trainer" },
            { TrainerClasses.Hiker,         "Hiker" },
            { TrainerClasses.Lass,          "Lass" },
            { TrainerClasses.Youngster,     "Youngster" },
            { TrainerClasses.Swimmer,       "Swimmer" },
            { TrainerClasses.BugCatcher,    "Bug Catcher" },
            { TrainerClasses.GymLeader,     "Gym Leader" },
            { TrainerClasses.EliteFour,     "Elite Four" },
            { TrainerClasses.Champion,      "Champion" },
            { TrainerClasses.Trainer,       "Trainer" },
        };
    }

    private List<Pokemon> CloneParty( List<Pokemon> party )
    {
        List<Pokemon> clonedParty = new();

        for( int p = 0; p < party.Count; p++ )
        {
            var pokemon = party[p];
            List<MoveSO> moves = new();

            //--Recreate Movelist
            for( int m = 0; m < pokemon.ActiveMoves.Count; m++ )
            {
                moves.Add( pokemon.ActiveMoves[m].MoveSO );
            }

            //--Create Objects
            TrainerPokemon template = new(
                pokemon.PokeSO,
                pokemon.NickName,
                pokemon.Level,
                pokemon.CurrentNature,
                pokemon.AbilityID,
                pokemon.HeldItem,
                pokemon.HP_EVs,
                pokemon.ATK_EVs,
                pokemon.DEF_EVs,
                pokemon.SPATK_EVs,
                pokemon.SPDEF_EVs,
                pokemon.SPE_EVs,
                pokemon.CurrentBallType,
                moves
                );
                
            Pokemon clone = new( template );

            //--Sync
            clone.CurrentHP = pokemon.CurrentHP;
            if( pokemon.SevereStatus != null )
                clone.SyncSevereStatus( pokemon.SevereStatus.ID );

            for( int m = 0; m < clone.ActiveMoves.Count; m++ )
            {
                clone.ActiveMoves[m].PP = pokemon.ActiveMoves[m].PP;
            }

            clone.CloneOverridePID( pokemon.PID );

            clonedParty.Add( clone );
        }

        return clonedParty;
    }

    public Pokemon GetHealthyPokemon( List<Pokemon> dontInclude = null )
    {
        var healthyPokemon = Party.Where( x => x.CurrentHP > 0 ).ToList();
        
        if( dontInclude != null )
            healthyPokemon = healthyPokemon.Where( p => !dontInclude.Contains( p ) ).ToList();

        return healthyPokemon.FirstOrDefault();
    }

    public List<Pokemon> GetHealthyPokemon( int unitCount )
    {
        return Party.Where( x => x.CurrentHP > 0 ).Take( unitCount ).ToList();
    }

    public void SwitchPokemonPosition( Pokemon a, Pokemon b )
    {
        int indexA = Party.IndexOf( a );
        int indexB = Party.IndexOf( b );

        Debug.Log( $"Swapping. Index A: {indexA}, Index B: {indexB}" );

        if( indexA < 0 || indexB < 0 )
            return;
        
        Debug.Log( $"Swapping {a.NickName} and {b.NickName}" );
        ( Party[indexA], Party[indexB] ) = ( Party[indexB], Party[indexA] );
        BattleSystem.OnBattlePartyUpdated?.Invoke( Party );
    }
}

public static class BattleTrainerFactory
{
    public static BattleTrainer FromNPC( Trainer npc )
    {
        return new BattleTrainer(
            controller: ControlType.CPU,
            name: npc.TrainerName,
            trainerClass: npc.TrainerClass,
            party: npc.BuildTrainerParty(),
            trainerCenter: npc.TrainerCenter,
            skillLevel: npc.TrainerSkillLevel,
            battleTheme: npc.TrainerSO.BattleTheme,
            portrait: npc.TrainerSO.Portrait,
            dialogueColor: npc.DialogueColor,
            onDefeated:
            npc.SetDefeated
            );
    }
    
    public static BattleTrainer FromPlayer( PlayerTrainer player )
    {
        return new BattleTrainer(
            controller: ControlType.Player,
            name: player.TrainerName,
            trainerClass: player.TrainerClass,
            party: player.ActiveParty,
            trainerCenter: player.TrainerCenter,
            skillLevel: 100,
            portrait: player.Portrait,
            dialogueColor:
            player.DialogueColor
            );
    }
}
