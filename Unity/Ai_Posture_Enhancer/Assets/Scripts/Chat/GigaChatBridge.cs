using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using LikhodedDynamics.Sber.GigaChatSDK;
using LikhodedDynamics.Sber.GigaChatSDK.Models;

public class GigaChatBridge : MonoBehaviour
{
    [Header("Links / Settings")]
    public Chat Chat;
    [TextArea] public string chatCreds;
    [TextArea(15, 10)] public string systemPrompt;

    GigaChat client;

    async void Start()
    {
        client = new GigaChat(chatCreds, false, true, true);
        await client.CreateTokenAsync();
    }

    /* =================================================================== */
    public async void SendToGigaChat(string userText)
    {
        /* готов ли стимулятор */
        bool stimReady = await StimulatorAutoController.Instance.IsAvailableAsync();
        string flag = stimReady ? "##STIM_READY:YES##" : "##STIM_READY:NO##";

        /* история */
        var msgs = HistoryHelper.BuildHistory(ChatStorage.LoadChat());
        msgs.Insert(0, new MessageContent("system", systemPrompt + "\n" + flag));
        msgs.Add(new MessageContent("user", userText));

        var q = new MessageQuery(msgs, "GigaChat-Max", 1f, 0.87f, 1, false, 768);

        string raw;
        try
        {
            raw = (await client.CompletionsAsync(q))
                     .choices[0].message.content.Trim();
        }
        catch (System.Exception e) { raw = "[Ошибка: " + e.Message + "]"; }

        /* --- выделяем JSON (если есть) --- */
        string json;
        string tail;
        SplitJson(raw, out json, out tail);

        /* CALIBRATE → STIM */
        if (HandleCalibrate(json)) { ShowTail(tail); return; }
        if (HandleStim(json, stimReady)) { ShowTail(tail); return; }

        /* обычный текст */
        ShowTail(raw);
    }

    /* =================================================================== */
    void SplitJson(string src, out string json, out string tail)
    {
        json = tail = "";
        int open = src.IndexOf('{');
        if (open < 0) { tail = src; return; }

        int depth = 0, close = -1;
        for (int i = open; i < src.Length; i++)
        {
            if (src[i] == '{') depth++;
            if (src[i] == '}') depth--;
            if (depth == 0) { close = i; break; }
        }
        if (close < 0) { tail = src; return; }

        json = src.Substring(open, close - open + 1).Trim();
        tail = src.Substring(close + 1).Trim();
    }

    /* ---------------- CALIBRATE ---------------- */
    bool HandleCalibrate(string txt)
    {
        if (string.IsNullOrEmpty(txt)) return false;
        if (!Regex.IsMatch(txt, @"""command""\s*:\s*""CALIBRATE""",
                           RegexOptions.IgnoreCase)) return false;

        PlankAutoCalibrator.Instance.Calibrate();
        Debug.Log("GigaChat → CALIBRATE");
        return true;
    }

    /* ------------------ STIM ------------------- */
    bool HandleStim(string txt, bool ready)
    {
        if (string.IsNullOrEmpty(txt)) return false;
        if (!Regex.IsMatch(txt, @"""command""\s*:\s*""STIM""",
                           RegexOptions.IgnoreCase)) return false;

        int mode = ExtractInt(txt, "\"mode\"");
        int power = ExtractInt(txt, "\"power\"");

        if (!ready)
        {
            ShowTail("Стимулятор не подключён. Подключите устройство и попробуйте снова.");
            return true;
        }

        StimulatorAutoController.Instance.Apply(mode, power);
        Debug.Log($"GigaChat STIM → mode={mode} power={power}");
        return true;
    }

    /* --------------- helpers ------------------- */
    void ShowTail(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        var msg = new Message("GigaChat", text.Trim());
        Chat.ReceiveMessage(msg);

        var all = ChatStorage.LoadChat();
        all.Add(msg);
        ChatStorage.SaveChat(all);

        TTSManager.Speak(text);
    }

    int ExtractInt(string src, string key)
    {
        var m = Regex.Match(src, key + @"\s*:\s*(\d+)");
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }
}
