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
        ChatStorage.ClearHistory(); // ������� �����

        // �������� ������� ��������� � �����, ���� ��� �������
        MessageContainer container = FindObjectOfType<MessageContainer>();
        if (container != null)
        {
            container.ClearMessagesRuntime(); // ����� ������, ��. ����
        }

        Debug.Log("������� ���� �������.");
    }
}
