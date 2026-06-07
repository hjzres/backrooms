using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Sessions
{
    public class UIManager : MonoBehaviour
    {
        VisualElement ui;

        Button createButton;
        Button refreshButton;
        Button backButton;

        void Awake()
        {
            ui = GetComponent<UIDocument>().rootVisualElement;
        }

        void OnEnable()
        {
            createButton = ui.Q<Button>("CreateButton");
            createButton.clicked += onCreateButtonClicked;

            refreshButton = ui.Q<Button>("RefreshButton");
            refreshButton.clicked += onRefreshButtonClicked;

            backButton = ui.Q<Button>("BackButton");
            backButton.clicked += onBackButtonClicked;
        }
        
        void onCreateButtonClicked()
        {
            SceneManager.LoadScene(4);
        }

        void onRefreshButtonClicked()
        {
            Debug.Log("Refresh");
        }

        void onBackButtonClicked()
        {
            SceneManager.LoadScene(1);
        }
    }
}