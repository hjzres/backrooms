using UnityEngine;
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
            Debug.Log("Start");
        }

        void OnSessionsButtonClicked()
        {
            Debug.Log("Sessions");
        }

        void OnCodeButtonClicked()
        {
            Debug.Log("Code");
        }
        
        void OnBackButtonClicked()
        {
            Debug.Log("Back");
        }
    }
}