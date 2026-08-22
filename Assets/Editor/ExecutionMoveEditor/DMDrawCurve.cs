using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace EGamePlay
{
    [RequireComponent(typeof(LineRenderer))]
    public class DMDrawCurve : MonoBehaviour
    {
        public List<Transform> m_allPoints;
        private GameObject m_anchorPoint;
        private GameObject m_controlPoint;
        private GameObject m_pointParent;
        private LineRenderer m_lineRenderer;

        private Vector3 PlayerPos;

        private int m_curveCount = 0;
        private int SEGMENT_COUNT = 20;//曲线取点个数（取点越多这个长度越趋向于精确）

        private static DMDrawCurve m_instance;
        public static DMDrawCurve Instance
        {
            get {
                if (null == m_instance)
                    m_instance = new DMDrawCurve();
                return m_instance;
            }
        }

        void Awake()
        {
            if (null == m_instance)
                m_instance = this;
            SetLine();
            if (null == m_anchorPoint)
                m_anchorPoint = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EditorResources/Prefabs/AnchorPoint.prefab");
            if (null == m_controlPoint)
                m_controlPoint = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/EditorResources/Prefabs/ControlPoint.prefab");
        }

        void SetLine()
        {
            if (null == m_lineRenderer)
                m_lineRenderer = GetComponent<LineRenderer>();
            m_lineRenderer.material = AssetDatabase.LoadAssetAtPath<Material>("Assets/EditorResources/Materials/Line");
            m_lineRenderer.startColor = Color.red;
            m_lineRenderer.endColor = Color.green;
            m_lineRenderer.widthMultiplier = 0.2f;
        }

        public void Init(GameObject player)
        {
            //初始化一个基准点（Player）
            if (player == null) return;
            PlayerPos = player.transform.position;
        }

        public void ClearPoints()
        {
            for(int i = m_allPoints.Count - 1; i >= 0; i--)
            {
                GameObject.Destroy(m_allPoints[i].gameObject);
            }
            m_allPoints.Clear();
            DrawCurve();
        }

        public Vector3[] GetPoint()
        {
            Vector3[] vecs = new Vector3[m_allPoints.Count];
            for(int i = 0; i < m_allPoints.Count; ++i)
            {
                vecs[i] = m_allPoints[i].transform.position;
            }
            return vecs;
        }

        public void AddPoint(Vector3 anchorPointPos, bool isTest)
        {
            //初始化时m_allPoints添加了一个player
            if (isTest == true && m_allPoints.Count == 0)
            {
                GameObject PlayeranchorPoint = LoadPoint(m_anchorPoint, PlayerPos);
                m_allPoints.Add(PlayeranchorPoint.transform);
            }

            GameObject anchorPoint = LoadPoint(m_anchorPoint, anchorPointPos);
            m_allPoints.Add(anchorPoint.transform);
            DrawCurve();
        }

        public void DeletePoint(GameObject anchorPoint)
        {
            if (anchorPoint == null) return;
            CurvePointControl curvePoint = anchorPoint.GetComponent<CurvePointControl>();
            if (curvePoint && anchorPoint.tag.Equals("AnchorPoint"))
            {
                if (curvePoint.m_controlObject)
                {
                    m_allPoints.Remove(curvePoint.m_controlObject.transform);
                    Destroy(curvePoint.m_controlObject);
                } 
                if (curvePoint.m_controlObject2)
                {
                    m_allPoints.Remove(curvePoint.m_controlObject2.transform);
                    Destroy(curvePoint.m_controlObject2);
                }
                if (m_allPoints.IndexOf(curvePoint.transform) == (m_allPoints.Count - 1))
                {
                    //先判断删除的是最后一个元素再移除
                    m_allPoints.Remove(curvePoint.transform);
                    Transform lastPoint = m_allPoints[m_allPoints.Count - 2];
                    GameObject lastPointCtrObject = lastPoint.GetComponent<CurvePointControl>().m_controlObject2;
                    if (lastPointCtrObject)
                    {
                        m_allPoints.Remove(lastPointCtrObject.transform);
                        Destroy(lastPointCtrObject);
                        lastPoint.GetComponent<CurvePointControl>().m_controlObject2 = null;
                    }
                }
                else
                {
                    m_allPoints.Remove(curvePoint.transform);
                }
                Destroy(anchorPoint);
                if(m_allPoints.Count == 1)
                {
                    m_lineRenderer.positionCount = 0;
                }
            }

            DrawCurve();
        }

        public void UpdateLine(GameObject anchorPoint, Vector3 offsetPos1, Vector3 offsetPos2)
        {
            if (anchorPoint == null) return;
            if (anchorPoint.tag.Equals("AnchorPoint"))
            {
                CurvePointControl curvePoint = anchorPoint.GetComponent<CurvePointControl>();
                if (curvePoint)
                {
                    if (curvePoint.m_controlObject)
                        curvePoint.m_controlObject.transform.position = anchorPoint.transform.position + offsetPos1;
                    if (curvePoint.m_controlObject2)
                        curvePoint.m_controlObject2.transform.position = anchorPoint.transform.position + offsetPos2;
                }
            }
            DrawCurve();
        }

        public List<Vector3> HiddenLine(bool isHidden=false)
        {
            m_pointParent.SetActive(isHidden);
            m_lineRenderer.enabled = isHidden;
            List<Vector3> pathPoints = new List<Vector3>();
            if(!isHidden)
            {
                for(int i = 0; i < m_lineRenderer.positionCount; i++)
                {
                    pathPoints.Add(m_lineRenderer.GetPosition(i));
                }
            }
            return pathPoints;
        }

        private void DrawCurve()//画曲线
        {
            if (m_allPoints == null || m_allPoints.Count < 2)
            {
                m_lineRenderer.enabled = false;
                return;
            }

            m_lineRenderer.enabled = true;

            Vector3[] points = new Vector3[m_allPoints.Count];
            for (int i = 0; i < m_allPoints.Count; i++)
            {
                if (m_allPoints[i] != null)
                    points[i] = m_allPoints[i].position;
            }

            List<Vector3> curvePoints = SimpleBezierCurve.GenerateBezierPoints(points, 30);
            m_lineRenderer.positionCount = curvePoints.Count;
            m_lineRenderer.SetPositions(curvePoints.ToArray());
        }

        private GameObject LoadPoint(GameObject pointPrefab,Vector3 pos)
        {
            if (pointPrefab == null)
            {
                Debug.LogError("The Prefab is Null!");
                return null;
            }
            if (null == m_pointParent)
                m_pointParent = new GameObject("AllPoints");
            GameObject pointClone = Instantiate(pointPrefab);
            pointClone.name = pointClone.name.Replace("(Clone)", "");
            pointClone.transform.SetParent(m_pointParent.transform);
            pointClone.transform.position = pos;

            return pointClone;
        }
    }
}