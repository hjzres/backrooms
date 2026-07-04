using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Netcode;

namespace Create
{
    public class UIManager : MonoBehaviour
    {
        const int MinPlayers = 2;
        const int MaxPlayersLimit = 6;
        const int MinPasswordLength = 8;

        VisualElement ui;

        Button createButton;
        Button cancelButton;
        Button upButton;
        Button downButton;
        Label maxPlayersLabel;
        TextField nameField;
        Toggle privateToggle;
        Label errorLabel;

        VisualElement passwordOverlay;
        TextField passwordField;
        Label passwordError;
        Button passwordConfirmButton;
        Button passwordCancelButton;
        Button passwordShowButton;

        int maxPlayers = MaxPlayersLimit;
        ISession currentSession;
        bool isCreatingSession;

        async void Awake()
        {
            ui = GetComponent<UIDocument>().rootVisualElement;

            await InitializeUnityServices();
        }

        void OnEnable()
        {
            createButton = ui.Q<Button>("CreateButton");
            cancelButton = ui.Q<Button>("CancelButton");
            upButton = ui.Q<Button>("UpButton");
            downButton = ui.Q<Button>("DownButton");
            maxPlayersLabel = ui.Q("MaxPlayers").Q<Label>("Label");
            nameField = ui.Q<TextField>("NameField");
            privateToggle = ui.Q<Toggle>("Private");
            errorLabel = ui.Q<Label>("ErrorLabel");

            passwordOverlay = ui.Q("PasswordOverlay");
            passwordField = ui.Q<TextField>("PasswordField");
            passwordError = ui.Q<Label>("PasswordError");
            passwordConfirmButton = ui.Q<Button>("PasswordConfirmButton");
            passwordCancelButton = ui.Q<Button>("PasswordCancelButton");
            passwordShowButton = ui.Q<Button>("PasswordShowButton");

            createButton.clicked += OnCreateButtonClicked;
            cancelButton.clicked += OnCancelButtonClicked;
            upButton.clicked += OnUpButtonClicked;
            downButton.clicked += OnDownButtonClicked;
            passwordConfirmButton.clicked += OnPasswordConfirmClicked;
            passwordCancelButton.clicked += OnPasswordCancelClicked;
            passwordShowButton.clicked += OnPasswordShowClicked;

            // The shared input style is tuned for the code entry field; tone it
            // down for a lobby name.
            var nameInput = nameField.Q(className: "unity-text-field__input");
            if (nameInput != null)
            {
                nameInput.style.fontSize = 20;
                nameInput.style.letterSpacing = 2;
            }

            UpdateMaxPlayersLabel();
        }

        void OnDisable()
        {
            createButton.clicked -= OnCreateButtonClicked;
            cancelButton.clicked -= OnCancelButtonClicked;
            upButton.clicked -= OnUpButtonClicked;
            downButton.clicked -= OnDownButtonClicked;
            passwordConfirmButton.clicked -= OnPasswordConfirmClicked;
            passwordCancelButton.clicked -= OnPasswordCancelClicked;
            passwordShowButton.clicked -= OnPasswordShowClicked;
        }

        void OnPasswordShowClicked()
        {
            passwordField.isPasswordField = !passwordField.isPasswordField;
            passwordShowButton.text = passwordField.isPasswordField ? "SHOW" : "HIDE";
        }

        async Task InitializeUnityServices()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                {
                    await UnityServices.InitializeAsync();
                }

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Unity Services initialization failed: {e}");
            }
        }

        void OnUpButtonClicked()
        {
            maxPlayers = Mathf.Min(maxPlayers + 1, MaxPlayersLimit);
            UpdateMaxPlayersLabel();
        }

        void OnDownButtonClicked()
        {
            maxPlayers = Mathf.Max(maxPlayers - 1, MinPlayers);
            UpdateMaxPlayersLabel();
        }

        void UpdateMaxPlayersLabel()
        {
            maxPlayersLabel.text = maxPlayers.ToString();
            downButton.SetEnabled(maxPlayers > MinPlayers);
            upButton.SetEnabled(maxPlayers < MaxPlayersLimit);
        }

        void ShowError(string message)
        {
            errorLabel.text = message ?? "";
            errorLabel.style.display = string.IsNullOrEmpty(message) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        void OnCreateButtonClicked()
        {
            if (isCreatingSession)
                return;

            string sessionName = nameField.value?.Trim();
            if (string.IsNullOrEmpty(sessionName))
            {
                ShowError("ENTER A LOBBY NAME");
                return;
            }

            ShowError(null);

            if (privateToggle.value)
            {
                passwordField.value = "";
                passwordError.text = "";
                passwordField.isPasswordField = true;
                passwordShowButton.text = "SHOW";
                passwordOverlay.style.display = DisplayStyle.Flex;
                passwordField.Focus();
            }
            else
            {
                CreateSession(sessionName, null);
            }
        }

        void OnPasswordConfirmClicked()
        {
            string password = passwordField.value;
            if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            {
                passwordError.text = "AT LEAST 8 CHARACTERS REQUIRED";
                return;
            }

            passwordOverlay.style.display = DisplayStyle.None;
            CreateSession(nameField.value.Trim(), password);
        }

        void OnPasswordCancelClicked()
        {
            passwordOverlay.style.display = DisplayStyle.None;
        }

        async void CreateSession(string sessionName, string password)
        {
            isCreatingSession = true;
            createButton.SetEnabled(false);

            try
            {
                await InitializeUnityServices();

                var options = new SessionOptions
                {
                    Name = sessionName,
                    MaxPlayers = maxPlayers,
                    Password = string.IsNullOrEmpty(password) ? null : password
                }.WithRelayNetwork();

                currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

                Debug.Log($"Session created!");
                Debug.Log($"Session ID: {currentSession.Id}");
                Debug.Log($"Join Code: {currentSession.Code}");

                if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.StartHost();
                }

                string sceneName = System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(5));
                NetworkManager.Singleton.SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create session: {e}");
                ShowError("FAILED TO CREATE SESSION");
                createButton.SetEnabled(true);
                isCreatingSession = false;
            }
        }

        void OnCancelButtonClicked()
        {
            SceneManager.LoadScene(3);
        }
    }
}
