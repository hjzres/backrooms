using System;
using System.Linq;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace Session
{
    public class SessionHUD : MonoBehaviour
    {
        const int MenuSceneIndex = 1;

        Label codeLabel;
        VisualElement staminaBar;
        VisualElement staminaFill;
        VisualElement crosshair;

        VisualElement camOverlay;
        VisualElement recDot;
        Label camTimeLabel;
        float camTimer;

        VisualElement inventoryOverlay;
        VisualElement inventoryGrid;
        Label inventoryCount;

        VisualElement pauseOverlay;
        VisualElement pausePanel;
        VisualElement optionsPanel;
        Button resumeButton;
        Button optionsButton;
        Button quitButton;
        Button optionsBackButton;
        Slider sensSlider;
        Slider volumeSlider;
        Label playersCount;
        VisualElement playersList;

        PlayerMovement localMovement;
        PlayerInventory subscribedInventory;
        float nextCodePoll;
        float nextPlayersPoll;
        bool codeResolved;
        bool isQuitting;

        void OnEnable()
        {
            var root = GetComponent<UIDocument>().rootVisualElement;

            codeLabel = root.Q<Label>("CodeLabel");
            staminaBar = root.Q("StaminaBar");
            staminaFill = root.Q("StaminaFill");
            crosshair = root.Q("Crosshair");

            camOverlay = root.Q("CamOverlay");
            recDot = root.Q("RecDot");
            camTimeLabel = root.Q<Label>("CamTime");

            inventoryOverlay = root.Q("InventoryOverlay");
            inventoryGrid = root.Q("InventoryGrid");
            inventoryCount = root.Q<Label>("InventoryCount");

            pauseOverlay = root.Q("PauseOverlay");
            pausePanel = root.Q("PausePanel");
            optionsPanel = root.Q("OptionsPanel");
            resumeButton = root.Q<Button>("ResumeButton");
            optionsButton = root.Q<Button>("OptionsButton");
            quitButton = root.Q<Button>("QuitButton");
            optionsBackButton = root.Q<Button>("OptionsBackButton");
            sensSlider = root.Q<Slider>("SensSlider");
            volumeSlider = root.Q<Slider>("VolumeSlider");
            playersCount = root.Q<Label>("PlayersCount");
            playersList = root.Q("PlayersList");

            resumeButton.clicked += OnResumeClicked;
            optionsButton.clicked += OnOptionsClicked;
            quitButton.clicked += OnQuitClicked;
            optionsBackButton.clicked += OnOptionsBackClicked;
            sensSlider.RegisterValueChangedCallback(OnSensChanged);
            volumeSlider.RegisterValueChangedCallback(OnVolumeChanged);

            LocalUi.InventoryToggled += OnInventoryToggled;
            LocalUi.PauseToggled += OnPauseToggled;
            Player.PlayerCam.CamcorderToggled += OnCamcorderToggled;

            AudioListener.volume = PlayerPrefs.GetFloat(GameSettings.VolumePrefKey, GameSettings.DefaultVolume);

            // Single player has no session code to show.
            if (GameSettings.SinglePlayer)
            {
                var codePanel = root.Q("CodePanel");
                if (codePanel != null)
                    codePanel.style.display = DisplayStyle.None;
                codeResolved = true;
            }

            OnInventoryToggled(LocalUi.InventoryOpen);
            OnPauseToggled(LocalUi.PauseOpen);
            OnCamcorderToggled(Player.PlayerCam.CamcorderOn);
        }

        void OnDisable()
        {
            resumeButton.clicked -= OnResumeClicked;
            optionsButton.clicked -= OnOptionsClicked;
            quitButton.clicked -= OnQuitClicked;
            optionsBackButton.clicked -= OnOptionsBackClicked;

            LocalUi.InventoryToggled -= OnInventoryToggled;
            LocalUi.PauseToggled -= OnPauseToggled;
            Player.PlayerCam.CamcorderToggled -= OnCamcorderToggled;
            UnsubscribeInventory();
        }

        void Update()
        {
            HandleEscape();
            UpdateCode();
            UpdateStamina();
            UpdateCamcorder();

            if (LocalUi.PauseOpen && Time.unscaledTime >= nextPlayersPoll)
            {
                nextPlayersPoll = Time.unscaledTime + 0.5f;
                RefreshPlayers();
            }
        }

        void HandleEscape()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame || isQuitting)
                return;

            // Escape closes the inventory first; otherwise it toggles the menu.
            if (LocalUi.InventoryOpen)
                LocalUi.SetInventory(false);
            else
                LocalUi.SetPause(!LocalUi.PauseOpen);
        }

        // ── Camcorder ──

        void OnCamcorderToggled(bool on)
        {
            if (camOverlay == null) return;

            camOverlay.style.display = on ? DisplayStyle.Flex : DisplayStyle.None;

            // The viewfinder brackets replace the crosshair.
            if (crosshair != null)
                crosshair.style.display = on ? DisplayStyle.None : DisplayStyle.Flex;

            if (on)
                camTimer = 0f;
        }

        void UpdateCamcorder()
        {
            if (camOverlay == null || camOverlay.style.display == DisplayStyle.None)
                return;

            camTimer += Time.deltaTime;
            var t = TimeSpan.FromSeconds(camTimer);
            camTimeLabel.text = $"{(int)t.TotalHours:00}:{t.Minutes:00}:{t.Seconds:00}";

            // Classic camcorder REC blink.
            recDot.style.opacity = Time.unscaledTime % 1f < 0.6f ? 1f : 0f;
        }

        // ── Pause menu ──

        void OnPauseToggled(bool open)
        {
            if (pauseOverlay == null) return;

            pauseOverlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            if (open)
            {
                ShowPauseMain();
                RefreshPlayers();
                nextPlayersPoll = Time.unscaledTime + 0.5f;

                sensSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(GameSettings.SensPrefKey, GameSettings.DefaultSens));
                volumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(GameSettings.VolumePrefKey, GameSettings.DefaultVolume));
            }
        }

        void OnResumeClicked()
        {
            LocalUi.SetPause(false);
        }

        void OnOptionsClicked()
        {
            pausePanel.style.display = DisplayStyle.None;
            optionsPanel.style.display = DisplayStyle.Flex;
        }

        void OnOptionsBackClicked()
        {
            ShowPauseMain();
        }

        void ShowPauseMain()
        {
            pausePanel.style.display = DisplayStyle.Flex;
            optionsPanel.style.display = DisplayStyle.None;
        }

        void OnSensChanged(ChangeEvent<float> evt)
        {
            PlayerPrefs.SetFloat(GameSettings.SensPrefKey, evt.newValue);

            var cam = LocalPlayerComponent<Player.PlayerCam>();
            if (cam != null)
                cam.Sensitivity = evt.newValue;
        }

        void OnVolumeChanged(ChangeEvent<float> evt)
        {
            AudioListener.volume = evt.newValue;
            PlayerPrefs.SetFloat(GameSettings.VolumePrefKey, evt.newValue);
        }

        async void OnQuitClicked()
        {
            if (isQuitting) return;
            isQuitting = true;

            try
            {
                var session = GetSession();
                if (session != null)
                {
                    if (session.IsHost)
                        await session.AsHost().DeleteAsync();
                    else
                        await session.LeaveAsync();
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"Failed to leave session cleanly: {e}");
            }

            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.Shutdown();

            LocalUi.Clear();
            SceneManager.LoadScene(MenuSceneIndex);
        }

        void RefreshPlayers()
        {
            if (playersList == null) return;

            var players = FindObjectsByType<PlayerMovement>(FindObjectsSortMode.None)
                .OrderBy(p => p.OwnerClientId)
                .ToArray();

            var session = GetSession();
            playersCount.text = session != null
                ? $"{players.Length}/{session.MaxPlayers}"
                : players.Length.ToString();

            playersList.Clear();

            foreach (var player in players)
            {
                var row = new VisualElement();
                row.AddToClassList("player-row");

                var name = new Label($"PLAYER {player.OwnerClientId + 1}");
                name.AddToClassList("player-name");
                row.Add(name);

                if (player.IsOwner)
                {
                    var you = new Label("YOU");
                    you.AddToClassList("player-you");
                    row.Add(you);
                }

                playersList.Add(row);
            }
        }

        // ── Inventory ──

        void OnInventoryToggled(bool open)
        {
            if (inventoryOverlay == null) return;

            inventoryOverlay.style.display = open ? DisplayStyle.Flex : DisplayStyle.None;

            if (open)
            {
                RebuildInventory();

                if (PlayerInventory.Local != null && subscribedInventory != PlayerInventory.Local)
                {
                    UnsubscribeInventory();
                    subscribedInventory = PlayerInventory.Local;
                    subscribedInventory.Changed += RebuildInventory;
                }
            }
        }

        void UnsubscribeInventory()
        {
            if (subscribedInventory != null)
            {
                subscribedInventory.Changed -= RebuildInventory;
                subscribedInventory = null;
            }
        }

        void RebuildInventory()
        {
            if (inventoryGrid == null) return;

            inventoryGrid.Clear();

            var inventory = PlayerInventory.Local;
            int used = inventory != null ? inventory.Items.Count : 0;
            inventoryCount.text = $"{used}/{PlayerInventory.SlotCount}";

            for (int i = 0; i < PlayerInventory.SlotCount; i++)
            {
                var slot = new VisualElement();
                slot.AddToClassList("inv-slot");

                string item = inventory != null && i < inventory.Items.Count ? inventory.Items[i] : null;

                var label = new Label(item ?? "EMPTY");
                label.AddToClassList(item != null ? "inv-slot-item" : "inv-slot-empty");
                slot.Add(label);

                if (item != null)
                    slot.AddToClassList("inv-slot-filled");

                inventoryGrid.Add(slot);
            }
        }

        // ── HUD ──

        void UpdateCode()
        {
            if (codeResolved || codeLabel == null || Time.unscaledTime < nextCodePoll)
                return;

            nextCodePoll = Time.unscaledTime + 0.5f;

            string code = GetSession()?.Code;
            if (!string.IsNullOrEmpty(code))
            {
                codeLabel.text = code.ToUpperInvariant();
                codeResolved = true;
            }
        }

        void UpdateStamina()
        {
            if (staminaBar == null || staminaFill == null)
                return;

            if (localMovement == null)
            {
                localMovement = LocalPlayerComponent<PlayerMovement>();
                if (localMovement == null)
                {
                    staminaBar.style.opacity = 0f;
                    return;
                }
            }

            float stamina = localMovement.Stamina01;
            staminaFill.style.width = Length.Percent(stamina * 100f);
            staminaBar.style.opacity = stamina >= 0.999f ? 0f : 1f;
        }

        static T LocalPlayerComponent<T>() where T : Component
        {
            var nm = NetworkManager.Singleton;
            var playerObject = nm != null && nm.LocalClient != null ? nm.LocalClient.PlayerObject : null;
            return playerObject != null ? playerObject.GetComponent<T>() : null;
        }

        static ISession GetSession()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                return null;

            return MultiplayerService.Instance.Sessions.Values.FirstOrDefault();
        }
    }
}
