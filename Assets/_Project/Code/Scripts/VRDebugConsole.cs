using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VRDebugConsole : MonoBehaviour
{
    [Header("UI Referencias")]
    [SerializeField] private GameObject consolePanel; // Panel principal
    [SerializeField] private TextMeshProUGUI logText; // Texto donde se muestran los logs
    [SerializeField] private int maxLogs = 50; // Máximo número de logs a mostrar

    [Header("Configuración")]
    [SerializeField] private KeyCode toggleKeyPC = KeyCode.F1; // Para testing en PC
    [SerializeField] private string toggleButton = "PrimaryButton"; // Botón A/X en Quest

    // Lista para almacenar los logs
    private Queue<string> logQueue = new Queue<string>();
    private bool showConsole = false;

    void OnEnable()
    {
        // Suscribirse al evento de log
        Application.logMessageReceived += HandleLog;

        // Activar al inicio
        if (consolePanel) consolePanel.SetActive(true);
    }

    void OnDisable()
    {
        // Desuscribirse para evitar memory leaks
        Application.logMessageReceived -= HandleLog;
    }

    void Update()
    {
        // Toggle con tecla en PC (para pruebas)
        if (Input.GetKeyDown(toggleKeyPC))
        {
            ToggleConsole();
        }

        // Toggle con botón en VR
        if (UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand)
            .TryGetFeatureValue(new UnityEngine.XR.InputFeatureUsage<bool>(toggleButton), out bool pressed) && pressed)
        {
            ToggleConsole();
        }
    }

    void HandleLog(string logString, string stackTrace, LogType type)
    {
        // Crear formato del mensaje con timestamp y color según tipo
        string color = "white";
        switch (type)
        {
            case LogType.Error:
            case LogType.Exception:
            case LogType.Assert:
                color = "red";
                break;
            case LogType.Warning:
                color = "yellow";
                break;
        }

        string formattedLog = $"<color={color}>[{System.DateTime.Now.ToString("HH:mm:ss")}] {logString}</color>";

        // Añadir a la cola y limitar tamaño
        logQueue.Enqueue(formattedLog);
        if (logQueue.Count > maxLogs)
        {
            logQueue.Dequeue(); // Eliminar el log más antiguo
        }

        // Actualizar texto si la consola está visible
        if (showConsole)
        {
            UpdateConsoleText();
        }
    }

    void UpdateConsoleText()
    {
        if (logText == null) return;

        // Construir texto con todos los logs
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        foreach (string log in logQueue)
        {
            sb.AppendLine(log);
        }

        logText.text = sb.ToString();
    }

    public void ToggleConsole()
    {
        if (consolePanel == null) return;

        showConsole = !showConsole;
        consolePanel.SetActive(showConsole);

        // Actualizar texto cuando se activa
        if (showConsole)
        {
            UpdateConsoleText();
        }
    }

    public void ClearLogs()
    {
        logQueue.Clear();
        if (logText != null)
        {
            logText.text = "";
        }
    }
}