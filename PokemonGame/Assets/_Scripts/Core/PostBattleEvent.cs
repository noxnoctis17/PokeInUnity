using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PostBattleEvent : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI _eventText;
    [SerializeField] private Image _portrait;
    [SerializeField] private GameObject _animateThis;
    private PostBattleSummary _pbSummary;
    private WaitForSeconds _timer;
    public Transform AnimateThis => _animateThis.transform;

    private void OnEnable()
    {
        StartCoroutine( BeginTimer() );
    }

    public void Setup( PostBattleSummary pbSummary, string eventText, Sprite portrait )
    {
        _pbSummary = pbSummary;
        _eventText.text = eventText;
        _portrait.sprite = portrait;
        _timer = new( 5f );
    }

    private IEnumerator BeginTimer()
    {
        yield return _timer;
        _pbSummary.ReleaseSummaryEvent( this );
    }
}
