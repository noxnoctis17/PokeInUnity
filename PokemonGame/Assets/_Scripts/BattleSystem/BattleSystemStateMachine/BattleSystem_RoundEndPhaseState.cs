using System.Collections;
using System.Collections.Generic;
using NoxNoctisDev.StateMachine;
using UnityEngine;

public class BattleSystem_RoundEndPhaseState : State<BattleSystem>
{
    private BattleSystem _battleSystem;
    public Dictionary<RoundEndPhaseType, IRoundEndPhaseHandler> RoundEndPhaseDictionary { get; private set; }
    [SerializeField] private List<RoundEndPhaseSO> _roundEndPhases;
    public List<RoundEndPhaseSO> RoundEndPhases => _roundEndPhases;

    public override void EnterState( BattleSystem owner )
    {
        _battleSystem = owner;
    }

    public void Init()
    {
        RoundEndPhaseDictionary = new()
        {
            { RoundEndPhaseType.WeatherDamage, new RoundEndPhase_WeatherDamage()    },
            { RoundEndPhaseType.StatusDamage, new RoundEndPhase_StatusDamage()      },
            { RoundEndPhaseType.ItemRoundEnd, new RoundEndPhase_ItemRoundEnd()      },
            { RoundEndPhaseType.CourtEffect, new RoundEndPhase_CourtEffect()        },
        };
    }

    public void Clear()
    {
        RoundEndPhaseDictionary = null;
    }
    
}
