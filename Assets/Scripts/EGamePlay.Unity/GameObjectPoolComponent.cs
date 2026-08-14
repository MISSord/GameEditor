using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EGamePlay
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
