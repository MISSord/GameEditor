using UnityEngine;
using ACTGameEditor;

namespace XiaoCao
{
    public static class ResFinder
    {
        public static SoUsing SoUsingFinder
        {
            get
            {
                if (_soUsingFinder == null)
                {
                    _soUsingFinder = Resources.Load<SoUsing>(PrefabPath.SoUsing);
                }
                return _soUsingFinder;
            }
        }

        private static SoUsing _soUsingFinder;
    }

}
