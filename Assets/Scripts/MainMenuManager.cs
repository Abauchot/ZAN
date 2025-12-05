using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private GameObject infoPanel;
    
    public void StartDuel()
    {
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
