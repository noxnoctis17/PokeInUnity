using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HPBar : MonoBehaviour
{
    [SerializeField] private Slider _hpSlider;
    public Slider HPSlider => _hpSlider;
    public bool IsUpdating { get; private set; }

    public void SetMaxHP( int hp ){
        _hpSlider.maxValue = hp;
    }

    public void SetHP( int hp, int maxHP ){
        SetMaxHP( maxHP );
        _hpSlider.value = hp;
    }

    public IEnumerator AnimateHP( int newHP ){
        IsUpdating = true;
        Debug.Log( $"HPBar is updating hp to {newHP}" );

        float currentHP = _hpSlider.value;
        bool isDamaging = currentHP - newHP > 0;
        float changeAmount = currentHP - newHP;

        while( isDamaging ? ( currentHP > newHP ) : ( currentHP < newHP ) ){
            _hpSlider.value = currentHP -= changeAmount * Time.deltaTime * 2;

            yield return null;
        }

        Debug.Log( $"HPBar is finished updating, new hp is: {newHP}" );
        _hpSlider.value = newHP;
        Debug.Log( $"HPBar is finished updating, new hp slider value is: {_hpSlider.value}" );
        yield return new WaitForEndOfFrame();
        IsUpdating = false;
    }
}
