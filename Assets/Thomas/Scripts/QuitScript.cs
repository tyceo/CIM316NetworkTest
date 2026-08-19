using UnityEngine;
using UnityEngine.SceneManagement;

public class QuitScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void LoadTutorialSceneAfterDelay()
    {
        StartCoroutine(LoadTutorialSceneCoroutine());
    }

    private System.Collections.IEnumerator LoadTutorialSceneCoroutine()
    {
        yield return new WaitForSeconds(2f);
        SceneManager.LoadScene("Tutorial Scene");
    }

    /// <summary>
    /// Quits the application after 3 seconds. In the editor, it stops play mode.
    /// </summary>
    public void QuitAfterDelay()
    {
        //StartCoroutine(QuitCoroutine());
    }
    
    

    private System.Collections.IEnumerator QuitCoroutine()
    {
        yield return new WaitForSeconds(2f);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}