using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

public class StimulatorAutoController : MonoBehaviour
{
    /* singleton */
    public static StimulatorAutoController Instance { get; private set; }
    void Awake()
    {
        if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
        else Destroy(gameObject);
    }

    /* settings */
    const int PORT = 1234;
    const int TIMEOUT_MS = 2000;
    const string PREF_KEY_IP = "LastConnectedIP";

    /* ------------------------------------------------------------ */
    public async Task<bool> IsAvailableAsync()
    {
        string ip = PlayerPrefs.GetString(PREF_KEY_IP, "");
        // string ip = "192.168.254.16";
        if (string.IsNullOrEmpty(ip)) return false;

        try
        {
            using var udp = new UdpClient();
            byte[] ping = Encoding.ASCII.GetBytes("PING");
            await udp.SendAsync(ping, ping.Length, ip, PORT);

            var recv = udp.ReceiveAsync();
            if (await Task.WhenAny(recv, Task.Delay(TIMEOUT_MS)) != recv) return false;

            return Encoding.ASCII.GetString(recv.Result.Buffer).Trim() == "PONG";
        }
        catch { return false; }
    }

    /* ------------------------------------------------------------ */
    public void Apply(int mode, int power)
    {
        mode = Mathf.Clamp(mode, 1, 8);
        power = Mathf.Clamp(power, 0, 19);
        _ = SendAsync(mode, power);
    }

    async Task SendAsync(int mode, int power)
    {
        string ip = PlayerPrefs.GetString(PREF_KEY_IP, "");
        if (string.IsNullOrEmpty(ip))
        {
            Debug.LogWarning("StimAutoCtrl: IP неизвестен");
            return;
        }

        string cmd = $"SET:{power},{mode}";
        byte[] buf = Encoding.ASCII.GetBytes(cmd);

        try
        {
            using var udp = new UdpClient();
            await udp.SendAsync(buf, buf.Length, ip, PORT);

            var recv = udp.ReceiveAsync();
            if (await Task.WhenAny(recv, Task.Delay(TIMEOUT_MS)) != recv)
            {
                Debug.LogWarning("StimAutoCtrl: таймаут ответа");
                return;
            }

            if (Encoding.ASCII.GetString(recv.Result.Buffer).Trim() == "OK")
            {
                PlayerPrefs.SetString(PREF_KEY_IP, ip);
                PlayerPrefs.Save();
                Debug.Log($"StimAutoCtrl OK  (mode={mode}, power={power})");
            }
            else
                Debug.LogWarning("StimAutoCtrl: устройство ответило не OK");
        }
        catch (Exception e)
        {
            Debug.LogError("StimAutoCtrl: " + e.Message);
        }
    }
}