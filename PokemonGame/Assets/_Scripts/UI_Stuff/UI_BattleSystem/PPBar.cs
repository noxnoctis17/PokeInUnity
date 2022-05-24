using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class PPBar : MonoBehaviour
{
    [SerializeField] private Slider _ppBar;
    public Slider ppBar => _ppBar;
    private int _newPP;

    public void SetMaxPP(PokemonSO pokeSO, int pp)
    {
        _ppBar.maxValue = pp;
    }

    public void SetPP(PokemonSO pokeSO, int pp, int mpp)
    {
        SetMaxPP(pokeSO, mpp);
        _ppBar.value = pp;
    }

    public void UpdateCurrentPP(PokemonSO pokeSO, int pp)
    {
        _newPP =  pp;
    }

    public IEnumerator AnimatePP(PokemonSO pokeSO, int pp)
    {
        UpdateCurrentPP(pokeSO, pp);

        float previousPP = _ppBar.value;
        float changeAmount = previousPP - _newPP;

        while(previousPP > _newPP)
        {
            _ppBar.value = previousPP -= changeAmount * Time.deltaTime * 2.5f;
            yield return null;
        }

        _ppBar.value = _newPP;

    }
}
