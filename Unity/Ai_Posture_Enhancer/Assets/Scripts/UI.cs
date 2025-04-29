using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UI : MonoBehaviour
{
    // Привязка кнопок из инспектора
    public Button Back;
    public GameObject DebugPanel;
    public GameObject ExercisesPanel;
    public GameObject ExercisesImage;
    public GameObject TextPlEx;
    public GameObject PanelMain;
    void Start()
    {
        if (Back != null)
            Back.onClick.AddListener(GoBack);
    }

    public void GoToSettings()
    {
        SceneManager.LoadScene("Settings");
    }

    public void GoToAiChat()
    {
        SceneManager.LoadScene("AiChat");
    }

    public void GoToTraining()
    {
        SceneManager.LoadScene("Training");
    }

    public void GoBack()
    {
        SceneManager.LoadScene("Main");
    }

    public void OpenDebug()
    {
        DebugPanel.SetActive(!DebugPanel.activeSelf);
    }
    public void GoMain()
    {
        PanelMain.SetActive(!PanelMain.activeSelf);
    }

    public void ExercisesPanelAct()
    {
        ExercisesPanel.SetActive(!ExercisesPanel.activeSelf);
        ExercisesImage.SetActive(!ExercisesImage.activeSelf);
        TextPlEx.SetActive(!TextPlEx.activeSelf);
    }


}
