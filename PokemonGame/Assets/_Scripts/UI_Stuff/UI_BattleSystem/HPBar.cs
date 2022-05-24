using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class HPBar : MonoBehaviour
{
    PokemonClass _pokemon;
    [SerializeField] private Slider _hpBar;
    public Slider hpBar => _hpBar;
    private int _newHP;

    public void SetMaxHP(PokemonSO pokeSO, int hp)
    {
        _hpBar.maxValue = hp;
    }

    public void SetHP(PokemonSO pokeSO, int hp, int mHP)
    {
        SetMaxHP(pokeSO, mHP);
        _hpBar.value = hp;
    }

    public void UpdateCurrentHP(PokemonSO pokeSO, int hp)
    {
        _newHP =  hp;
    }

    public IEnumerator AnimateHP(PokemonSO pokeSO, int hp)
    {
        UpdateCurrentHP(pokeSO, hp);

        float previousHP = _hpBar.value;
        float changeAmount = previousHP - _newHP;

        while(previousHP > _newHP)
        {
            _hpBar.value = previousHP -= changeAmount * Time.deltaTime * 1.5f;
            yield return null;
        }

        _hpBar.value = _newHP;

    }
}
