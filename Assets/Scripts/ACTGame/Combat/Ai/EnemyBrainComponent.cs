using EGamePlay;
using EGamePlay.Combat;
using EGamePlay.Unity;
using UnityEngine;

namespace ACTGameEditor.Combat.Ai
{
    /// <summary>
    /// D 效用选招；E Relax；F 走向导演 4 槽锚点。
    /// Tick 走宿主 <see cref="CombatTimeClock"/>；Alert / 选招节流用世界钟；Token 仍走导演。
    /// </summary>
    public sealed class EnemyBrainComponent : EGamePlay.Component
    {
        public override bool DefaultEnable { get; set; } = true;
        public override bool IsNeedUpdate { get; protected set; } = true;

        const float PlanarEpsilonSq = 0.0001f;
        const float AnchorArrive = 0.55f;
        const float AnchorLeave = 1.25f;

        CombatEntity _owner;
        InputMoveComponent _inputMove;
        ActSpellComponent _spell;
        AbilityComponent _abilities;
        AnimComponent _anim;
        EnemyBrainProfile _profile;
        EnemyMoveSet _runtimeMoveSet;
        bool _ownsFallback;
        bool _registered;

        readonly AiMoveInputProvider _move = new();
        readonly AiAimBasisCameraProvider _aim = new();
        readonly AiTargetFacingProvider _facing = new();

        float _acquireMaxDist = 4.4f;
        bool _installed;
        bool _inCombat;
        bool _castPending;
        bool _hasMove;
        bool _isElite;
        bool _followUpUsed;
        float _alertEndWorld;
        float _selectAtWorld;
        float _recoverRemain;
        float _nextFeintAt;
        float _castPendingSince;
        int _lastSkillId;
        EnemyMoveEntry _selected;
        EnemyTacticalState _state = EnemyTacticalState.Idle;

        EnemyMoveSet MoveSet =>
            _profile != null && _profile.MoveSet != null && _profile.MoveSet.Moves != null && _profile.MoveSet.Moves.Length > 0
                ? _profile.MoveSet
                : _runtimeMoveSet;

        float AggroRadius => _profile != null ? _profile.AggroRadius : 20f;
        float AlertDelayFirst => _profile != null ? _profile.AlertDelayFirst : 0.35f;
        float AlertDelayRepeat => _profile != null ? _profile.AlertDelayRepeat : 0.1f;
        float KeepDistance => _profile != null ? _profile.KeepDistance : 3.6f;
        float KeepDeadzone => _profile != null ? _profile.KeepDeadzone : 0.35f;
        float DefaultRecover => _profile != null ? _profile.RecoverSeconds : 0.65f;
        float SelectInterval => _profile != null ? _profile.SelectInterval : 0.1f;

        /// <summary>当前战术态，只读，供调试。</summary>
        public EnemyTacticalState State => _state;

        /// <summary>战术 Windup（持牌进带）。导演抢夺时乘 <see cref="EncounterDirectorProfile.StealWindupProtect"/>。</summary>
        public bool IsTacticalWindup => _state == EnemyTacticalState.Windup;

        /// <inheritdoc />
        public override void Awake()
        {
            _owner = GetEntity<CombatEntity>();
            if (_owner == null)
                return;

            _inputMove = _owner.GetComponent<InputMoveComponent>();
            _spell = _owner.GetComponent<ActSpellComponent>();
            _abilities = _owner.GetComponent<AbilityComponent>();
            _anim = _owner.GetComponent<AnimComponent>();

            // 档位由生成时的模型位决定（EnemyA=精英）；AddComponent 时 Awake 即跑，AttackPlayer 已在 Init 里赋好
            _isElite = _owner.AttackPlayer != null && _owner.AttackPlayer.ModelType == AgentModelType.EnemyA;
            _profile = EnemyBrainProfile.LoadForArchetype(_isElite, out _ownsFallback);
            if (MoveSet == null)
                _runtimeMoveSet = EnemyMoveSet.CreateGruntFallback();

            RefreshAcquireMax();
            InstallLocomotion();
            EnemySkillSelector.AttachAbilities(_abilities, MoveSet);
            TryRegisterEncounter();
            ApplyPoiseFromProfile();
            ApplyDazeFromProfile();
            _state = EnemyTacticalState.Idle;
        }

        void RefreshAcquireMax()
        {
            float acquire = KeepDistance + (_profile != null ? _profile.AcquirePadding : 0.8f);
            EnemyMoveSet set = MoveSet;
            if (set?.Moves != null)
            {
                int count = set.Moves.Length;
                if (count > EnemyMoveSet.MaxMoves)
                    count = EnemyMoveSet.MaxMoves;
                for (int i = 0; i < count; i++)
                {
                    EnemyMoveEntry entry = set.Moves[i];
                    if (!entry.RequiresToken || entry.TokenKind != EncounterTokenKind.Melee)
                        continue;
                    if (entry.MaxRange > acquire)
                        acquire = entry.MaxRange;
                }
            }

            if (acquire > AggroRadius)
                acquire = AggroRadius;
            _acquireMaxDist = acquire;
        }

        void ApplyPoiseFromProfile()
        {
            CombatPoiseComponent poise = _owner.GetComponent<CombatPoiseComponent>();
            if (poise == null)
                return;
            int anti = _profile != null ? _profile.AntiInterruptBase : 0;
            int armor = _profile != null ? _profile.SuperArmorBonus : CombatInterrupt.DefaultSuperArmorBonus;
            poise.Configure(anti, armor, _isElite);
        }

        void ApplyDazeFromProfile()
        {
            CombatMeterComponent meter = _owner.DazeMeter;
            if (meter == null)
                return;
            meter.Configure(_isElite ? DazeTierId.Elite : DazeTierId.Grunt);
        }

        void InstallLocomotion()
        {
            if (_installed || _inputMove == null)
                return;

            _inputMove.InstallAiDrivers(_move, _aim, _facing);
            CombatAnimDirector director = _anim?.Director;
            if (director != null)
                director.MoveIntentProvider = _move.GetAxis;

            _installed = true;
        }

        /// <inheritdoc />
        public override void Update(float deltaTime)
        {
            if (_owner == null || _owner.IsDisposed)
                return;

            TryRegisterEncounter();

            if (_owner.IsDead)
            {
                EnterDead();
                return;
            }

            if (deltaTime <= 0f)
                return;

            bool occupying = IsOccupyingAxis();
            if (EnemyHfsm.TryResolveForced(_owner, occupying, out EnemyTacticalState forced))
            {
                if (forced == EnemyTacticalState.Dead)
                {
                    EnterDead();
                    return;
                }

                if (forced == EnemyTacticalState.Hit
                    || forced == EnemyTacticalState.Control
                    || forced == EnemyTacticalState.Stagger)
                {
                    if (_state != EnemyTacticalState.Hit
                        && _state != EnemyTacticalState.Control
                        && _state != EnemyTacticalState.Stagger)
                        EnterInterrupted();
                    else
                        StopMove();
                    if (forced == EnemyTacticalState.Stagger)
                    {
                        CombatEntity staggerTarget = ResolveTarget();
                        if (staggerTarget != null)
                            AimAt(staggerTarget);
                    }
                    return;
                }

                _castPending = false;
                _state = EnemyTacticalState.Skill;
                StopMove();
                CombatEntity axisTarget = ResolveTarget();
                if (axisTarget != null)
                    AimAt(axisTarget);
                TryFollowUp(axisTarget);
                return;
            }

            if (_state == EnemyTacticalState.Hit
                || _state == EnemyTacticalState.Control
                || _state == EnemyTacticalState.Stagger)
                _state = _inCombat ? EnemyTacticalState.Approach : EnemyTacticalState.Idle;

            CombatEntity target = ResolveTarget();
            if (target == null)
            {
                EnterIdle();
                return;
            }

            Vector3 toTarget = target.Position - _owner.Position;
            toTarget.y = 0f;
            float distSq = toTarget.sqrMagnitude;
            float dist = distSq > PlanarEpsilonSq ? Mathf.Sqrt(distSq) : 0f;
            if (distSq > PlanarEpsilonSq)
            {
                _aim.SetPlanarForward(toTarget / dist);
                _facing.SetPoint(target.Position);
            }

            if (_state == EnemyTacticalState.Skill)
            {
                if (_castPending)
                {
                    // 等轴起来：只有「要牌的招丢了牌」或「超时才开轴」才放弃；
                    // 无牌招（假前摇）与连段第二击（仍持牌）都在此等
                    bool needsToken = _hasMove && _selected.RequiresToken;
                    bool lostToken = needsToken && !HasTokenFor(_selected.TokenKind);
                    bool launchTimeout = GameTimeManager.WorldTime - _castPendingSince > 0.5f;
                    if (lostToken || launchTimeout)
                    {
                        _castPending = false;
                        ClearSelection();
                        EnterOrbitOrApproach(dist);
                    }
                    else
                        StopMove();
                    return;
                }

                EnterRecover();
                TickRecover(dist, deltaTime);
                return;
            }

            if (_state == EnemyTacticalState.Recover)
            {
                TickRecover(dist, deltaTime);
                return;
            }

            if (_state == EnemyTacticalState.Idle || _state == EnemyTacticalState.Alert)
            {
                TickDetect(dist);
                return;
            }

            TryAcquireIfReady(dist);
            bool hasToken = HasAnyAttackToken();
            if (hasToken && _state != EnemyTacticalState.Windup)
                _state = EnemyTacticalState.Windup;
            else if (!hasToken && _state == EnemyTacticalState.Windup)
            {
                ClearSelection();
                EnterOrbitOrApproach(dist);
            }

            switch (_state)
            {
                case EnemyTacticalState.Approach:
                    TickApproach(dist, hasToken, target);
                    break;
                case EnemyTacticalState.Orbit:
                    TickOrbit(dist, hasToken, target);
                    break;
                case EnemyTacticalState.Windup:
                    TickWindup(dist, target);
                    break;
                default:
                    StopMove();
                    break;
            }

#if UNITY_EDITOR
            DrawStateDebug();
#endif
        }

        void TickDetect(float dist)
        {
            if (dist > AggroRadius)
            {
                EnterIdle();
                return;
            }

            if (_state == EnemyTacticalState.Idle)
            {
                _state = EnemyTacticalState.Alert;
                float delay = _inCombat ? AlertDelayRepeat : AlertDelayFirst;
                _alertEndWorld = GameTimeManager.WorldTime + delay;
                CombatAlertCue.Play(_owner, delay);
                StopMove();
                return;
            }

            StopMove();
            if (GameTimeManager.WorldTime < _alertEndWorld)
                return;

            _inCombat = true;
            _state = EnemyTacticalState.Approach;
            SteerToAnchor();
        }

        void TickApproach(float dist, bool hasToken, CombatEntity target)
        {
            if (hasToken)
            {
                _state = EnemyTacticalState.Windup;
                TickWindup(dist, target);
                return;
            }

            if (TryGetAnchorDistance(out float anchorDist) && anchorDist <= AnchorArrive)
            {
                _state = EnemyTacticalState.Orbit;
                TickOrbit(dist, false, target);
                return;
            }

            SteerToAnchor();
        }

        void TickOrbit(float dist, bool hasToken, CombatEntity target)
        {
            if (hasToken)
            {
                _state = EnemyTacticalState.Windup;
                TickWindup(dist, target);
                return;
            }

            if (!TryGetAnchorDistance(out float anchorDist) || anchorDist > AnchorLeave)
            {
                _state = EnemyTacticalState.Approach;
                SteerToAnchor();
                return;
            }

            SteerToAnchor();
            TryFeint(dist, target);
        }

        void TickWindup(float dist, CombatEntity target)
        {
            if (!HasAnyAttackToken())
            {
                ClearSelection();
                EnterOrbitOrApproach(dist);
                return;
            }

            if (_castPending)
            {
                StopMove();
                return;
            }

            if (!RefreshSelection(dist))
            {
                CloseTowardEngage(dist);
                return;
            }

            if (dist < _selected.MinRange)
            {
                _move.SetBackOff();
                return;
            }

            if (dist > _selected.MaxRange)
            {
                _move.SetClose();
                return;
            }

            StopMove();
            TryEnqueue(target);
        }

        bool RefreshSelection(float dist)
        {
            bool inBand = _hasMove && dist >= _selected.MinRange && dist <= _selected.MaxRange;
            if (inBand)
                return true;

            float now = GameTimeManager.WorldTime;
            if (_hasMove && now < _selectAtWorld)
                return true;

            EnemySelectQuery query = BuildQuery(dist);
            if (!EnemySkillSelector.TrySelect(in query, MoveSet, out EnemyMoveEntry picked, out _))
            {
                _hasMove = false;
                _selectAtWorld = now + SelectInterval;
                return false;
            }

            _selected = picked;
            _hasMove = true;
            _selectAtWorld = now + SelectInterval;
            GameLog.CombatError($"[Parry] pick skill={picked.SkillId} telegraph={picked.TelegraphKind} dist={dist:0.00} last={_lastSkillId}");
            return true;
        }

        EnemySelectQuery BuildQuery(float dist)
        {
            CombatEncounterDirector encounter = CombatEncounterDirector.Instance;
            bool onScreen = encounter == null || encounter.IsOnScreen(_owner);
            return new EnemySelectQuery
            {
                Owner = _owner,
                Distance = dist,
                HasMeleeToken = HasMeleeToken(),
                HasRangedToken = encounter != null && encounter.HasToken(_owner, EncounterTokenKind.Ranged),
                HasSpecialToken = encounter != null && encounter.HasToken(_owner, EncounterTokenKind.Special),
                LastSkillId = _lastSkillId,
                PhaseIndex = 0,
                OnScreen = onScreen,
                TempoBlocksToken = encounter != null && encounter.IsTokenGrantBlocked,
                CdTimer = _spell != null ? _spell.CDTimer : null,
            };
        }

        void CloseTowardEngage(float dist)
        {
            if (dist < KeepDistance - KeepDeadzone)
                _move.SetBackOff();
            else
                SteerToAnchor();
        }

        void TickRecover(float dist, float deltaTime)
        {
            _recoverRemain -= deltaTime;
            if (dist < KeepDistance - KeepDeadzone)
                _move.SetBackOff();
            else
                StopMove();

            if (_recoverRemain > 0f)
                return;

            EnterOrbitOrApproach(dist);
        }

        void TryAcquireIfReady(float dist)
        {
            if (dist > _acquireMaxDist)
                return;
            CombatEncounterDirector encounter = CombatEncounterDirector.Instance;
            if (encounter == null)
                return;
            if (HasAnyAttackToken())
                return;
            if (encounter.IsTokenGrantBlocked)
                return;
            if (!_owner.IsCanSpellSkill)
                return;
            encounter.TryAcquireToken(_owner, ChooseTokenKind(dist));
        }

        /// <summary>精英：当前距离落在 Special 招带内则抢 Special 牌，否则抢近战牌。</summary>
        EncounterTokenKind ChooseTokenKind(float dist)
        {
            if (_isElite)
            {
                EnemyMoveSet set = MoveSet;
                if (set?.Moves != null)
                {
                    int count = set.Moves.Length;
                    if (count > EnemyMoveSet.MaxMoves)
                        count = EnemyMoveSet.MaxMoves;
                    for (int i = 0; i < count; i++)
                    {
                        ref readonly EnemyMoveEntry entry = ref set.Moves[i];
                        if (entry.TokenKind != EncounterTokenKind.Special || !entry.RequiresToken || entry.IsFeint)
                            continue;
                        if (dist >= entry.MinRange && dist <= entry.MaxRange)
                            return EncounterTokenKind.Special;
                    }
                }
            }

            return EncounterTokenKind.Melee;
        }

        void SteerToAnchor()
        {
            if (!TryGetAnchor(out Vector3 anchor, out float dist))
            {
                _move.SetClose();
                return;
            }

            if (dist <= AnchorArrive)
            {
                StopMove();
                return;
            }

            Vector3 dir = anchor - _owner.Position;
            dir.y = 0f;
            float mag = dir.magnitude;
            if (mag < 0.0001f)
            {
                StopMove();
                return;
            }

            dir /= mag;
            float x = Vector3.Dot(dir, _aim.PlanarRight);
            float y = Vector3.Dot(dir, _aim.PlanarForward);
            // 距锚点越近轴越短：徘徊微调是慢走，不是反复冲刺
            float speedScale = Mathf.Clamp01(mag / 1.2f);
            _move.SetMove(x * speedScale, y * speedScale);
        }

        bool TryGetAnchorDistance(out float dist)
        {
            if (!TryGetAnchor(out _, out dist))
                return false;
            return true;
        }

        bool TryGetAnchor(out Vector3 anchor, out float dist)
        {
            anchor = default;
            dist = 0f;
            CombatEncounterDirector encounter = CombatEncounterDirector.Instance;
            if (encounter == null || !encounter.TryGetOrbitAnchor(_owner, KeepDistance, out anchor))
                return false;
            Vector3 to = anchor - _owner.Position;
            to.y = 0f;
            dist = to.magnitude;
            return true;
        }

        void EnterOrbitOrApproach(float dist)
        {
            _ = dist;
            if (TryGetAnchorDistance(out float anchorDist) && anchorDist <= AnchorLeave)
            {
                _state = EnemyTacticalState.Orbit;
                SteerToAnchor();
            }
            else
            {
                _state = EnemyTacticalState.Approach;
                SteerToAnchor();
            }
        }

        void EnterRecover()
        {
            ReleaseToken(TokenReleaseReason.SkillFinished);
            _state = EnemyTacticalState.Recover;
            float recover = _hasMove && _selected.RecoverSeconds > 0f
                ? _selected.RecoverSeconds
                : DefaultRecover;
            _recoverRemain = recover;
            _inCombat = true;
            ClearSelection();
        }

        void EnterIdle()
        {
            _state = EnemyTacticalState.Idle;
            _inCombat = false;
            _castPending = false;
            ClearSelection();
            StopMove();
            _facing.Clear();
        }

        void EnterInterrupted()
        {
            ReleaseToken(TokenReleaseReason.Interrupted);
            if (_owner.StateDirector != null && _owner.StateDirector.IsControl)
                _state = EnemyTacticalState.Control;
            else if (_owner.StateDirector != null && _owner.StateDirector.IsStagger)
                _state = EnemyTacticalState.Stagger;
            else
                _state = EnemyTacticalState.Hit;
            _castPending = false;
            ClearSelection();
            StopMove();
        }

        void EnterDead()
        {
            ReleaseToken(TokenReleaseReason.Death);
            _state = EnemyTacticalState.Dead;
            ClearSelection();
            StopMove();
            _facing.Clear();
        }

        void ClearSelection()
        {
            _hasMove = false;
            _selectAtWorld = 0f;
        }

        void TryRegisterEncounter()
        {
            if (_registered || _owner == null)
                return;
            CombatEncounterDirector director = CombatEncounterDirector.Instance;
            if (director == null)
                return;
            _registered = director.Register(
                _owner,
                KeepDistance,
                _profile != null ? _profile.DesireBonusPerSec : 0f);
        }

        void AimAt(CombatEntity target)
        {
            if (target == null || _owner == null)
                return;
            Vector3 to = target.Position - _owner.Position;
            to.y = 0f;
            float magSq = to.sqrMagnitude;
            if (magSq <= PlanarEpsilonSq)
                return;
            _aim.SetPlanarForward(to * (1f / Mathf.Sqrt(magSq)));
            _facing.SetPoint(target.Position);
        }

        bool IsOccupyingAxis()
        {
            ISkillExecutionHandle exec = _owner.ActiveExecution;
            if (exec == null || exec.IsDisposed)
                return false;
            return !exec.IsFinished;
        }

        bool HasMeleeToken()
        {
            return HasTokenFor(EncounterTokenKind.Melee);
        }

        bool HasTokenFor(EncounterTokenKind kind)
        {
            CombatEncounterDirector encounter = CombatEncounterDirector.Instance;
            return encounter != null && encounter.HasToken(_owner, kind);
        }

        bool HasAnyAttackToken()
        {
            CombatEncounterDirector encounter = CombatEncounterDirector.Instance;
            return encounter != null
                && (encounter.HasToken(_owner, EncounterTokenKind.Melee)
                    || encounter.HasToken(_owner, EncounterTokenKind.Special));
        }

        CombatEntity ResolveTarget()
        {
            CombatEncounterDirector encounter = CombatEncounterDirector.Instance;
            if (encounter != null)
            {
                CombatEntity focus = encounter.FocusTarget;
                if (focus != null && !focus.IsDisposed && !focus.IsDead)
                    return focus;
                return null;
            }

            PlayerManager mgr = PlayerManager.Instance;
            ActPlayer local = mgr != null ? mgr.LocalPlayer : null;
            CombatEntity target = local != null ? local.Combat : null;
            if (target == null || target.IsDisposed || target.IsDead)
                return null;
            return target;
        }

        void TryEnqueue(CombatEntity target)
        {
            if (target == null || _spell == null || _abilities == null || !_hasMove)
                return;
            if (_selected.RequiresToken && !HasTokenFor(_selected.TokenKind))
                return;
            if (!_owner.IsCanSpellSkill)
                return;
            if (!_abilities.IdAbilities.ContainsKey(_selected.SkillId))
                return;

            ActivateFail fail = AbilityActivationGate.Evaluate(
                _owner,
                _selected.SkillId,
                (int)_selected.Sort,
                _spell.CDTimer);
            if (fail != ActivateFail.None)
            {
                ClearSelection();
                return;
            }

            SkillSpellInfo info = PoolManager.Instance.TryGet<SkillSpellInfo>();
            info.SkillId = _selected.SkillId;
            info.Sort = (int)_selected.Sort;
            info.Target = target;
            info.Point = target.Position;
            _spell.Enqueue(info);
            _lastSkillId = _selected.SkillId;
            _castPending = true;
            _castPendingSince = GameTimeManager.WorldTime;
            _state = EnemyTacticalState.Skill;
            if (_selected.RequiresToken)
                CombatEncounterDirector.Instance?.NotifySkillCommitted(_owner, _selected.TokenKind);
            // 新的一手清连段标记；TelegraphSeconds<0 的招走轴上 AiTelegraph
            _followUpUsed = false;
            CombatTelegraph.PlayFromBrain(_owner, _selected.TelegraphKind, _selected.TelegraphSeconds);
            CombatParry.ArmIncoming(_owner, target, _selected.SkillId, _selected.TelegraphSeconds, _selected.TelegraphKind);
            Vector3 toTargetLog = target.Position - _owner.Position;
            toTargetLog.y = 0f;
            GameLog.CombatError($"[Parry] enemy enqueue skill={_selected.SkillId} telegraph={_selected.TelegraphKind} sec={_selected.TelegraphSeconds} dist={Mathf.Sqrt(toTargetLog.sqrMagnitude):0.00} target={target.Id}");
        }

        /// <summary>
        /// 同 Token 连段（§8.4）：主轴 IsMainFinish（Gate 此时视为可接）且仍持牌时追加一段，
        /// 最多一次，不重新走导演授予；第二击结束照样在 EnterRecover 还牌。
        /// </summary>
        void TryFollowUp(CombatEntity target)
        {
            if (_followUpUsed || !_hasMove || target == null || _spell == null || _abilities == null)
                return;
            int followUpId = _selected.FollowUpSkillId;
            if (followUpId <= 0)
                return;
            ISkillExecutionHandle exec = _owner.ActiveExecution;
            if (exec == null || exec.IsDisposed || !exec.IsMainFinish)
                return;
            if (!HasTokenFor(_selected.TokenKind) || !_owner.IsCanSpellSkill)
                return;
            if (!_abilities.IdAbilities.ContainsKey(followUpId))
                return;

            ActivateFail fail = AbilityActivationGate.Evaluate(
                _owner, followUpId, (int)_selected.Sort, _spell.CDTimer);
            if (fail != ActivateFail.None)
                return;

            SkillSpellInfo info = PoolManager.Instance.TryGet<SkillSpellInfo>();
            info.SkillId = followUpId;
            info.Sort = (int)_selected.Sort;
            info.Target = target;
            info.Point = target.Position;
            _spell.Enqueue(info);
            _lastSkillId = followUpId;
            _followUpUsed = true;
            // 挂起等第二击的轴起来，避免轴间隙掉进 EnterRecover 提前还牌
            _castPending = true;
            _castPendingSince = GameTimeManager.WorldTime;
            // 第二击也要可读：用招表里该招的 Telegraph 配置再亮一次
            if (TryFindMove(followUpId, out EnemyMoveEntry followUpMove))
                CombatTelegraph.PlayFromBrain(_owner, followUpMove.TelegraphKind, followUpMove.TelegraphSeconds);
        }

        /// <summary>
        /// 假前摇（§6.6，精英 Orbit 专用）：别人在真打时播一条无盒威胁轴施压。
        /// 不占牌、不进效用选招；Relax 期间允许（假动作不破反击窗）。
        /// </summary>
        void TryFeint(float dist, CombatEntity target)
        {
            if (!_isElite || target == null || _spell == null || _abilities == null)
                return;
            float interval = _profile != null ? _profile.FeintInterval : 0f;
            if (interval <= 0f)
                return;

            float now = GameTimeManager.WorldTime;
            if (_nextFeintAt <= 0f)
            {
                // 首次按 Id 散布，避免多精英同步虚招
                _nextFeintAt = now + interval * (0.5f + Hash01(_owner.Id));
                return;
            }
            if (now < _nextFeintAt)
                return;

            CombatEncounterDirector encounter = CombatEncounterDirector.Instance;
            if (encounter == null || encounter.MeleeHolder == null)
                return; // 没人真打就不虚张
            if (!TryGetFeint(dist, out EnemyMoveEntry feint))
                return;

            _selected = feint;
            _hasMove = true;
            _selectAtWorld = now + SelectInterval;
            TryEnqueue(target);
            // 无论 Gate 是否放行都进冷却，避免每帧重试
            _nextFeintAt = now + interval;
        }

        bool TryGetFeint(float dist, out EnemyMoveEntry feint)
        {
            feint = default;
            EnemyMoveSet set = MoveSet;
            if (set?.Moves == null)
                return false;
            int count = set.Moves.Length;
            if (count > EnemyMoveSet.MaxMoves)
                count = EnemyMoveSet.MaxMoves;
            for (int i = 0; i < count; i++)
            {
                ref readonly EnemyMoveEntry entry = ref set.Moves[i];
                if (!entry.IsFeint || entry.RequiresToken)
                    continue;
                if (dist < entry.MinRange || dist > entry.MaxRange)
                    continue;
                feint = entry;
                return true;
            }
            return false;
        }

        bool TryFindMove(int skillId, out EnemyMoveEntry entry)
        {
            entry = default;
            EnemyMoveSet set = MoveSet;
            if (set?.Moves == null)
                return false;
            int count = set.Moves.Length;
            if (count > EnemyMoveSet.MaxMoves)
                count = EnemyMoveSet.MaxMoves;
            for (int i = 0; i < count; i++)
            {
                if (set.Moves[i].SkillId != skillId)
                    continue;
                entry = set.Moves[i];
                return true;
            }
            return false;
        }

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

        void ReleaseToken(TokenReleaseReason reason)
        {
            _castPending = false;
            CombatEncounterDirector.Instance?.ReleaseToken(_owner, EncounterTokenKind.Melee, reason);
            CombatEncounterDirector.Instance?.ReleaseToken(_owner, EncounterTokenKind.Special, reason);
        }

        void StopMove() => _move.SetStop();

        /// <inheritdoc />
        public override void OnDestroy()
        {
            if (_owner != null)
                CombatEncounterDirector.Instance?.Unregister(_owner.Id);

            StopMove();
            _facing.Clear();
            CombatAnimDirector director = _anim?.Director;
            if (director != null)
                director.MoveIntentProvider = static () => Vector2.zero;

            if (_ownsFallback && _profile != null)
            {
                if (_profile.MoveSet != null)
                    UnityEngine.Object.Destroy(_profile.MoveSet);
                UnityEngine.Object.Destroy(_profile);
            }

            if (_runtimeMoveSet != null)
            {
                UnityEngine.Object.Destroy(_runtimeMoveSet);
                _runtimeMoveSet = null;
            }

            _owner = null;
            _inputMove = null;
            _spell = null;
            _abilities = null;
            _anim = null;
            _profile = null;
            _installed = false;
            _inCombat = false;
            _castPending = false;
            _hasMove = false;
            _recoverRemain = 0f;
            _lastSkillId = 0;
            _ownsFallback = false;
            _isElite = false;
            _followUpUsed = false;
            _nextFeintAt = 0f;
            _castPendingSince = 0f;
            _registered = false;
            _state = EnemyTacticalState.Idle;
        }

        /// <inheritdoc />
        public override void OnReset() => OnDestroy();

#if UNITY_EDITOR
        void DrawStateDebug()
        {
            if (_owner == null)
                return;
            Color color = _state switch
            {
                EnemyTacticalState.Orbit => Color.cyan,
                EnemyTacticalState.Windup => Color.yellow,
                EnemyTacticalState.Recover => Color.green,
                EnemyTacticalState.Approach => Color.white,
                EnemyTacticalState.Alert => Color.gray,
                EnemyTacticalState.Skill => Color.red,
                EnemyTacticalState.Stagger => new Color(1f, 0.55f, 0.1f),
                _ => Color.clear,
            };
            if (_state == EnemyTacticalState.Windup && _hasMove && _selected.SkillId == 12002)
                color = new Color(1f, 0.4f, 1f);
            if (color.a <= 0f)
                return;
            Debug.DrawRay(_owner.Position + Vector3.up * 2f, Vector3.up * 0.35f, color);
            if (TryGetAnchor(out Vector3 anchor, out _))
                Debug.DrawLine(_owner.Position + Vector3.up * 0.2f, anchor + Vector3.up * 0.2f, color);
        }
#endif
    }
}
