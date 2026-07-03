using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum ActionResultType { Move, Switch }
public interface IActionResult
{
    public ActionResultType Type { get; set; }
    public ActionType ActionType { get; set; }
    public IBattleAIUnit CurrentActor { get; set; }
    public IBattleAIUnit Target { get; set; }
    public IBattleAIUnit Candidate { get; set; }
    public Move Move { get; set; }
    public TurnOutcomeProjection Top { get; set; }
}
