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
        public static event Action OnJumpPressed
        {
            add => InputHub.Instance.OnJumpPressed += value;
            remove => InputHub.Instance.OnJumpPressed -= value;
        }
        public static event Action<Vector2> OnMoveChanged
        {
            add => InputHub.Instance.OnMoveChanged += value;
            remove => InputHub.Instance.OnMoveChanged -= value;
        }
        public static event Action OnCrouchStarted
        {
            add => InputHub.Instance.OnCrouchStarted += value;
            remove => InputHub.Instance.OnCrouchStarted -= value;
        }
        public static event Action OnCrouchCanceled
        {
            add => InputHub.Instance.OnCrouchCanceled += value;
            remove => InputHub.Instance.OnCrouchCanceled -= value;
        }
        public static event Action OnInteractPressed
        {
            add => InputHub.Instance.OnInteractPressed += value;
            remove => InputHub.Instance.OnInteractPressed -= value;
        }
        public static event Action OnInteractReleased
        {
            add => InputHub.Instance.OnInteractReleased += value;
            remove => InputHub.Instance.OnInteractReleased -= value;
        }

        // --- Fitur baru: kontrol on/off ---
        public static bool IsInputEnabled => InputHub.Instance.IsInputEnabled;

        public static void EnableInput() => InputHub.Instance.EnableInput();
        public static void DisableInput() => InputHub.Instance.DisableInput();
        public static void SetInputEnabled(bool enabled) => InputHub.Instance.SetInputEnabled(enabled);
    }

    public class InputHub : GameSystem<InputHub>
    {
        [SerializeField] private InputActionAsset inputActions;

        public event Action OnJumpPressed;
        public event Action<Vector2> OnMoveChanged;
        public event Action OnCrouchStarted;
        public event Action OnCrouchCanceled;
        public event Action OnInteractPressed;
        public event Action OnInteractReleased;

        private InputAction _jumpAction;
        private InputAction _moveAction;
        private InputAction _crouchAction;
        private InputAction _interactAction;
        private InputActionMap _gameplayMap;

        public bool IsInputEnabled { get; private set; } = true;

        public override IEnumerator Initialize() { yield return null; }
        public override void PostInitialize()
        {
            SceneLoader.Instance.OnSceneLoadCompleted += _ => EnableInput();
        }

        protected override void OnSingletonAwake()
        {
            base.OnSingletonAwake();

            _gameplayMap = inputActions.FindActionMap("Gameplay");
            _jumpAction = _gameplayMap.FindAction("Jump");
            _moveAction = _gameplayMap.FindAction("Move");
            _crouchAction = _gameplayMap.FindAction("Crouch");
            _interactAction = _gameplayMap.FindAction("Interact");

            _jumpAction.performed += ctx => OnJumpPressed?.Invoke();
            _moveAction.performed += ctx => OnMoveChanged?.Invoke(ctx.ReadValue<Vector2>());
            _moveAction.canceled += ctx => OnMoveChanged?.Invoke(Vector2.zero);
            _crouchAction.started += ctx => OnCrouchStarted?.Invoke();
            _crouchAction.canceled += ctx => OnCrouchCanceled?.Invoke();
            _interactAction.performed += ctx => OnInteractPressed?.Invoke();
            _interactAction.canceled += ctx => OnInteractReleased?.Invoke();

            _gameplayMap.Enable();
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

            // Pastikan state gerak ter-reset sebelum dimatikan
            // (biar player nggak "nyangkut" jalan terus)
            if (_moveAction.IsPressed())
                OnMoveChanged?.Invoke(Vector2.zero);

            if (_crouchAction.IsPressed())
                OnCrouchCanceled?.Invoke();

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