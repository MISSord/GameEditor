using EGamePlay;
using EGamePlay.Combat;
using System.Collections.Generic;
using UnityEngine;
using XiaoCao;

namespace ACTGameEditor
{
    public class NormalActPlayer : ActPlayer, IAttackPlayer
    {
        [Tooltip("槽位定义：输入绑定、默认技能、释放条件")]
        public SkillSlotConfig SlotConfig;
        [Tooltip("角色/武器覆盖：槽位→技能，换角色时加载")]
        public CharacterSlotConfig CharacterSlotConfig;

        [Header("被动技能（Buff实现）")]
        [Tooltip("额外被动技能ID（硬编码/测试用；会按 PassiveSkillBuffMaps 映射为 Buff 常驻挂载）")]
        public List<int> ExtraPassiveSkillIds = new List<int>();

        [Header("输入缓冲")]
        [Tooltip("预输入有效时长（秒），超时则丢弃输入记录")]
        [Range(0.1f, 2f)]
        public float InputTimeout = 0.8f;

        /// <summary> 槽位运行时：SlotId → 当前 SkillId </summary>
        public SkillSlotRuntime SlotRuntime { get; private set; }
        /// <summary> 技能冷却管理器，供 UI 查询剩余/总时长、填充等。 </summary>
        public SkillCDTimer CDTimer { get; private set; }
        // 目标缓存，默认使用 LockSystem 的锁定目标
        CombatEntity Target;

        // 缓存动画组件，避免每帧 GetComponent
        private AnimComponent _animComponent;

        protected override void StartCallBack()
        {
            Combat.ListenActionPoint(ActionPointType.PreSpell, OnPreSpell);
            Combat.ListenActionPoint(ActionPointType.PostSpell, OnPostSpell);
            Combat.ListenActionPoint(ActionPointType.PostReceiveDamage, OnReceiveDamage);
            Combat.ListenActionPoint(ActionPointType.PostReceiveCure, OnReceiveCure);
            Combat.ListenActionPoint(ActionPointType.PostReceiveStatus, OnReceiveStatus);

            CDTimer = new SkillCDTimer();
            SlotRuntime = new SkillSlotRuntime();

            // 缓存动画组件引用
            _animComponent = Combat?.GetComponent<AnimComponent>();

            if (SlotConfig != null)
            {
                SlotRuntime.Load(SlotConfig, CharacterSlotConfig);
                CDTimer.InitFromSlotConfig(SlotConfig);
                EnsureSlotAbilitiesAttached();
                SyncPassiveSkillBuffs();
            }
        }

        protected override void DisposeCallBack()
        {
            Combat.UnListenActionPoint(ActionPointType.PreSpell, OnPreSpell);
            Combat.UnListenActionPoint(ActionPointType.PostSpell, OnPostSpell);
            Combat.UnListenActionPoint(ActionPointType.PostReceiveDamage, OnReceiveDamage);
            Combat.UnListenActionPoint(ActionPointType.PostReceiveCure, OnReceiveCure);
            Combat.UnListenActionPoint(ActionPointType.PostReceiveStatus, OnReceiveStatus);

            CDTimer = null;
        }

        protected virtual void OnPreSpell(Entity combatAction)
        {
            var spellAction = combatAction as SpellAction;
            Quaternion localRotation;
            if (spellAction.InputTarget != null)
            {
                localRotation = Quaternion.LookRotation(spellAction.InputTarget.Position - Combat.ModelTrans.position);
            }
            else
            {
                localRotation = Quaternion.LookRotation(spellAction.InputPoint - Combat.ModelTrans.position);
            }
            transform.rotation = localRotation; //调整角度，对齐目标
        }

        protected virtual void OnPostSpell(Entity combatAction)
        {

        }

        protected virtual void OnReceiveDamage(Entity combatAction)
        {
            var damageAction = combatAction as DamageAction;
            UIMrg.Instance.PlayDamageText(damageAction.DamageValue, this.UINode.position);
        }

        protected virtual void OnReceiveCure(Entity combatAction)
        {

        }

        protected virtual void OnReceiveStatus(Entity combatAction)
        {

        }

        protected virtual void OnRemoveStatus(RemoveStatusEvent eventData)
        {

        }

        //输入缓存
        public List<InputRecord> _records = new List<InputRecord>();

        void Update()
        {
            CDTimer.OnUpdate(GameTimeManager.WorldDelta);
            CheckInitialInput();
        }

        void LateUpdate()
        {
            var animComp = _animComponent;
            if (animComp?.animator == null) return;

            float baseSpeed = 1f;
            if (Combat.SpellingExecution != null && Combat.SpellingExecution.SubRuners.Count > 0)
                baseSpeed = Combat.SpellingExecution.SubRuners[0].Speed;

            float entityScale = Combat.GetTimeScale();
            animComp.animator.speed = baseSpeed * GameTimeManager.PlayerScale * entityScale;
        }

        /// <summary>Idle 时根据槽位配置或旧映射：输入→槽位→技能。</summary>
        private void CheckInitialInput()
        {
            if (Combat.SpellingExecution != null || _records.Count == 0) return;

            if (SlotConfig != null && SlotRuntime != null)
            {
                TrySpellBySlot();
                return;
            }
        }

        void TrySpellBySlot()
        {
            foreach (var entry in SlotConfig.Slots)
            {
                int skillId = SlotRuntime.GetSkillId(entry.SlotId);
                if (skillId <= 0) continue;

                var def = EGamePlay.Combat.AbilityDefinitionManager.Instance.GetOrLoad(skillId);
                if (def != null && def.Config != null)
                {
                    if (Combat.CanSpellSkillWithTagLists(def.Config.RequiredTags, def.Config.BlockedTags) == false) continue;
                }

                float timeout = entry.InputTimeout > 0 ? entry.InputTimeout : InputTimeout;
                if (!CheckAndConsume(entry.InputType, entry.PressType, entry.InputCallBackType, timeout))
                    continue;

                AddSpellInfo(skillId, entry.Sort);
                break;
            }
        }

        /// <summary>确保槽位技能及技能链已 AttachAbility。</summary>
        void EnsureSlotAbilitiesAttached()
        {
            var abilityComp = Combat.GetComponent<AbilityComponent>();
            if (abilityComp == null) return;

            var toAttach = new HashSet<int>();
            SlotRuntime.GetAllSkillIdsToAttach(toAttach);
            foreach (int skillId in toAttach)
            {
                if (skillId <= 0) continue;
                if (!abilityComp.IdAbilities.ContainsKey(skillId))
                    abilityComp.AttachAbility(skillId);
            }
        }

        /// <summary>切换角色/武器时调用，重新加载槽位技能。</summary>
        public void LoadCharacterSlots(CharacterSlotConfig charConfig)
        {
            CharacterSlotConfig = charConfig;
            if (SlotRuntime != null && SlotConfig != null)
            {
                SlotRuntime.LoadCharacter(charConfig);
                EnsureSlotAbilitiesAttached();
                SyncPassiveSkillBuffs();
            }
        }

        /// <summary>
        /// 汇总三类来源（槽位链 / 学习树(外部) / 测试列表）并同步常驻被动 Buff。
        /// </summary>
        private void SyncPassiveSkillBuffs()
        {
            if (Combat == null) return;
            var comp = Combat.GetComponent<EGamePlay.Combat.PassiveSkillBuffComponent>();
            if (comp == null)
                comp = Combat.AddComponent<EGamePlay.Combat.PassiveSkillBuffComponent>();

            var ids = PoolManager.Instance.TryGet<List<int>>();
            ids.Clear();

            CollectPassiveSkillIdsFromSlots(ids);
            CollectPassiveSkillIdsFromLearnedTable(ids);
            CollectPassiveSkillIdsFromExtraList(ids);

            comp.SyncFromPassiveSkillIds(ids);
            PoolManager.Instance.Return(ids);
        }

        private void CollectPassiveSkillIdsFromExtraList(List<int> outIds)
        {
            if (ExtraPassiveSkillIds == null || ExtraPassiveSkillIds.Count == 0) return;
            for (int i = 0; i < ExtraPassiveSkillIds.Count; i++)
            {
                int id = ExtraPassiveSkillIds[i];
                if (id > 0) outIds.Add(id);
            }
        }

        /// <summary>
        /// 从“角色技能表/学习树系统”收集被动技能ID。
        /// 这里先留空实现，方便你后续把真实系统接进来（例如：角色成长、天赋、装备词条）。
        /// </summary>
        protected virtual void CollectPassiveSkillIdsFromLearnedTable(List<int> outIds)
        {
            // Intentionally empty (project-specific integration point).
        }

        private void CollectPassiveSkillIdsFromSlots(List<int> outIds)
        {
            if (SlotRuntime == null) return;

            var tmp = PoolManager.Instance.TryGet<HashSet<int>>();
            tmp.Clear();
            SlotRuntime.GetAllSkillIdsToAttach(tmp);

            foreach (int skillId in tmp)
            {
                if (skillId <= 0) continue;
                var setting = SkillSettingMgr.Instance.GetSkillDemoSetting(skillId);
                if (setting == null) continue;
                if (setting.Type == EGamePlay.Combat.AbilityType.PassiveSkill.ToString())
                    outIds.Add(skillId);
            }

            PoolManager.Instance.Return(tmp);
        }

        private void AddSpellInfo(int skillId, int sort)
        {
            if (!CDTimer.IsCDEnd(skillId))
                return;

            CDTimer.StartCooldown(skillId);

            var info = PoolManager.Instance.TryGet<SkillSpellInfo>();
            info.Target = LockSystem.Instance?.LockedCombatEntity ?? Target;
            info.Point = MathHelper.GetPositionInFront(Combat.Position, Combat.Rotation, 3f);
            info.SkillId = skillId;
            info.Sort = sort;
            Combat.GetComponent<SpellComponent>().AddSkillSpellInfo(info);
            InputRecordsClear();
        }

        //技能释放的实现思想
        //1.当玩家通过ConfigurableInputManager输入后，加入到输入列表中进行缓存（缓存是为了兼容玩家的提前部分输入，保证流畅度）
        //2.当前技能可以被打断后，即Runner进入IsMainFinish后。从待释放列表中依次按照优先级取出，进行各种判断，选出能释放的进行释放。
        //3.最后清空待释放列表。等待新的加入

        public void AddInputRecord(InputListernType cmd, PressType type, InputCallBackType inputCallBackType = InputCallBackType.Performed)
        {
            _records.Add(new InputRecord
            {
                Command = cmd,
                PressType = type,
                InputCallBackType = inputCallBackType,
                timestamp = GameTimeManager.WorldTime
            });
        }

        public void ChangeInputMoveState(bool state)
        {
            Combat.ChangeInputMoveState(state);
        }

        // 供轨道调用 //是否有对应的操作
        //未来看看是否能优化一下，每帧遍历不是很友好
        public bool CheckAndConsume(InputListernType cmd, PressType type, InputCallBackType inputCallBackType = InputCallBackType.Performed, float customTimeout = -1f)
        {
            float timeout = customTimeout > 0 ? customTimeout : InputTimeout;
            float now = GameTimeManager.WorldTime;

            // 从后往前遍历，RemoveAt 不影响尚未检查的元素
            for (int i = _records.Count - 1; i >= 0; i--)
            {
                var record = _records[i];
                if (record.Command != cmd || record.PressType != type || record.InputCallBackType != inputCallBackType)
                {
                    continue;
                }

                // 超过预输入有效时长则丢弃
                if ((now - record.timestamp) >= timeout)
                {
                    _records.RemoveAt(i);
                    continue;
                }

                _records.RemoveAt(i);
                return true;
            }

            return false;
        }

        public bool HasValidInput(InputListernType cmd, PressType type, InputCallBackType inputCallBackType = InputCallBackType.Performed)
        {
            for (int i = _records.Count - 1; i >= 0; i--)
            {
                var record = _records[i];
                if (record.Command == cmd && record.PressType == type && record.InputCallBackType == inputCallBackType)
                {
                    return true;
                }
            }

            return false;
        }

        public void InputRecordsClear()
        {
            this._records.Clear();
        }

        public bool IsHadInputRecords()
        {
            return this._records.Count > 0;
        }

    }
}
