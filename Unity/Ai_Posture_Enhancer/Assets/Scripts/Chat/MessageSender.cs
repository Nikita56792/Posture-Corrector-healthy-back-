using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class MessageSender : MonoBehaviour
{
    public InputAction SendMessageAction;
    public TMP_InputField MessageField;
    public Chat Chat;
    public GigaChatBridge GigaChatBridge; // Новый компонент, см. ниже

    private void OnEnable() => SendMessageAction.Enable();
    private void OnDisable() => SendMessageAction.Disable();
    private void Awake() => SendMessageAction.performed += OnSendMessageAction;

    public void Send()
    {
        if (string.IsNullOrEmpty(MessageField.text)) return;

        string userMessage = MessageField.text;

        // 1. Отправка твоего сообщения
        var message = new Message(Chat.Owner, userMessage);
        Chat.ReceiveMessage(message);

        // 2. Отправка в GigaChat
        if (GigaChatBridge != null)
            GigaChatBridge.SendToGigaChat(userMessage);

        // 3. Очистка поля
        MessageField.text = string.Empty;
    }

    private void OnSendMessageAction(InputAction.CallbackContext ctx)
    {
        if (MessageField.isFocused) Send();
    }

    private void Reset() => Chat = FindObjectOfType<Chat>();
}
