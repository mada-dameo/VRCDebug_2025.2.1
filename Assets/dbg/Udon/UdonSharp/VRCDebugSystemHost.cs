using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCDebug.Runtime.Udon
{

    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class VRCDebugSystemHost : UdonSharpBehaviour
    {
        #region Serialized Fields

        [Header("Settings")]
        [SerializeField] private bool _isDebugMode;

        [Header("Internal References")]
        [SerializeField] private VRCDebugSystemPlayer[] _players;

        #endregion

        #region Synced Fields

        [UdonSynced(UdonSyncMode.None)]
        private int _currentPlayerIndex = 0;

        [UdonSynced(UdonSyncMode.None)]
        private bool _isReverse = false;

        [UdonSynced(UdonSyncMode.None)]
        private int[] _currentJoinedUserIds;

        [UdonSynced(UdonSyncMode.None)]
        private bool _isGameStarted = false;

        #endregion

        #region Fields

        private bool _isLocalPlayerJoined = false;

        public bool IsGameStarted
        {
            get => _isGameStarted;
            set
            {
                var player = GetPlayerWithOwner(gameObject);

                if (player == null)
                {
                    return;
                }

                _isGameStarted = value;

                RequestSerialization();
            }
        }

        public bool IsReverse
        {
            get { return _isReverse; }
        }

        public int CurrentTurnPlayerId
        {
            get
            {
                if (_currentPlayerIndex < 0 || _currentPlayerIndex >= _players.Length)
                {
                    return -1;
                }
                return _players[_currentPlayerIndex].SeatOwnerPlayerId;
            }
        }

        public int CurrentPlayerIndex
        {
            get { return _currentPlayerIndex; }
        }

        public VRCDebugSystemPlayer[] Players
        {
            get { return _players; }
        }

        public bool IsLocalPlayerJoined
        {
            get => _isLocalPlayerJoined;
            set => _isLocalPlayerJoined = value;
        }

        #endregion

        #region Unity Callbacks

        private void Start()
        {
            Debug.Log("VRCDebugSystemHost: Start: Called");
            _currentPlayerIndex = 0;
            
            Debug.Log("VRCDebugSystemHost: Start: UpdatePlayersTurn");
            UpdatePlayersTurn();
        }

        #endregion

        #region Udon Callbacks

        public override void OnDeserialization()
        {
            if (!IsLocalPlayerJoined)
            {
                Debug.Log("VRCDebugSystemHost: OnDeserialization: Local player is not joined");
                return;
            }

            Debug.Log("VRCDebugSystemHost: OnDeserialization: Sync received");
            ApplyJoinedUserIds();
            Debug.Log("VRCDebugSystemHost: OnDeserialization: UpdatePlayersTurn");
            UpdatePlayersTurn();
            UpdatePlayersEvent12Button();
        }

        #endregion

        #region Custom Callbacks

        public void StartGame(int[] userIds)
        {
            if (userIds == null)
            {
                Debug.LogError("VRCDebugSystemHost: StartGame: UserIds is null");
                return;
            }

            var player = GetPlayerWithOwner(gameObject);

            _currentJoinedUserIds = new int[userIds.Length];
            for (int i = 0; i < userIds.Length; i++)
            {
                _currentJoinedUserIds[i] = userIds[i];
            }

            _currentPlayerIndex = 0;

            RequestSerialization();
            ApplyJoinedUserIds();
            
            bool p_isgamestarted= false;
            Debug.LogWarning($"VRCDebugSystemHost: StartGame: Before");
            for (int i = 0; i < _players.Length; i++)
            {
                p_isgamestarted = _players[i].GetHostGameStarted();
                Debug.Log($"VRCDebugSystemHost: StartGame0000000000000{i} : {p_isgamestarted}");
            }
            
            UpdatePlayersTurn();

            for (int i = 0; i < _players.Length; i++)
            {
                p_isgamestarted = _players[i].GetHostGameStarted();
                Debug.Log($"VRCDebugSystemHost: StartGame1111111111111{i} : {p_isgamestarted}");
            }
            Debug.LogWarning($"VRCDebugSystemHost: StartGame: After");
        }

        public void EndGame()
        {
            _currentPlayerIndex = 0;
            Debug.Log("VRCDebugSystemHost: EndGame: UpdatePlayersTurn");
            UpdatePlayersTurn();
        }

        public void OnNextPlayer()
        {
            Debug.Log("VRCDebugSystemHost: OnNextPlayer: Called");

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            int loopCount = _players.Length;
            if (_isReverse)
            {
                do
                {
                    _currentPlayerIndex--;
                    if (_currentPlayerIndex < 0)
                    {
                        _currentPlayerIndex = _players.Length - 1;
                    }
                    loopCount--;
                }
                while (loopCount > 0 && !IsSeatJoined(_currentPlayerIndex));
            }
            else
            {
                do
                {
                    _currentPlayerIndex++;
                    if (_currentPlayerIndex >= _players.Length)
                    {
                        _currentPlayerIndex = 0;
                    }
                    loopCount--;
                }
                while (loopCount > 0 && !IsSeatJoined(_currentPlayerIndex));
            }

            RequestSerialization();
            Debug.Log("VRCDebugSystemHost: OnNextPlayer: UpdatePlayersTurn");
            UpdatePlayersTurn();
        }

        public void ToggleReverse()
        {
            Debug.Log("VRCDebugSystemHost: ToggleReverse: Called");
            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(Networking.LocalPlayer, gameObject);
            }

            _isReverse = !_isReverse;
            RequestSerialization();

            UpdatePlayersEvent12Button();
            FixCurrentIndexIfEmptySeat();
        }

        #endregion

        #region Helper Methods

        private void ApplyJoinedUserIds()
        {
            Debug.Log("VRCDebugSystemHost: ApplyJoinedUserIds: Called");

            if (_currentJoinedUserIds == null)
            {
                for (int i = 0; i < _players.Length; i++)
                {
                    _players[i].SetUserName("");
                    _players[i].SetPlayerId(-1);
                }
                return;
            }

            for (int i = 0; i < _players.Length; i++)
            {
                if (i >= _currentJoinedUserIds.Length || _currentJoinedUserIds[i] == -1)
                {
                    _players[i].SetUserName("");
                    _players[i].SetPlayerId(-1);
                }
                else
                {
                    var p = VRCPlayerApi.GetPlayerById(_currentJoinedUserIds[i]);
                    if (p != null)
                    {
                        _players[i].SetUserName(p.displayName);
                        _players[i].SetPlayerId(_currentJoinedUserIds[i]);
                    }
                    else
                    {
                        _players[i].SetUserName("");
                        _players[i].SetPlayerId(-1);
                    }
                }
            }

            FixCurrentIndexIfEmptySeat();
        }

        private void FixCurrentIndexIfEmptySeat()
        {
            if (IsSeatJoined(_currentPlayerIndex)) return;

            for (int i = 0; i < _players.Length; i++)
            {
                if (IsSeatJoined(i))
                {
                    _currentPlayerIndex = i;
                    return;
                }
            }

            _currentPlayerIndex = 0;
        }

        private bool IsSeatJoined(int seatIndex)
        {
            if (_currentJoinedUserIds == null) return false;
            if (seatIndex < 0 || seatIndex >= _currentJoinedUserIds.Length) return false;
            return _currentJoinedUserIds[seatIndex] != -1;
        }

        private void UpdatePlayersTurn()
        {
            Debug.Log("VRCDebugSystemHost: UpdatePlayersTurn: Called");

            for (int i = 0; i < _players.Length; i++)
            {
                bool isMyTurn = (i == _currentPlayerIndex && IsSeatJoined(i));
                _players[i].SetIsMyTurn(isMyTurn, _isDebugMode);

                NoticeType noticeType = NoticeType.None;

                if (_isGameStarted && IsSeatJoined(i))
                {
                    bool isLocal = Networking.LocalPlayer.playerId == _currentJoinedUserIds[i];
                    bool isCurrentPlayer = i == _currentPlayerIndex;

                    if (isLocal)
                    {
                        noticeType = isCurrentPlayer ? NoticeType.Current : NoticeType.Wait;
                    }
                    else
                    {
                        noticeType = isCurrentPlayer ? NoticeType.Active : NoticeType.Inactive;
                    }
                }
                _players[i].SetNotice(noticeType);
            }

            if (IsSeatJoined(_currentPlayerIndex))
            {
            }
        }

        private void UpdatePlayersEvent12Button()
        {
            for (int i = 0; i < _players.Length; i++)
            {
                _players[i].SyncEvent12Bottun();
            }
        }
        

        private VRCPlayerApi GetPlayerWithOwner(GameObject gameObject)
        {
            var player = Networking.LocalPlayer;
            if (player == null)
            {
                Debug.LogError("VRCDebugSystemHost: GetPlayerWithOwner: Player is null");
                return null;
            }

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(player, gameObject);
            }

            if (!Networking.IsOwner(gameObject))
            {
                Debug.LogError("VRCDebugSystemHost: GetPlayerWithOwner: Failed to set owner");
                return null;
            }

            return player;
        }

        #endregion
    }
}
