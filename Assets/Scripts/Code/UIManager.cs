using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Netcode;

namespace Code
{
    public class UIManager : MonoBehaviour
    {
        VisualElement ui;

        Button joinButton;
        Button backButton;
        IntegerField codeField;

        bool isJoining;

        async void Awake()
        {
            ui = GetComponent<UIDocument>().rootVisualElement;
            await InitializeUnityServices();
        }

        void OnEnable()
        {
            joinButton = ui.Q<Button>("JoinButton");
            joinButton.clicked += OnJoinButtonClicked;

            backButton = ui.Q<Button>("BackButton");
            backButton.clicked += OnBackButtonClicked;

            codeField = ui.Q<IntegerField>();
        }

        void OnDisable()
        {
            joinButton.clicked -= OnJoinButtonClicked;
            backButton.clicked -= OnBackButtonClicked;
        }

        async Task InitializeUnityServices()
        {
            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Unity Services init failed: {e}");
            }
        }

        async void OnJoinButtonClicked()
        {
            if (isJoining) return;

            string code = codeField.value.ToString();
            if (string.IsNullOrEmpty(code) || code == "0")
            {
                Debug.LogWarning("Please enter a valid session code.");
                return;
            }

            isJoining = true;
            joinButton.SetEnabled(false);

            try
            {
                await InitializeUnityServices();

                var options = new JoinSessionOptions();
                GameSettings.SinglePlayer = false;
                ISession session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code, options);

                Debug.Log($"Joined session by code: {session.Id}");

                if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.StartClient();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to join session: {e}");
                joinButton.SetEnabled(true);
                isJoining = false;
            }
        }

        void OnBackButtonClicked()
        {
            SceneManager.LoadScene(1);
        }
    }
}
