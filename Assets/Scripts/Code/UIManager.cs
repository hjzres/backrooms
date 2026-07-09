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

        const int CodeLength = 6;

        Button joinButton;
        Button backButton;
        TextField codeField;
        Label[] slots;

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

            codeField = ui.Q<TextField>("CodeField");
            codeField.RegisterValueChangedCallback(OnCodeChanged);
            codeField.RegisterCallback<FocusInEvent>(OnCodeFocusChanged);
            codeField.RegisterCallback<FocusOutEvent>(OnCodeFocusChanged);
            codeField.RegisterCallback<KeyDownEvent>(OnCodeKeyDown);

            slots = new Label[CodeLength];
            for (int i = 0; i < CodeLength; i++)
                slots[i] = ui.Q<Label>($"Slot{i}");

            UpdateSlots();
            codeField.schedule.Execute(() => codeField.Focus());
        }

        void OnDisable()
        {
            joinButton.clicked -= OnJoinButtonClicked;
            backButton.clicked -= OnBackButtonClicked;

            codeField.UnregisterValueChangedCallback(OnCodeChanged);
            codeField.UnregisterCallback<FocusInEvent>(OnCodeFocusChanged);
            codeField.UnregisterCallback<FocusOutEvent>(OnCodeFocusChanged);
            codeField.UnregisterCallback<KeyDownEvent>(OnCodeKeyDown);
        }

        void OnCodeChanged(ChangeEvent<string> evt)
        {
            string sanitized = "";
            foreach (char c in evt.newValue.ToUpperInvariant())
            {
                if (sanitized.Length >= CodeLength) break;
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9'))
                    sanitized += c;
            }

            codeField.SetValueWithoutNotify(sanitized);
            UpdateSlots();
        }

        void OnCodeFocusChanged(EventBase evt) => UpdateSlots();

        void OnCodeKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                OnJoinButtonClicked();
        }

        void UpdateSlots()
        {
            string code = codeField.value ?? "";
            bool focused = codeField.panel != null && codeField.panel.focusController.focusedElement == codeField;

            for (int i = 0; i < CodeLength; i++)
            {
                slots[i].text = i < code.Length ? code[i].ToString() : "";
                bool active = focused && i == Mathf.Min(code.Length, CodeLength - 1);
                slots[i].EnableInClassList("code-slot-active", active);
            }

            if (!isJoining)
                joinButton.SetEnabled(code.Length == CodeLength);
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

            string code = codeField.value;
            if (string.IsNullOrEmpty(code) || code.Length != CodeLength)
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
                isJoining = false;
                UpdateSlots();
            }
        }

        void OnBackButtonClicked()
        {
            SceneManager.LoadScene(1);
        }
    }
}
