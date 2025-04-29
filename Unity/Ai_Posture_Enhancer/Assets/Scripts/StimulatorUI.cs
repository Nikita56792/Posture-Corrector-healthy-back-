using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public class StimulatorUI : MonoBehaviour
{
    [Header("UI Elements")]
    public Slider sliderPower;
    public Text textPowerValue;

    public Slider sliderMode;
    public Text textModeValue;

    public Button btnConnect;
    public Button btnApply;
    public Button btnTogglePanel;
    public Button btnDebug;

    public GameObject stimPanel;
    public GameObject DebugPanel;
    public Text textStatus;

    // UDP
    private UdpClient udp;
    private IPEndPoint deviceEP;

    private const int UDP_PORT = 1234;
    private const string DISCOVER = "DISCOVER";
    private const float TIMEOUT_SEC = 3f;
    private const float CHECK_INTERVAL = 5f;

    private bool isConnected = false;
    private string lastConnectedIPKey = "LastConnectedIP";

    void Awake()
    {
        btnTogglePanel.onClick.AddListener(OnTogglePanelClicked);
        btnDebug.onClick.AddListener(OnDebugClicked);
        btnConnect.onClick.AddListener(OnConnectClickedWrapper);
        btnApply.onClick.AddListener(OnApplyClicked);

        stimPanel.SetActive(false);
        sliderPower.minValue = 0;
        sliderPower.maxValue = 19;
        sliderPower.wholeNumbers = true;
        sliderPower.value = 0;

        sliderMode.minValue = 1;
        sliderMode.maxValue = 8;
        sliderMode.wholeNumbers = true;
        sliderMode.value = 1;

        sliderPower.onValueChanged.AddListener(v => UpdatePowerText((int)v));
        sliderMode.onValueChanged.AddListener(v => UpdateModeText((int)v));

        UpdatePowerText((int)sliderPower.value);
        UpdateModeText((int)sliderMode.value);

        SetUIConnectedState(false);

        TryAutoReconnect();
    }

    void SetUIConnectedState(bool connected)
    {
        isConnected = connected;
        sliderPower.interactable = connected;
        sliderMode.interactable = connected;
        btnApply.interactable = connected;
        btnConnect.interactable = !connected;

        if (!connected)
            textStatus.text = "Не подключено";
    }

    void UpdatePowerText(int p)
    {
        textPowerValue.text = $"Мощность: {p}";
    }

    void UpdateModeText(int m)
    {
        textModeValue.text = $"Режим: {m}";
    }

    void OnTogglePanelClicked()
    {
        stimPanel.SetActive(!stimPanel.activeSelf);
    }

    async void OnConnectClickedWrapper()
    {
        await OnConnectClicked();
    }

    public async Task OnConnectClicked()
    {
        textStatus.text = "Поиск устройства...";
        SetUIConnectedState(false);

        udp?.Close();
        udp = new UdpClient();
        udp.EnableBroadcast = true;

        byte[] msg = Encoding.ASCII.GetBytes(DISCOVER);

        // Попробовать последний IP
        string lastIP = PlayerPrefs.GetString(lastConnectedIPKey, "");
        if (!string.IsNullOrEmpty(lastIP))
        {
            try
            {
                await udp.SendAsync(msg, msg.Length, new IPEndPoint(IPAddress.Parse(lastIP), UDP_PORT));
                var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(TIMEOUT_SEC);
                while (DateTime.UtcNow < timeout)
                {
                    if (udp.Available > 0)
                    {
                        var result = await udp.ReceiveAsync();
                        deviceEP = result.RemoteEndPoint;
                        if (deviceEP.Address.ToString() == lastIP)
                        {
                            OnDeviceConnected();
                            return;
                        }
                    }
                    await Task.Delay(100);
                }
            }
            catch { }
        }

        // Автопоиск
        List<string> prefixes = GetTargetPrefixes();
        var tasks = new List<Task>();

        foreach (string prefix in prefixes)
        {
            for (int i = 1; i < 255; i++)
            {
                string ip = prefix + i;
                tasks.Add(udp.SendAsync(msg, msg.Length, new IPEndPoint(IPAddress.Parse(ip), UDP_PORT)));
            }
        }

        await Task.WhenAll(tasks);

        var timeoutAll = DateTime.UtcNow + TimeSpan.FromSeconds(TIMEOUT_SEC);
        bool found = false;

        while (DateTime.UtcNow < timeoutAll && !found)
        {
            if (udp.Available > 0)
            {
                try
                {
                    var result = await udp.ReceiveAsync();
                    deviceEP = result.RemoteEndPoint;
                    found = true;
                }
                catch { }
            }
            await Task.Delay(100);
        }

        if (found)
        {
            PlayerPrefs.SetString(lastConnectedIPKey, deviceEP.Address.ToString());
            PlayerPrefs.Save();
            OnDeviceConnected();
        }
        else
        {
            textStatus.text = "Устройство не найдено";
            btnConnect.interactable = true;
            udp.Close();
        }
    }

    void OnDeviceConnected()
    {
        textStatus.text = $"Подключено: {deviceEP.Address}";
        SetUIConnectedState(true);
        InvokeRepeating(nameof(CheckConnection), CHECK_INTERVAL, CHECK_INTERVAL);
    }

    async void OnApplyClicked()
    {
        if (deviceEP == null || udp == null) return;

        int pw = (int)sliderPower.value;
        int md = (int)sliderMode.value;
        string setMsg = $"SET:{pw},{md}";
        byte[] data = Encoding.ASCII.GetBytes(setMsg);

        try
        {
            await udp.SendAsync(data, data.Length, deviceEP);
            textStatus.text = $"Отправлено: {setMsg}";
        }
        catch (Exception e)
        {
            textStatus.text = $"Ошибка: {e.Message}";
        }
    }

    List<string> GetTargetPrefixes()
    {
        List<string> prefixes = new List<string>();
        string localIP = GetLocalIPAddress();

        if (!string.IsNullOrEmpty(localIP))
        {
            var parts = localIP.Split('.');
            if (parts.Length == 4)
            {
                prefixes.Add($"{parts[0]}.{parts[1]}.{parts[2]}.");
            }
        }

        prefixes.Add("192.168.43.");
        prefixes.Add("192.168.254.");
        prefixes.Add("192.168.1.");
        prefixes.Add("192.168.0.");
        prefixes.Add("10.0.0.");
        prefixes.Add("172.20.10.");

        return new List<string>(new HashSet<string>(prefixes));
    }

    string GetLocalIPAddress()
    {
        try
        {
            using (var socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
            {
                socket.Connect("8.8.8.8", 53);
                IPEndPoint endPoint = socket.LocalEndPoint as IPEndPoint;
                return endPoint.Address.ToString();
            }
        }
        catch
        {
            return null;
        }
    }

    async void CheckConnection()
    {
        if (deviceEP == null || udp == null || !isConnected) return;

        try
        {
            byte[] msg = Encoding.ASCII.GetBytes("PING");
            await udp.SendAsync(msg, msg.Length, deviceEP);

            var timeout = DateTime.UtcNow + TimeSpan.FromSeconds(1.5f);
            while (DateTime.UtcNow < timeout)
            {
                if (udp.Available > 0)
                {
                    var result = await udp.ReceiveAsync();
                    string response = Encoding.ASCII.GetString(result.Buffer);
                    if (response.Contains("PONG")) return;
                }
                await Task.Delay(100);
            }

            Disconnect();
        }
        catch
        {
            Disconnect();
        }
    }

    void TryAutoReconnect()
    {
        string lastIP = PlayerPrefs.GetString(lastConnectedIPKey, "");
        if (!string.IsNullOrEmpty(lastIP))
        {
            _ = OnConnectClicked(); // fire-and-forget
        }
    }

    void Disconnect()
    {
        CancelInvoke(nameof(CheckConnection));
        deviceEP = null;
        udp?.Close();
        udp = null;
        SetUIConnectedState(false);
        textStatus.text = "Соединение потеряно";
    }

    void OnDebugClicked()
    {
        if (stimPanel.activeSelf)
        {
            Disconnect();
            stimPanel.SetActive(false);
            textStatus.text = "Отключено (DEBUG)";
            DebugPanel.SetActive(false);
        }
    }

    void OnDestroy()
    {
        Disconnect();
    }
}