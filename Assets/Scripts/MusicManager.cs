using UnityEngine;
using UnityEngine.SceneManagement;

public enum MusicTrack
{
    None,
    Menu,
    Duel,
    GameOver,
}

public class MusicManager : MonoBehaviour
{
    public static MusicManager Instance { get; private set; }

    [Header("Sources")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioSource sfxSource;
    [SerializeField] private AudioSource ambienceSource;

    [Header("Music Clips")]
    [SerializeField] private AudioClip menuMusic;
    [SerializeField] private AudioClip duelMusic;
    [SerializeField] private AudioClip gameOverMusic;
    
    [Header("Duel Ambience")]
    [SerializeField] private AudioClip duelWindLoop;

    private MusicTrack _currentTrack = MusicTrack.None;
    
    private float _masterVolume = 1f;
    private float _musicVolume  = 1f;
    private float _sfxVolume    = 1f;

    private const string MasterKey = "MasterVolume";
    private const string MusicKey  = "MusicVolume";
    private const string SfxKey    = "SfxVolume";

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        
        _masterVolume = PlayerPrefs.GetFloat(MasterKey, 1f);
        _musicVolume  = PlayerPrefs.GetFloat(MusicKey,  1f);
        _sfxVolume    = PlayerPrefs.GetFloat(SfxKey,    1f);

        ApplyVolumes();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        if (Instance == this)
            SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void Start()
    {
        UpdateMusicForScene(SceneManager.GetActiveScene().name);
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        UpdateMusicForScene(scene.name);
    }

    private void UpdateMusicForScene(string sceneName)
    {
        MusicTrack targetTrack = sceneName switch
        {
            "Menu"      => MusicTrack.Menu,
            "DuelScene" => MusicTrack.Duel,
            "GameOver"  => MusicTrack.GameOver,
            _           => MusicTrack.None
        };

        PlayMusic(targetTrack);
    }

    public void PlayMusic(MusicTrack track)
    {
        if (!musicSource) return;
        if (_currentTrack == track) return;
        
        musicSource.Stop();
        musicSource.clip = null;

        if (ambienceSource)
        {
            ambienceSource.Stop();
            ambienceSource.clip = null;
        }

        AudioClip clipToPlay = track switch
        {
            MusicTrack.Menu     => menuMusic,
            MusicTrack.Duel     => duelMusic,
            MusicTrack.GameOver => gameOverMusic,
            _                   => null
        };

        if (!clipToPlay)
        {
            _currentTrack = MusicTrack.None;
            return;
        }

        musicSource.clip = clipToPlay;
        musicSource.loop = track != MusicTrack.GameOver; 
        musicSource.Play();

        if (track == MusicTrack.Duel && ambienceSource && duelWindLoop)
        {
            ambienceSource.clip = duelWindLoop;
            ambienceSource.loop = true;
            ambienceSource.Play();
        }

        _currentTrack = track;
        ApplyVolumes();
    }

    // ----- SFX Management ----- //

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (!sfxSource || !clip) return;
        sfxSource.PlayOneShot(clip, volume * _masterVolume * _sfxVolume);
    }

    public static void PlaySfxStatic(AudioClip clip, float volume = 1f)
    {
        if (Instance)
            Instance.PlaySfx(clip, volume);
    }
    

    private void ApplyVolumes()
    {
        if (musicSource)
            musicSource.volume = _masterVolume * _musicVolume;

        if (ambienceSource)
            ambienceSource.volume = _masterVolume * _musicVolume;

        if (sfxSource)
            sfxSource.volume = _masterVolume * _sfxVolume;
    }

    public float GetMasterVolume() => _masterVolume;
    public float GetMusicVolume()  => _musicVolume;
    public float GetSfxVolume()    => _sfxVolume;

    public void SetMasterVolume(float value)
    {
        _masterVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(MasterKey, _masterVolume);
        ApplyVolumes();
    }

    public void SetMusicVolume(float value)
    {
        _musicVolume = Mathf.Clamp01(value);
        Debug.Log($"[MusicManager] SetMusicVolume = {_musicVolume}");
        PlayerPrefs.SetFloat(MusicKey, _musicVolume);
        ApplyVolumes();
    }

    public void SetSfxVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);
        PlayerPrefs.SetFloat(SfxKey, _sfxVolume);
        ApplyVolumes();
    }
}
