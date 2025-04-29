using UnityEngine;
using System.Text.RegularExpressions;
using System.Collections.Generic; // Добавлено

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using System.Speech.Synthesis;
#endif

public static class TTSManager
{
    public static bool Enabled = true;

    // Android-специфика
#if UNITY_ANDROID && !UNITY_EDITOR
    private static AndroidJavaObject tts;
    private static bool androidReady;
    private static bool androidInitTried;
    private static readonly Queue<string> androidQueue = new Queue<string>();
    public static string PreferredVoiceName = "";

    // Windows-специфика
#elif UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    private static SpeechSynthesizer synth;
    private static bool winInitTried;
#endif

    static string Clean(string s) =>
        Regex.Replace(s, @"(\*\*|__|~~|`|[#/@\|\{\}\[\]\<\>])", "");

    public static void Speak(string text)
    {
        if (!Enabled || string.IsNullOrWhiteSpace(text)) return;
        text = Clean(text);

#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        WindowsSpeak(text);
#elif UNITY_ANDROID && !UNITY_EDITOR
        AndroidSpeak(text);
#endif
    }

    #region Windows Implementation
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
    static void WindowsSpeak(string text)
    {
        if (!winInitTried)
        {
            winInitTried = true;
            try { synth = new SpeechSynthesizer { Rate = 0 }; }
            catch (System.Exception e)
            {
                Debug.LogError("Win-TTS init: " + e);
                Enabled = false;
                return;
            }
        }
        synth?.SpeakAsyncCancelAll();
        synth?.SpeakAsync(text);
    }
#endif
    #endregion

    #region Android Implementation
#if UNITY_ANDROID && !UNITY_EDITOR
    static void AndroidSpeak(string text)
    {
        if (!androidInitTried) InitAndroidTTS();
        if (!androidReady) 
        { 
            androidQueue.Enqueue(text); 
            return; 
        }
        SpeakAndroid(text);
    }

    class TTSInitProxy : AndroidJavaProxy
    {
        readonly System.Action<int> cb;
        public TTSInitProxy(System.Action<int> cb) : 
            base("android.speech.tts.TextToSpeech$OnInitListener") => this.cb = cb;
        void onInit(int status) => cb(status);
    }

    static void InitAndroidTTS()
    {
        androidInitTried = true;
        var act = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                     .GetStatic<AndroidJavaObject>("currentActivity");
        tts = new AndroidJavaObject("android.speech.tts.TextToSpeech", 
                                    act, new TTSInitProxy(OnAndroidInit));
    }

    static void OnAndroidInit(int status)
    {
        androidReady = (status == 0);
        if (!androidReady) return;

        ApplyAndroidVoice();
        while (androidQueue.Count > 0) SpeakAndroid(androidQueue.Dequeue());
    }

    static void SpeakAndroid(string txt)
    {
        bool latin = Regex.IsMatch(txt, @"\p{IsBasicLatin}");
        var locale = new AndroidJavaObject("java.util.Locale",
                         latin ? "en" : "ru", latin ? "US" : "RU");
        tts.Call<int>("setLanguage", locale);
        tts.Call<int>("speak", txt, 0, null, "chat");
    }

    static void ApplyAndroidVoice()
    {
        if (string.IsNullOrEmpty(PreferredVoiceName)) return;
        var voices = tts.Call<AndroidJavaObject>("getVoices")
                       .Call<System.Collections.ICollection>("toArray");
        foreach (var v in voices)
        {
            string name = new AndroidJavaObject(v.ToString()).Call<string>("getName");
            if (name == PreferredVoiceName) { tts.Call("setVoice", v); break; }
        }
    }

    public static List<string> GetAndroidVoiceNames()
    {
        InitAndroidTTS();
        var list = new List<string>();
        var voices = tts.Call<AndroidJavaObject>("getVoices")
                       .Call<System.Collections.ICollection>("toArray");
        foreach (var v in voices)
            list.Add(new AndroidJavaObject(v.ToString()).Call<string>("getName"));
        return list;
    }

    public static AndroidJavaObject GetAndroidTTS() => tts;
    public static void ResetAndroid()
    {
        tts = null;
        androidReady = false;
    }
    public static bool IsAndroidInitialized => androidInitTried && androidReady;
#endif
    #endregion

    public static void Stop()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        synth?.SpeakAsyncCancelAll();
#elif UNITY_ANDROID && !UNITY_EDITOR
        if (tts != null) tts.Call("stop");
#endif
    }

    public static bool IsSpeaking()
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        return synth != null && synth.State == SynthesizerState.Speaking;
#elif UNITY_ANDROID && !UNITY_EDITOR
        return tts != null && tts.Call<bool>("isSpeaking");
#else
        return false;
#endif
    }

    public static void SelectVoice(string id)
    {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
        try { synth.SelectVoice(id); } catch { }
#elif UNITY_ANDROID && !UNITY_EDITOR
        PreferredVoiceName = id;
        if (androidReady) ApplyAndroidVoice();
#endif
    }
}