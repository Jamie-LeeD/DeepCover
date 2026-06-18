using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Wires the existing StartScene Play/Quit buttons to scene loading and application quit behavior.
/// </summary>
[DisallowMultipleComponent]
public class StartMenuController : MonoBehaviour
{
    [Header("Scene")]
    [SerializeField] private string gameplaySceneName = "GameScene";

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void EnsureStartMenuController()
    {
        if (SceneManager.GetActiveScene().name != "StartScene")
        {
            return;
        }

        if (FindFirstObjectByType<StartMenuController>(FindObjectsInactive.Include) != null)
        {
            return;
        }

        GameObject root = GameObject.Find("MainMenu") ?? GameObject.Find("Canvas") ?? new GameObject("StartMenuController");
        root.AddComponent<StartMenuController>();
    }

    private void Awake()
    {
        ResolveReferences();
        WireButtons();
        Time.timeScale = 1f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnDestroy()
    {
        UnwireButtons();
    }

    public void PlayGame()
    {
        Debug.Log("Loading Gameplay Scene", this);
        Time.timeScale = 1f;

        if (!string.IsNullOrWhiteSpace(gameplaySceneName) && Application.CanStreamedLevelBeLoaded(gameplaySceneName))
        {
            SceneManager.LoadScene(gameplaySceneName);
            return;
        }

        Debug.LogWarning(
            $"[StartMenu] Gameplay scene '{gameplaySceneName}' is not available in Build Settings.",
            this);
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        Debug.Log("Quit Button Pressed", this);
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ResolveReferences()
    {
        if (playButton == null)
        {
            playButton = FindNamedButton("PlayButton");
        }

        if (quitButton == null)
        {
            quitButton = FindNamedButton("QuitButton");
        }

        if (playButton == null)
        {
            Debug.LogWarning("[StartMenu] PlayButton was not found in StartScene.", this);
        }

        if (quitButton == null)
        {
            Debug.LogWarning("[StartMenu] QuitButton was not found in StartScene.", this);
        }
    }

    private void WireButtons()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(PlayGame);
            playButton.onClick.AddListener(PlayGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    private void UnwireButtons()
    {
        if (playButton != null)
        {
            playButton.onClick.RemoveListener(PlayGame);
        }

        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
        }
    }

    private static Button FindNamedButton(string buttonName)
    {
        Button[] buttons = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] != null && buttons[i].name == buttonName)
            {
                return buttons[i];
            }
        }

        return null;
    }
}
