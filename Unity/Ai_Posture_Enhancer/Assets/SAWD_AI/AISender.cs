using LikhodedDynamics.Sber.GigaChatSDK;
using LikhodedDynamics.Sber.GigaChatSDK.Models;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace SAWD.AI
{
    public class AISender : MonoBehaviour
    {
        public TMP_InputField text;         // Поле ввода
        public TMP_Text responseField;      // Поле для вывода ответа
        public string prompt = "";          // Системный промпт
        public string chatCreds = "";       // Ключ для авторизации
        public static GigaChat Chat;

        public void Start()
        {
            Chat = new GigaChat(chatCreds, false, true, true);
        }

        public void clickButton()
        {
            submitText(text.text);
        }

        async public void submitText(string userInput)
        {
            await Chat.CreateTokenAsync();

            var contents = new List<MessageContent>();
            string hack = "```json{\"methods_calls\":[{";

            contents.Add(new MessageContent("system", prompt));
            contents.Add(new MessageContent("user", userInput));
            contents.Add(new MessageContent("assistant", hack));

            var mq = new MessageQuery(contents, "GigaChat-Pro", 1f, 0.87f, 1, false, 1024);
            var res = await Chat.CompletionsAsync(mq);

            string assistantResponse = res.choices[res.choices.Count - 1].message.content;

            // Удаляем всё, что между ```json ... ```
            string[] splitted = assistantResponse.Split("```json");
            string cleaned = splitted.Length > 1 ? splitted[1] : splitted[0];
            cleaned = cleaned.Split("```")[0];

            Debug.Log("Ответ GigaChat:\n" + cleaned);

            if (responseField != null)
                responseField.text = cleaned;

            // В конце submitText:
            FindObjectOfType<ChatUIManager>().AddMessage(assistantResponse, false);

        }
    }
}
