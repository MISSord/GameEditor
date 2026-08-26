using UnityEngine;

namespace EGamePlay.Unity
{
    // 非 Unity 编辑器下使用
    public class ComponentView: MonoBehaviour
    {
        public string Type;
        public object Component { get; set; }
    }
}
