using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Create
{
    public class UIManager : MonoBehaviour
    {
        VisualElement ui;

        Button createButton;
        Button cancelButton;

        void Awake()
        {
            ui = GetComponent<UIDocument>().rootVisualElement;
        }

        void OnEnable()
        {
            createButton = ui.Q<Button>("CreateButton");
            createButton.clicked += onCreateButtonClicked;

            cancelButton = ui.Q<Button>("CancelButton");
            cancelButton.clicked += onCancelButtonClicked;
        }
        
        void onCreateButtonClicked()
        {
            Debug.Log("Create");
        }

        void onCancelButtonClicked()
        {
            SceneManager.LoadScene(3);
        }
    }
}