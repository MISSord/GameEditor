using System.Collections.Generic;
using ACTGameEditor;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 当前战斗形态。FormId=0 表示默认（用 SkillSlotRuntime）。
    /// </summary>
    public sealed class CombatFormComponent : Component
    {
        private readonly Dictionary<int, SkillFormConfig> _forms = new Dictionary<int, SkillFormConfig>(4);
        ICombatUnit _actor;

        /// <summary>当前形态 ID，0 为默认槽位表。</summary>
        public int ActiveFormId { get; private set; }

        /// <summary>当前形态配置，默认形态为 null。</summary>
        public SkillFormConfig ActiveForm { get; private set; }

        public override void Awake()
        {
            _actor = Entity as ICombatUnit;
        }

        public override void OnDestroy()
        {
            ClearFormTags();
            _forms.Clear();
            ActiveForm = null;
            ActiveFormId = 0;
            _actor = null;
        }

        /// <summary>注册角色可用形态。重复 FormId 后者覆盖。</summary>
        public void Init(List<SkillFormConfig> forms)
        {
            _forms.Clear();
            if (forms == null)
                return;

            for (int i = 0; i < forms.Count; i++)
            {
                SkillFormConfig form = forms[i];
                if (form == null || form.FormId <= 0)
                    continue;
                _forms[form.FormId] = form;
            }
        }

        /// <summary>进入指定形态。未知 FormId 则保持不变。</summary>
        public void SetForm(int formId)
        {
            if (formId <= 0)
            {
                ClearForm();
                return;
            }

            if (!_forms.TryGetValue(formId, out SkillFormConfig form) || form == null)
                return;

            if (ActiveFormId == formId && ActiveForm == form)
                return;

            ClearFormTags();
            ActiveFormId = formId;
            ActiveForm = form;
            ApplyFormTags(form);
            TryPushTag(TagSource.Form(formId), CombatTags.StanceForm);
        }

        /// <summary>退回默认形态（角色槽位表）。</summary>
        public void ClearForm()
        {
            ClearFormTags();
            ActiveForm = null;
            ActiveFormId = 0;
        }

        /// <summary>收集所有已注册形态的技能 ID。</summary>
        public void CollectSkillIds(HashSet<int> outIds)
        {
            if (outIds == null)
                return;

            foreach (var kv in _forms)
                kv.Value?.CollectSkillIds(outIds);
        }

        private void ApplyFormTags(SkillFormConfig form)
        {
            List<string> tags = form.GrantedTags;
            if (tags == null)
                return;
            var src = TagSource.Form(ActiveFormId);
            for (int i = 0; i < tags.Count; i++)
                TryPushTag(src, tags[i]);
        }

        private void ClearFormTags()
        {
            if (_actor == null)
                return;
            // 形态切换：整源清掉，避免漏 Remove
            if (ActiveFormId > 0)
                _actor.PopTagsFrom(TagSource.Form(ActiveFormId));
            else
            {
                TryPopTag(TagSource.Form(0), CombatTags.StanceForm);
            }
        }

        private void TryPushTag(TagSource src, string tag)
        {
            if (string.IsNullOrEmpty(tag) || _actor?.TagHost == null)
                return;
            if (TagCollection.TagToIndexDic == null || !TagCollection.TagToIndexDic.ContainsKey(tag))
                return;
            _actor.TagHost.PushTag(src, tag);
        }

        private void TryPopTag(TagSource src, string tag)
        {
            if (string.IsNullOrEmpty(tag) || _actor?.TagHost == null)
                return;
            if (TagCollection.TagToIndexDic == null || !TagCollection.TagToIndexDic.ContainsKey(tag))
                return;
            _actor.TagHost.PopTag(src, tag);
        }
    }
}
