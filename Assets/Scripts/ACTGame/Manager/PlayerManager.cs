using System;
using System.Collections.Generic;
using UnityEngine;
using EGamePlay;

namespace ACTGameEditor
{
    public class PlayerManager : Singleton<PlayerManager>
    {
        static uint idCounter = 1;
        public static uint GetID(bool isTruePlayer)
        {
            if (isTruePlayer)
            {
                return 0;
            }
            else
            {
                return idCounter++;
            }
        }

        #region NetWorkManager

        public uint LocalNetId { get; private set; }
        public ActPlayer LocalPlayer { get; private set; }
        public Dictionary<uint, ActPlayer> MonoAttackerDic { get; private set; } = new Dictionary<uint, ActPlayer>();

        #endregion

        #region ===== 事件 =======

        public delegate void PlayerEvent(uint NetId);

        public Action<uint> RemoveAckerAct;

        public Action<ActPlayer> AddAckerAct;

        private PlayerEvent OnValueChangeEvent; //值改变时 事件触发

        public void AddListener(ClientEventType eventType, PlayerEvent player)
        {
            if ((eventType == ClientEventType.ValueChange))
                OnValueChangeEvent += player;
        }

        public void RemoveListener(ClientEventType eventType, PlayerEvent player)
        {
            if ((eventType == ClientEventType.ValueChange))
                OnValueChangeEvent -= player;
        }

        #endregion ====== ??? ======

        private int CurIndex = 0;

        private List<uint> PlayerNetIdList = new List<uint>();

        #region Switch Cooldown (Honkai3-style)

        /// <summary> 切换角色冷却时长（秒），冷却期间无法再次切换。 </summary>
        public float SwitchCooldownDuration { get; set; } = 5f;

        /// <summary> 上次切换视角的时间戳（Time.time），用于计算剩余冷却。 </summary>
        private float _lastSwitchTime = -999f;

        /// <summary> 当前摄像机跟随的单位 NetId；用于 UI 高亮当前视角。 </summary>
        public uint CurrentFollowNetId { get; private set; }

        /// <summary> 当前剩余切换冷却时间（秒），0 表示无冷却。 </summary>
        public float GetRemainingSwitchCooldown()
        {
            float elapsed = GameTimeManager.WorldTime - _lastSwitchTime;
            return elapsed >= SwitchCooldownDuration ? 0f : Mathf.Max(0f, SwitchCooldownDuration - elapsed);
        }

        /// <summary> 是否处于切换冷却中（冷却中不可再次切换）。 </summary>
        public bool IsSwitchInCooldown() => GetRemainingSwitchCooldown() > 0f;

        #endregion

        public ActPlayer GetAcker(uint netID)
        {
            if (!MonoAttackerDic.ContainsKey(netID)) return null;
            return MonoAttackerDic[netID];
        }

        private void RegisterAttacker(ActPlayer attacker)
        {
            uint netID = attacker.Combat.NetId;
            if (!MonoAttackerDic.ContainsKey(netID))
            {
                MonoAttackerDic.Add(netID, attacker);
            }
            else
            {
                Debug.LogError($"重复注册攻击者 NetId:{netID}");
            }
            AddAckerAct?.Invoke(attacker);
        }

        private void DisRegisterAttacker(uint netID)
        {
            if (MonoAttackerDic.ContainsKey(netID))
            {
                MonoAttackerDic.Remove(netID);
            }
            else
            {
                Debug.LogError($"注销攻击者失败，未找到 NetId:{netID}");
            }
            RemoveAckerAct?.Invoke(netID);
        }

        public void RegisterPlayer(ActPlayer player)
        {
            uint netID = player.Combat.NetId;
            RegisterAttacker(player);
            OnValueChangeEvent?.Invoke(netID);
        }

        public void DisRegisterPlayer(uint netID)
        {
            DisRegisterAttacker(netID);
            OnValueChangeEvent?.Invoke(netID);
        }

        public void SendBool(uint NetId, bool isLocalOnly,string name, bool msg)
        {
            var acker = GetAcker(NetId);
            if (acker == null)
                return;

            if (!isLocalOnly || (isLocalOnly && acker.Combat.isTruePlayer))
            {
                //acker.SetBool(name, msg);
                Debug.Log($"yns Msg {name} {msg}");
            }
        }

        public void SendAll(uint NetId, bool isLocalOnly, string name, float num, bool isOn = false, string str="")
        {
            var acker = GetAcker(NetId);
            if (acker == null)
                return;

            if (!isLocalOnly || (isLocalOnly && acker.Combat.isTruePlayer))
            {
                //acker.SendAll(name, num, isOn, str);
            }
        }

        public void AddTruePlayer()
        {
            GameObject asset = AssetBundleManager.Instance.LoadAssetSync<GameObject>("actors/character_prefab","ActPlayer");
            GameObject obj = GameObject.Instantiate(asset);
            obj.transform.position = Vector3.zero;

            ActPlayer player = obj.GetComponent<ActPlayer>();
            player.Agent = AgentTag.PlayerA;
            player.Init();

            //跟随玩家
            CameraManager.Instance.ChangeCurFollowTarget(player);

            LocalPlayer = player;
            LocalNetId = player.Combat.NetId;
            CurrentFollowNetId = player.Combat.NetId;

            PlayerNetIdList.Add(player.Combat.NetId);
            RegisterPlayer(player);
        }

        public void AddFakePlayer(Vector3 startPos, bool isAi, AgentTag agentTag, AgentModelType agentName = AgentModelType.Player)
        {
            string prefabPath = agentName == AgentModelType.Player ? PrefabPath.Player : PrefabPath.EnemyB;
            string assetPath = agentName == AgentModelType.Player ? "ActPlayer" : "EnemyB";

            GameObject asset = AssetBundleManager.Instance.LoadAssetSync<GameObject>(prefabPath, assetPath);
            GameObject obj = GameObject.Instantiate(asset);
            obj.transform.position = startPos;

            ActPlayer player = obj.GetComponent<ActPlayer>();
            player.Agent = agentTag;
            player.Init();

            PlayerNetIdList.Add(player.Combat.NetId);
            RegisterPlayer(player);
        }

        public ActPlayer GetOtherPlayer()
        {
            if (PlayerNetIdList.Count == 0) return null;
            if (PlayerNetIdList.Count == 1) return LocalPlayer;
            CurIndex = (CurIndex + 1) % PlayerNetIdList.Count;
            ActPlayer act = GetAcker(PlayerNetIdList[CurIndex]);

            return act;
        }

        /// <summary> UI 功能，在本地玩家右侧生成假玩家，AgentTag 为 PlayerB </summary>
        public void AddFakePlayerFromUI()
        {
            Vector3 pos = LocalPlayer != null
                ? LocalPlayer.transform.position + LocalPlayer.transform.TransformDirection(Vector3.right) * 3f
                : Vector3.zero;
            AddFakePlayer(pos, false, AgentTag.PlayerB, AgentModelType.Player);
        }

        /// <summary> UI 功能，在本地玩家前方生成敌人，AgentTag 为 enemy </summary>
        public void AddEnemyFromUI()
        {
            Vector3 pos = LocalPlayer != null
                ? LocalPlayer.transform.position + LocalPlayer.transform.TransformDirection(Vector3.forward) * 5f
                : Vector3.forward * 5f;
            AddFakePlayer(pos, false, AgentTag.enemy, AgentModelType.EnemyB);
        }

        /// <summary> 切换摄像机跟随玩家；冷却中时不会切换。 </summary>
        public void SwitchCameraToPlayer(uint netId)
        {
            if (IsSwitchInCooldown()) return;
            ActPlayer player = GetAcker(netId);
            if (player == null) return;
            if (CameraManager.Instance != null)
                CameraManager.Instance.ChangeCurFollowTarget(player);
            _lastSwitchTime = GameTimeManager.WorldTime;
            CurrentFollowNetId = netId;
        }
    }
}
