using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using EGamePlay;
using System;

namespace EGamePlay.Unity
{
    public class OnTriggerEnterCallback : MonoBehaviour
    {
        public Action<Collider> OnTriggerEnterCallbackAction;

        private void OnTriggerEnter(Collider other)
        {
            OnTriggerEnterCallbackAction?.Invoke(other);
        }
    }
}