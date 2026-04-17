using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenu : MonoBehaviour
{
    [SerializeField] private string levelSceneName = "level_0";

    public void NewGame()
    {
        SceneManager.LoadScene(levelSceneName);
    }
}