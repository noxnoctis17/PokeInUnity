using UnityEngine;

// [CreateAssetMenu( menuName = "Dialogue/DialogueItem")]

[System.Serializable] 
public class DialogueItem
{
    [SerializeField] private string _speakerName;
    public string SpeakerName => _speakerName;
    [SerializeField] private DialogueSpeaker _dialogueSpeaker;
    public DialogueSpeaker DialogueSpeaker => _dialogueSpeaker;

    [SerializeField] private Sprite _leftPortrait_1, _leftPortrait_2, _rightPortrait_1, _rightPortrait_2;
    public Sprite LeftPortrait_1 => _leftPortrait_1;
    public Sprite LeftPortrait_2 => _leftPortrait_2;
    public Sprite RightPortrait_1 => _rightPortrait_1;
    public Sprite RightPortrait_2 => _rightPortrait_2;

    [SerializeField] [TextArea(5, 5)] private string _dialogue;
    public string Dialogue => _dialogue;
}
