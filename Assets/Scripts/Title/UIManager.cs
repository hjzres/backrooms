using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Title
{
    public class UIManager : MonoBehaviour
    {
        VisualElement ui;

        Button playButton;
        Button optionsButton;
        Button quitButton;

        VisualElement optionsOverlay;
        Slider sensSlider;
        Slider volumeSlider;
        Button optionsBackButton;

        void Awake()
        {
            ui = GetComponent<UIDocument>().rootVisualElement;

            // Apply persisted volume as soon as the game reaches the title.
            AudioListener.volume = PlayerPrefs.GetFloat(GameSettings.VolumePrefKey, GameSettings.DefaultVolume);
        }

        void OnEnable()
        {
            playButton = ui.Q<Button>("PlayButton");
            optionsButton = ui.Q<Button>("OptionsButton");
            quitButton = ui.Q<Button>("QuitButton");
            optionsOverlay = ui.Q("OptionsOverlay");
            sensSlider = ui.Q<Slider>("SensSlider");
            volumeSlider = ui.Q<Slider>("VolumeSlider");
            optionsBackButton = ui.Q<Button>("OptionsBackButton");

            playButton.clicked += OnPlayButtonClicked;
            optionsButton.clicked += OnOptionsButtonClicked;
            quitButton.clicked += OnQuitButtonClicked;
            optionsBackButton.clicked += OnOptionsBackClicked;
            sensSlider.RegisterValueChangedCallback(OnSensChanged);
            volumeSlider.RegisterValueChangedCallback(OnVolumeChanged);
        }

        void OnDisable()
        {
            playButton.clicked -= OnPlayButtonClicked;
            optionsButton.clicked -= OnOptionsButtonClicked;
            quitButton.clicked -= OnQuitButtonClicked;
            optionsBackButton.clicked -= OnOptionsBackClicked;
        }

        void OnPlayButtonClicked()
        {
            SceneManager.LoadScene(1);
        }

        void OnOptionsButtonClicked()
        {
            sensSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(GameSettings.SensPrefKey, GameSettings.DefaultSens));
            volumeSlider.SetValueWithoutNotify(PlayerPrefs.GetFloat(GameSettings.VolumePrefKey, GameSettings.DefaultVolume));
            optionsOverlay.style.display = DisplayStyle.Flex;
        }

        void OnOptionsBackClicked()
        {
            optionsOverlay.style.display = DisplayStyle.None;
            PlayerPrefs.Save();
        }

        void OnSensChanged(ChangeEvent<float> evt)
        {
            PlayerPrefs.SetFloat(GameSettings.SensPrefKey, evt.newValue);
        }

        void OnVolumeChanged(ChangeEvent<float> evt)
        {
            AudioListener.volume = evt.newValue;
            PlayerPrefs.SetFloat(GameSettings.VolumePrefKey, evt.newValue);
        }

        void OnQuitButtonClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
