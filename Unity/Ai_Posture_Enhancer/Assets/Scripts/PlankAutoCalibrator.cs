/**************************************************************************
 * PlankAutoCalibrator
 *  Х берЄт IP планки из PlayerPrefs("LastConnectedPlankIP")
 *  Х провер€ет доступность (DISCOVER -> 123985)
 *  Х отправл€ет пакет "567" Ц калибровка
 **************************************************************************/
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class PlankAutoCalibrator : MonoBehaviour
{
    public static PlankAutoCalibrator Instance { get; private set; }
    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    const int PORT = 1234;
    const int TIMEOUT_MS = 1200;
    const string PREF_KEY_IP = "LastConnectedPlankIP";
    const string DISCOVER = "242";
    const string DISCOVER_REPLY = "123985";
    const string CALIBRATE = "567";

    /* ---------- публичный вызов ---------- */
    public async void Calibrate()
    {
        string ip = PlayerPrefs.GetString(PREF_KEY_IP, "");
        if (string.IsNullOrEmpty(ip))
        {
            Debug.LogWarning("PlankCalib: IP не найден в PlayerPrefs");
            return;
        }

        if (!await IsAvailable(ip))
        {
            Debug.LogWarning("PlankCalib: планка недоступна");
            return;
        }

        await SendPacket(ip, CALIBRATE);
        Debug.Log("PlankCalib: команда калибровки отправлена");
    }

    /* ---------- доступность ---------- */
    async Task<bool> IsAvailable(string ip)
    {
        try
        {
            using var udp = new UdpClient();
            byte[] buf = Encoding.ASCII.GetBytes(DISCOVER);
            await udp.SendAsync(buf, buf.Length, ip, PORT);

            var recv = udp.ReceiveAsync();
            if (await Task.WhenAny(recv, Task.Delay(TIMEOUT_MS)) != recv) return false;
            return Encoding.ASCII.GetString(recv.Result.Buffer).Trim() == DISCOVER_REPLY;
        }
        catch { return false; }
    }

    /* ---------- отправка ----------- */
    async Task SendPacket(string ip, string msg)
    {
        try
        {
            using var udp = new UdpClient();
            byte[] data = Encoding.ASCII.GetBytes(msg);
            await udp.SendAsync(data, data.Length, ip, PORT);
        }
        catch (Exception e) { Debug.LogError("PlankCalib: " + e.Message); }
    }
}
