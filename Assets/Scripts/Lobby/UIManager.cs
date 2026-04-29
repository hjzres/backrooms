using UnityEditor.SearchService;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Lobby
{
    public class UIManager : MonoBehaviour
    {
        public VisualElement ui;

        public Button startButton;
        public Button optionsButton;
        public Button quitButton;

        void Awake()
        {
            ui = GetComponent<UIDocument>().rootVisualElement;
        }

        void OnEnable()
        {
            startButton = ui.Q<Button>("StartButton");
            startButton.clicked += onStartButtonClicked;

            optionsButton = ui.Q<Button>("OptionsButton");
            optionsButton.clicked += onOptionsButtonClicked;

            quitButton = ui.Q<Button>("QuitButton");
            quitButton.clicked += onQuitButtonClicked;
        }

        void onStartButtonClicked()
        {
            Debug.Log("Start");
            SceneManager.LoadScene(1);
        }

        void onOptionsButtonClicked()
        {
            Debug.Log("Options");
        }

        void onQuitButtonClicked()
        {
            Debug.Log("Quit");
        }
    }
}