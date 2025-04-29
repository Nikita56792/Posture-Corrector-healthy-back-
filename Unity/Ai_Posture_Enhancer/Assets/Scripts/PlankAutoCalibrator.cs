/**************************************************************************
 * PlankAutoCalibrator
 *  � ���� IP ������ �� PlayerPrefs("LastConnectedPlankIP")
 *  � ��������� ����������� (DISCOVER -> 123985)
 *  � ���������� ����� "567" � ����������
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

    /* ---------- ��������� ����� ---------- */
    public async void Calibrate()
    {
        string ip = PlayerPrefs.GetString(PREF_KEY_IP, "");
        if (string.IsNullOrEmpty(ip))
        {
            Debug.LogWarning("PlankCalib: IP �� ������ � PlayerPrefs");
            return;
        }

        if (!await IsAvailable(ip))
        {
            Debug.LogWarning("PlankCalib: ������ ����������");
            return;
        }

        await SendPacket(ip, CALIBRATE);
        Debug.Log("PlankCalib: ������� ���������� ����������");
    }

    /* ---------- ����������� ---------- */
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

    /* ---------- �������� ----------- */
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
