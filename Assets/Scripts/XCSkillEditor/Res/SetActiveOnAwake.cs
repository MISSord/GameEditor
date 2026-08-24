using UnityEngine;

namespace ACTGameEditor
{
    public class SetActiveOnAwake : MonoBehaviour
    {
        public GameObject target;
        public bool isActive = false;
        public bool isON = true;

        private void Awake()
        {
            if (!isON)
                return;

            if (target == null)
                target = gameObject;
            target.SetActive(isActive);
        }
    }
}
