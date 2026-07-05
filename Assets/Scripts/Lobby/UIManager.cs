using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Lobby
{
    public class UIManager : MonoBehaviour
    {
        VisualElement ui;

        Button startButton;
        Button sessionsButton;
        Button codeButton;
        Button backButton;

        void Awake()
        {
            ui = GetComponent<UIDocument>().rootVisualElement;
        }

        void OnEnable()
        {
            startButton = ui.Q<Button>("StartButton");
            startButton.clicked += OnStartButtonClicked;

            sessionsButton = ui.Q<Button>("SessionsButton");
            sessionsButton.clicked += OnSessionsButtonClicked;

            codeButton = ui.Q<Button>("CodeButton");
            codeButton.clicked += OnCodeButtonClicked;

            backButton = ui.Q<Button>("BackButton");
            backButton.clicked += OnBackButtonClicked;
        }

        void OnStartButtonClicked()
        {
            // Single player: the Session scene's AutoStartHost spins up a
            // local host and the player spawns straight into the elevator.
            GameSettings.SinglePlayer = true;
            SceneManager.LoadScene(5);
        }

        void OnSessionsButtonClicked()
        {
            SceneManager.LoadScene(3);
        }

        void OnCodeButtonClicked()
        {
            SceneManager.LoadScene(2);
        }
        
        void OnBackButtonClicked()
        {
            SceneManager.LoadScene(0);
        }
    }
}