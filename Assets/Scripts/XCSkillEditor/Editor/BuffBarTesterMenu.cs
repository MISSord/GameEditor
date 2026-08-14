using ACTGameEditor;
using EGamePlay.Combat;
using UnityEditor;
using UnityEngine;

namespace XiaoCao.Editor
{
    /// <summary>
    /// Buff 栏测试的编辑器菜单，仅在 Play 模式下可用。
    /// </summary>
    public static class BuffBarTesterMenu
    {
        private const int DefaultTestBuffId = 1;

        [MenuItem("Tools/Buff Bar Test/Add Test Buff (BuffId=1)", true)]
        private static bool ValidateAddTestBuff() => Application.isPlaying && PlayerManager.Instance?.LocalPlayer != null;

        [MenuItem("Tools/Buff Bar Test/Add Test Buff (BuffId=1)", false)]
        private static void AddTestBuff()
        {
            if (!Application.isPlaying) return;
            var tester = Object.FindObjectOfType<BuffBarTester>();
            if (tester != null)
                tester.AddTestBuff(tester.testBuffId);
            else
                RunAddBuffDirect(DefaultTestBuffId);
        }

        [MenuItem("Tools/Buff Bar Test/Remove Test Buff (BuffId=1)", true)]
        private static bool ValidateRemoveTestBuff() => Application.isPlaying && PlayerManager.Instance?.LocalPlayer != null;

        [MenuItem("Tools/Buff Bar Test/Remove Test Buff (BuffId=1)", false)]
        private static void RemoveTestBuff()
        {
            if (!Application.isPlaying) return;
            var tester = Object.FindObjectOfType<BuffBarTester>();
            if (tester != null)
                tester.RemoveTestBuff(tester.testBuffId);
            else
                RunRemoveBuffDirect(DefaultTestBuffId);
        }

        [MenuItem("Tools/Buff Bar Test/Add Batch Buffs", true)]
        private static bool ValidateAddBatch() => Application.isPlaying && PlayerManager.Instance?.LocalPlayer != null;

        [MenuItem("Tools/Buff Bar Test/Add Batch Buffs", false)]
        private static void AddBatchBuffs()
        {
            if (!Application.isPlaying) return;
            var tester = Object.FindObjectOfType<BuffBarTester>();
            if (tester != null)
                tester.AddBatchBuffs();
        }

        [MenuItem("Tools/Buff Bar Test/Remove All Buffs", true)]
        private static bool ValidateRemoveAll() => Application.isPlaying && PlayerManager.Instance?.LocalPlayer != null;

        [MenuItem("Tools/Buff Bar Test/Remove All Buffs", false)]
        private static void RemoveAllBuffs()
        {
            if (!Application.isPlaying) return;
            var tester = Object.FindObjectOfType<BuffBarTester>();
            if (tester != null)
                tester.RemoveAllBuffs();
        }

        /// <summary>
        /// 不依赖 BuffBarTester 组件，直接对当前本地玩家添加 Buff（供菜单或自动化调用）。
        /// </summary>
        public static bool RunAddBuffDirect(int buffId)
        {
            if (!Application.isPlaying) return false;
            var combat = PlayerManager.Instance?.LocalPlayer?.Combat;
            if (combat == null) return false;
            var statusComp = combat.GetComponent<StatusComponent>();
            if (statusComp == null) return false;
            if (SkillSettingMgr.Instance.GetBuffDemoSetting(buffId) == null) return false;
            var buff = statusComp.AttachStatus(buffId);
            buff?.ActivateBuff();
            return true;
        }

        /// <summary>
        /// 不依赖 BuffBarTester 组件，直接移除指定 Buff。
        /// </summary>
        public static bool RunRemoveBuffDirect(int buffId)
        {
            if (!Application.isPlaying) return false;
            var combat = PlayerManager.Instance?.LocalPlayer?.Combat;
            if (combat == null) return false;
            var statusComp = combat.GetComponent<StatusComponent>();
            if (statusComp == null || !statusComp.HasBuffId(buffId)) return false;
            statusComp.RemoveStatus(buffId);
            return true;
        }
    }
}
