using EGamePlay;

namespace EGamePlay.Unity
{
    public sealed class GameObjectPool: Entity
    {
        public static GameObjectPool Instance;

        public override void Awake()
        {
            Instance = this;
        }
    }
}
