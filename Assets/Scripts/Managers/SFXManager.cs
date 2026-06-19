using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Central sound-effects service. The sfxLibrary Dictionary is the project's
/// Hash Map: string keys are hashed internally so clips can be retrieved quickly by name.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(AudioSource))]
public class SFXManager : MonoBehaviour
{
    [Serializable]
    public class SFXEntry
    {
        [Tooltip("Hash Map key used by code, for example ButtonClick or Footstep_01.")]
        public string key;
        public AudioClip clip;
    }

    public static SFXManager Instance { get; private set; }

    [Header("Hash Map Library")]
    [SerializeField] private SFXEntry[] soundEffects;
#if UNITY_EDITOR
    [SerializeField] private bool autoFindCommonClipsInEditor = true;
#endif

    [Header("Playback")]
    [SerializeField] [Range(0f, 1f)] private float masterVolume = 1f;
    [SerializeField] [Range(0f, 1f)] private float buttonVolume = 0.85f;
    [SerializeField] [Range(0f, 1f)] private float footstepVolume = 0.7f;

    [Header("Background Music")]
    [SerializeField] private AudioSource musicAudioSource;
    [SerializeField] private AudioClip lobbyMusicClip;
    [SerializeField] [Range(0f, 1f)] private float musicVolume = 0.35f;
    [SerializeField] private bool playLobbyMusicOnStart = true;

    [Header("Button SFX")]
    [SerializeField] private bool autoBindUiButtons = true;
    [SerializeField] private string buttonClickKey = "ButtonClick";
    [SerializeField] private float buttonScanInterval = 1f;

    [Header("Footstep SFX")]
    [SerializeField] private bool autoAddFootstepComponent = true;
    [SerializeField] private string defaultFootstepKey = "Footstep";
    [SerializeField] private float playerScanInterval = 1f;
    [SerializeField] private string[] randomFootstepKeys =
    {
        "Footstep_01",
        "Footstep_02",
        "Footstep_03",
        "Footstep_04"
    };

    // Dictionary<string, AudioClip> is the Hash Map required by the assignment.
    // All SFX requests go through this lookup instead of holding direct clip references.
    private readonly Dictionary<string, AudioClip> sfxLibrary = new Dictionary<string, AudioClip>();
    private readonly HashSet<Button> boundButtons = new HashSet<Button>();

    private AudioSource audioSource;
    private float nextButtonScanTime;
    private float nextPlayerScanTime;
    private bool musicInitialized;

    public IReadOnlyDictionary<string, AudioClip> SfxLibrary => sfxLibrary;
    public string DefaultFootstepKey => defaultFootstepKey;
    public string[] RandomFootstepKeys => randomFootstepKeys;
    public float FootstepVolume => footstepVolume;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureRuntimeInstance()
    {
        if (Instance != null)
        {
            return;
        }

        SFXManager existing = FindFirstObjectByType<SFXManager>(FindObjectsInactive.Include);
        if (existing != null)
        {
            existing.gameObject.SetActive(true);
            existing.enabled = true;
            return;
        }

        GameObject managerObject = new GameObject("SFXManager");
        managerObject.AddComponent<SFXManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        EnsureMusicAudioSource();
        RebuildLibrary();
        ResolveLobbyMusicClip();
        Debug.Log("MusicManager initialized.", this);
    }

    private void OnValidate()
    {
        musicVolume = Mathf.Clamp01(musicVolume);
        if (musicAudioSource != null)
        {
            ConfigureMusicAudioSource(logLoopEnabled: false);
        }
    }

    private void Start()
    {
        if (playLobbyMusicOnStart)
        {
            PlayLobbyMusic();
        }

        BindUiButtons();
        EnsurePlayerFootsteps();
    }

    private void Update()
    {
        if (autoBindUiButtons && Time.unscaledTime >= nextButtonScanTime)
        {
            BindUiButtons();
            nextButtonScanTime = Time.unscaledTime + Mathf.Max(0.1f, buttonScanInterval);
        }

        if (autoAddFootstepComponent && Time.unscaledTime >= nextPlayerScanTime)
        {
            EnsurePlayerFootsteps();
            nextPlayerScanTime = Time.unscaledTime + Mathf.Max(0.1f, playerScanInterval);
        }
    }

    public void PlaySFX(string key)
    {
        PlaySFX(key, masterVolume);
    }

    public void PlaySFX(string key, float volumeScale)
    {
        if (!TryGetClip(key, out AudioClip clip))
        {
            return;
        }

        audioSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * masterVolume);
    }

    public void PlayButtonClick()
    {
        PlaySFX(buttonClickKey, buttonVolume);
    }

    public void PlayRandomSFX(IReadOnlyList<string> keys, string fallbackKey, float volumeScale)
    {
        if (keys != null && keys.Count > 0)
        {
            int startIndex = UnityEngine.Random.Range(0, keys.Count);
            for (int i = 0; i < keys.Count; i++)
            {
                string key = keys[(startIndex + i) % keys.Count];
                if (TryGetClip(key, out AudioClip clip))
                {
                    audioSource.PlayOneShot(clip, Mathf.Clamp01(volumeScale) * masterVolume);
                    return;
                }
            }
        }

        PlaySFX(fallbackKey, volumeScale);
    }

    public bool TryGetClip(string key, out AudioClip clip)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            clip = null;
            Debug.LogWarning("[SFX] Cannot play a sound with an empty key.", this);
            return false;
        }

        if (sfxLibrary.TryGetValue(key, out clip) && clip != null)
        {
            return true;
        }

        Debug.LogWarning($"[SFX] No AudioClip found for key '{key}'. Add it to the SFXManager library.", this);
        return false;
    }

    public void PlayLobbyMusic()
    {
        EnsureMusicAudioSource();
        ResolveLobbyMusicClip();

        if (musicAudioSource == null)
        {
            Debug.LogWarning("[MusicManager] No AudioSource available for background music.", this);
            return;
        }

        if (lobbyMusicClip == null)
        {
            Debug.LogWarning("[MusicManager] Lobby music clip is not assigned or could not be found.", this);
            return;
        }

        ConfigureMusicAudioSource();

        if (musicAudioSource.isPlaying && musicAudioSource.clip == lobbyMusicClip)
        {
            return;
        }

        musicAudioSource.clip = lobbyMusicClip;
        musicAudioSource.volume = musicVolume;
        musicAudioSource.Play();
        Debug.Log("Lobby music started.", this);
    }

    private void EnsureMusicAudioSource()
    {
        if (musicAudioSource == null)
        {
            AudioSource[] sources = GetComponents<AudioSource>();
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null && sources[i] != audioSource)
                {
                    musicAudioSource = sources[i];
                    break;
                }
            }
        }

        if (musicAudioSource == null)
        {
            musicAudioSource = gameObject.AddComponent<AudioSource>();
        }

        ConfigureMusicAudioSource();
    }

    private void ConfigureMusicAudioSource(bool logLoopEnabled = true)
    {
        if (musicAudioSource == null)
        {
            return;
        }

        musicAudioSource.loop = true;
        musicAudioSource.playOnAwake = true;
        musicAudioSource.spatialBlend = 0f;
        musicAudioSource.volume = musicVolume;

        if (logLoopEnabled && !musicInitialized)
        {
            musicInitialized = true;
            Debug.Log("Lobby music loop enabled.", this);
        }
    }

    public void RegisterButton(Button button)
    {
        if (button == null || boundButtons.Contains(button))
        {
            return;
        }

        button.onClick.AddListener(PlayButtonClick);
        boundButtons.Add(button);
    }

    private void RebuildLibrary()
    {
        sfxLibrary.Clear();

        if (soundEffects != null)
        {
            for (int i = 0; i < soundEffects.Length; i++)
            {
                SFXEntry entry = soundEffects[i];
                if (entry == null || string.IsNullOrWhiteSpace(entry.key) || entry.clip == null)
                {
                    continue;
                }

                AddToLibrary(entry.key, entry.clip);
            }
        }

#if UNITY_EDITOR
        if (autoFindCommonClipsInEditor && sfxLibrary.Count == 0)
        {
            AddEditorClipIfFound("ButtonClick", "Modern9");
            AddEditorClipIfFound(defaultFootstepKey, "Footsteps_Tile_Walk_01");
            AddEditorClipIfFound("Footstep_01", "Footsteps_Tile_Walk_01");
            AddEditorClipIfFound("Footstep_02", "Footsteps_Tile_Walk_02");
            AddEditorClipIfFound("Footstep_03", "Footsteps_Tile_Walk_03");
            AddEditorClipIfFound("Footstep_04", "Footsteps_Tile_Walk_04");
        }
#endif
    }

    private void ResolveLobbyMusicClip()
    {
        if (lobbyMusicClip != null)
        {
            return;
        }

#if UNITY_EDITOR
        lobbyMusicClip = FindEditorAudioClip("Lobby");
        if (lobbyMusicClip == null)
        {
            lobbyMusicClip = FindEditorAudioClip("The Lobby");
        }
#endif
    }

    private void AddToLibrary(string key, AudioClip clip)
    {
        if (string.IsNullOrWhiteSpace(key) || clip == null)
        {
            return;
        }

        if (sfxLibrary.ContainsKey(key))
        {
            Debug.LogWarning($"[SFX] Duplicate key '{key}' ignored. Dictionary keys must be unique.", this);
            return;
        }

        sfxLibrary.Add(key, clip);
    }

#if UNITY_EDITOR
    private AudioClip FindEditorAudioClip(string clipName)
    {
        string[] guids = AssetDatabase.FindAssets($"{clipName} t:AudioClip");
        AudioClip fallbackClip = null;
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip == null)
            {
                continue;
            }

            if (fallbackClip == null)
            {
                fallbackClip = clip;
            }
            if (!path.Contains("/Imports/"))
            {
                return clip;
            }
        }

        return fallbackClip;
    }

    private void AddEditorClipIfFound(string key, string clipName)
    {
        string[] guids = AssetDatabase.FindAssets($"{clipName} t:AudioClip");
        for (int i = 0; i < guids.Length; i++)
        {
            string path = AssetDatabase.GUIDToAssetPath(guids[i]);
            if (path.Contains("/Imports/"))
            {
                continue;
            }

            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
            if (clip != null)
            {
                AddToLibrary(key, clip);
                return;
            }
        }

        if (guids.Length > 0)
        {
            AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guids[0]));
            AddToLibrary(key, clip);
        }
    }
#endif

    private void BindUiButtons()
    {
        if (!autoBindUiButtons)
        {
            return;
        }

        boundButtons.RemoveWhere(button => button == null);
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            RegisterButton(buttons[i]);
        }
    }

    private void EnsurePlayerFootsteps()
    {
        if (!autoAddFootstepComponent)
        {
            return;
        }

        FirstPersonController controller = FindFirstObjectByType<FirstPersonController>();
        if (controller == null || controller.GetComponent<PlayerFootstepSFX>() != null)
        {
            return;
        }

        controller.gameObject.AddComponent<PlayerFootstepSFX>();
    }
}
