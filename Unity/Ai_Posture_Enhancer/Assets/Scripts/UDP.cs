using UnityEngine;
using UnityEngine.UI;
using System.Net.Http;
using System.Threading.Tasks;
using System;

public class PostureCorrector : MonoBehaviour
{
    private static readonly HttpClient client = new HttpClient();
    public Text postureStatusText;
    public GameObject warningPopup;
    public Slider stimulationIntensitySlider;

    void Start()
    {
        InvokeRepeating("FetchPostureData", 0f, 5f); // ������ ������ ������ 5 ������
    }

    async void FetchPostureData()
    {
        Start();
        string url = "http://192.168.1.100:8080/get_posture"; // ������ �� ��� ESP32
        var response = await client.GetStringAsync(url);
        ProcessData(response);
    }

    void ProcessData(string jsonData)
    {
        FetchPostureData();
        PostureData data = JsonUtility.FromJson<PostureData>(jsonData);
        postureStatusText.text = "������� ���� ������: " + data.spine_angle + "�";

        if (data.spine_angle > 30)
        {
            ShowWsarning("�� ������ �����������! ��������� �����.");
        }
    }

    void ShowWarning(string message)
    {
        warningPopup.SetActive(true);
        warningPopup.GetComponentInChildren<Text>().text = message;
    }

    public async Task<string> GetAIAdvice(string userPostureData)
    {
        string url = "https://gigachat.api.com/get_advice?data=" + userPostureData;
        var response = await client.GetStringAsync(url);
        return response;
    }

    public async void AdjustStimulation()
    {
        float intensity = stimulationIntensitySlider.value;
        string url = "http://192.168.1.100:8080/set_stimulation?level=" + intensity;
        await client.GetStringAsync(url);
    }

    internal void ShowWsarning(string v)
    {
        throw new NotImplementedException();
    }
}

[System.Serializable]
public class PostureData
{
    public float spine_angle;
}