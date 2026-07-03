using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleAI_Blackboard
{
    private readonly BattleAI _ai;

    //--Each Battle AI monob should now become an ai "agent".
    //--An "agent" should be able to use all tools available to it. it makes decisions and submits its decisions to the battle system.
    //--universal tracking information should be handled by the blackboard. the blackboard can be accessed by each one if its OWN agents.
    //--each ai trainer will have its own blackboard, in the event of ai vs ai battles, or player + ai vs ai ( and even + a 3rd ai) multi battles.
    //--It should be a refactor goal to have the blackboard own all of the brain layer and scoring layer functions as well.
    //--for example, in a player vs ai double battle, there is one blackboard, which then generates one set of classes for its two agents to access for decision making.
    //--the blackboard itself will naturally build all unit trackers and anything else of that nature, such as team adapters and active unit adapters.

    //--ohh, no, an agent doesn't run its own code - the blackboard should choose commands for each unit.
    //--so realistically, BattleAI becomes the blackboard, and instead battle unit objects should receive an "ai agent" class, or maybe even not that, just the flag inside of BattleUnit
    //--noting whether it is an ai controlled unit or not, because a lot of battle system architecture itself depends on that flag.
    //--the BattleAI class should simply loop over its own units and choose a command for each one. essentially moving the part of this that already exists
    //--out of the ai turn state and into the main ai command selection. "ThisUnitAdapter" should effectively get phased out, and replaced across all analysis
    //--by the "current unit the ai is making a decision for". then, we can skip marking units as ai, and instead, we simply turn on the BattleAI object associated with each team's side
    //--this will be a dynamic option to allow for multiple ai controllers depending on what the battle calls for. regular player vs 1 ai single and double battles will obviously have
    //--1 BattleAI mono object turned on for the top court. ai vs ai singles/doubles will have an object for top and bottom courts. and then any ai controlled slots in a multibattle
    //--will simply have their own controllers as well.

    //--so if BattleAI.cs becomes the main brain and orchestrates decision making for units, i think the blackboard class should assist by actually handling all static information and info tracking
    //--that means team adapters, team comp, game plan, last actions, last active pokemon, current active units on both sides, etc. all of that should exist here, and be extracted out of BattleAI.cs
    //--that way, when BattleAI.cs or any downstream decision making function needs access to static/tracked information, it will simply look at the blackboard. the blackboard can even have functions
    //--to update learned information across any given battle. improved estimates on EV and nature spreads, for example, could live here.

    public BattleAI_Blackboard( BattleAI ai )
    {
        _ai = ai;
    }
}

public struct UnitTracker
{
    //--This Slot Tracking
    public Pokemon CurrentPokemon;
    public Pokemon PreviousPokemon;
    public ActionEvaluation LastAction;
    public int SwitchAmount;
    public int SetupAmount;

    //--This Slot's Opponent Tracking
    public BattleUnit LastTargetSlot;
    public Pokemon LastTarget;
    public List<Pokemon> LastOpponents;
}
