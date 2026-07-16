using UnityEngine;

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

    /// <summary>
    /// Quits the application after 3 seconds. In the editor, it stops play mode.
    /// </summary>
    public void QuitAfterDelay()
    {
        StartCoroutine(QuitCoroutine());
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