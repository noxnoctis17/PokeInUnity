using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class HPBar : MonoBehaviour
{
    [SerializeField] private Slider _redHPSlider;
    [SerializeField] private Slider _instantHPSlider;
    public Slider RedHPSlider => _redHPSlider;
    public bool IsUpdating { get; private set; }

    public void SetMaxHP( int maxHP ){
        _redHPSlider.maxValue = maxHP;
        _instantHPSlider.maxValue = maxHP;
    }

    public void SetHP( int hp, int maxHP ){
        SetMaxHP( maxHP );
        _redHPSlider.value = hp;
        _instantHPSlider.value = hp;
    }

    public IEnumerator AnimateHP( int newHP ){
        IsUpdating = true;

        float currentHP = _redHPSlider.value;
        bool isDamaging = currentHP - newHP > 0;
        float changeAmount = currentHP - newHP;

        yield return GetComponentInParent<BattleHUD>().gameObject.GetComponent<RectTransform>().DOShakeAnchorPos( 0.25f, 100f, 10 ).WaitForCompletion();
        // yield return _instantHPSlider.transform.DOPunchPosition( new( -10f, 0f, 0 ), 0.75f, 10, 0f ).WaitForCompletion();
        _instantHPSlider.value = newHP;
        yield return new WaitForSeconds( 0.25f );

        while( isDamaging ? ( currentHP > newHP ) : ( currentHP < newHP ) ){
            _redHPSlider.value = currentHP -= changeAmount * Time.deltaTime * 2;

            yield return null;
        }

        _redHPSlider.value = newHP;
        yield return new WaitForEndOfFrame();
        IsUpdating = false;
    }
}
