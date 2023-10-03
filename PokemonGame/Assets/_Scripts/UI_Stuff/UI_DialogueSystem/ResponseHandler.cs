using System.Collections.Generic;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResponseHandler : MonoBehaviour
{
    [SerializeField] private RectTransform _responseBox;
    [SerializeField] private RectTransform _responseButtonTemplate;
    [SerializeField] private RectTransform _responseContainer;
    private DialogueUI _dialogueUI;
    private ResponseEvent[] _responseEvents;
    private List<GameObject> _temporaryResponseButtons = new List<GameObject>();
    private Button _initialButton;
    // public static event Action<DialogueSO> OnResponseChosen;

    private void Start( ){
        _dialogueUI = GetComponent<DialogueUI>();
    }

    public void AddResponseEvents( ResponseEvent[] responseEvents ){
        _responseEvents = responseEvents;
    }

    public void ShowResponses(Response[] responses){
        float responseBoxHeight = 0;
        
        for( int i = 0; i < responses.Length; i++ ){

            Response response = responses[i];
            int responseIndex = i;

            GameObject responseButton = Instantiate( _responseButtonTemplate.gameObject, _responseContainer );
            responseButton.gameObject.SetActive( true );
            responseButton.GetComponentInChildren<TMP_Text>().text = response.ResponseText;
            responseButton.GetComponent<Button>().onClick.AddListener( () => OnPickedResponse( response, responseIndex ) );
            
            _temporaryResponseButtons.Add( responseButton );
            
            responseBoxHeight += _responseButtonTemplate.sizeDelta.y;
        }
        
        _responseBox.sizeDelta = new Vector2( _responseBox.sizeDelta.x, responseBoxHeight );
        _responseBox.gameObject.SetActive( true );
        _initialButton = _temporaryResponseButtons[0]?.GetComponent<Button>();
        StartCoroutine( SetInitialButton() );
    }

    private void OnPickedResponse( Response response, int responseIndex ){
        _responseBox.gameObject.SetActive( false );
        
        foreach( GameObject button in _temporaryResponseButtons ){
            Destroy( button );
        }
        
        _temporaryResponseButtons.Clear();

        Debug.Log( _responseEvents );
        if( _responseEvents != null && responseIndex <= _responseEvents.Length ){
            Debug.Log( _responseEvents );
            //--Set the dialogue finished callback
            DialogueManager.Instance.SetDialogueFinishedCallback( () => {
                //--Invoke Unit Event
                Debug.Log( _responseEvents );
                Debug.Log( _responseEvents[ responseIndex ] );
                _responseEvents[ responseIndex ].OnPickedResponse?.Invoke();
                _responseEvents = null;
                
            } ); //--Lambdas inide of the overload are funky lookin
            
        }

        // _responseEvents = null; //--Putting this inside of the callback to see if that fixes or breaks everything more

        if( response.DialogueSO ){
            DialogueManager.Instance.OnResponseChosen?.Invoke( response.DialogueSO );
        }
        else{
            _dialogueUI.CloseDialogueBox();
        }

        //--I know this method "works"

        // if( _responseEvents != null && responseIndex <= _responseEvents.Length ){
        //     _responseEvents[ responseIndex ].OnPickedResponse?.Invoke();
        // }

        // _responseEvents = null;
        
    }

    private IEnumerator SetInitialButton(){
        yield return new WaitForSeconds(0.15f);
        _initialButton.Select();
    }
}
