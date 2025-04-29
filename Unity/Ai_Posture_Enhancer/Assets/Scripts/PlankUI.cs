/**************************************************************************
 *  PlankUI
 *  ───────────────────────────────────────────────────────────────────────
 *  • Кнопка Connect   – поиск / подключение к корректору-планке
 *  • Кнопка Calibrate – отправка пакета «567»  (калибровка)
 *  • Кнопка OpenPage  – открывает веб-страницу устройства в браузере
 *  • IP хранится в PlayerPrefs("LastConnectedPlankIP")
 *  • Доступность проверяется периодическим «242» → «123985»
 **************************************************************************/
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class PlankUI : MonoBehaviour
{
    [Header("UI")]
    public Button btnConnect;
    public Button btnCalibrate;
    public Button btnOpenPage;
    public Text txtStatus;
    public GameObject panelRoot;      // можно скрывать/показывать

    /* ----------------- константы ----------------- */
    const int PORT = 1234;
    const string DISCOVER_MSG = "242";     // планка слушает именно это
    const string EXPECTED_REPLY = "123985";  // vendorId
    const string CALIBRATE_MSG = "567";
    const string PREF_KEY_IP = "LastConnectedPlankIP";
    const float SCAN_TIMEOUT = 3f;
    const float HB_INTERVAL = 5f;

    /* ----------------- внутреннее ---------------- */
    UdpClient udp;
    IPEndPoint plankEP;
    bool isConnected;

    void Awake()
    {
        btnConnect.onClick.AddListener(() => _ = ConnectAsync());
        btnCalibrate.onClick.AddListener(() => _ = SendCalibrate());
        btnOpenPage.onClick.AddListener(OpenWebPage);

        SetUI(false);
        TryAutoReconnect();
    }

    /* =================== Подключение =================== */
    async Task ConnectAsync()
    {
        txtStatus.text = "Поиск планки…";
        SetUI(false);

        udp?.Close();
        udp = new UdpClient() { EnableBroadcast = true };
        byte[] probe = Encoding.ASCII.GetBytes(DISCOVER_MSG);

        string savedIP = PlayerPrefs.GetString(PREF_KEY_IP, "");
        if (await ProbeIP(savedIP, probe)) { FinishConnect(savedIP); return; }

        // broadcast по всем подсетям
        foreach (string prefix in BuildPrefixes())
            for (int i = 1; i < 255; i++)
                _ = udp.SendAsync(probe, probe.Length, $"{prefix}{i}", PORT);

        float end = Time.time + SCAN_TIMEOUT;
        while (Time.time < end)
        {
            if (udp.Available > 0)
            {
                var res = await udp.ReceiveAsync();
                if (Encoding.ASCII.GetString(res.Buffer).Trim() == EXPECTED_REPLY)
                { FinishConnect(res.RemoteEndPoint.Address.ToString()); return; }
            }
            await Task.Delay(50);
        }

        txtStatus.text = "Планка не найдена";
    }

    async Task<bool> ProbeIP(string ip, byte[] probe)
    {
        if (string.IsNullOrEmpty(ip)) return false;
        try
        {
            await udp.SendAsync(probe, probe.Length, ip, PORT);
            var recv = udp.ReceiveAsync();
            if (await Task.WhenAny(recv, Task.Delay(600)) != recv) return false;
            return Encoding.ASCII.GetString(recv.Result.Buffer).Trim() == EXPECTED_REPLY;
        }
        catch { return false; }
    }

    void FinishConnect(string ip)
    {
        plankEP = new IPEndPoint(IPAddress.Parse(ip), PORT);
        PlayerPrefs.SetString(PREF_KEY_IP, ip);
        PlayerPrefs.Save();

        txtStatus.text = "Подключено: " + ip;
        SetUI(true);

        isConnected = true;
        InvokeRepeating(nameof(Heartbeat), HB_INTERVAL, HB_INTERVAL);
    }

    /* =================== Калибровка ==================== */
    async Task SendCalibrate()
    {
        if (!isConnected) return;
        try
        {
            byte[] data = Encoding.ASCII.GetBytes(CALIBRATE_MSG);
            await udp.SendAsync(data, data.Length, plankEP);
            txtStatus.text = "Калибровка отправлена";
        }
        catch (Exception e) { txtStatus.text = "Ошибка: " + e.Message; }
    }

    /* ================== Heartbeat ====================== */
    async void Heartbeat()
    {
        if (!isConnected) return;
        try
        {
            byte[] probe = Encoding.ASCII.GetBytes(DISCOVER_MSG);
            await udp.SendAsync(probe, probe.Length, plankEP);

            var recv = udp.ReceiveAsync();
            if (await Task.WhenAny(recv, Task.Delay(800)) != recv ||
                Encoding.ASCII.GetString(recv.Result.Buffer).Trim() != EXPECTED_REPLY)
            {
                Disconnect("Связь потеряна");
            }
        }
        catch { Disconnect("Связь потеряна"); }
    }

    /* =================== Вспомогательные ================= */
    void OpenWebPage()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        string url = "http://" + plankEP.Address;
        using (var jc = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
        using (var act = jc.GetStatic<AndroidJavaObject>("currentActivity"))
        {
            var intent = new AndroidJavaObject("android.content.Intent",
                                               "android.intent.action.VIEW",
                                               new AndroidJavaObject("android.net.Uri", url));
            act.Call("startActivity", intent);
        }
#else
        Application.OpenURL("http://" + plankEP.Address);
#endif
    }

    void SetUI(bool connected)
    {
        btnCalibrate.interactable = btnOpenPage.interactable = connected;
        btnConnect.interactable = !connected;
    }

    void Disconnect(string msg)
    {
        CancelInvoke(nameof(Heartbeat));
        isConnected = false;
        SetUI(false);
        txtStatus.text = msg;
    }

    void OnDestroy() { udp?.Close(); }

    /* ---------- утилиты сети ---------- */
    IEnumerable<string> BuildPrefixes()
    {
        string local = GetLocalIP();
        if (!string.IsNullOrEmpty(local))
        {
            var p = local.Split('.');
            if (p.Length == 4) yield return $"{p[0]}.{p[1]}.{p[2]}.";
        }
        yield return "192.168.43."; yield return "192.168.1.";
        yield return "192.168.0."; yield return "192.168.254.";
        yield return "10.0.0."; yield return "172.20.10.";
    }

    string GetLocalIP()
    {
        try
        {
            using var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            s.Connect("8.8.8.8", 53);
            return (s.LocalEndPoint as IPEndPoint)?.Address.ToString();
        }
        catch { return null; }
    }

    void TryAutoReconnect()
    {
        string last = PlayerPrefs.GetString(PREF_KEY_IP, "");
        if (!string.IsNullOrEmpty(last)) _ = ConnectAsync();
    }
}
