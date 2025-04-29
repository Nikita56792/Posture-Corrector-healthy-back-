using UnityEngine;
using TMPro;
using System.Collections;

[RequireComponent(typeof(TMP_Dropdown))]
public class TTSVoiceSelector : MonoBehaviour
{
    TMP_Dropdown dd;

    IEnumerator Start()
    {
        dd = GetComponent<TMP_Dropdown>();
        dd.ClearOptions();
        dd.interactable = false;
        dd.options.Add(new TMP_Dropdown.OptionData("Загрузка..."));

        // Для редактора Windows
#if UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
        var voices = new System.Collections.Generic.List<string>();
        var synth = new System.Speech.Synthesis.SpeechSynthesizer();
        foreach (var v in synth.GetInstalledVoices())
            voices.Add(v.VoiceInfo.Name);

        UpdateDropdown(voices);
        yield break;
#endif

        // Для Android
#if UNITY_ANDROID && !UNITY_EDITOR
        yield return new WaitUntil(() => TTSManager.IsAndroidInitialized);
        var androidVoices = TTSManager.GetAndroidVoiceNames();
        UpdateDropdown(androidVoices);
#endif
    }

    void UpdateDropdown(System.Collections.Generic.List<string> voices)
    {
        dd.ClearOptions();

        if (voices.Count == 0)
        {
            dd.options.Add(new TMP_Dropdown.OptionData("Нет доступных голосов"));
            dd.interactable = false;
            return;
        }

        dd.AddOptions(voices);
        dd.interactable = true;
        dd.onValueChanged.AddListener(i =>
            TTSManager.SelectVoice(voices[i]));
    }
}