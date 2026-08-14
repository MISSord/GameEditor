using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EGamePlay
{
    public class CurvePointControl : MonoBehaviour
    {
        [Header("锁定X轴")]
        public bool m_isLockX = false;
        [Header("锁定Y轴")]
        public bool m_isLockY = true;
        [Header("锁定Z轴")]
        public bool m_isLockZ = false;
       
        public GameObject m_controlObject;
        public GameObject m_controlObject2;

        private Vector3 _offsetPos1 = Vector3.zero;
        private Vector3 _offsetPos2 = Vector3.zero;
        private LineRenderer _lineRenderer;
        void Start()
        {
            if (gameObject.tag.Equals("AnchorPoint") && !_lineRenderer)
                _lineRenderer = gameObject.AddComponent<LineRenderer>();
            if (_lineRenderer)
            {
                _lineRenderer.sortingOrder = 1;
                //lineRenderer.material = new Material(Shader.Find("Particles/Alpha Blended"));
                _lineRenderer.startColor = _lineRenderer.endColor = Color.yellow;
                _lineRenderer.widthMultiplier = 0.03f;
                _lineRenderer.positionCount = 0;
            }
        }
        void OnMouseDown()
        {
            if (!gameObject.tag.Equals("AnchorPoint")) return;
            OffsetPos();
        }
        public List<Vector3> OffsetPos()
        {
            List<Vector3> offsetPosList = new List<Vector3>();
            if (m_controlObject)
                _offsetPos1 = m_controlObject.transform.position - transform.position;
            if (m_controlObject2)
                _offsetPos2 = m_controlObject2.transform.position - transform.position;
            offsetPosList.Add(_offsetPos1);
            offsetPosList.Add(_offsetPos2);

            return offsetPosList;
        }
        void OnMouseDrag()
        {
            //if (gameObject.tag.Equals("AnchorPoint")) return;
            Vector3 pos0 = Camera.main.WorldToScreenPoint(transform.position);
            Vector3 mousePos = new Vector3(Input.mousePosition.x, Input.mousePosition.y, pos0.z);
            Vector3 mousePosInWorld= Camera.main.ScreenToWorldPoint(mousePos);
            Vector3 thisPos = mousePosInWorld;
            if (m_isLockX)
                thisPos.x = transform.position.x;
            if (m_isLockY)
                thisPos.y = transform.position.y;
            if (m_isLockZ)
                thisPos.z = transform.position.z;
            transform.position = thisPos;
            DMDrawCurve.Instance.UpdateLine(gameObject, _offsetPos1, _offsetPos2);   
        }      
        private void DrawControlLine()
        {
            if (!gameObject.tag.Equals("AnchorPoint") || (!m_controlObject && !m_controlObject2)) return;
            if (_lineRenderer)
            {
                _lineRenderer.positionCount = (m_controlObject && m_controlObject2) ? 3 : 2;
                if (m_controlObject && !m_controlObject2)
                {
                    _lineRenderer.SetPosition(0, m_controlObject.transform.position);
                    _lineRenderer.SetPosition(1, transform.position);
                }
                if (m_controlObject2 && !m_controlObject)
                {
                    _lineRenderer.SetPosition(0, transform.position);
                    _lineRenderer.SetPosition(1, m_controlObject2.transform.position);
                }
                if (m_controlObject && m_controlObject2)
                {
                    _lineRenderer.SetPosition(0, m_controlObject.transform.position);
                    _lineRenderer.SetPosition(1, transform.position);
                    _lineRenderer.SetPosition(2, m_controlObject2.transform.position);
                }
            }
        }
        void Update()
        {
            DrawControlLine();
        }
    }
}