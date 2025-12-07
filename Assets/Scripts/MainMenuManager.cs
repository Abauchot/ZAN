using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private GameObject infoPanel;
    
    [Header("SFX")]
    [SerializeField] private AudioClip buttonClickSfx;
    
    [Header("Fade Settings")]
    [SerializeField] private Image fadeImage;
    [SerializeField] private float fadeDuration = 0.8f;
    [SerializeField] private float minWaitTime = 1.5f;

    private void Start()
    {
        MusicManager.Instance?.PlayMusic(MusicTrack.Menu);
    }
    
    public void StartDuel()
    {
        StartCoroutine(StartDuelSequence());
    }

    private System.Collections.IEnumerator StartDuelSequence()
    {
        MusicManager.Instance?.PlayMusic(MusicTrack.None);
        MusicManager.PlaySfxStatic(buttonClickSfx);

        float timer = 0f;
        Color c = fadeImage.color;

        while (timer < fadeDuration)
        {
            timer += Time.deltaTime;
            float t = timer / fadeDuration;
            fadeImage.color = new Color(c.r, c.g, c.b, Mathf.Lerp(0f, 1f, t));
            yield return null;
        }
        
        yield return new WaitForSeconds(minWaitTime);

        SceneManager.LoadScene("DuelScene");
    }

    
    public void ShowInfoPanel()
    {
        if (infoPanel != null)
        {
            infoPanel.SetActive(!infoPanel.activeSelf);
        }
    }
}
