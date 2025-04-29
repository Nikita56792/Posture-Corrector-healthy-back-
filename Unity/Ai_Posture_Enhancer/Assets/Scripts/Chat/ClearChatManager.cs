using UnityEngine;
using UnityEngine.UI;

public class ClearChatManager : MonoBehaviour
{
    public Button clearButton;

    private void Start()
    {
        clearButton.onClick.AddListener(ClearHistory);
    }

    public void ClearHistory()
    {
        ChatStorage.ClearHistory(); // Очистка файла

        // Удаление текущих сообщений в сцене, если она открыта
        MessageContainer container = FindObjectOfType<MessageContainer>();
        if (container != null)
        {
            container.ClearMessagesRuntime(); // вызов метода, см. ниже
        }

        Debug.Log("История чата очищена.");
    }
}
