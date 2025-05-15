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
        // M�todo 1: Comprobaci�n directa de plataforma
        bool isAndroid = Application.platform == RuntimePlatform.Android;

        // M�todo 2: Comprobaci�n de caracter�sticas del dispositivo (m�s espec�fico)
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
        // Tambi�n podemos ajustar la configuraci�n de VSync
        QualitySettings.vSyncCount = 0; // Desactivar VSync para que Application.targetFrameRate funcione
    }
}
