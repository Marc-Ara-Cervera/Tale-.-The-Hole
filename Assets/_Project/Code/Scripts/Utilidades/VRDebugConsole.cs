using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.Collections;
using UnityEngine.InputSystem;

public class VRDebugConsole : MonoBehaviour
{
    [Header("UI Referencias")]
    [SerializeField] private GameObject consolePanel;
    [SerializeField] private TextMeshProUGUI logText;
    [SerializeField] private int maxLogs = 300;
    [SerializeField] private bool startVisible = true;
    [SerializeField] private float updateInterval = 0.5f;

    [Header("Input")]
    [SerializeField] private KeyCode toggleKeyPC = KeyCode.F1;
    [SerializeField] private InputActionReference toggleAction; // Referencia a la acción de input

    // Variables estáticas para garantizar captura temprana
    private static List<LogEntry> allLogs = new List<LogEntry>();
    private static bool isStaticInitialized = false;
    private static VRDebugConsole instance;

    private bool showConsole = false;
    private bool isUpdatingText = false;
    private float lastUpdateTime = 0f;
    private float lastToggleTime = 0f;
    private const float TOGGLE_COOLDOWN = 0.5f; // Evitar toggles múltiples

    // Clase para almacenar logs
    private class LogEntry
    {
        public string Message;
        public string StackTrace;
        public LogType Type;
        public DateTime Time;
        public string FormattedText;

        public LogEntry(string message, string stackTrace, LogType type)
        {
            Message = message;
            StackTrace = stackTrace;
            Type = type;
            Time = DateTime.Now;

            // Pre-formatear el texto del log
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

            FormattedText = $"<color={color}>[{Time.ToString("HH:mm:ss")}] {Message}</color>";
        }
    }

    // Método que se ejecuta antes de que se inicialice la escena
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void EarlyInitialize()
    {
        if (!isStaticInitialized)
        {
            // Capturar logs desde el inicio absoluto
            Application.logMessageReceived += StaticLogHandler;
            isStaticInitialized = true;
            allLogs = new List<LogEntry>();
            Debug.Log("[VRDebugConsole] Inicialización estática completada. Capturando logs...");
        }
    }

    // Handler estático que se ejecuta desde el principio
    private static void StaticLogHandler(string logString, string stackTrace, LogType type)
    {
        // Almacenar el log incluso antes de que exista la instancia
        if (allLogs.Count >= 300)
        {
            allLogs.RemoveAt(0); // Mantener tamaño controlado
        }
        allLogs.Add(new LogEntry(logString, stackTrace, type));

        // Si hay una instancia activa, forzar actualización
        if (instance != null && instance.showConsole)
        {
            instance.needsUpdate = true;
        }
    }

    // Para controlar las actualizaciones
    private bool needsUpdate = false;

    private void Awake()
    {
        // Singleton pattern para una sola instancia
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);

            // Asegurarse de que la inicialización estática ocurrió
            if (!isStaticInitialized)
            {
                EarlyInitialize();
            }

            // Iniciar visible si está configurado así
            showConsole = startVisible;
            if (consolePanel) consolePanel.SetActive(showConsole);

            Debug.Log("[VRDebugConsole] Instancia creada. Logs capturados tempranamente: " + allLogs.Count);

            // Iniciar la coroutine de actualización
            StartCoroutine(AutoUpdateConsole());
        }
        else
        {
            Destroy(gameObject);
            return;
        }
    }

    private void OnEnable()
    {
        // Configurar visibilidad inicial
        if (consolePanel) consolePanel.SetActive(showConsole);

        // Habilitar la acción de input si está asignada
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.Enable();
            toggleAction.action.performed += OnToggleActionPerformed;
        }

        // Forzar actualización inicial
        if (showConsole)
        {
            UpdateConsoleText();
        }
    }

    private void OnDisable()
    {
        // Deshabilitar la acción de input si está asignada
        if (toggleAction != null && toggleAction.action != null)
        {
            toggleAction.action.performed -= OnToggleActionPerformed;
            toggleAction.action.Disable();
        }
    }

    private void OnToggleActionPerformed(InputAction.CallbackContext context)
    {
        // Control de cooldown para evitar múltiples toggles
        if (Time.time - lastToggleTime < TOGGLE_COOLDOWN)
            return;

        lastToggleTime = Time.time;
        ToggleConsole();
    }

    private void Update()
    {
        // Toggle con tecla en PC
        if (Input.GetKeyDown(toggleKeyPC))
        {
            ToggleConsole();
        }

        // Actualizar el texto si es necesario y ha pasado suficiente tiempo
        if (showConsole && needsUpdate && Time.time - lastUpdateTime > updateInterval)
        {
            UpdateConsoleText();
            lastUpdateTime = Time.time;
            needsUpdate = false;
        }
    }

    // Coroutine para actualización automática
    private IEnumerator AutoUpdateConsole()
    {
        while (true)
        {
            // Si la consola está visible, actualizarla periódicamente
            if (showConsole && !isUpdatingText)
            {
                UpdateConsoleText();
            }

            // Esperar el intervalo configurado
            yield return new WaitForSeconds(updateInterval);
        }
    }

    // Para activar/desactivar la consola
    public void ToggleConsole()
    {
        if (consolePanel == null) return;

        showConsole = !showConsole;
        consolePanel.SetActive(showConsole);

        if (showConsole)
        {
            UpdateConsoleText();
        }
    }

    // Actualizar el texto de la consola con todos los logs capturados
    private void UpdateConsoleText()
    {
        if (logText == null || isUpdatingText) return;

        isUpdatingText = true;

        try
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();

            // Mostrar logs más recientes primero (orden inverso)
            for (int i = allLogs.Count - 1; i >= 0 && i >= allLogs.Count - maxLogs; i--)
            {
                sb.AppendLine(allLogs[i].FormattedText);
            }

            logText.text = sb.ToString();
        }
        catch (System.Exception e)
        {
            Debug.LogError("Error actualizando texto de consola: " + e.Message);
        }
        finally
        {
            isUpdatingText = false;
            lastUpdateTime = Time.time;
        }
    }

    // Limpiar todos los logs
    public void ClearLogs()
    {
        allLogs.Clear();
        if (logText != null)
        {
            logText.text = "";
        }
    }
}