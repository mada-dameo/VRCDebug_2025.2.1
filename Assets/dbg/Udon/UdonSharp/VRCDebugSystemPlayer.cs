using TMPro;
using UdonSharp;
using UnityEngine;
using UnityEngine.UI;
using VRC.SDKBase;
using VRC.Udon;
using VRC.Udon.Common.Interfaces;

namespace VRCDebug.Runtime.Udon
{
    public enum PlayerAudioType
    {
        None,
        DiceRoll,
        DiceResult,
        Event4,
        Event9,
        Event12,
        Next
    }

    public enum NoticeType
    {
        None,
        Current,
        Wait,
        Active,
        Inactive
    }

    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class VRCDebugSystemPlayer : UdonSharpBehaviour
    {
        #region Serialized Fields

        [Header("Settings")]
        [SerializeField] private string _diceRollBoolParameter;
        [SerializeField] private string _diceRollResultIntParameter;
        [SerializeField] private Color _enableButtonColor;

        [Header("External References")]
        [SerializeField] private VRCDebugSystemHost _host;

        [Header("Internal References")]

        [Header("+ Buttons")]
        [SerializeField] private Button _diceButton;
        [SerializeField] private Button _nextButton;
        [SerializeField] private Button _event4Button;
        [SerializeField] private Button _event9Button;
        [SerializeField] private Button _event12Button;

        [Header("+ Texts")]
        [SerializeField] private TMP_Text _userNameText;

        [Header("+ Animators")]
        [SerializeField] private Animator _diceAnimator;

        [Header("+ Icon Objects")]
        [SerializeField] private GameObject _reverseIconRight;
        [SerializeField] private GameObject _reverseIconLeft;

        [Header("+ Hand Deck")]

        [Header("+ Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _diceRollSound;
        [SerializeField] private AudioClip _diceResultSound;
        [SerializeField] private AudioClip _event4Sound;
        [SerializeField] private AudioClip _event9Sound;
        [SerializeField] private AudioClip _event12Sound;
        [SerializeField] private AudioClip _nextSound;

        [Header("+ Notification")]
        [SerializeField] private Animator _noticeAnimator;
        [SerializeField] private string _noticeParamCurrent;
        [SerializeField] private string _noticeParamWait;
        [SerializeField] private string _noticeParamActive;
        [SerializeField] private string _noticeParamInactive;

        #endregion

        #region Synced Fields

        [UdonSynced(UdonSyncMode.None)]
        private int _diceResult = 0;

        [UdonSynced(UdonSyncMode.None)]
        private PlayerAudioType _audioType = PlayerAudioType.None;

        #endregion

        #region Private Fields

        private bool _isMyTurn;
        private int _seatOwnerPlayerId = -1;

        Color _defaultButtonColor;

        #endregion

        #region Fields

        public int SeatOwnerPlayerId 
        {
            get => _seatOwnerPlayerId;
        }

        #endregion

        #region Unity Callbacks

        private void Start()
        {
            SetAllButtonsInteractableState(false);

            if (_diceAnimator != null)
            {
                _diceAnimator.SetBool(_diceRollBoolParameter, false);
                _diceAnimator.SetInteger(_diceRollResultIntParameter, 0);
            }
            if(_event12Button != null)
            {
                _defaultButtonColor = _event12Button.image.color;
            }
        }

        public override void OnDeserialization()
        {
            if (!_host.IsLocalPlayerJoined)
            {
                Debug.LogWarning("VRCDebugSystemPlayer: OnDeserialization: Local player not joined");
                return;
            }

            OnDiceRollStateChanged();
            ApplyDiceResultToAnimator();

            if (_audioType != PlayerAudioType.None)
            {
                PlaySoundInternal(_audioType);
                _audioType = PlayerAudioType.None;
            }
        }

        #endregion

        #region uGUI Callbacks

        public void OnClickDiceButton()
        {
            if (!_host.IsGameStarted)
            {
                Debug.LogError("VRCDebugSystemPlayer: OnClickDiceButton: Game not started");
                return;
            }

            if (!_isMyTurn || Networking.LocalPlayer.playerId != _seatOwnerPlayerId)
            {
                Debug.LogWarning("VRCDebugSystemPlayer: OnClickDiceButton: Not your turn or not your seat");
                return;
            }

            if (_diceAnimator != null && !_diceAnimator.GetBool(_diceRollBoolParameter))
            {
                _diceAnimator.SetBool(_diceRollBoolParameter, true);
                RequestSerialization();
                OnDiceResultDecided();
                PlaySound(PlayerAudioType.DiceRoll);
            }
            else
            {
                DiceRoll();
                PlaySound(PlayerAudioType.DiceResult);
            }
        }

        public void OnClickNextButton()
        {
            if (!_host.IsGameStarted)
            {
                Debug.LogError("VRCDebugSystemPlayer: OnClickNextButton: Game not started");
                return;
            }

            if (_isMyTurn && Networking.LocalPlayer.playerId == _seatOwnerPlayerId)
            {
                if (_host != null)
                {
                    _host.OnNextPlayer();
                }

                if (_diceAnimator != null)
                {
                    _diceAnimator.SetBool(_diceRollBoolParameter, false);
                    _diceAnimator.SetInteger(_diceRollResultIntParameter, 0);
                }

                PlaySound(PlayerAudioType.Next);

                ResetDiceResultAndSync();
            }
            else
            {
                Debug.Log("VRCDebugSystemPlayer: OnClickNextButton: Not your turn or not your seat");
            }
        }

        public void OnClickEvent4Button()
        {
            if (!_host.IsGameStarted)
            {
                Debug.LogError("VRCDebugSystemPlayer: OnClickEvent4Button: Game not started");
                return;
            }

            PlaySound(PlayerAudioType.Event4);

            Debug.Log("VRCDebugSystemPlayer: OnClickEvent4Button");
        }

        public void OnClickEvent9Button()
        {
            if (!_host.IsGameStarted)
            {
                Debug.LogError("VRCDebugSystemPlayer: OnClickEvent9Button: Game not started");
                return;
            }

            PlaySound(PlayerAudioType.Event9);

            Debug.Log("VRCDebugSystemPlayer: OnClickEvent9Button");
        }

        public void OnClickEvent12Button()
        {
            if (!_host.IsGameStarted)
            {
                Debug.LogError("VRCDebugSystemPlayer: OnClickEvent12Button: Game not started");
                return;
            }

            PlaySound(PlayerAudioType.Event12);

            Debug.Log("VRCDebugSystemPlayer: OnClickEvent12Button");
            _host.ToggleReverse();
            RequestSerialization();
        }
        
        public void SyncEvent12Bottun()
        {
            if(_host.IsReverse)
            {
                _event12Button.image.color = _enableButtonColor;
                _reverseIconRight.SetActive(false);
                _reverseIconLeft.SetActive(true);
            }
            else
            {
                _event12Button.image.color = _defaultButtonColor;
                _reverseIconRight.SetActive(true);
                _reverseIconLeft.SetActive(false);
            }
        }

        #endregion

        #region Dice Roll Methods

        private void DiceRoll()
        {
            var localPlayer = Networking.LocalPlayer;
            if (localPlayer == null) return;

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(localPlayer, gameObject);
            }

            if (!Networking.IsOwner(gameObject))
            {
                Debug.LogError("VRCDebugSystemPlayer: DiceRoll: Failed to get ownership");
                return;
            }

            _diceButton.interactable = false;

            if (_diceAnimator != null)
            {
                _diceAnimator.SetBool(_diceRollBoolParameter, true);
            }

            _diceResult = Random.Range(1, 7);
            Debug.Log($"VRCDebugSystemPlayer: DiceRoll: diceResult={_diceResult}");

            RequestSerialization();

            OnDiceResultDecided();
        }

        public void OnDiceRollStateChanged()
        {
            if (_diceAnimator != null)
            {
                _diceAnimator.SetBool(_diceRollBoolParameter, _isMyTurn);
            }
        }

        public void OnDiceResultDecided()
        {
            Debug.Log("VRCDebugSystemPlayer: OnDiceResultDecided: Called");
            ApplyDiceResultToAnimator();
        }

        private void ApplyDiceResultToAnimator()
        {
            if (_diceAnimator != null)
            {
                _diceAnimator.SetInteger(_diceRollResultIntParameter, _diceResult);
            }
        }

        private void ResetDiceResultAndSync()
        {
            if (Networking.IsOwner(gameObject))
            {
                _diceResult = 0;
                RequestSerialization();
            }
        }

        #endregion

        #region Custom Callbacks

        public void SetPlayerId(int playerId)
        {
            _seatOwnerPlayerId = playerId;
        }

        public void SetIsMyTurn(bool isTurn, bool isDev)
        {
            Debug.Log($"VRCDebugSystemPlayer: SetIsMyTurn: isTurn={isTurn}, isDev={isDev}");
            _isMyTurn = isTurn;

            if (!_host.IsGameStarted)
            {
                Debug.LogError($"VRCDebugSystemPlayer: SetIsMyTurn: _host.IsGameStarted == False");
                SetAllButtonsInteractableState(false);
                if (_diceAnimator != null)
                {
                    _diceAnimator.SetBool(_diceRollBoolParameter, false);
                    _diceAnimator.SetInteger(_diceRollResultIntParameter, 0);
                }
                return;
            }

            if (_diceAnimator != null)
            {
                _diceAnimator.SetBool(_diceRollBoolParameter, false);
                _diceAnimator.SetInteger(_diceRollResultIntParameter, 0);
            }

            bool isMySeat = (Networking.LocalPlayer != null && Networking.LocalPlayer.playerId == _seatOwnerPlayerId);
            bool canInteract = (isTurn && isMySeat);

            if (isDev)
            {
                SetAllButtonsInteractableState(true);
            }
            else
            {
                SetAllButtonsInteractableState(canInteract);
                Debug.LogWarning($"VRCDebugSystemPlayer: SetIsMyTurn: canInteract == {canInteract}");
                Debug.LogWarning($"VRCDebugSystemPlayer: SetIsMyTurn: isTurn == {isTurn}");
                Debug.LogWarning($"VRCDebugSystemPlayer: SetIsMyTurn: isMySeat == {isMySeat}");
            }
        }

        public void SetNotice(NoticeType noticeType)
        {
            if (_noticeAnimator == null) return;
            switch (noticeType)
            {
                case NoticeType.None:
                    _noticeAnimator.SetBool(_noticeParamCurrent, false);
                    _noticeAnimator.SetBool(_noticeParamWait, false);
                    _noticeAnimator.SetBool(_noticeParamActive, false);
                    _noticeAnimator.SetBool(_noticeParamInactive, false);
                    break;
                case NoticeType.Current:
                    _noticeAnimator.SetBool(_noticeParamCurrent, true);
                    _noticeAnimator.SetBool(_noticeParamWait, false);
                    _noticeAnimator.SetBool(_noticeParamActive, false);
                    _noticeAnimator.SetBool(_noticeParamInactive, false);
                    break;
                case NoticeType.Wait:
                    _noticeAnimator.SetBool(_noticeParamCurrent, false);
                    _noticeAnimator.SetBool(_noticeParamWait, true);
                    _noticeAnimator.SetBool(_noticeParamActive, false);
                    _noticeAnimator.SetBool(_noticeParamInactive, false);
                    break;
                case NoticeType.Active:
                    _noticeAnimator.SetBool(_noticeParamCurrent, false);
                    _noticeAnimator.SetBool(_noticeParamWait, false);
                    _noticeAnimator.SetBool(_noticeParamActive, true);
                    _noticeAnimator.SetBool(_noticeParamInactive, false);
                    break;
                case NoticeType.Inactive:
                    _noticeAnimator.SetBool(_noticeParamCurrent, false);
                    _noticeAnimator.SetBool(_noticeParamWait, false);
                    _noticeAnimator.SetBool(_noticeParamActive, false);
                    _noticeAnimator.SetBool(_noticeParamInactive, true);
                    break;
            }
        }

        public void SetUserName(string userName)
        {
            _userNameText.text = userName;
        }

        #endregion

        #region Helper Methods

        private void SetAllButtonsInteractableState(bool canInteract)
        {
            //Debug.LogWarning($"VRCDebugSystemPlayer: SetAllButtonsInteractableState: SetAllButtons == {canInteract}");
            _diceButton.interactable = canInteract;
            _nextButton.interactable = canInteract;
            _event4Button.interactable = canInteract;
            _event9Button.interactable = canInteract;
            _event12Button.interactable = canInteract;
        }

        public void PlaySound(PlayerAudioType audioType)
        {
            var localPlayer = Networking.LocalPlayer;
            if (localPlayer == null) return;

            if (!Networking.IsOwner(gameObject))
            {
                Networking.SetOwner(localPlayer, gameObject);
            }

            if (!Networking.IsOwner(gameObject))
            {
                Debug.LogError("VRCDebugSystemPlayer: PlaySound: Failed to get ownership");
                return;
            }

            _audioType = audioType;
            RequestSerialization();
            PlaySoundInternal(audioType);
        }

        private void PlaySoundInternal(PlayerAudioType audioType)
        {
            switch (audioType)
            {
                case PlayerAudioType.DiceRoll:
                    _audioSource.PlayOneShot(_diceRollSound);
                    break;
                case PlayerAudioType.DiceResult:
                    _audioSource.PlayOneShot(_diceResultSound);
                    break;
                case PlayerAudioType.Event4:
                    _audioSource.PlayOneShot(_event4Sound);
                    break;
                case PlayerAudioType.Event9:
                    _audioSource.PlayOneShot(_event9Sound);
                    break;
                case PlayerAudioType.Event12:
                    _audioSource.PlayOneShot(_event12Sound);
                    break;
                case PlayerAudioType.Next:
                    _audioSource.PlayOneShot(_nextSound);
                    break;
            }
        }
        public bool GetHostGameStarted()
        {
            return _host.IsGameStarted;
        }

        #endregion
    }
}
