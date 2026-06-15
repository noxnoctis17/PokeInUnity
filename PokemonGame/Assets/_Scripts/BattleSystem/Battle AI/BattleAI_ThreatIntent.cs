using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BattleAI_ThreatIntent
{
    private BattleAI _ai;

    public BattleAI_ThreatIntent( BattleAI ai )
    {
        _ai = ai;
    }

    public ThreatIntentCandidates GetThreatCandidates( IBattleAIUnit threat, IBattleAIUnit us )
    {
        ThreatIntentCandidates tic = new()
        {
            Threat = threat,
            MoveThreatResult = _ai.MoveCommand.GetMove_BestAttack( threat, us ),
            DefensiveSwitchCandidateResult = _ai.SwitchCommand.GetSwitch_Defensive( threat ),
            OffensiveSwitchCandidateResult = _ai.SwitchCommand.GetSwitch_Offensive( threat ),
            SetupThreatResult = _ai.MoveCommand.GetMove_Setup( threat, us ),
            OffensiveStatusThreatResult = _ai.MoveCommand.GetMove_OffensiveStatus( threat, us ),
            // SupportiveStatusThreatResult
            // ProtectThreatResult
        };

        return tic;
    }

    public ThreatIntentResult GetThreatIntentResult( ThreatIntentCandidates tic )
    {


        return new()
        {
            Threat = tic.Threat,
        };
    }
}

public enum ThreatIntent{ None, Attack, DefensiveSwitch, OffensiveSwitch, Setup, OffensiveStatus, SupportiveStatus, Protect }
public struct ThreatIntentResult
{
    public IBattleAIUnit Threat;
    public ThreatIntent Intent;
    public float Confidence;
    public object Candidate;
}

public struct ThreatIntentCandidates
{
    public IBattleAIUnit Threat;
    public MoveThreatResult MoveThreatResult;
    public SwitchCandidateResult DefensiveSwitchCandidateResult;
    public SwitchCandidateResult OffensiveSwitchCandidateResult;
    public SetupThreatResult SetupThreatResult;
    public StatusThreatResult OffensiveStatusThreatResult;
    public StatusThreatResult SupportiveStatusThreatResult;
    public StatusThreatResult ProtectThreatResult;
}
