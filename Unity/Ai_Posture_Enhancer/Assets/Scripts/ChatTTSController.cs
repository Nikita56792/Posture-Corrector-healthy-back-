using UnityEngine;
using UnityEngine.UI;
using TMPro;

[RequireComponent(typeof(Button))]
public class ChatTTSController : MonoBehaviour
{
    [SerializeField] Color activeColor = Color.white;
    [SerializeField] Color inactiveColor = new Color(.6f, .6f, .6f);

    TMP_Text label;
    Button btn;

    void Awake()
    {
        btn = GetComponent<Button>();
        label = GetComponentInChildren<TMP_Text>();
        btn.onClick.AddListener(OnClick);
    }

    void Update() => RefreshVisuals();

    void OnClick()
    {
        if (TTSManager.IsSpeaking())
        {
            TTSManager.Stop();
        }
        else
        {
            TTSManager.Enabled = !TTSManager.Enabled;
        }
    }

    void RefreshVisuals()
    {
        bool isSpeaking = TTSManager.IsSpeaking();
        bool isEnabled = TTSManager.Enabled;

        label.text = isSpeaking ? "Стоп"
            : (isEnabled ? "TTS ON" : "TTS OFF");

        label.color = isEnabled ? activeColor : inactiveColor;
    }
}