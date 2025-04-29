/*  HistoryHelper.cs  ─ формирует контекст истории для GigaChat  */
using System.Collections.Generic;
using LikhodedDynamics.Sber.GigaChatSDK.Models;

public static class HistoryHelper
{
    public static List<MessageContent> BuildHistory(
        List<Message> allMsgs, int maxTokens = 2000)
    {
        var list = new List<MessageContent>();
        int tok = 0;
        for (int i = allMsgs.Count - 1; i >= 0; i--)
        {
            string role = allMsgs[i].Sender == "GigaChat" ? "assistant" : "user";
            string txt = allMsgs[i].Content;
            tok += txt.Length / 4;                 // грубая оценка токенов
            if (tok > maxTokens) break;
            list.Insert(0, new MessageContent(role, txt));
        }
        return list;
    }
}
