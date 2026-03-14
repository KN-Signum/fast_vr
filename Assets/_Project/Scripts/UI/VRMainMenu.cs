using UnityEngine;
using UnityEngine.SceneManagement; // Required for SceneManager

public class VRMainMenu : MonoBehaviour
{
    
    public void LoadGameScene(string sceneName)
    {
        Debug.Log("Loading scene: " + sceneName);
        SceneManager.LoadScene(sceneName);
    }

    public void QuitGame()
    {
        Debug.Log("Exiting application...");
        Application.Quit(); 
    }
    
}
