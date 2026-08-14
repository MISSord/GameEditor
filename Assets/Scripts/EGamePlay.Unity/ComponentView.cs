using UnityEngine;

namespace EGamePlay
{
    //非unity编辑器下使用
    public class ComponentView: MonoBehaviour
    {
        public string Type;
        public object Component { get; set; }
    }
}