using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class FPSManager : MonoBehaviour
{
    [SerializeField] private int questFrameRate = 72;
    [SerializeField] private int pcFrameRate = 90;

    private void Awake()
    {
        // Detectar plataforma y establecer FPS apropiados
        if (IsQuestDevice())
        {
            SetFrameRate(questFrameRate);
            Debug.Log($"Quest detectado: FPS limitado a {questFrameRate}");
        }
        else
        {
            SetFrameRate(pcFrameRate);
            Debug.Log($"PC VR detectado: FPS limitado a {pcFrameRate}");
        }
    }

    private bool IsQuestDevice()
    {
        // Método 1: Comprobación directa de plataforma
        bool isAndroid = Application.platform == RuntimePlatform.Android;

        // Método 2: Comprobación de características del dispositivo (más específico)
        bool isQuestModel = false;
        List<InputDevice> devices = new List<InputDevice>();
        InputDevices.GetDevices(devices);

        foreach (var device in devices)
        {
            if (device.name.Contains("Quest") || device.name.Contains("Oculus"))
            {
                isQuestModel = true;
                break;
            }
        }

        return isAndroid && isQuestModel;
    }

    private void SetFrameRate(int targetFrameRate)
    {
        Application.targetFrameRate = targetFrameRate;
        // También podemos ajustar la configuración de VSync
        QualitySettings.vSyncCount = 0; // Desactivar VSync para que Application.targetFrameRate funcione
    }
}
