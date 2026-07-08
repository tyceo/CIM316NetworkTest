using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialButtons : MonoBehaviour
{
    public void StartGame()
    {
        LoadScene(1); // BasicScene
    }

    public void BackToMenu()
    {
        LoadScene(0); // 1 Start Scene
    }

    private void LoadScene(int sceneIndex)
    {
        if (SceneTransitionManager.singleton != null)
        {
            SceneTransitionManager.singleton.GoToSceneAsync(sceneIndex);
        }
        else
        {
            SceneManager.LoadScene(sceneIndex);
        }
    }
}