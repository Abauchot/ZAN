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

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
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
        Debug.Log($"PlayMusic track={track}, menu={menuMusic}, duel={duelMusic}, gameOver={gameOverMusic}");
        if (musicSource == null) return;
        if (_currentTrack == track) return;
        
        musicSource.Stop();
        musicSource.clip = null;

        if (ambienceSource != null)
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

        if (clipToPlay == null)
        {
            _currentTrack = MusicTrack.None;
            return;
        }

        musicSource.clip = clipToPlay;
        musicSource.loop = true;
        musicSource.Play();
        
        if (track == MusicTrack.Duel && ambienceSource != null && duelWindLoop != null)
        {
            ambienceSource.clip = duelWindLoop;
            ambienceSource.loop = true;
            ambienceSource.Play();
        }

        _currentTrack = track;
    }

    // ----- SFX Management ----- //

    public void PlaySfx(AudioClip clip, float volume = 1f)
    {
        if (!sfxSource || !clip) return;
        sfxSource.PlayOneShot(clip, volume);
    }

    public static void PlaySfxStatic(AudioClip clip, float volume = 1f)
    {
        if (Instance)
            Instance.PlaySfx(clip, volume);
    }
}
