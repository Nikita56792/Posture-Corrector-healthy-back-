using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using SAWD.AI;

public class ChatUIManager : MonoBehaviour
{
    public TMP_InputField inputField;
    public Button sendButton;
    public Transform contentContainer; // Контейнер ScrollView (Content)
    public GameObject userMessagePrefab;
    public GameObject assistantMessagePrefab;

    private void Start()
    {
        sendButton.onClick.AddListener(OnSendClicked);
    }

    public void OnSendClicked()
    {
        string userText = inputField.text.Trim();
        if (string.IsNullOrEmpty(userText)) return;

        AddMessage(userText, true); // user message
        inputField.text = "";

        FindObjectOfType<AISender>().submitText(userText); // Запуск отправки
    }

    public void AddMessage(string text, bool isUser)
    {
        GameObject prefab = isUser ? userMessagePrefab : assistantMessagePrefab;
        GameObject messageGO = Instantiate(prefab, contentContainer);
        messageGO.transform.SetAsLastSibling();

        var msgText = messageGO.GetComponentInChildren<TMP_Text>();
        msgText.text = text;
    }
}
