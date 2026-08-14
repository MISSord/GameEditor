using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace EGamePlay
{
    public class Test : MonoBehaviour
    {
        public GameObject m_player;
        public List<Vector3> m_pathPoints;
        public float moveSpeed = 2.0f;
        private float _t = 0f;
        private bool _isMoving = false;
        private Vector3 _originPosition;

        void Start()
        {
            DMDrawCurve.Instance.Init(m_player);
            _originPosition = m_player.transform.position;
        }

        IEnumerator Move()
        {
            if (m_pathPoints.Count == 0) yield break;
            int item = 1;
            while (true)
            {
                m_player.transform.LookAt(m_pathPoints[item]);
                m_player.transform.position = Vector3.Lerp(m_pathPoints[item - 1], m_pathPoints[item], 1f);
                item++;
                if (item >= m_pathPoints.Count)
                {
                    item = 1;
                    yield break;
                }
                yield return new WaitForEndOfFrame();
            }
        }

        void Update()
        {
            if (Input.GetKey(KeyCode.LeftControl) && (Input.GetMouseButtonUp(0) || Input.GetMouseButtonUp(1)))
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;
                if (Physics.Raycast(ray, out hit))
                {
                    if (hit.collider == null) return;
                    if (Input.GetMouseButtonUp(0) && hit.collider.tag.Equals("Terrain"))
                    {
                        Vector3 pointPos = new Vector3(hit.point.x, m_player.transform.position.y, hit.point.z);
                        DMDrawCurve.Instance.AddPoint(pointPos, true);
                    }
                    else if (Input.GetMouseButtonUp(1) && hit.collider.tag.Equals("AnchorPoint"))
                    {
                        DMDrawCurve.Instance.DeletePoint(hit.collider.gameObject);
                    }
                }
            }
            if (Input.GetKeyUp(KeyCode.A))
                m_pathPoints = DMDrawCurve.Instance.HiddenLine(false);
            else if (Input.GetKeyUp(KeyCode.Escape))
            {
                DMDrawCurve.Instance.HiddenLine(true);
                m_pathPoints.Clear();
            }

            if (Input.GetKeyDown(KeyCode.Space) && DMDrawCurve.Instance.m_allPoints.Count >= 2)
            {
                StartMovement();
            }

            if (_isMoving)
            {
                MoveAlongBezier();
            }
        }

        void StartMovement()
        {
            _t = 0f;
            _isMoving = true;
        }

        void MoveAlongBezier()
        {
            _t += moveSpeed * Time.deltaTime;

            if (_t >= 1f)
            {
                _t = 1f;
                _isMoving = false;
                m_player.transform.position = _originPosition;
                return;
            }

            // 使用通用方法
            Vector3[] points = new Vector3[DMDrawCurve.Instance.m_allPoints.Count];
            for (int i = 0; i < DMDrawCurve.Instance.m_allPoints.Count; i++)
            {
                points[i] = DMDrawCurve.Instance.m_allPoints[i].position;
            }

            m_player.transform.position = SimpleBezierCurve.CalculateBezier(points, _t);

            // 或者使用特定阶数的方法（性能更好）
            // switch (controlPoints.Length)
            // {
            //     case 2:
            //         transform.position = BezierCurve.CalculateBezier2(
            //             controlPoints[0].position, controlPoints[1].position, t);
            //         break;
            //     case 3:
            //         transform.position = BezierCurve.CalculateBezier3(
            //             controlPoints[0].position, controlPoints[1].position, 
            //             controlPoints[2].position, t);
            //         break;
            //     // ... 其他情况
            // }
        }
    }
}