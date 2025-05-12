using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace VRCDebug.Runtime.Udon
{
    [UdonBehaviourSyncMode(BehaviourSyncMode.Manual)]
    public class VRCDebugRegistry : UdonSharpBehaviour
    {
        private VRCDebugManager[] _gameManagers = new VRCDebugManager[0];

        public void RegisterGameManager(VRCDebugManager gameManager)
        {
            var newGameManagers = new VRCDebugManager[_gameManagers.Length + 1];
            for (int i = 0; i < _gameManagers.Length; i++)
            {
                newGameManagers[i] = _gameManagers[i];
            }
            newGameManagers[_gameManagers.Length] = gameManager;
            _gameManagers = newGameManagers;
        }

        public bool IsLocalUserJoined()
        {
            foreach (var gameManager in _gameManagers)
            {
                if (gameManager.IsLocalPlayerJoined)
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsGameStarted()
        {
            foreach (var gameManager in _gameManagers)
            {
                if (gameManager.IsGameStarted)
                {
                    return true;
                }
            }
            return false;
        }
    }
}
