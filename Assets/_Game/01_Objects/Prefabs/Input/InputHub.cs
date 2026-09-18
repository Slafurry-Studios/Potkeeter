using System;
using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Slafurry.Core.Abstract;
using Slafurry.System.Scene;

namespace Slafurry.System.InputHub
{
    public static class Controls
    {
        // --- Gameplay Actions ---
        public static event Action OnParryPressed
        {
            add => InputHub.Instance.OnParryPressed += value;
            remove => InputHub.Instance.OnParryPressed -= value;
        }

        public static event Action OnShootStarted
        {
            add => InputHub.Instance.OnShootStarted += value;
            remove => InputHub.Instance.OnShootStarted -= value;
        }

        public static event Action OnShootCanceled
        {
            add => InputHub.Instance.OnShootCanceled += value;
            remove => InputHub.Instance.OnShootCanceled -= value;
        }

        public static event Action<Vector2> OnLookAtChanged
        {
            add => InputHub.Instance.OnLookAtChanged += value;
            remove => InputHub.Instance.OnLookAtChanged -= value;
        }

        // --- UI Actions ---
        public static event Action OnPauseMenuPressed
        {
            add => InputHub.Instance.OnPauseMenuPressed += value;
            remove => InputHub.Instance.OnPauseMenuPressed -= value;
        }

        // --- Kontrol State & Properties ---
        public static bool IsInputEnabled => InputHub.Instance.IsInputEnabled;
        
        // Mengembalikan nilai Posisi Layar Mouse (Screen Position) dari InputHub
        public static Vector2 MousePosition => InputHub.Instance.MousePosition;

        public static void EnableInput() => InputHub.Instance.EnableInput();
        public static void DisableInput() => InputHub.Instance.DisableInput();
        public static void SetInputEnabled(bool enabled) => InputHub.Instance.SetInputEnabled(enabled);
    }

    public class InputHub : GameSystem<InputHub>
    {
        [SerializeField] private InputActionAsset inputActions;

        // Gameplay Events
        public event Action OnParryPressed;
        public event Action OnShootStarted;
        public event Action OnShootCanceled;
        public event Action<Vector2> OnLookAtChanged;

        // UI Events
        public event Action OnPauseMenuPressed;

        private InputActionMap _gameplayMap;
        private InputActionMap _uiMap;

        private InputAction _parryAction;
        private InputAction _shootAction;
        private InputAction _lookAtAction;
        private InputAction _pauseMenuAction;

        public bool IsInputEnabled { get; private set; } = true;
        
        public Vector2 MousePosition { get; private set; }

        public override IEnumerator Initialize() { yield return null; }

        public override void PostInitialize()
        {
            SceneLoader.Instance.OnSceneLoadCompleted += _ => EnableInput();
        }

        protected override void OnSingletonAwake()
        {
            base.OnSingletonAwake();

            _gameplayMap = inputActions.FindActionMap("Gameplay");
            _uiMap = inputActions.FindActionMap("UI");

            _parryAction = _gameplayMap.FindAction("Parry");
            _shootAction = _gameplayMap.FindAction("Shoot");
            _lookAtAction = _gameplayMap.FindAction("LookAt");
            _pauseMenuAction = _uiMap.FindAction("PauseMenu");

            _parryAction.performed += ctx => OnParryPressed?.Invoke();

            _shootAction.started += ctx => OnShootStarted?.Invoke();
            _shootAction.canceled += ctx => OnShootCanceled?.Invoke();

            // Membaca posisi mouse terkini
            _lookAtAction.performed += ctx =>
            {
                MousePosition = ctx.ReadValue<Vector2>();
                OnLookAtChanged?.Invoke(MousePosition);
            };

            _pauseMenuAction.performed += ctx => OnPauseMenuPressed?.Invoke();

            _gameplayMap.Enable();
            _uiMap.Enable();
            IsInputEnabled = true;
        }

        public void EnableInput()
        {
            if (IsInputEnabled) return;

            _gameplayMap.Enable();
            IsInputEnabled = true;
        }

        public void DisableInput()
        {
            if (!IsInputEnabled) return;

            if (_shootAction.IsPressed())
                OnShootCanceled?.Invoke();

            _gameplayMap.Disable();
            IsInputEnabled = false;
        }

        public void SetInputEnabled(bool enabled)
        {
            if (enabled) EnableInput();
            else DisableInput();
        }
    }
}