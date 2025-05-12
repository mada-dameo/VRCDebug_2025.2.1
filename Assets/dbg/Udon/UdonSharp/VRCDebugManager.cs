using Limitex.MonoUI.Udon;
using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace VRCDebug.Runtime.Udon
{
    public enum ButtonType
    {
        Join,
        Leave,
        Start,
        Reset,
        ActivateDialog,
    }

    public enum ButtonState
    {
        Waiting,
        Ready,
        Playing,
        Disabled,
        Enabled,
    }

    public enum AudioType
    {
        None,
        Join,
        Leave,
        Start,
        Reset,
        Stop,
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class VRCDebugManager : UdonSharpBehaviour
    {
        [Header("Settings")]
        [SerializeField] private bool _isDebugMode;
        [SerializeField] private int _minPlayers;

        [Header("External References")]
        [SerializeField] private VRCDebugRegistry _worldRegistry;
        [SerializeField] private VRCDebugSystemHost _gameSystemHost;

        [Header("Internal References")]
        [Header("+ Buttons")]
        [SerializeField] private Button _joinButton;
        [SerializeField] private Button _leaveButton;
        [SerializeField] private Button _startButton;
        [SerializeField] private Button _resetButton;

        [Header("+ Dialog")]
        [SerializeField] private DialogManager _dialogManager;

        [Header("+ UI")]
        [SerializeField] private TMP_Text[] _joinedUsernames;

        [Header("+ Toggle Objects")]
        [SerializeField] private GameObject[] _gameObjectsToEnableForPlayer;
        [SerializeField] private GameObject[] _gameObjectsToEnableForNotPlayer;

        [Header("+ Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _joinSound;
        [SerializeField] private AudioClip _leaveSound;
        [SerializeField] private AudioClip _startSound;
        [SerializeField] private AudioClip _resetSound;
        [SerializeField] private AudioClip _stopSound;

        [UdonSynced(UdonSyncMode.None)] private int[] _joinedUserIds;
        [UdonSynced(UdonSyncMode.None)] private bool _isGameStarted = false;
        [UdonSynced(UdonSyncMode.None)] private AudioType _audioType = AudioType.None;

        private bool _isLocalPlayerJoined = false;

        public bool IsLocalPlayerJoined
        {
            get => _isLocalPlayerJoined;
            private set
            {
                _isLocalPlayerJoined = value;
                _gameSystemHost.IsLocalPlayerJoined = value;
            }
        }

        public bool IsGameStarted
        {
            get => _isGameStarted;
            set {
                var player = GetPlayerWithOwner(gameObject);

                if (player == null)
                {
                    return;
                }

                _isGameStarted = value;

                RequestSerialization();

                if (_gameSystemHost != null)
                {
                    _gameSystemHost.IsGameStarted = value;
                }
            }
        }

        #region uGUI Callbacks

        public void OnClickJoinButton() => OnClickButton(ButtonType.Join);

        public void OnClickLeaveButton() => OnClickButton(ButtonType.Leave);

        public void OnClickStartButton() => OnClickButton(ButtonType.Start);

        public void OnClickResetButton() => OnClickButton(ButtonType.Reset);

        public void OnClickActivateDialogButton() => OnClickButton(ButtonType.ActivateDialog);

        #endregion

        #region Unity Callbacks

        private void Start()
        {
            _joinedUserIds = new int[_joinedUsernames.Length];

            for (int i = 0; i < _joinedUserIds.Length; i++)
            {
                _joinedUserIds[i] = -1;
                _joinedUsernames[i].text = string.Empty;
            }

            if (_worldRegistry != null)
            {
                _worldRegistry.RegisterGameManager(this);
            }
            else
            {
                Debug.LogWarning("VRCDebugManager: Start: World registry is null");
            }
            
            SetButton(ButtonState.Waiting);
        }

        #endregion

        #region Udon Callbacks

        public override void OnDeserialization()
        {
            RefreshUI();

            if (_audioType != AudioType.None)
            {
                PlaySoundInternal(_audioType);
                _audioType = AudioType.None;
            }
        }

        public void OnGameStart()
        {
            if (!IsLocalPlayerJoined)
            {
                Debug.LogError("VRCDebugManager: OnGameStart: Local user not joined");
                return;
            }
            
            RefreshUI();

            Debug.Log("VRCDebugManager: OnGameStart: Game started");

            if (_gameSystemHost != null)
            {
                _gameSystemHost.StartGame(_joinedUserIds);
            }
        }

        public void OnGameReset()
        {
            if (!IsLocalPlayerJoined)
            {
                Debug.LogError("VRCDebugManager: OnGameReset: Local user not joined");
                return;
            }

            RefreshUI();

            Debug.Log("VRCDebugManager: OnGameReset: Game reset");

            if (_gameSystemHost != null)
            {
                _gameSystemHost.EndGame();
            }
        }

        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            Debug.Log("VRCDebugManager: OnPlayerLeft: Called");

            if (player == null)
            {
                Debug.LogWarning("VRCDebugManager: OnPlayerLeft: player is null");
                return;
            }

            for (int i = 0; i < _joinedUserIds.Length; i++)
            {
                if (_joinedUserIds[i] != player.playerId) continue;

                for (int j = i; j < _joinedUserIds.Length - 1; j++)
                {
                    _joinedUserIds[j] = _joinedUserIds[j + 1];
                    _joinedUsernames[j].text = _joinedUsernames[j + 1].text;
                }

                int lastIndex = _joinedUserIds.Length - 1;
                _joinedUserIds[lastIndex] = -1;
                _joinedUsernames[lastIndex].text = string.Empty;

                if (Networking.LocalPlayer != null &&
                    player.playerId == Networking.LocalPlayer.playerId)
                {
                    IsLocalPlayerJoined = false;
                }

                RequestSerialization();
                break;
            }

            if (_isGameStarted)
            {
                Debug.Log("VRCDebugManager: OnPlayerLeft: Forcing end of game");
                IsGameStarted = false;
                if (_gameSystemHost != null)
                {
                    _gameSystemHost.EndGame();
                }
                RefreshUI();
            }
        }

        #endregion

        #region Custom Callbacks

        private void OnClickButton(ButtonType buttonType)
        {
            switch (buttonType)
            {
                case ButtonType.Join:
                    if (JoinGame())
                    {
                        SetButton(ButtonState.Ready);
                        PlaySound(AudioType.Join);
                    }
                    else Debug.LogError("Failed to join.");
                    break;
                case ButtonType.Leave:
                    if (LeaveGame())
                    {
                        SetButton(ButtonState.Waiting);
                        PlaySound(AudioType.Leave);
                    }
                    else Debug.LogError("Failed to leave.");
                    break;
                case ButtonType.Start:
                    if (StartGame())
                    {
                        SetButton(ButtonState.Playing);
                        PlaySound(AudioType.Start);
                    }
                    else Debug.LogError("Failed to start.");
                    break;
                case ButtonType.Reset:
                    if (ResetGame())
                    {
                        SetButton(ButtonState.Ready);
                        PlaySound(AudioType.Reset);
                    }
                    else Debug.LogError("Failed to reset.");
                    break;
                case ButtonType.ActivateDialog:
                    if (ConfirmGame())
                    {
                        SetButton(ButtonState.Playing);
                        PlaySound(AudioType.Stop);
                    }
                    else Debug.LogError("Failed to confirm.");
                    break;
            }
        }

        #endregion

        #region Helper Methods

        private void RefreshUI()
        {
            for (int i = 0; i < _joinedUserIds.Length; i++)
            {
                if (_joinedUserIds[i] == -1)
                {
                    _joinedUsernames[i].text = string.Empty;
                    continue;
                }

                var player = VRCPlayerApi.GetPlayerById(_joinedUserIds[i]);
                if (player != null)
                {
                    _joinedUsernames[i].text = player.displayName;
                }
            }

            if (IsLocalPlayerJoined && IsGameStarted)
            {
                SetButton(ButtonState.Playing);
            }
            else if (IsLocalPlayerJoined && !IsGameStarted)
            {
                SetButton(ButtonState.Ready);
            }
            else if (!IsLocalPlayerJoined && IsGameStarted)
            {
                SetButton(ButtonState.Disabled);
            }
            else if (!IsLocalPlayerJoined && !IsGameStarted)
            {
                SetButton(ButtonState.Waiting);
            }

            foreach (var gameObject in _gameObjectsToEnableForPlayer)
            {
                gameObject.SetActive(IsGameStarted);
            }

            foreach (var gameObject in _gameObjectsToEnableForNotPlayer)
            {
                gameObject.SetActive(!IsLocalPlayerJoined && IsGameStarted);
            }
            
            _dialogManager.SendCustomEvent(nameof(DialogManager.OnClickCloseButton));
        }

        private bool JoinGame()
        {
            if (IsLocalPlayerJoined)
            {
                Debug.LogError("VRCDebugManager: JoinGame: Local user already joined");
                return false;
            }

            if (_worldRegistry != null && _worldRegistry.IsLocalUserJoined())
            {
                Debug.LogError("VRCDebugManager: JoinGame: Local user already joined");
                return false;
            }

            if (IsGameStarted)
            {
                Debug.LogError("VRCDebugManager: JoinGame: Game started");
                return false;
            }

            var player = GetPlayerWithOwner(gameObject);

            if (player == null)
            {
                return false;
            }

            for (int i = 0; i < _joinedUserIds.Length; i++)
            {
                if (_joinedUserIds[i] == player.playerId)
                {
                    return false;
                }
                if (_joinedUserIds[i] == -1)
                {
                    _joinedUserIds[i] = player.playerId;
                    _joinedUsernames[i].text = player.displayName;
                    IsLocalPlayerJoined = true;
                    RequestSerialization();
                    return true;
                }
            }

            return false;
        }

        private bool LeaveGame()
        {
            if (!IsLocalPlayerJoined)
            {
                Debug.LogError("VRCDebugManager: LeaveGame: Local user not joined");
                return false;
            }

            if (IsGameStarted)
            {
                Debug.LogError("VRCDebugManager: LeaveGame: Game started");
                return false;
            }

            var player = GetPlayerWithOwner(gameObject);

            if (player == null)
            {
                return false;
            }

            for (int i = 0; i < _joinedUserIds.Length; i++)
            {
                if (_joinedUserIds[i] != player.playerId) continue;
                _joinedUserIds[i] = -1;
                _joinedUsernames[i].text = string.Empty;
                for (int j = i; j < _joinedUserIds.Length - 1; j++)
                {
                    _joinedUserIds[j] = _joinedUserIds[j + 1];
                    _joinedUsernames[j].text = _joinedUsernames[j + 1].text;
                }
                _joinedUserIds[_joinedUserIds.Length - 1] = -1;
                _joinedUsernames[_joinedUserIds.Length - 1].text = string.Empty;
                IsLocalPlayerJoined = false;
                RequestSerialization();
                return true;
            }

            return false;
        }

        private bool StartGame()
        {
            if (!IsLocalPlayerJoined)
            {
                Debug.LogError("VRCDebugManager: StartGame: Local user not joined");
                return false;
            }

            if (IsGameStarted)
            {
                Debug.LogError("VRCDebugManager: StartGame: Game already started");
                return false;
            }

            if (JoinedPlayerCount() < _minPlayers)
            {
                Debug.LogError("VRCDebugManager: StartGame: Not enough players");
                return false;
            }

            GetPlayerWithOwner(gameObject);

            IsGameStarted = true;

            RequestSerialization();

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnGameStart));

            return true;
        }

        private bool ResetGame()
        {
            if (!IsLocalPlayerJoined)
            {
                Debug.LogError("VRCDebugManager: ResetGame: Local user not joined");
                return false;
            }

            if (!IsGameStarted)
            {
                Debug.LogError("VRCDebugManager: ResetGame: Game not started");
                return false;
            }

            IsGameStarted = false;

            SendCustomNetworkEvent(NetworkEventTarget.All, nameof(OnGameReset));

            return true;
        }

        private bool ConfirmGame()
        {
            if (!IsLocalPlayerJoined)
            {
                Debug.LogError("VRCDebugManager: ConfirmGame: Local user not joined");
                return false;
            }

            if (!_isGameStarted)
            {
                Debug.LogError("VRCDebugManager: ConfirmGame: Game not started");
                return false;
            }

            _dialogManager.SendCustomEvent(nameof(DialogManager.OnClickOpenButton));

            return true;
        }

        private void SetButton(ButtonState state)
        {
            if (_isDebugMode)
            {
                Debug.Log($"SetButton: {state}");
                return;
            }

            switch (state)
            {
                case ButtonState.Waiting:
                    _joinButton.interactable = true;
                    _leaveButton.interactable = false;
                    _startButton.interactable = false;
                    _resetButton.interactable = false;
                    break;
                case ButtonState.Ready:
                    _joinButton.interactable = false;
                    _leaveButton.interactable = true;
                    _startButton.interactable = true;
                    _resetButton.interactable = false;
                    break;
                case ButtonState.Playing:
                    _joinButton.interactable = false;
                    _leaveButton.interactable = false;
                    _startButton.interactable = false;
                    _resetButton.interactable = true;
                    break;
                case ButtonState.Disabled:
                    _joinButton.interactable = false;
                    _leaveButton.interactable = false;
                    _startButton.interactable = false;
                    _resetButton.interactable = false;
                    break;
                case ButtonState.Enabled:
                    _joinButton.interactable = true;
                    _leaveButton.interactable = true;
                    _startButton.interactable = true;
                    _resetButton.interactable = true;
                    break;
            }
        }

        private int JoinedPlayerCount()
        {
            int count = 0;
            foreach (var id in _joinedUserIds)
            {
                if (id == -1) continue;
                count++;
            }
            return count;
        }

        private VRCPlayerApi GetPlayerWithOwner(GameObject gameObject)
        {
            var player = Networking.LocalPlayer;
            if (player == null)
            {
                Debug.LogError("VRCDebugManager: GetPlayerWithOwner: Player is null");
                return null;
            }

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(player, gameObject);
            }

            if (!Networking.IsOwner(gameObject))
            {
                Debug.LogError("VRCDebugManager: GetPlayerWithOwner: Failed to set owner");
                return null;
            }

            return player;
        }

        public void PlaySound(AudioType audioType)
        {
            GetPlayerWithOwner(gameObject);
            _audioType = audioType;
            RequestSerialization();
            PlaySoundInternal(audioType);
        }

        private void PlaySoundInternal(AudioType audioType)
        {
            switch (audioType)
            {
                case AudioType.Join:
                    _audioSource.PlayOneShot(_joinSound);
                    break;
                case AudioType.Leave:
                    _audioSource.PlayOneShot(_leaveSound);
                    break;
                case AudioType.Start:
                    _audioSource.PlayOneShot(_startSound);
                    break;
                case AudioType.Reset:
                    _audioSource.PlayOneShot(_resetSound);
                    break;
                case AudioType.Stop:
                    _audioSource.PlayOneShot(_stopSound);
                    break;
            }
        }

        #endregion
    }
}
