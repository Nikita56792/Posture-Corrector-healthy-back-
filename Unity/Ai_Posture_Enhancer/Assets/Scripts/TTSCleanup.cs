using UnityEngine;
using UnityEngine.SceneManagement;

public class TTSCleanup : MonoBehaviour
{
    void OnEnable()
    {
        SceneManager.sceneUnloaded += OnSceneChange;
        Application.quitting += OnQuit;
    }

    void OnDisable()
    {
        SceneManager.sceneUnloaded -= OnSceneChange;
        Application.quitting -= OnQuit;
        ForceStop();
    }

    void OnDestroy() => ForceStop();

    static void OnSceneChange(Scene _) => ForceStop();
    static void OnQuit() => ForceStop();

    static void ForceStop()
    {
        TTSManager.Stop();
#if UNITY_ANDROID && !UNITY_EDITOR
        if (TTSManager.GetAndroidTTS() != null)
        {
            TTSManager.GetAndroidTTS().Call("shutdown");
            TTSManager.ResetAndroid();
        }
#endif
    }
}