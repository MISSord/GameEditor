using System;
using System.Collections.Generic;

namespace EGamePlay
{
    //作为Entity的总根节点
    public sealed class ECSNode : Entity
    {
        public Dictionary<Type, List<Entity>> Entities { get; private set; } = new Dictionary<Type, List<Entity>>();
        public static ECSNode Instance { get; private set; }

        private ECSNode(){ }

        public static ECSNode Create()
        {
            if (Instance == null)
            {
                Instance = new ECSNode();
#if !NOT_UNITY
                Instance.AddComponent<GameObjectComponent>();
                UnityEngine.GameObject.DontDestroyOnLoad(Instance.GetComponent<GameObjectComponent>().GameObject);
#endif
            }
            return Instance;
        }

        public static void Destroy()
        {
            Entity.Destroy(Instance);
            Instance = null;
        }
    }
}