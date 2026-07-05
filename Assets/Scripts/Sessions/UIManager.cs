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

        VisualElement passwordOverlay;
        Label passwordTarget;
        TextField passwordField;
        Label passwordError;
        Button passwordJoinButton;
        Button passwordCancelButton;
        Button passwordShowButton;

        string pendingSessionId;

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

            passwordOverlay = ui.Q("PasswordOverlay");
            passwordTarget = ui.Q<Label>("PasswordTarget");
            passwordField = ui.Q<TextField>("PasswordField");
            passwordError = ui.Q<Label>("PasswordError");
            passwordJoinButton = ui.Q<Button>("PasswordJoinButton");
            passwordCancelButton = ui.Q<Button>("PasswordCancelButton");
            passwordShowButton = ui.Q<Button>("PasswordShowButton");

            passwordJoinButton.clicked += OnPasswordJoinClicked;
            passwordCancelButton.clicked += OnPasswordCancelClicked;
            passwordShowButton.clicked += OnPasswordShowClicked;

            sessionList = ui.Q<ScrollView>();

            InitAndRefresh();
        }

        void OnDisable()
        {
            createButton.clicked -= OnCreateButtonClicked;
            refreshButton.clicked -= OnRefreshButtonClicked;
            backButton.clicked -= OnBackButtonClicked;
            passwordJoinButton.clicked -= OnPasswordJoinClicked;
            passwordCancelButton.clicked -= OnPasswordCancelClicked;
            passwordShowButton.clicked -= OnPasswordShowClicked;
        }

        void OnPasswordShowClicked()
        {
            passwordField.isPasswordField = !passwordField.isPasswordField;
            passwordShowButton.text = passwordField.isPasswordField ? "SHOW" : "HIDE";
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

            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.marginBottom = 4;

            string title = string.IsNullOrEmpty(session.Name)
                ? $"SESSION: {session.Id[..8].ToUpper()}"
                : session.Name.ToUpper();

            var nameLabel = new Label(title);
            nameLabel.style.color = new Color(0.78f, 0.71f, 0.47f);
            nameLabel.style.fontSize = 16;
            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            nameLabel.style.letterSpacing = 2;
            titleRow.Add(nameLabel);

            if (session.HasPassword)
                titleRow.Add(CreateLockIcon());

            info.Add(titleRow);

            int currentPlayers = Mathf.Max(0, session.MaxPlayers - session.AvailableSlots);
            var playersLabel = new Label($"PLAYERS: {currentPlayers}/{session.MaxPlayers}");
            playersLabel.style.color = new Color(0.82f, 0.78f, 0.67f, 0.5f);
            playersLabel.style.fontSize = 13;
            playersLabel.style.letterSpacing = 1;
            info.Add(playersLabel);

            row.Add(info);

            var joinBtn = new Button(() => OnJoinClicked(session)) { text = "JOIN" };
            joinBtn.AddToClassList("button-small");
            joinBtn.AddToClassList("button-accent");
            row.Add(joinBtn);

            return row;
        }

        static VisualElement CreateLockIcon()
        {
            var gold = new Color(0.78f, 0.71f, 0.47f);

            var icon = new VisualElement();
            icon.style.marginLeft = 10;
            icon.style.alignItems = Align.Center;

            var shackle = new VisualElement();
            shackle.style.width = 10;
            shackle.style.height = 6;
            shackle.style.borderTopWidth = 2;
            shackle.style.borderLeftWidth = 2;
            shackle.style.borderRightWidth = 2;
            shackle.style.borderTopColor = gold;
            shackle.style.borderLeftColor = gold;
            shackle.style.borderRightColor = gold;
            shackle.style.borderTopLeftRadius = 5;
            shackle.style.borderTopRightRadius = 5;
            icon.Add(shackle);

            var body = new VisualElement();
            body.style.width = 14;
            body.style.height = 10;
            body.style.backgroundColor = gold;
            body.style.borderTopLeftRadius = 2;
            body.style.borderTopRightRadius = 2;
            body.style.borderBottomLeftRadius = 2;
            body.style.borderBottomRightRadius = 2;
            icon.Add(body);

            return icon;
        }

        void OnJoinClicked(ISessionInfo session)
        {
            if (isJoining) return;

            if (session.HasPassword)
            {
                pendingSessionId = session.Id;
                passwordTarget.text = string.IsNullOrEmpty(session.Name)
                    ? session.Id[..8].ToUpper()
                    : session.Name.ToUpper();
                passwordField.value = "";
                passwordError.text = "";
                passwordField.isPasswordField = true;
                passwordShowButton.text = "SHOW";
                passwordOverlay.style.display = DisplayStyle.Flex;
                passwordField.Focus();
            }
            else
            {
                JoinSession(session.Id, null);
            }
        }

        void OnPasswordJoinClicked()
        {
            string password = passwordField.value;
            if (string.IsNullOrEmpty(password))
            {
                passwordError.text = "ENTER THE PASSWORD";
                return;
            }

            JoinSession(pendingSessionId, password);
        }

        void OnPasswordCancelClicked()
        {
            passwordOverlay.style.display = DisplayStyle.None;
            pendingSessionId = null;
        }

        async void JoinSession(string sessionId, string password)
        {
            if (isJoining) return;
            isJoining = true;
            passwordError.text = "";

            try
            {
                if (NetworkManager.Singleton == null)
                {
                    Debug.LogError("NetworkManager not found. Make sure NetworkBootstrap is in this scene with the prefab assigned.");
                    isJoining = false;
                    return;
                }

                var options = new JoinSessionOptions();
                if (!string.IsNullOrEmpty(password))
                    options.Password = password;

                GameSettings.SinglePlayer = false;
                ISession session = await MultiplayerService.Instance.JoinSessionByIdAsync(sessionId, options);

                Debug.Log($"Joined session: {session.Id}");
                Debug.Log($"Session code: {session.Code}");

                passwordOverlay.style.display = DisplayStyle.None;

                if (!NetworkManager.Singleton.IsListening)
                {
                    NetworkManager.Singleton.StartClient();
                }
            }
            catch (SessionException e) when (!string.IsNullOrEmpty(password))
            {
                passwordError.text = e.Error == SessionError.Forbidden
                    ? "WRONG PASSWORD"
                    : "FAILED TO JOIN SESSION";
                Debug.LogError($"Failed to join session: {e}");
                isJoining = false;
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
