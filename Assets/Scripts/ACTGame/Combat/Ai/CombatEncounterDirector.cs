using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;

namespace ACTGameEditor.Combat.Ai
{
    /// <summary>
    /// 战局导演。Melee Token=1，Relax 停发牌，F 切片 4 槽站位。
    /// 发牌为「拍卖制」：Brain 登记意愿（TTL 续期），导演下一 Tick 按分（距离×屏内×槽位×欲望）竞拍发放；
    /// 欲望随时间增长、出手清零（车轮战）；两次发放间隔 GrantInterval（拍卖周期）。
    /// 用世界钟计租约 / 个人 CD / 欲望 / Tempo；不乘单个敌人实体钟。
    /// </summary>
    public sealed class CombatEncounterDirector : Entity
    {
        /// <summary>同屏登记上限（热路径固定数组）。</summary>
        public const int Capacity = 16;

        /// <summary>调参资产（Config/Ai/DefaultEncounter）；缺资产用内置默认。难度只改这里。</summary>
        static EncounterDirectorProfile P => EncounterDirectorProfile.Active;

        /// <summary>战局单例；由 <see cref="CombatContext"/> 子实体持有。</summary>
        public static CombatEncounterDirector Instance { get; private set; }

        struct EnemyRecord
        {
            public CombatEntity Entity;
            public float CooldownUntil;
            /// <summary>动态进攻欲望：随时间增长，出手后清零；竞拍打分因子。</summary>
            public float Desire;
            /// <summary>近战攻击请求有效期（世界钟）；过期视为不想打。</summary>
            public float WantMeleeUntil;
            /// <summary>Special（精英重击）请求有效期。</summary>
            public float WantSpecialUntil;
            /// <summary>个体保距，槽分配取场上活人均值。</summary>
            public float KeepDistance;
            /// <summary>欲望增速额外项（精英拉开）。</summary>
            public float DesireBonusPerSec;
            /// <summary>登记时缓存，抢夺热路径不 GetComponent。</summary>
            public EnemyBrainComponent Brain;
        }

        struct MeleeSlot
        {
            public CombatEntity Holder;
            public float LeaseExpire;
            public float CommittedAt;
            /// <summary>发放时刻；<see cref="StealGrace"/> 保护期内不可被抢。</summary>
            public float GrantedAt;
            public bool Committed => Holder != null && CommittedAt > 0f;
        }

        readonly EnemyRecord[] _enemies = new EnemyRecord[Capacity];
        readonly CombatEntity[] _livingScratch = new CombatEntity[Capacity];
        readonly EncounterSlotAssigner _slots = new();
        int _count;
        MeleeSlot _melee;
        /// <summary>精英重击牌（Special 预算默认 1，场上有精英才有申请者）。不抢夺。</summary>
        MeleeSlot _special;
        Camera _frameCamera;
        float _relaxUntil;
        float _nextSlotAssign;
        float _nextGrantAt;
        bool _slotsDirty;

        /// <summary>本场焦点；P0 为本地玩家。</summary>
        public CombatEntity FocusTarget { get; private set; }

        /// <summary>战局节奏。E 只切 Build / Relax。</summary>
        public EncounterTempo Tempo { get; private set; } = EncounterTempo.Build;

        /// <summary>Relax / Punish 时禁止新发牌。已承诺或占轴的不收回。</summary>
        public bool IsTokenGrantBlocked =>
            Tempo == EncounterTempo.Relax || Tempo == EncounterTempo.Punish;

        /// <summary>当前近战 Token 持有者；无人持有为 null。</summary>
        public CombatEntity MeleeHolder => _melee.Holder;

        /// <summary>近战槽下标 0 前 / 1 左 / 2 右 / 3 后；未分配为 -1。</summary>
        public int GetMeleeSlotIndex(CombatEntity enemy)
        {
            if (enemy == null)
                return -1;
            return _slots.IndexOf(enemy.Id);
        }

        /// <summary>环绕锚点。有槽走 4 槽，否则走溢出环。半径用调用方 KeepDistance。</summary>
        public bool TryGetOrbitAnchor(CombatEntity enemy, float keepDistance, out Vector3 worldPos)
        {
            worldPos = default;
            CombatEntity focus = FocusTarget;
            if (enemy == null || enemy.IsDisposed || focus == null || focus.IsDisposed)
                return false;
            if (keepDistance < 0.5f)
                keepDistance = EncounterSlotAssigner.DefaultKeepDistance;
            int slot = _slots.IndexOf(enemy.Id);
            worldPos = EncounterSlotAssigner.WorldAnchor(focus, slot, keepDistance, enemy.Id);
            worldPos.y = enemy.Position.y;
            return true;
        }

        /// <summary>实体是否在画幅内。无相机视为在屏内。D 选招 ScreenScore 用。</summary>
        public bool IsOnScreen(CombatEntity enemy)
        {
            if (enemy == null || enemy.IsDisposed)
                return false;
            return ComputeOnScreen(enemy.Position);
        }

        /// <inheritdoc />
        public override void Awake()
        {
            Instance = this;
            // 编辑器里对调参资产的改动按 Play 生效
            EncounterDirectorProfile.ResetCache();
            _ = EncounterDirectorProfile.Active;
        }

        /// <inheritdoc />
        public override void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            _count = 0;
            _melee = default;
            _special = default;
            FocusTarget = null;
            _frameCamera = null;
            Tempo = EncounterTempo.Build;
            _relaxUntil = 0f;
            _nextSlotAssign = 0f;
            _nextGrantAt = 0f;
            _slotsDirty = false;
            _slots.Clear();
        }

        /// <inheritdoc />
        public override void OnReset() => OnDestroy();

        /// <summary>登记敌人。重复登记返回 true；满员拒绝。半径/欲望增速由 Brain 传入。</summary>
        public bool Register(CombatEntity enemy, float keepDistance = 0f, float desireBonusPerSec = 0f)
        {
            if (enemy == null || enemy.IsDisposed)
                return false;
            int existing = FindIndex(enemy.Id);
            if (existing >= 0)
            {
                if (_enemies[existing].Brain == null)
                    _enemies[existing].Brain = enemy.GetComponent<EnemyBrainComponent>();
                if (keepDistance >= 0.5f)
                    _enemies[existing].KeepDistance = keepDistance;
                if (desireBonusPerSec != 0f)
                    _enemies[existing].DesireBonusPerSec = desireBonusPerSec;
                return true;
            }
            if (_count >= Capacity)
            {
                GameLog.CombatError($"[Encounter] 登记已满 Capacity={Capacity} id={enemy.Id}");
                return false;
            }

            if (keepDistance < 0.5f)
                keepDistance = EncounterSlotAssigner.DefaultKeepDistance;

            // 欲望初始值按 Id 散布，避免同批刷出的怪欲望完全相同
            _enemies[_count++] = new EnemyRecord
            {
                Entity = enemy,
                CooldownUntil = 0f,
                Desire = 0.3f + 0.5f * Hash01(enemy.Id),
                WantMeleeUntil = 0f,
                WantSpecialUntil = 0f,
                KeepDistance = keepDistance,
                DesireBonusPerSec = desireBonusPerSec,
                Brain = enemy.GetComponent<EnemyBrainComponent>(),
            };
            _slotsDirty = true;
            AssignMeleeSlots();
            return true;
        }

        /// <summary>注销并还牌。对象池回收时调用。</summary>
        public void Unregister(long entityId)
        {
            int index = FindIndex(entityId);
            if (index < 0)
            {
                if (_melee.Holder != null && _melee.Holder.Id == entityId)
                    ClearSlot(ref _melee);
                if (_special.Holder != null && _special.Holder.Id == entityId)
                    ClearSlot(ref _special);
                _slots.Remove(entityId);
                return;
            }

            CombatEntity entity = _enemies[index].Entity;
            if (_melee.Holder != null && _melee.Holder.Id == entityId)
                ReleaseInternal(ref _melee, entity, TokenReleaseReason.Unregistered, applyCooldown: false);
            if (_special.Holder != null && _special.Holder.Id == entityId)
                ReleaseInternal(ref _special, entity, TokenReleaseReason.Unregistered, applyCooldown: false);

            int last = _count - 1;
            _enemies[index] = _enemies[last];
            _enemies[last] = default;
            _count = last;
            _slots.Remove(entityId);
            _slotsDirty = true;
        }

        /// <summary>是否持有指定种类 Token。</summary>
        public bool HasToken(CombatEntity enemy, EncounterTokenKind kind)
        {
            if (enemy == null)
                return false;
            if (kind == EncounterTokenKind.Melee)
                return _melee.Holder != null && _melee.Holder.Id == enemy.Id;
            if (kind == EncounterTokenKind.Special)
                return _special.Holder != null && _special.Holder.Id == enemy.Id;
            return false;
        }

        /// <summary>
        /// 申请近战进攻权。空闲时不再先到先得：登记意愿由导演下一 Tick 竞拍发放（Brain 每帧续期）；
        /// 已持有返回 true；持有未承诺且过保护期时可被分数更高者抢走。
        /// </summary>
        public bool TryAcquireToken(CombatEntity enemy, EncounterTokenKind kind)
        {
            if (kind == EncounterTokenKind.Ranged || enemy == null || enemy.IsDisposed || enemy.IsDead)
                return false;
            if (kind == EncounterTokenKind.Melee && P.MeleeBudget <= 0)
                return false;
            if (kind == EncounterTokenKind.Special && P.SpecialBudget <= 0)
                return false;

            int index = FindIndex(enemy.Id);
            if (index < 0)
            {
                Register(enemy);
                index = FindIndex(enemy.Id);
                if (index < 0)
                    return false;
            }

            if (HasToken(enemy, kind))
                return true;
            if (IsTokenGrantBlocked)
                return false;
            if (GameTimeManager.WorldTime < _enemies[index].CooldownUntil)
                return false;
            if (!enemy.IsCanSpellSkill)
                return false;

            // 不再先到先得：登记进攻意愿，导演下一 Tick 竞拍发牌（Brain 每帧会续期）
            float wantUntil = GameTimeManager.WorldTime + P.WantTtl;
            if (kind == EncounterTokenKind.Melee)
                _enemies[index].WantMeleeUntil = wantUntil;
            else
                _enemies[index].WantSpecialUntil = wantUntil;

            // Special 不抢：精英稀有不值得抢夺复杂度；等竞拍或持牌者自然还牌
            if (kind == EncounterTokenKind.Special)
                return false;

            CombatEntity holder = _melee.Holder;
            if (holder == null || holder.IsDisposed)
                return false;

            if (_melee.Committed)
                return false;
            if (GameTimeManager.WorldTime - _melee.GrantedAt < P.StealGrace)
                return false;

            float requester = Score(enemy);
            float current = Score(holder);
            int holderIndex = FindIndex(holder.Id);
            EnemyBrainComponent holderBrain = holderIndex >= 0 ? _enemies[holderIndex].Brain : null;
            if (holderBrain != null && holderBrain.IsTacticalWindup)
                current *= P.StealWindupProtect;
            if (requester <= current * P.StealHysteresis)
                return false;

            ReleaseInternal(ref _melee, holder, TokenReleaseReason.Stolen, applyCooldown: true);
            Grant(ref _melee, enemy);
            return true;
        }

        /// <summary>Enqueue 成功后调用，租约改为承诺，禁止再被抢。</summary>
        public void NotifySkillCommitted(CombatEntity enemy, EncounterTokenKind kind)
        {
            if (enemy == null)
                return;
            if (kind == EncounterTokenKind.Melee)
                CommitSlot(ref _melee, enemy);
            else if (kind == EncounterTokenKind.Special)
                CommitSlot(ref _special, enemy);
        }

        static void CommitSlot(ref MeleeSlot slot, CombatEntity enemy)
        {
            if (slot.Holder == null || slot.Holder.Id != enemy.Id)
                return;
            if (slot.CommittedAt <= 0f)
                slot.CommittedAt = GameTimeManager.WorldTime;
        }

        /// <summary>还牌。非持有者忽略。</summary>
        public void ReleaseToken(CombatEntity enemy, EncounterTokenKind kind, TokenReleaseReason reason)
        {
            if (enemy == null)
                return;
            bool applyCooldown = reason != TokenReleaseReason.Unregistered
                && reason != TokenReleaseReason.TempoRecall
                && reason != TokenReleaseReason.Death;
            if (kind == EncounterTokenKind.Melee)
                ReleaseInternal(ref _melee, enemy, reason, applyCooldown);
            else if (kind == EncounterTokenKind.Special)
                ReleaseInternal(ref _special, enemy, reason, applyCooldown);
        }

        /// <summary>玩家闪避。perfect=闪过攻击（极限闪）；false=翻滚结束未接到刀。</summary>
        public void NotifyPlayerDodge(bool perfect)
        {
            if (Tempo == EncounterTempo.Punish)
                return;
            BeginRelax(perfect ? P.RelaxPerfectDodge : P.RelaxNormalDodge);
        }

        /// <summary>敌人攻击轴结束且盒从未碰到玩家。不打断已出手轴。</summary>
        public void NotifyAttackWhiff(CombatEntity enemy)
        {
            if (enemy == null || enemy.IsDisposed)
                return;
            if (Tempo == EncounterTempo.Punish)
                return;
            BeginRelax(P.RelaxWhiff);
        }

        /// <summary>世界钟 Tick：Tempo、焦点、欲望、竞拍发放、回收、槽位。应在 CombatContext.Update 之前。</summary>
        public void Tick(float worldDelta)
        {
            _frameCamera = Camera.main;
            TickTempo();
            RefreshFocus();
            ValidateSlotHolders();
            CompactDisposed();
            ExpireLeasesAndFailedLaunches();
            TickDesire(worldDelta);
            ResolveMeleeWant();
            ResolveSpecialWant();
            AssignMeleeSlots();
#if UNITY_EDITOR
            DrawHolderDebug();
#endif
        }

        void TickTempo()
        {
            if (Tempo != EncounterTempo.Relax)
                return;
            if (GameTimeManager.WorldTime < _relaxUntil)
                return;
            Tempo = EncounterTempo.Build;
            _relaxUntil = 0f;
            GameLog.CombatDebug("[Encounter] Relax -> Build");
        }

        void BeginRelax(float seconds)
        {
            if (seconds <= 0f)
                return;
            float until = GameTimeManager.WorldTime + seconds;
            if (Tempo == EncounterTempo.Relax && until <= _relaxUntil)
                return;
            Tempo = EncounterTempo.Relax;
            _relaxUntil = until;
            RecallUncommittedMelee();
            GameLog.CombatDebug($"[Encounter] Relax {seconds:0.00}s");
        }

        void RecallUncommittedMelee()
        {
            RecallUncommittedSlot(ref _melee);
            RecallUncommittedSlot(ref _special);
        }

        void RecallUncommittedSlot(ref MeleeSlot slot)
        {
            CombatEntity holder = slot.Holder;
            if (holder == null || holder.IsDisposed)
                return;
            if (slot.Committed)
                return;
            ReleaseInternal(ref slot, holder, TokenReleaseReason.TempoRecall, applyCooldown: false);
        }

        void RefreshFocus()
        {
            PlayerManager mgr = PlayerManager.Instance;
            ActPlayer local = mgr != null ? mgr.LocalPlayer : null;
            CombatEntity combat = local != null ? local.Combat : null;
            if (combat == null || combat.IsDisposed || combat.IsDead)
            {
                FocusTarget = null;
                return;
            }

            FocusTarget = combat;
        }

        void ValidateSlotHolders()
        {
            ValidateSlotHolder(ref _melee);
            ValidateSlotHolder(ref _special);
        }

        void ValidateSlotHolder(ref MeleeSlot slot)
        {
            CombatEntity holder = slot.Holder;
            if (holder == null)
                return;
            if (holder.IsDisposed)
            {
                ClearSlot(ref slot);
                return;
            }

            if (holder.IsDead)
            {
                ReleaseInternal(ref slot, holder, TokenReleaseReason.Death, applyCooldown: false);
                return;
            }

            if (IsInterrupted(holder))
                ReleaseInternal(ref slot, holder, TokenReleaseReason.Interrupted, applyCooldown: true);
        }

        void CompactDisposed()
        {
            for (int i = _count - 1; i >= 0; i--)
            {
                CombatEntity entity = _enemies[i].Entity;
                if (entity == null || entity.IsDisposed)
                    UnregisterAt(i);
            }
        }

        void ExpireLeasesAndFailedLaunches()
        {
            ExpireLease(ref _melee);
            ExpireLease(ref _special);
        }

        void ExpireLease(ref MeleeSlot slot)
        {
            CombatEntity holder = slot.Holder;
            if (holder == null)
                return;

            float now = GameTimeManager.WorldTime;
            if (!slot.Committed)
            {
                if (now >= slot.LeaseExpire)
                    ReleaseInternal(ref slot, holder, TokenReleaseReason.LeaseExpired, applyCooldown: true);
                return;
            }

            ISkillExecutionHandle exec = holder.ActiveExecution;
            bool occupying = exec != null && !exec.IsDisposed;
            if (occupying)
                return;
            if (now - slot.CommittedAt >= P.FailedLaunchTimeout)
                ReleaseInternal(ref slot, holder, TokenReleaseReason.FailedLaunch, applyCooldown: true);
        }

        /// <summary>欲望随时间增长（槽内更快）；持牌者不涨（它马上要打了）。</summary>
        void TickDesire(float worldDelta)
        {
            if (worldDelta <= 0f)
                return;

            for (int i = 0; i < _count; i++)
            {
                CombatEntity enemy = _enemies[i].Entity;
                if (enemy == null || enemy.IsDisposed || enemy.IsDead)
                    continue;
                if (_melee.Holder == enemy)
                    continue;

                float rate = P.DesireBasePerSec + _enemies[i].DesireBonusPerSec;
                if (_slots.IndexOf(enemy.Id) >= 0)
                    rate += P.DesireSlotBonusPerSec;
                float desire = _enemies[i].Desire + rate * worldDelta;
                _enemies[i].Desire = desire > P.DesireCap ? P.DesireCap : desire;
            }
        }

        /// <summary>
        /// 拍卖发放：牌空闲时，在登记了进攻意愿的敌人里按分取最高（含欲望因子）。
        /// 一拍一发（Profile GrantInterval），即「拍卖时间周期 = 进攻频率」。
        /// </summary>
        void ResolveMeleeWant() => ResolveWant(ref _melee, P.MeleeBudget, melee: true);

        /// <summary>Special 牌发放：不抢夺，只等竞拍与持牌者自然还牌。</summary>
        void ResolveSpecialWant() => ResolveWant(ref _special, P.SpecialBudget, melee: false);

        void ResolveWant(ref MeleeSlot slot, int budget, bool melee)
        {
            if (budget <= 0 || slot.Holder != null || IsTokenGrantBlocked)
                return;

            float now = GameTimeManager.WorldTime;
            if (now < _nextGrantAt)
                return;

            CombatEntity best = null;
            float bestScore = 0f;
            for (int i = 0; i < _count; i++)
            {
                float wantUntil = melee ? _enemies[i].WantMeleeUntil : _enemies[i].WantSpecialUntil;
                if (wantUntil < now)
                    continue;

                CombatEntity enemy = _enemies[i].Entity;
                if (enemy == null || enemy.IsDisposed || enemy.IsDead)
                    continue;
                if (now < _enemies[i].CooldownUntil)
                    continue;
                if (!enemy.IsCanSpellSkill)
                    continue;

                float score = Score(enemy);
                if (score <= bestScore)
                    continue;

                bestScore = score;
                best = enemy;
            }

            if (best == null)
                return;

            Grant(ref slot, best);
            _nextGrantAt = now + P.GrantInterval;
        }

        /// <summary>
        /// 内外圈轮换：外圈最渴的与内圈最寡的（都不算持牌者）差距够大则换槽，
        /// 避免永远只有内圈 4 只出手、外圈发呆。换入者欲望减半，防下一拍立刻换回。
        /// </summary>
        void RotateSlotsByDesire()
        {
            CombatEntity holder = _melee.Holder;
            int hungryIndex = -1;
            float hungryDesire = 0f;
            int slottyIndex = -1;
            float slottyDesire = float.MaxValue;

            for (int i = 0; i < _count; i++)
            {
                CombatEntity enemy = _enemies[i].Entity;
                if (enemy == null || enemy.IsDisposed || enemy.IsDead)
                    continue;
                if (holder != null && enemy.Id == holder.Id)
                    continue;

                if (_slots.IndexOf(enemy.Id) >= 0)
                {
                    if (_enemies[i].Desire < slottyDesire)
                    {
                        slottyDesire = _enemies[i].Desire;
                        slottyIndex = i;
                    }
                }
                else if (_enemies[i].Desire > hungryDesire)
                {
                    hungryDesire = _enemies[i].Desire;
                    hungryIndex = i;
                }
            }

            if (hungryIndex < 0 || slottyIndex < 0)
                return;
            if (hungryDesire <= slottyDesire + P.SwapDesireMargin)
                return;

            _slots.Swap(_enemies[slottyIndex].Entity.Id, _enemies[hungryIndex].Entity.Id);
            _enemies[hungryIndex].Desire = hungryDesire * 0.5f;
            _slotsDirty = true;
        }

        /// <summary>Id 哈希到 [0,1]，用于新怪欲望初始散布。</summary>
        static float Hash01(long id)
        {
            unchecked
            {
                uint h = (uint)id * 747796405u;
                h ^= h >> 16;
                h *= 0x7feb352d;
                h ^= h >> 15;
                return (h & 1023u) * (1f / 1023f);
            }
        }

        void Grant(ref MeleeSlot slot, CombatEntity enemy)
        {
            float now = GameTimeManager.WorldTime;
            slot.Holder = enemy;
            slot.LeaseExpire = now + P.GrantTimeout;
            slot.CommittedAt = 0f;
            slot.GrantedAt = now;
            int index = FindIndex(enemy.Id);
            if (index >= 0)
            {
                _enemies[index].WantMeleeUntil = 0f;
                _enemies[index].WantSpecialUntil = 0f;
            }
            _slotsDirty = true;
            GameLog.CombatDebug($"[Encounter] Grant id={enemy.Id}");
        }

        void ReleaseInternal(ref MeleeSlot slot, CombatEntity enemy, TokenReleaseReason reason, bool applyCooldown)
        {
            if (slot.Holder == null || enemy == null || slot.Holder.Id != enemy.Id)
                return;

            GameLog.CombatDebug($"[Encounter] Release id={enemy.Id} reason={reason}");
            ClearSlot(ref slot);
            _slotsDirty = true;

            int index = FindIndex(enemy.Id);
            if (index < 0)
                return;

            // 出过手（或占轴被打断）清欲望，把机会让给别人（车轮战）；被抢/租约到期/TempoRecall 保留欲望
            if (reason == TokenReleaseReason.SkillFinished || reason == TokenReleaseReason.Interrupted)
                _enemies[index].Desire = 0f;

            if (!applyCooldown || enemy.IsDisposed)
                return;
            _enemies[index].CooldownUntil = GameTimeManager.WorldTime + P.PerEnemyCooldown;
        }

        static void ClearSlot(ref MeleeSlot slot)
        {
            slot.Holder = null;
            slot.LeaseExpire = 0f;
            slot.CommittedAt = 0f;
        }

        void UnregisterAt(int index)
        {
            long id = 0;
            CombatEntity entity = _enemies[index].Entity;
            if (entity != null)
                id = entity.Id;

            int last = _count - 1;
            _enemies[index] = _enemies[last];
            _enemies[last] = default;
            _count = last;
            if (id != 0)
            {
                _slots.Remove(id);
                _slotsDirty = true;
            }
        }

        void AssignMeleeSlots()
        {
            float now = GameTimeManager.WorldTime;
            if (!_slotsDirty && now < _nextSlotAssign)
                return;
            _slotsDirty = false;
            _nextSlotAssign = now + EncounterSlotAssigner.AssignInterval;

            int living = 0;
            for (int i = 0; i < _count; i++)
            {
                CombatEntity enemy = _enemies[i].Entity;
                if (enemy == null || enemy.IsDisposed || enemy.IsDead)
                    continue;
                _livingScratch[living++] = enemy;
            }

            RotateSlotsByDesire();
            _slots.Reassign(FocusTarget, _melee.Holder, _livingScratch, living, SlotKeepDistance());
            for (int i = living; i < Capacity; i++)
                _livingScratch[i] = null;
        }

        /// <summary>槽分配/调试柱用场上活人 KeepDistance 均值；个体 Orbit 仍走自己的 Profile。</summary>
        float SlotKeepDistance()
        {
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < _count; i++)
            {
                CombatEntity enemy = _enemies[i].Entity;
                if (enemy == null || enemy.IsDisposed || enemy.IsDead)
                    continue;
                float keep = _enemies[i].KeepDistance;
                if (keep < 0.5f)
                    keep = EncounterSlotAssigner.DefaultKeepDistance;
                sum += keep;
                n++;
            }

            return n > 0 ? sum / n : EncounterSlotAssigner.DefaultKeepDistance;
        }

        int FindIndex(long entityId)
        {
            for (int i = 0; i < _count; i++)
            {
                CombatEntity entity = _enemies[i].Entity;
                if (entity != null && entity.Id == entityId)
                    return i;
            }

            return -1;
        }

        static bool IsInterrupted(CombatEntity enemy)
        {
            if (enemy.StateDirector != null && enemy.StateDirector.IsControl)
                return true;
            return enemy.CurState == PlayerStateEnum.Hit;
        }

        float Score(CombatEntity enemy)
        {
            if (enemy == null || enemy.IsDisposed || enemy.IsDead || !enemy.IsCanSpellSkill)
                return 0f;

            CombatEntity focus = FocusTarget;
            float dist = 0f;
            if (focus != null)
            {
                Vector3 to = enemy.Position - focus.Position;
                to.y = 0f;
                dist = to.magnitude;
            }

            float distScore = Mathf.Clamp(1f - dist / P.DistanceScoreSpan, 0.2f, 1f);
            float screenScore = ComputeOnScreen(enemy.Position) ? 1f : P.OffscreenScore;
            int slot = _slots.IndexOf(enemy.Id);
            float slotScore = slot switch
            {
                0 => 1f,
                1 => 0.88f,
                2 => 0.88f,
                3 => 0.32f,
                _ => 0.22f,
            };
            // 欲望：出手过的 0.25 起步，久未出手的趋近 1（雷火文：欲望高中标概率大）
            int index = FindIndex(enemy.Id);
            float desire = index >= 0 ? _enemies[index].Desire : 0f;
            float desireScore = 0.25f + 0.75f * Mathf.Clamp01(desire);
            return distScore * screenScore * slotScore * desireScore;
        }

        bool ComputeOnScreen(Vector3 worldPos)
        {
            Camera cam = _frameCamera != null ? _frameCamera : Camera.main;
            if (cam == null)
                return true;
            Vector3 vp = cam.WorldToViewportPoint(worldPos);
            return vp.z > 0f && vp.x > 0.05f && vp.x < 0.95f && vp.y > 0.05f && vp.y < 0.95f;
        }

#if UNITY_EDITOR
        void DrawHolderDebug()
        {
            CombatEntity focus = FocusTarget;
            if (focus != null && !focus.IsDisposed)
            {
                if (Tempo == EncounterTempo.Relax)
                    Debug.DrawRay(focus.Position + Vector3.up * 2.4f, Vector3.up * 0.7f, Color.green);

                float slotRadius = SlotKeepDistance();
                for (int s = 0; s < EncounterSlotAssigner.MeleeSlotCount; s++)
                {
                    Vector3 anchor = EncounterSlotAssigner.WorldAnchor(
                        focus, s, slotRadius, 0);
                    anchor.y = focus.Position.y + 0.15f;
                    Debug.DrawRay(anchor, Vector3.up * 0.4f, s == 0 ? Color.red : Color.white);
                }

                // 欲望条：头顶青色横线越长越想出手（出手后清空）
                for (int i = 0; i < _count; i++)
                {
                    CombatEntity enemy = _enemies[i].Entity;
                    if (enemy == null || enemy.IsDisposed || enemy.IsDead)
                        continue;
                    Vector3 basePos = enemy.Position + Vector3.up * 2.35f;
                    Debug.DrawRay(basePos, Vector3.right * (_enemies[i].Desire * 0.6f), Color.cyan);
                }
            }

            CombatEntity holder = _melee.Holder;
            if (holder == null || holder.IsDisposed)
                return;
            Vector3 from = holder.Position + Vector3.up * 2.1f;
            Debug.DrawRay(from, Vector3.up * 0.6f, Color.red);
            if (focus != null && !focus.IsDisposed)
                Debug.DrawLine(from, focus.Position + Vector3.up, Color.red);
        }
#endif
    }
}
