using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("Audio Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;

    // Volume values (0–1)
    public float MasterVolume { get; private set; } = 1f;
    public float MusicVolume  { get; private set; } = 1f;
    public float SFXVolume    { get; private set; } = 1f;

    private const string PREF_MASTER  = "TF_Audio_Master";
    private const string PREF_MUSIC   = "TF_Audio_Music";
    private const string PREF_SFX     = "TF_Audio_SFX";
    private const string PREF_QUALITY = "TF_Graphics_Quality";

    private Dictionary<string, AudioClip> _sfxCache = new();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Init()
    {
        if (Instance != null) return;
        var go = new GameObject("AudioManager");
        go.AddComponent<AudioManager>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (musicSource == null)
        {
            musicSource = gameObject.AddComponent<AudioSource>();
            musicSource.loop = true;
            musicSource.playOnAwake = false;
        }
        if (sfxSource == null)
        {
            sfxSource = gameObject.AddComponent<AudioSource>();
            sfxSource.playOnAwake = false;
        }

        LoadSettings();
        ApplyVolumes();

        // Intentar reproducir inmediatamente en Awake, no esperar a Start
        Debug.Log("[AudioManager] Awake — intentando cargar música...");
        TryPlayMusic("backgroundMenu");
    }

    void Start()
    {
        Debug.Log("[AudioManager] Start — musicSource.isPlaying=" + musicSource.isPlaying);
        if (!musicSource.isPlaying)
            PlayMusic("backgroundMenu");
    }

    void TryPlayMusic(string clipName)
    {
        if (musicSource.isPlaying && musicSource.clip != null && musicSource.clip.name == clipName)
        {
            Debug.Log($"[AudioManager] '{clipName}' ya está sonando, no se reinicia.");
            return;
        }
        AudioClip clip = Resources.Load<AudioClip>($"Audios/{clipName}");
        if (clip == null)
        {
            Debug.LogWarning($"[AudioManager] No se encontró el clip 'Audios/{clipName}'. " +
                "Asegúrate de que el archivo esté en una carpeta llamada 'Resources/Audios/'.");
            return;
        }
        musicSource.clip = clip;
        musicSource.Play();
        Debug.Log($"[AudioManager] Reproduciendo '{clipName}' — duración: {clip.length}s, " +
            "volumen: " + musicSource.volume);
    }

    public void PlayMusic(string clipName)
    {
        TryPlayMusic(clipName);
    }

    public void PlaySFX(string clipName)
    {
        if (sfxSource == null) return;

        if (!_sfxCache.TryGetValue(clipName, out var clip) || clip == null)
        {
            clip = Resources.Load<AudioClip>($"Audios/{clipName}");
            if (clip == null)
            {
                Debug.LogWarning($"[AudioManager] No se encontró el clip 'Audios/{clipName}'");
                return;
            }
            _sfxCache[clipName] = clip;
        }

        sfxSource.PlayOneShot(clip);
    }

    public void SetMasterVolume(float value)
    {
        MasterVolume = Mathf.Clamp01(value);
        ApplyVolumes();
        PlayerPrefs.SetFloat(PREF_MASTER, MasterVolume);
    }

    public void SetMusicVolume(float value)
    {
        MusicVolume = Mathf.Clamp01(value);
        ApplyVolumes();
        PlayerPrefs.SetFloat(PREF_MUSIC, MusicVolume);
    }

    public void SetSFXVolume(float value)
    {
        SFXVolume = Mathf.Clamp01(value);
        ApplyVolumes();
        PlayerPrefs.SetFloat(PREF_SFX, SFXVolume);
    }

    public void SetQualityLevel(int index)
    {
        QualitySettings.SetQualityLevel(index);
        PlayerPrefs.SetInt(PREF_QUALITY, index);
    }

    void ApplyVolumes()
    {
        if (musicSource != null)
            musicSource.volume = MasterVolume * MusicVolume;
        if (sfxSource != null)
            sfxSource.volume = MasterVolume * SFXVolume;
    }

    void LoadSettings()
    {
        MasterVolume = PlayerPrefs.GetFloat(PREF_MASTER, 1f);
        MusicVolume  = PlayerPrefs.GetFloat(PREF_MUSIC,  1f);
        SFXVolume    = PlayerPrefs.GetFloat(PREF_SFX,    1f);

        int quality = PlayerPrefs.GetInt(PREF_QUALITY, QualitySettings.GetQualityLevel());
        QualitySettings.SetQualityLevel(quality);
    }
}
