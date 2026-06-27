using System;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Multiplayer;
using Unity.Netcode;

namespace Sessions
{
    public class UIManager : MonoBehaviour
    {
        VisualElement ui;
        ScrollView sessionList;

        Button createButton;
        Button refreshButton;
        Button backButton;

        bool isJoining;
        bool isInitialized;

        void Awake()
        {
            ui = GetComponent<UIDocument>().rootVisualElement;
        }

        void OnEnable()
        {
            createButton = ui.Q<Button>("CreateButton");
            createButton.clicked += OnCreateButtonClicked;

            refreshButton = ui.Q<Button>("RefreshButton");
            refreshButton.clicked += OnRefreshButtonClicked;

            backButton = ui.Q<Button>("BackButton");
            backButton.clicked += OnBackButtonClicked;

            sessionList = ui.Q<ScrollView>();

            InitAndRefresh();
        }

        void OnDisable()
        {
            createButton.clicked -= OnCreateButtonClicked;
            refreshButton.clicked -= OnRefreshButtonClicked;
            backButton.clicked -= OnBackButtonClicked;
        }

        async void InitAndRefresh()
        {
            await InitializeUnityServices();
            await RefreshSessions();
        }

        async Task InitializeUnityServices()
        {
            if (isInitialized) return;

            try
            {
                if (UnityServices.State != ServicesInitializationState.Initialized)
                    await UnityServices.InitializeAsync();

                if (!AuthenticationService.Instance.IsSignedIn)
                {
                    await AuthenticationService.Instance.SignInAnonymouslyAsync();
                    Debug.Log($"Signed in as: {AuthenticationService.Instance.PlayerId}");
                }

                isInitialized = true;
            }
            catch (AuthenticationException)
            {
                if (AuthenticationService.Instance.IsSignedIn)
                    isInitialized = true;
                else
                    Debug.LogError("Authentication failed.");
            }
            catch (Exception e)
            {
                Debug.LogError($"Unity Services init failed: {e}");
            }
        }

        async Task RefreshSessions()
        {
            sessionList.Clear();

            var loadingLabel = new Label("SEARCHING...");
            loadingLabel.style.color = new Color(0.82f, 0.78f, 0.67f, 0.5f);
            loadingLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
            loadingLabel.style.fontSize = 16;
            loadingLabel.style.letterSpacing = 3;
            loadingLabel.style.marginTop = 20;
            sessionList.Add(loadingLabel);

            try
            {
                var queryOptions = new QuerySessionsOptions
                {
                    Count = 20
                };

                var results = await MultiplayerService.Instance.QuerySessionsAsync(queryOptions);
                var sessions = results.Sessions;

                sessionList.Clear();

                if (sessions.Count == 0)
                {
                    var emptyLabel = new Label("NO SESSIONS FOUND");
                    emptyLabel.style.color = new Color(0.82f, 0.78f, 0.67f, 0.4f);
                    emptyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                    emptyLabel.style.fontSize = 16;
                    emptyLabel.style.letterSpacing = 3;
                    emptyLabel.style.marginTop = 20;
                    sessionList.Add(emptyLabel);
                    return;
                }

                foreach (var session in sessions)
                {
                    sessionList.Add(CreateSessionEntry(session));
                }
            }
            catch (Exception e)
            {
                sessionList.Clear();
                var errorLabel = new Label("FAILED TO LOAD SESSIONS");
                errorLabel.style.color = new Color(0.7f, 0.31f, 0.24f);
                errorLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
                errorLabel.style.fontSize = 16;
                errorLabel.style.marginTop = 20;
                sessionList.Add(errorLabel);
                Debug.LogError($"Failed to query sessions: {e}");
            }
        }

        VisualElement CreateSessionEntry(ISessionInfo session)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.justifyContent = Justify.SpaceBetween;
            row.style.alignItems = Align.Center;
            row.style.backgroundColor = new Color(0.16f, 0.14f, 0.1f, 0.6f);
            row.style.borderBottomColor = new Color(0.33f, 0.29f, 0.2f, 0.3f);
            row.style.borderBottomWidth = 1;
            row.style.paddingTop = 12;
            row.style.paddingBottom = 12;
            row.style.paddingLeft = 16;
            row.style.paddingRight = 16;
            row.style.marginBottom = 4;
            row.style.borderTopLeftRadius = 3;
            row.style.borderTopRightRadius = 3;
            row.style.borderBottomLeftRadius = 3;
            row.style.borderBottomRightRadius = 3;

            var info = new VisualElement();

            var idLabel = new Label($"SESSION: {session.Id[..8].ToUpper()}");
            idLabel.style.color = new Color(0.78f, 0.71f, 0.47f);
            idLabel.style.fontSize = 16;
            idLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            idLabel.style.letterSpacing = 2;
            idLabel.style.marginBottom = 4;
            info.Add(idLabel);

            var playersLabel = new Label($"PLAYERS: {session.MaxPlayers} MAX");
            playersLabel.style.color = new Color(0.82f, 0.78f, 0.67f, 0.5f);
            playersLabel.style.fontSize = 13;
            playersLabel.style.letterSpacing = 1;
            info.Add(playersLabel);

            row.Add(info);

            var joinBtn = new Button(() => JoinSession(session.Id)) { text = "JOIN" };
            joinBtn.AddToClassList("button-small");
            joinBtn.AddToClassList("button-accent");
            row.Add(joinBtn);

            return row;
        }

        async void JoinSession(string sessionId)
        {
            if (isJoining) return;
            isJoining = true;

            try
            {
                if (NetworkManager.Singleton == null)
                {
                    Debug.LogError("NetworkManager not found. Make sure NetworkBootstrap is in this scene with the prefab assigned.");
                    isJoining = false;
                    return;
                }

                var options = new JoinSessionOptions();
                ISession session = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, options);

                Debug.Log($"Joined session: {session.Id}");
                Debug.Log($"Session code: {session.Code}");

                if (!NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.StartClient();
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to join session: {e}");
                isJoining = false;
            }
        }

        void OnCreateButtonClicked()
        {
            SceneManager.LoadScene(4);
        }

        void OnRefreshButtonClicked()
        {
            RefreshSessions();
        }

        void OnBackButtonClicked()
        {
            SceneManager.LoadScene(1);
        }
    }
}
