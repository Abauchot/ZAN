using UnityEngine;
using UnityEngine.UI;

public class SettingsMenuManager : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject darkBackground;

    [Header("Sliders")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider musicSlider;
    [SerializeField] private Slider sfxSlider;

    private void Start()
    {
        if (settingsPanel)   settingsPanel.SetActive(false);
        if (darkBackground)  darkBackground.SetActive(false);

        if (!MusicManager.Instance) return;
        
        if (masterSlider)
            masterSlider.SetValueWithoutNotify(MusicManager.Instance.GetMasterVolume());

        if (musicSlider)
            musicSlider.SetValueWithoutNotify(MusicManager.Instance.GetMusicVolume());

        if (sfxSlider)
            sfxSlider.SetValueWithoutNotify(MusicManager.Instance.GetSfxVolume());
    }

    public void OpenSettings()
    {
        if (settingsPanel)  settingsPanel.SetActive(true);
        if (darkBackground) darkBackground.SetActive(true);
    }

    public void CloseSettings()
    {
        if (settingsPanel)  settingsPanel.SetActive(false);
        if (darkBackground) darkBackground.SetActive(false);
    }

    public void OnMasterVolumeChanged(float value)
    {
        Debug.Log($"[Settings] Music slider = {value}");
        if (MusicManager.Instance)
            MusicManager.Instance.SetMasterVolume(value);
    }

    public void OnMusicVolumeChanged(float value)
    {
        Debug.Log($"[Settings] Music slider = {value}");
        if (MusicManager.Instance)
            MusicManager.Instance.SetMusicVolume(value);
    }

    public void OnSfxVolumeChanged(float value)
    {
        Debug.Log($"[Settings] SFX slider = {value}");
        if (MusicManager.Instance)
            MusicManager.Instance.SetSfxVolume(value);
    }
    
    public void TestHalfMusic()
    {
        if (MusicManager.Instance)
            MusicManager.Instance.SetMusicVolume(0.2f);
    }

}