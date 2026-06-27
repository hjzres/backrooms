using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;

namespace Create
{
    public class UIManager : MonoBehaviour
    {
        VisualElement ui;

        Button createButton;
        Button cancelButton;

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

            createButton.clicked += OnCreateButtonClicked;
            cancelButton.clicked += OnCancelButtonClicked;
        }

        void OnDisable()
        {
            createButton.clicked -= OnCreateButtonClicked;
            cancelButton.clicked -= OnCancelButtonClicked;
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

        async void OnCreateButtonClicked()
        {
            if (isCreatingSession)
                return;

            isCreatingSession = true;
            createButton.SetEnabled(false);

            try
            {
                await InitializeUnityServices();

                var options = new SessionOptions
                {
                    MaxPlayers = 2
                }.WithRelayNetwork();

                currentSession = await MultiplayerService.Instance.CreateSessionAsync(options);

                Debug.Log($"Session created!");
                Debug.Log($"Session ID: {currentSession.Id}");
                Debug.Log($"Join Code: {currentSession.Code}");

                SceneManager.LoadScene(5);
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to create session: {e}");
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