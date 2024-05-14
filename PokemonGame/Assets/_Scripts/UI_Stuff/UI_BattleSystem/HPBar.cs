using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HPBar : MonoBehaviour
{
    [SerializeField] private Slider _hpBar;
    public Slider hpBar => _hpBar;
    private int _newHP;
    public bool IsUpdating { get; private set; }

    public void SetMaxHP( int hp ){
        _hpBar.maxValue = hp;
    }

    public void SetHP( int hp, int mHP ){
        SetMaxHP( mHP );
        _hpBar.value = hp;
    }

    public void UpdateCurrentHP( int hp )
    {
        _newHP = hp;
    }

    public IEnumerator AnimateHP( int hp ){
        IsUpdating = true;
        UpdateCurrentHP( hp );

        float previousHP = _hpBar.value;
        float changeAmount = previousHP - _newHP;

        while( previousHP > _newHP ){
            _hpBar.value = previousHP -= changeAmount * Time.deltaTime * 1.5f;
            yield return null;
        }

        _hpBar.value = _newHP;
        IsUpdating = false;
    }
}
