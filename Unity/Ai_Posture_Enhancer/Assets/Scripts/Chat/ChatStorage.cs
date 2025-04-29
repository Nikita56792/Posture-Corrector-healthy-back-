using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class ChatStorage
{
    private static readonly string SavePath = Path.Combine(Application.persistentDataPath, "chat_history.json");

    public static void SaveChat(List<Message> messages)
    {
        string json = JsonUtility.ToJson(new MessageListWrapper { messages = messages });
        File.WriteAllText(SavePath, json);
        Debug.Log("Chat saved to: " + SavePath);
    }

    public static List<Message> LoadChat()
    {
        if (!File.Exists(SavePath))
            return new List<Message>();

        string json = File.ReadAllText(SavePath);
        var wrapper = JsonUtility.FromJson<MessageListWrapper>(json);
        return wrapper?.messages ?? new List<Message>();
    }

    [System.Serializable]
    private class MessageListWrapper
    {
        public List<Message> messages;
    }

    public static void ClearHistory()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
    }

}
