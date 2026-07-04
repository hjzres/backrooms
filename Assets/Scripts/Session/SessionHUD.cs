using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Unity.Services.Core;
using Unity.Services.Multiplayer;

namespace Session
{
    public class SessionHUD : MonoBehaviour
    {
        Label codeLabel;
        float nextPollTime;

        void OnEnable()
        {
            codeLabel = GetComponent<UIDocument>().rootVisualElement.Q<Label>("CodeLabel");
        }

        void Update()
        {
            if (codeLabel == null || Time.unscaledTime < nextPollTime)
                return;

            nextPollTime = Time.unscaledTime + 0.5f;

            string code = GetJoinCode();
            if (!string.IsNullOrEmpty(code))
            {
                codeLabel.text = code.ToUpperInvariant();
                enabled = false;
            }
        }

        static string GetJoinCode()
        {
            if (UnityServices.State != ServicesInitializationState.Initialized)
                return null;

            var session = MultiplayerService.Instance.Sessions.Values.FirstOrDefault();
            return session?.Code;
        }
    }
}
