using System;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Player
{
    public class PlayerCam : NetworkBehaviour
    {
        [SerializeField] Camera cam;
        [SerializeField] private float sens = 5f;

        public float Sensitivity
        {
            get => sens;
            set => sens = value;
        }

        [Header("Feel")]
        [SerializeField] float standHeight = 0.497f;
        [SerializeField] float crouchHeight = 0.05f;
        [SerializeField] float baseFov = 60f;
        [SerializeField] float sprintFov = 68f;
        [SerializeField] float bobFrequency = 9f;
        [SerializeField] float bobAmplitude = 0.035f;

        [Header("Camcorder")]
        [SerializeField] float camcorderFov = 52f;
        [SerializeField] float swaySpeed = 1.4f;
        [SerializeField] float swayAmplitude = 1.1f;
        [SerializeField] float jitterSpeed = 11f;
        [SerializeField] float jitterAmplitude = 0.25f;
        [SerializeField] float rollAmplitude = 0.9f;

        // Local-only camcorder state; only the owning player's PlayerCam runs.
        public static bool CamcorderOn { get; private set; }
        public static event Action<bool> CamcorderToggled;

        private float _xRotation, _yRotation;
        private float _camHeight;
        private float _bobTimer;
        private PlayerInput _playerInput;
        private InputAction _deltaMouse;
        private PlayerMovement _movement;
        private Rigidbody _rb;

        private bool _camcorder;
        private float _camcorderWeight;
        private GameObject _volumeObject;
        private Volume _camcorderVolume;
        private VolumeProfile _camcorderProfile;
        private UniversalAdditionalCameraData _camData;
        private bool _basePostProcessing;
        private bool _baseDepthTexture;

        public override void OnNetworkSpawn()
        {
            if (!IsOwner)
            {
                if (cam != null)
                    cam.gameObject.SetActive(false);

                AudioListener listener = GetComponentInChildren<AudioListener>(true);
                if (listener != null)
                    listener.enabled = false;

                enabled = false;
                return;
            }

            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            _movement = GetComponent<PlayerMovement>();
            _rb = GetComponent<Rigidbody>();
            _camHeight = standHeight;
            _yRotation = transform.eulerAngles.y;
            sens = PlayerPrefs.GetFloat(GameSettings.SensPrefKey, sens);

            _playerInput = GetComponent<PlayerInput>();
            _playerInput.enabled = true;
            _deltaMouse = _playerInput.actions["Camera"];
            _deltaMouse.Enable();

            if (cam != null)
            {
                cam.gameObject.SetActive(true);
                cam.enabled = true;
                cam.fieldOfView = baseFov;

                _camData = cam.GetUniversalAdditionalCameraData();
                _basePostProcessing = _camData.renderPostProcessing;
                _baseDepthTexture = _camData.requiresDepthTexture;
            }

            SetupCamcorderVolume();
        }

        void OnDisable()
        {
            if (_deltaMouse != null)
                _deltaMouse.Disable();
        }

        public override void OnDestroy()
        {
            if (IsOwner)
                SetCamcorder(false);

            if (_volumeObject != null)
                Destroy(_volumeObject);
            if (_camcorderProfile != null)
                Destroy(_camcorderProfile);

            base.OnDestroy();
        }

        void Update()
        {
            if (_deltaMouse == null || cam == null)
                return;

            if (!LocalUi.AnyOpen)
            {
                var keyboard = Keyboard.current;
                if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
                    SetCamcorder(!_camcorder);

                Vector2 mouse = _deltaMouse.ReadValue<Vector2>();

                // Mouse deltas are already per-frame; scaling by deltaTime would
                // make sensitivity depend on frame rate.
                _yRotation += mouse.x * sens * 0.02f;
                _xRotation = Mathf.Clamp(_xRotation - mouse.y * sens * 0.02f, -90f, 90f);
            }

            UpdateCamcorder();

            Vector3 shake = CamcorderShake();
            transform.rotation = Quaternion.Euler(0, _yRotation, 0);
            cam.transform.rotation = Quaternion.Euler(_xRotation + shake.x, _yRotation + 90f + shake.y, shake.z);

            UpdateCameraFeel();
        }

        void UpdateCameraFeel()
        {
            if (cam == null || _movement == null)
                return;

            float targetHeight = _movement.IsCrouching ? crouchHeight : standHeight;
            _camHeight = Mathf.Lerp(_camHeight, targetHeight, 12f * Time.deltaTime);

            float speed = 0f;
            if (_rb != null)
                speed = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z).magnitude;

            float bob = 0f;
            if (_movement.IsGrounded && speed > 0.5f)
            {
                _bobTimer += Time.deltaTime * bobFrequency * Mathf.Clamp(speed / 5f, 0.6f, 1.8f);
                bob = Mathf.Sin(_bobTimer) * bobAmplitude;
            }
            else
            {
                _bobTimer = 0f;
            }

            cam.transform.localPosition = new Vector3(0f, _camHeight + bob, 0f);

            float targetFov = _movement.IsSprinting ? sprintFov : baseFov;
            // The camcorder is slightly zoomed in, like looking through a viewfinder.
            targetFov = Mathf.Lerp(targetFov, camcorderFov, _camcorderWeight);
            cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 8f * Time.deltaTime);
        }

        // ── Camcorder mode ──

        void SetCamcorder(bool on)
        {
            if (_camcorder == on) return;
            _camcorder = on;
            CamcorderOn = on;
            CamcorderToggled?.Invoke(on);
        }

        void UpdateCamcorder()
        {
            _camcorderWeight = Mathf.MoveTowards(_camcorderWeight, _camcorder ? 1f : 0f, 4f * Time.deltaTime);

            if (_camcorderVolume != null)
                _camcorderVolume.weight = _camcorderWeight;

            if (_camData != null)
            {
                bool active = _camcorderWeight > 0.001f;
                _camData.renderPostProcessing = _basePostProcessing || active;
                // Gaussian depth of field needs the depth texture to blur by distance.
                _camData.requiresDepthTexture = _baseDepthTexture || active;
            }
        }

        // Handheld shake: a slow drifting sway plus a fine fast jitter, both
        // driven by Perlin noise so the motion stays smooth and aimless.
        Vector3 CamcorderShake()
        {
            if (_camcorderWeight <= 0.001f)
                return Vector3.zero;

            float t = Time.time;

            float sway = swayAmplitude;
            float pitch = (Mathf.PerlinNoise(t * swaySpeed, 0.3f) - 0.5f) * 2f * sway;
            float yaw = (Mathf.PerlinNoise(0.7f, t * swaySpeed) - 0.5f) * 2f * sway;
            float roll = (Mathf.PerlinNoise(t * swaySpeed * 0.8f, 13.7f) - 0.5f) * 2f * rollAmplitude;

            pitch += (Mathf.PerlinNoise(t * jitterSpeed, 51.2f) - 0.5f) * 2f * jitterAmplitude;
            yaw += (Mathf.PerlinNoise(87.9f, t * jitterSpeed) - 0.5f) * 2f * jitterAmplitude;

            return new Vector3(pitch, yaw, roll) * _camcorderWeight;
        }

        void SetupCamcorderVolume()
        {
            _volumeObject = new GameObject("CamcorderVolume");
            _camcorderVolume = _volumeObject.AddComponent<Volume>();
            _camcorderVolume.isGlobal = true;
            _camcorderVolume.priority = 100f;
            _camcorderVolume.weight = 0f;

            _camcorderProfile = ScriptableObject.CreateInstance<VolumeProfile>();

            var dof = _camcorderProfile.Add<DepthOfField>();
            dof.SetAllOverridesTo(true);
            dof.mode.value = DepthOfFieldMode.Gaussian;
            dof.gaussianStart.value = 2f;
            dof.gaussianEnd.value = 11f;
            dof.gaussianMaxRadius.value = 1.2f;
            dof.highQualitySampling.value = true;

            var grain = _camcorderProfile.Add<FilmGrain>();
            grain.SetAllOverridesTo(true);
            grain.type.value = FilmGrainLookup.Large01;
            grain.intensity.value = 0.6f;
            grain.response.value = 0.7f;

            var aberration = _camcorderProfile.Add<ChromaticAberration>();
            aberration.SetAllOverridesTo(true);
            aberration.intensity.value = 0.4f;

            var vignette = _camcorderProfile.Add<Vignette>();
            vignette.SetAllOverridesTo(true);
            vignette.intensity.value = 0.38f;
            vignette.smoothness.value = 0.55f;

            var color = _camcorderProfile.Add<ColorAdjustments>();
            color.SetAllOverridesTo(true);
            color.saturation.value = -20f;
            color.contrast.value = 8f;

            _camcorderVolume.sharedProfile = _camcorderProfile;
        }
    }
}
