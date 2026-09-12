using System;
using System.Collections.Generic;
using ACTGameEditor.Combat;
using ACTGameEditor.Locomotion;
using EGamePlay;
using EGamePlay.Unity;
using UnityEngine;

namespace ACTGameEditor
{
    public class PlayerManager : Singleton<PlayerManager>
    {
        static uint idCounter;

        /// <summary>生成时分配，之后不变。小队三槽从 0 起递增。</summary>
        public static uint GetID() => idCounter++;

        /// <summary>当前主控变化（换人同一帧）。HUD 跟上场者。</summary>
        public Action<ActPlayer> SquadControlChanged;

        readonly ActPlayer[] _squadPlayers = new ActPlayer[CombatSquad.SlotCount];

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
                AddAckerAct?.Invoke(attacker);
            }
            else
            {
                Debug.LogError($"重复注册攻击者 NetId:{netID}");
            }
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
            PlayerNetIdList.Remove(netID);
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

        /// <summary>开战生成同一预制体 ×3：槽 0 上场，1/2 候场隐藏。</summary>
        public void AddTruePlayer()
        {
            if (LocalPlayer != null)
                return;

            CombatEntity[] members = new CombatEntity[CombatSquad.SlotCount];
            for (int i = 0; i < CombatSquad.SlotCount; i++)
            {
                bool onField = i == 0;
                ActPlayer player = SpawnActPlayer(
                    PrefabPath.Player,
                    "ActPlayer",
                    Vector3.zero,
                    AgentTag.PlayerA,
                    isTruePlayer: onField,
                    isPlayerSquad: true,
                    squadSlot: i);
                _squadPlayers[i] = player;
                members[i] = player.Combat;
                PlayerNetIdList.Add(player.Combat.NetId);
                RegisterPlayer(player);
            }

            IgnoreSquadCollisions(true);
            CombatSquad.Instance?.Bind(members[0], members[1], members[2]);

            for (int i = 1; i < CombatSquad.SlotCount; i++)
                ApplySquadPresence(members[i], SquadPresence.Bench);

            ActPlayer lead = _squadPlayers[0];
            CameraManager.Instance.ChangeCurFollowTarget(lead);
            LocalPlayer = lead;
            LocalNetId = lead.Combat.NetId;
            CurrentFollowNetId = lead.Combat.NetId;
            SquadControlChanged?.Invoke(lead);
        }

        /// <summary>Q/E 与肖像：小队走换人，其余单位只切镜头（调试）。</summary>
        public void TrySelectAttacker(uint netId)
        {
            ActPlayer player = GetAcker(netId);
            if (player?.Combat == null)
                return;
            if (player.Combat.IsPlayerSquad)
            {
                CombatSquad.Instance?.TrySwitch(player.Combat.SquadSlot, SwitchReason.Manual);
                return;
            }

            SwitchCameraToPlayer(netId);
        }

        /// <summary>换人同一帧：主控 / 显隐 / 相机 / 输入。逻辑判定在 <see cref="CombatSquad.TrySwitch"/>。</summary>
        public void ApplySquadSwitch(
            CombatEntity outgoing,
            CombatEntity incoming,
            bool comboExit,
            Vector3 incomingPos,
            Quaternion incomingRot)
        {
            if (incoming == null || incoming.AttackPlayer == null)
                return;

            if (outgoing != null)
            {
                ClearAttackerInput(outgoing);
                ApplySquadPresence(outgoing, comboExit ? SquadPresence.Exiting : SquadPresence.Bench);
                CombatFxPackagePlayer.Play(
                    CombatFxPackageId.SwitchOut,
                    CombatFxPlayContext.ForOwner(outgoing, CombatFxSource.Entity(outgoing.Id)));
            }

            incoming.WarpTo(incomingPos, incomingRot);
            ApplySquadPresence(incoming, SquadPresence.OnField);
            incoming.GetComponent<AnimComponent>()?.Director?.ForceLocomotion();
            CombatFxPackagePlayer.Play(
                CombatFxPackageId.SwitchIn,
                CombatFxPlayContext.ForOwner(incoming, CombatFxSource.Entity(incoming.Id)));

            ActPlayer lead = incoming.AttackPlayer;
            LocalPlayer = lead;
            LocalNetId = incoming.NetId;
            CurrentFollowNetId = incoming.NetId;
            if (CameraManager.Instance != null)
                CameraManager.Instance.ChangeCurFollowTarget(lead);
            ConfigurableInputManager.Instance.ChangeCurPlayer();
            SquadControlChanged?.Invoke(lead);
        }

        /// <summary>按 Presence 显隐、电机与本地输入绑定。</summary>
        public void ApplySquadPresence(CombatEntity combat, SquadPresence presence)
        {
            if (combat == null || combat.IsDisposed)
                return;

            combat.SetSquadPresence(presence);
            ActPlayer player = combat.AttackPlayer;
            if (player == null)
                return;

            if (presence == SquadPresence.Bench && !combat.IsDead)
            {
                combat.StateDirector?.ClearHit();
                combat.GetComponent<AnimComponent>()?.Director?.ForceLocomotion();
            }

            bool visible = presence != SquadPresence.Bench;
            if (player.gameObject.activeSelf != visible)
                player.gameObject.SetActive(visible);

            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
                cc.enabled = visible && !combat.IsDead;

            bool motor = visible && !combat.IsDead;
            combat.ChangeInputMoveState(motor);
            CombatLocomotionInstaller.BindLocalControl(
                combat,
                presence == SquadPresence.OnField && combat.isTruePlayer);
        }

        static void ClearAttackerInput(CombatEntity combat)
        {
            if (combat.AttackPlayer is NormalActPlayer normal)
                normal.InputBuffer?.Clear();
        }

        void IgnoreSquadCollisions(bool ignore)
        {
            for (int a = 0; a < CombatSquad.SlotCount; a++)
            {
                for (int b = a + 1; b < CombatSquad.SlotCount; b++)
                    IgnoreCollisionPair(_squadPlayers[a], _squadPlayers[b], ignore);
            }
        }

        static void IgnoreCollisionPair(ActPlayer a, ActPlayer b, bool ignore)
        {
            if (a == null || b == null)
                return;
            Collider[] ca = a.GetComponentsInChildren<Collider>(true);
            Collider[] cb = b.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < ca.Length; i++)
            {
                Collider left = ca[i];
                if (left == null)
                    continue;
                for (int j = 0; j < cb.Length; j++)
                {
                    Collider right = cb[j];
                    if (right == null || left == right)
                        continue;
                    Physics.IgnoreCollision(left, right, ignore);
                }
            }
        }

        public ActPlayer AddFakePlayer(Vector3 startPos, bool isAi, AgentTag agentTag, AgentModelType agentName = AgentModelType.Player)
        {
            string prefabPath = agentName == AgentModelType.Player ? PrefabPath.Player : PrefabPath.EnemyB;
            string assetPath = agentName == AgentModelType.Player ? "ActPlayer" : "EnemyB";

            ActPlayer player = SpawnActPlayer(prefabPath, assetPath, startPos, agentTag, isTruePlayer: false, modelType: agentName);

            PlayerNetIdList.Add(player.Combat.NetId);
            RegisterPlayer(player);
            return player;
        }

        /// <summary>
        /// 从运行时对象池取出角色；池未就绪时回退 Instantiate。
        /// </summary>
        ActPlayer SpawnActPlayer(
            string bundle,
            string asset,
            Vector3 position,
            AgentTag agent,
            bool isTruePlayer,
            AgentModelType modelType = AgentModelType.Player,
            bool isPlayerSquad = false,
            int squadSlot = -1)
        {
            GameObject obj = RunTimePoolManager.Instance != null
                ? RunTimePoolManager.Instance.LoadResPoolObj(bundle, asset)
                : null;

            if (obj == null)
            {
                GameObject prefab = AssetBundleManager.Instance.LoadAssetSync<GameObject>(bundle, asset);
                obj = GameObject.Instantiate(prefab);
            }

            RunTimePoolManager.Instance.AttachToSceneLayer(obj.transform);
            obj.transform.SetPositionAndRotation(position, Quaternion.identity);
            obj.SetActive(true);

            ActPlayer player = obj.GetComponent<ActPlayer>();
            player.SetPoolResPath(bundle, asset);
            player.RestoreForReuse();
            player.Agent = agent;
            player.ModelType = modelType;
            player.Init(isTruePlayer, isPlayerSquad, squadSlot);
            return player;
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
        public ActPlayer AddFakePlayerFromUI()
        {
            Vector3 pos = LocalPlayer != null
                ? LocalPlayer.transform.position + LocalPlayer.transform.TransformDirection(Vector3.right) * 3f
                : Vector3.zero;
            return AddFakePlayer(pos, false, AgentTag.PlayerB, AgentModelType.Player);
        }

        /// <summary> UI 功能，在本地玩家前方生成敌人，AgentTag 为 enemy </summary>
        public ActPlayer AddEnemyFromUI()
        {
            Vector3 pos = LocalPlayer != null
                ? LocalPlayer.transform.position + LocalPlayer.transform.TransformDirection(Vector3.forward) * 5f
                : Vector3.forward * 5f;
            return AddFakePlayer(pos, true, AgentTag.enemy, AgentModelType.EnemyB);
        }

        /// <summary> UI 功能，在本地玩家前方生成精英敌人（EnemyA 模型位 → 精英大脑档） </summary>
        public ActPlayer AddEliteEnemyFromUI()
        {
            Vector3 pos = LocalPlayer != null
                ? LocalPlayer.transform.position + LocalPlayer.transform.TransformDirection(Vector3.forward) * 5f
                : Vector3.forward * 5f;
            return AddFakePlayer(pos, true, AgentTag.enemy, AgentModelType.EnemyA);
        }

        /// <summary>调试：只切镜头，不换人。正式换人走 <see cref="TrySelectAttacker"/>。</summary>
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
