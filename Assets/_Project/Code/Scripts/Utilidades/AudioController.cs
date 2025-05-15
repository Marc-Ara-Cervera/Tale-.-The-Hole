using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

/// <summary>
/// Sistema de audio avanzado para efectos de sonido con funciones como fade, looping y cola de sonidos.
/// </summary>
public class AudioController : MonoBehaviour
{
    [System.Serializable]
    public class AudioData
    {
        public string id;
        public AudioClip clip;
        [Range(0f, 1f)] public float volume = 1f;
        [Range(0.1f, 3f)] public float pitch = 1f;
        public bool loop = false;
        [Range(0f, 1f)] public float spatialBlend = 0f; // 0 = 2D, 1 = 3D
        public AudioMixerGroup outputGroup;

        // Configuración fade personalizada
        [Header("Fade Settings")]
        public bool useFade = false;
        [Range(0.1f, 5f)] public float fadeInDuration = 0.5f;
        [Range(0.1f, 5f)] public float fadeOutDuration = 0.5f;
    }

    [Header("Configuración Principal")]
    [SerializeField] private List<AudioData> audioClips = new List<AudioData>();
    [SerializeField] private int poolSize = 5;
    [Range(0.1f, 5f)][SerializeField] private float defaultFadeDuration = 0.5f;

    [Header("Opciones")]
    [SerializeField] private bool persistBetweenScenes = false;
    [SerializeField] private bool debug = false;

    // Listas para gestionar AudioSources
    private List<AudioSource> audioSourcePool = new List<AudioSource>();
    private Dictionary<string, AudioSource> activeSounds = new Dictionary<string, AudioSource>();
    private Queue<string> soundQueue = new Queue<string>();
    private bool isProcessingQueue = false;

    // Para referencias internas
    private Dictionary<string, AudioData> audioDataDict = new Dictionary<string, AudioData>();

    private void Awake()
    {
        // Opcional: Persistir entre escenas
        if (persistBetweenScenes)
        {
            DontDestroyOnLoad(gameObject);
        }

        // Inicializar el pool de AudioSources
        InitializeAudioSourcePool();

        // Crear un diccionario de búsqueda rápida para los clips
        foreach (AudioData data in audioClips)
        {
            if (!string.IsNullOrEmpty(data.id) && !audioDataDict.ContainsKey(data.id))
            {
                audioDataDict.Add(data.id, data);
            }
        }
    }

    private void InitializeAudioSourcePool()
    {
        // Crear un pool de AudioSources para reutilizar
        for (int i = 0; i < poolSize; i++)
        {
            GameObject audioSourceObj = new GameObject($"AudioSource_Pool_{i}");
            audioSourceObj.transform.SetParent(transform);

            AudioSource source = audioSourceObj.AddComponent<AudioSource>();
            source.playOnAwake = false;
            audioSourcePool.Add(source);
        }

        LogDebug($"Pool de {poolSize} AudioSources inicializado");
    }

    /// <summary>
    /// Obtiene un AudioSource libre del pool o crea uno nuevo si es necesario
    /// </summary>
    private AudioSource GetAvailableAudioSource()
    {
        foreach (AudioSource source in audioSourcePool)
        {
            if (!source.isPlaying)
            {
                return source;
            }
        }

        // Si no hay disponibles, crear uno nuevo
        GameObject audioSourceObj = new GameObject($"AudioSource_Pool_{audioSourcePool.Count}");
        audioSourceObj.transform.SetParent(transform);

        AudioSource newSource = audioSourceObj.AddComponent<AudioSource>();
        newSource.playOnAwake = false;
        audioSourcePool.Add(newSource);

        LogDebug($"Creado nuevo AudioSource (total: {audioSourcePool.Count})");
        return newSource;
    }

    #region Métodos Públicos

    /// <summary>
    /// Reproduce un sonido por su ID con las configuraciones predeterminadas
    /// </summary>
    public AudioSource Play(string soundId)
    {
        AudioData data = GetAudioData(soundId);
        if (data == null) return null;

        // Si el clip tiene configuración de fade personalizada, usarla
        if (data.useFade)
        {
            return PlayWithFadeIn(soundId, data.fadeInDuration);
        }
        else
        {
            return PlayWithSettings(soundId, null, null, null, null, null);
        }
    }

    /// <summary>
    /// Reproduce un sonido con fade in
    /// </summary>
    public AudioSource PlayWithFadeIn(string soundId, float fadeDuration = -1)
    {
        AudioData data = GetAudioData(soundId);
        if (data == null) return null;

        // Usar duración de fade personalizada si está disponible
        if (fadeDuration < 0)
        {
            fadeDuration = data.useFade ? data.fadeInDuration : defaultFadeDuration;
        }

        // Comenzar con volumen 0 e incrementar
        AudioSource source = PlayWithSettings(soundId, 0f, null, null, null, null);
        if (source != null)
        {
            StartCoroutine(FadeVolume(source, 0f, data.volume, fadeDuration));
        }
        return source;
    }

    /// <summary>
    /// Reproduce un sonido con configuraciones personalizadas
    /// </summary>
    public AudioSource PlayWithSettings(string soundId, float? volume = null, float? pitch = null,
                                      bool? loop = null, float? spatialBlend = null,
                                      AudioMixerGroup outputGroup = null)
    {
        if (!audioDataDict.ContainsKey(soundId))
        {
            Debug.LogWarning($"Sonido '{soundId}' no encontrado en AudioController");
            return null;
        }

        // Si ya hay un sonido activo con este ID, detenerlo
        StopSound(soundId, false);

        AudioData data = audioDataDict[soundId];
        AudioSource source = GetAvailableAudioSource();

        // Configurar el AudioSource con los datos del clip
        source.clip = data.clip;
        source.volume = volume ?? data.volume;
        source.pitch = pitch ?? data.pitch;
        source.loop = loop ?? data.loop;
        source.spatialBlend = spatialBlend ?? data.spatialBlend;
        source.outputAudioMixerGroup = outputGroup ?? data.outputGroup;

        // Posicionar el AudioSource en este objeto si es un sonido 3D
        if (source.spatialBlend > 0)
        {
            source.transform.position = transform.position;
        }

        // Reproducir y registrar el sonido activo
        source.Play();
        activeSounds[soundId] = source;

        LogDebug($"Reproduciendo sonido: {soundId}");

        return source;
    }

    /// <summary>
    /// Añade un sonido a la cola para reproducir después del actual
    /// </summary>
    public void QueueSound(string soundId)
    {
        if (!audioDataDict.ContainsKey(soundId))
        {
            Debug.LogWarning($"Sonido '{soundId}' no encontrado en AudioController");
            return;
        }

        soundQueue.Enqueue(soundId);
        LogDebug($"Sonido '{soundId}' añadido a la cola. Total en cola: {soundQueue.Count}");

        if (!isProcessingQueue)
        {
            StartCoroutine(ProcessSoundQueue());
        }
    }

    /// <summary>
    /// Detiene un sonido inmediatamente o con fade out
    /// </summary>
    public void StopSound(string soundId, bool withFadeOut = true, float fadeDuration = -1)
    {
        if (!activeSounds.TryGetValue(soundId, out AudioSource source) || source == null)
        {
            return;
        }

        AudioData data = GetAudioData(soundId);

        // Determinar la duración del fade out
        if (fadeDuration < 0)
        {
            fadeDuration = (data != null && data.useFade)
                ? data.fadeOutDuration
                : defaultFadeDuration;
        }

        // Aplicar fade out si se solicita y si el sonido está reproduciéndose
        if (withFadeOut && source.isPlaying)
        {
            StartCoroutine(FadeVolume(source, source.volume, 0f, fadeDuration, true));
            LogDebug($"Deteniendo sonido con fade out: {soundId} (duración: {fadeDuration}s)");
        }
        else
        {
            source.Stop();
            activeSounds.Remove(soundId);
            LogDebug($"Sonido detenido inmediatamente: {soundId}");
        }
    }

    /// <summary>
    /// Detiene todos los sonidos activos
    /// </summary>
    public void StopAllSounds(bool withFadeOut = true, float fadeDuration = -1)
    {
        if (fadeDuration < 0) fadeDuration = defaultFadeDuration;

        foreach (var soundPair in new Dictionary<string, AudioSource>(activeSounds))
        {
            StopSound(soundPair.Key, withFadeOut, fadeDuration);
        }

        LogDebug("Todos los sonidos detenidos");
    }

    /// <summary>
    /// Pausa o reanuda un sonido específico
    /// </summary>
    public void PauseResumeSound(string soundId, bool pause)
    {
        if (activeSounds.TryGetValue(soundId, out AudioSource source) && source != null)
        {
            if (pause)
            {
                source.Pause();
                LogDebug($"Sonido pausado: {soundId}");
            }
            else
            {
                source.UnPause();
                LogDebug($"Sonido reanudado: {soundId}");
            }
        }
    }

    /// <summary>
    /// Cambia el volumen de un sonido específico con fade
    /// </summary>
    public void ChangeVolume(string soundId, float targetVolume, float fadeDuration = -1)
    {
        if (!activeSounds.TryGetValue(soundId, out AudioSource source) || source == null)
        {
            return;
        }

        AudioData data = GetAudioData(soundId);

        if (fadeDuration < 0)
        {
            fadeDuration = (data != null && data.useFade)
                ? data.fadeInDuration
                : defaultFadeDuration;
        }

        StartCoroutine(FadeVolume(source, source.volume, targetVolume, fadeDuration));
        LogDebug($"Cambiando volumen de {soundId} a {targetVolume} en {fadeDuration}s");
    }

    /// <summary>
    /// Añade un nuevo clip de audio en tiempo de ejecución
    /// </summary>
    public void AddAudioClip(string id, AudioClip clip, float volume = 1f, float pitch = 1f,
                           bool loop = false, float spatialBlend = 0f, AudioMixerGroup outputGroup = null,
                           bool useFade = false, float fadeInDuration = 0.5f, float fadeOutDuration = 0.5f)
    {
        if (audioDataDict.ContainsKey(id))
        {
            Debug.LogWarning($"Ya existe un sonido con ID '{id}'. Reemplazando.");
        }

        AudioData newData = new AudioData
        {
            id = id,
            clip = clip,
            volume = volume,
            pitch = pitch,
            loop = loop,
            spatialBlend = spatialBlend,
            outputGroup = outputGroup,
            useFade = useFade,
            fadeInDuration = fadeInDuration,
            fadeOutDuration = fadeOutDuration
        };

        audioClips.Add(newData);
        audioDataDict[id] = newData;

        LogDebug($"Añadido nuevo clip de audio: {id}");
    }

    /// <summary>
    /// Comprueba si un sonido está actualmente reproduciéndose
    /// </summary>
    public bool IsPlaying(string soundId)
    {
        return activeSounds.TryGetValue(soundId, out AudioSource source) && source != null && source.isPlaying;
    }

    #endregion

    #region Métodos Internos

    private IEnumerator FadeVolume(AudioSource source, float startVolume, float targetVolume,
                                  float duration, bool stopAfterFade = false)
    {
        float currentTime = 0;

        source.volume = startVolume;

        while (currentTime < duration)
        {
            currentTime += Time.deltaTime;
            source.volume = Mathf.Lerp(startVolume, targetVolume, currentTime / duration);
            yield return null;
        }

        source.volume = targetVolume;

        if (stopAfterFade)
        {
            source.Stop();

            // Eliminar de activos
            foreach (var pair in new Dictionary<string, AudioSource>(activeSounds))
            {
                if (pair.Value == source)
                {
                    activeSounds.Remove(pair.Key);
                    break;
                }
            }
        }
    }

    private IEnumerator ProcessSoundQueue()
    {
        isProcessingQueue = true;

        while (soundQueue.Count > 0)
        {
            string nextSoundId = soundQueue.Dequeue();
            AudioData data = GetAudioData(nextSoundId);

            if (data != null && data.clip != null)
            {
                AudioSource source = Play(nextSoundId);

                // Esperar a que termine antes de reproducir el siguiente
                float clipDuration = data.clip.length / source.pitch;
                yield return new WaitForSeconds(clipDuration);

                // Pequeña pausa entre clips
                yield return new WaitForSeconds(0.1f);
            }
        }

        isProcessingQueue = false;
    }

    private AudioData GetAudioData(string soundId)
    {
        if (audioDataDict.TryGetValue(soundId, out AudioData data))
        {
            return data;
        }
        return null;
    }

    private void LogDebug(string message)
    {
        if (debug)
        {
            Debug.Log($"[AudioController] {message}");
        }
    }

    #endregion

    #region Editor Support
#if UNITY_EDITOR
    // Método para ayudar a configurar el componente en el editor
    public void SetupDefaults()
    {
        if (audioClips.Count == 0)
        {
            audioClips.Add(new AudioData { id = "example_sound" });
        }
    }
#endif
    #endregion
}