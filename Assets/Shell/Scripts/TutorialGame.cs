using UnityEngine;
using UnityEngine.SceneManagement;

public class TutorialGame : MonoBehaviour
{
    public void LoadTutorialScene()
    {
        SceneManager.LoadScene(2);
    }
}