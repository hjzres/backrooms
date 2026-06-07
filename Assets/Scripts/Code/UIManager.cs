using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Code
{
    public class UIManager : MonoBehaviour
    {
        VisualElement ui;

        Button joinButton;
        Button backButton;

        void Awake()
        {
            ui = GetComponent<UIDocument>().rootVisualElement;
        }

        void OnEnable()
        {
            joinButton = ui.Q<Button>("JoinButton");
            joinButton.clicked += onJoinButtonClicked;

            backButton = ui.Q<Button>("BackButton");
            backButton.clicked += onBackButtonClicked;
        }
        
        void onJoinButtonClicked()
        {
            Debug.Log("Join");
        }

        void onBackButtonClicked()
        {
            SceneManager.LoadScene(1);
        }
    }
}