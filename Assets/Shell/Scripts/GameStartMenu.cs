using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class GameStartMenu : MonoBehaviour
{
    [Header("UI Pages")]
    public GameObject mainMenu;
    public GameObject howToPlay;
    public GameObject about;

    [Header("Scene Build Index")]
    public int gameSceneIndex = 1;
    public int howToPlaySceneIndex = 2;

    [Header("Main Menu Buttons")]
    public Button startButton;
    public Button howToPlayButton;
    public Button aboutButton;
    public Button quitButton;

    public List<Button> returnButtons;

    void Start()
    {
        EnableMainMenu();

        startButton.onClick.AddListener(StartGame);
        howToPlayButton.onClick.AddListener(StartHowToPlay);
        aboutButton.onClick.AddListener(EnableAbout);
        quitButton.onClick.AddListener(QuitGame);

        foreach (var item in returnButtons)
        {
            item.onClick.AddListener(EnableMainMenu);
        }
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void StartGame()
    {
        HideAll();
        SceneTransitionManager.singleton.GoToSceneAsync(gameSceneIndex);
    }

    public void StartHowToPlay()
    {
        HideAll();
        SceneTransitionManager.singleton.GoToSceneAsync(howToPlaySceneIndex);
    }

    public void HideAll()
    {
        mainMenu.SetActive(false);

        if (howToPlay != null)
        {
            howToPlay.SetActive(false);
        }

        if (about != null)
        {
            about.SetActive(false);
        }
    }

    public void EnableMainMenu()
    {
        mainMenu.SetActive(true);

        if (howToPlay != null)
        {
            howToPlay.SetActive(false);
        }

        if (about != null)
        {
            about.SetActive(false);
        }
    }

    public void EnableHowToPlayPanel()
    {
        mainMenu.SetActive(false);

        if (howToPlay != null)
        {
            howToPlay.SetActive(true);
        }

        if (about != null)
        {
            about.SetActive(false);
        }
    }

    public void EnableAbout()
    {
        mainMenu.SetActive(false);

        if (howToPlay != null)
        {
            howToPlay.SetActive(false);
        }

        if (about != null)
        {
            about.SetActive(true);
        }
    }
}