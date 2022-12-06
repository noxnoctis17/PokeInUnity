using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResponseHandler : MonoBehaviour
{
    [SerializeField] private RectTransform _responseBox;
    [SerializeField] private RectTransform _responseButtonTemplate;
    [SerializeField] private RectTransform _responseContainer;
    private DialogueUI _dialogueUI;
    private List<GameObject> _temporaryResponseButtons = new List<GameObject>();

    private void Start( ){
        _dialogueUI = GetComponent<DialogueUI>();
    }

    public void ShowResponses(Response[] responses){
        float responseBoxHeight = 0;
        
        foreach( Response response in responses ){
            GameObject responseButton = Instantiate( _responseButtonTemplate.gameObject, _responseContainer );
            responseButton.gameObject.SetActive( true );
            responseButton.GetComponent<TMP_Text>().text = response.ResponseText;
            responseButton.GetComponent<Button>().onClick.AddListener( () => OnPickedResponse( response ) );
            
            _temporaryResponseButtons.Add( responseButton );
            
            responseBoxHeight += _responseButtonTemplate.sizeDelta.y;
        }
        
        _responseBox.sizeDelta = new Vector2( _responseBox.sizeDelta.x, responseBoxHeight );
        _responseBox.gameObject.SetActive( true );
    }

    private void OnPickedResponse( Response response ){
        _responseBox.gameObject.SetActive( false );
        
        foreach( GameObject button in _temporaryResponseButtons ){
            Destroy( button );
        }
        _temporaryResponseButtons.Clear();
        _dialogueUI.ShowDialogue( response.DialogueSO );
    }
}
