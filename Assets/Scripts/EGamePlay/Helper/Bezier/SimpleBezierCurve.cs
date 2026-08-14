using EGamePlay.Combat;
using System.Collections.Generic;
using UnityEngine;

public class SimpleBezierCurve
{
    /// <summary>
    /// 计算二阶贝塞尔曲线点（2个控制点）
    /// </summary>
    /// <param name="p0">起始点</param>
    /// <param name="p1">控制点</param>
    /// <param name="p2">结束点</param>
    /// <param name="t">插值参数 [0,1]</param>
    /// <returns>曲线上的点</returns>
    public static Vector3 CalculateBezier2(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1 - t;
        return u * u * p0 + 2 * u * t * p1 + t * t * p2;
    }

    /// <summary>
    /// 计算三阶贝塞尔曲线点（3个控制点）
    /// </summary>
    /// <param name="p0">起始点</param>
    /// <param name="p1">控制点1</param>
    /// <param name="p2">控制点2</param>
    /// <param name="p3">结束点</param>
    /// <param name="t">插值参数 [0,1]</param>
    /// <returns>曲线上的点</returns>
    public static Vector3 CalculateBezier3(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1 - t;
        return u * u * u * p0 +
               3 * u * u * t * p1 +
               3 * u * t * t * p2 +
               t * t * t * p3;
    }

    /// <summary>
    /// 计算四阶贝塞尔曲线点（4个控制点）
    /// </summary>
    public static Vector3 CalculateBezier4(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1 - t;
        return u * u * u * u * p0 +
               4 * u * u * u * t * p1 +
               6 * u * u * t * t * p2 +
               4 * u * t * t * t * p3 +
               t * t * t * t * p4;
    }

    /// <summary>
    /// 计算五阶贝塞尔曲线点（5个控制点）
    /// </summary>
    public static Vector3 CalculateBezier5(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 p5, float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1 - t;
        return u * u * u * u * u * p0 +
               5 * u * u * u * u * t * p1 +
               10 * u * u * u * t * t * p2 +
               10 * u * u * t * t * t * p3 +
               5 * u * t * t * t * t * p4 +
               t * t * t * t * t * p5;
    }

    /// <summary>
    /// 计算六阶贝塞尔曲线点（6个控制点）
    /// </summary>
    public static Vector3 CalculateBezier6(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 p4, Vector3 p5, Vector3 p6, float t)
    {
        t = Mathf.Clamp01(t);
        float u = 1 - t;
        return u * u * u * u * u * u * p0 +
               6 * u * u * u * u * u * t * p1 +
               15 * u * u * u * u * t * t * p2 +
               20 * u * u * u * t * t * t * p3 +
               15 * u * u * t * t * t * t * p4 +
               6 * u * t * t * t * t * t * p5 +
               t * t * t * t * t * t * p6;
    }

    /// <summary>
    /// 通用贝塞尔曲线计算方法（支持2-6个点）
    /// </summary>
    /// <param name="points">控制点数组</param>
    /// <param name="t">插值参数 [0,1]</param>
    /// <returns>曲线上的点</returns>
    public static Vector3 CalculateBezier(Vector3[] points, float t)
    {
        if (points == null || points.Length < 2)
        {
            Debug.LogError("贝塞尔曲线需要至少2个控制点！");
            return Vector3.zero;
        }

        if (points.Length > 6)
        {
            Debug.LogWarning("只支持最多6个控制点，将使用前6个点");
        }

        t = Mathf.Clamp01(t);
        int n = Mathf.Min(points.Length - 1, 5); // 最大支持6阶
        Vector3 result = Vector3.zero;

        for (int i = 0; i <= n; i++)
        {
            if (i < points.Length)
            {
                float coefficient = BinomialCoefficient(n, i) * Mathf.Pow(1 - t, n - i) * Mathf.Pow(t, i);
                result += coefficient * points[i];
            }
        }

        return result;
    }

    /// <summary>
    /// 计算二项式系数 C(n, k)
    /// </summary>
    private static float BinomialCoefficient(int n, int k)
    {
        if (k < 0 || k > n) return 0;
        if (k == 0 || k == n) return 1;

        k = Mathf.Min(k, n - k);
        float result = 1;

        for (int i = 1; i <= k; i++)
        {
            result *= (n - k + i) / (float)i;
        }

        return result;
    }

    /// <summary>
    /// 生成贝塞尔曲线上的多个点（用于绘制或移动）
    /// </summary>
    /// <param name="points">控制点</param>
    /// <param name="resolution">分辨率（生成的点数）</param>
    /// <returns>曲线上的点列表</returns>
    public static List<Vector3> GenerateBezierPoints(Vector3[] points, int resolution = 50)
    {
        List<Vector3> curvePoints = new List<Vector3>();

        for (int i = 0; i <= resolution; i++)
        {
            float t = i / (float)resolution;
            curvePoints.Add(CalculateBezier(points, t));
        }

        return curvePoints;
    }

    #region 缩放方法

    // 方法1：等比缩放整个曲线
    public List<Vector3> ScaleByFactor(List<Vector3> points, float factor)
    {
        Vector3 startPoint = points[0];
        var scaledPoints = new List<Vector3>();

        foreach (var point in points)
        {
            Vector3 vector = point - startPoint;
            Vector3 scaledVector = vector * factor;
            Vector3 newPoint = startPoint + scaledVector;
            scaledPoints.Add(newPoint);
        }

        return scaledPoints;
    }

    // 方法2：保持起点不变，缩放曲线到目标距离
    public List<Vector3> ScaleToDistance(List<Vector3> points, float distance)
    {
        Vector3 start = points[0];
        Vector3 end = points[points.Count - 1];

        float originalDistance = Vector3.Distance(end, start);

        if (Mathf.Approximately(originalDistance, 0))
        {
            Debug.LogWarning("Start and end points are the same, cannot scale");
            return new List<Vector3>(points);
        }

        float scaleFactor = distance / originalDistance;

        var scaledPoints = new List<Vector3> { start }; // 起点保持不变

        // 缩放中间控制点
        for (int i = 1; i < points.Count; i++)
        {
            Vector3 point = points[i];
            Vector3 vectorFromStart = point - start;
            Vector3 scaledVector = vectorFromStart * scaleFactor;
            Vector3 newPoint = start + scaledVector;
            scaledPoints.Add(newPoint);
        }

        return scaledPoints;
    }

    // 方法3：基于倍数和方向进行缩放
    public List<Vector3> ScaleByFactorAndDirection(List<Vector3> points, float factor, Vector3 direction)
    {
        Vector3 startPoint = points[0];
        var scaledPoints = new List<Vector3>();

        // 归一化方向向量
        Vector3 normalizedDirection = direction.normalized;

        foreach (var point in points)
        {
            Vector3 vector = point - startPoint;

            // 分解向量到方向分量和垂直分量
            float magnitudeInDirection = Vector3.Dot(vector, normalizedDirection);
            Vector3 directionComponent = normalizedDirection * magnitudeInDirection;
            Vector3 perpendicularComponent = vector - directionComponent;

            // 只在指定方向上缩放
            Vector3 scaledDirectionComponent = directionComponent * factor;

            // 重新组合向量
            Vector3 scaledVector = scaledDirectionComponent + perpendicularComponent;
            Vector3 newPoint = startPoint + scaledVector;
            scaledPoints.Add(newPoint);
        }

        return scaledPoints;
    }

    // 方法4：在特定轴向上缩放
    public List<Vector3> ScaleOnAxis(List<Vector3> points, float factor, Vector3 axis)
    {
        Vector3 startPoint = points[0];
        var scaledPoints = new List<Vector3>();

        Vector3 normalizedAxis = axis.normalized;

        foreach (var point in points)
        {
            Vector3 vector = point - startPoint;

            // 分解向量到轴向分量和垂直分量
            float magnitudeOnAxis = Vector3.Dot(vector, normalizedAxis);
            Vector3 axisComponent = normalizedAxis * magnitudeOnAxis;
            Vector3 perpendicularComponent = vector - axisComponent;

            // 只在指定轴向上缩放
            Vector3 scaledAxisComponent = axisComponent * factor;

            // 重新组合向量
            Vector3 scaledVector = scaledAxisComponent + perpendicularComponent;
            Vector3 newPoint = startPoint + scaledVector;
            scaledPoints.Add(newPoint);
        }

        return scaledPoints;
    }

    // 方法5：非均匀缩放（每个轴不同的缩放因子）
    public List<Vector3> ScaleNonUniform(List<Vector3> points, Vector3 scaleFactors)
    {
        Vector3 startPoint = points[0];
        var scaledPoints = new List<Vector3>();

        foreach (var point in points)
        {
            Vector3 vector = point - startPoint;
            Vector3 scaledVector = new Vector3(
                vector.x * scaleFactors.x,
                vector.y * scaleFactors.y,
                vector.z * scaleFactors.z
            );
            Vector3 newPoint = startPoint + scaledVector;
            scaledPoints.Add(newPoint);
        }

        return scaledPoints;
    }

    /// <summary>
    /// ScaleFactor 是缩放倍数
    /// rotationAxis 是旋转轴
    /// rotationAngle 是旋转角
    /// </summary>
    /// <param name="points"></param>
    /// <param name="scaleFactor"></param>
    /// <param name="rotationAxis"></param>
    /// <param name="rotationAngle"></param>
    /// <returns></returns>
    // 方法6：结合缩放和旋转，保持起点不变
    public static List<Vector3> ScaleAndRotate(List<Vector3> points, float scaleFactor, Vector3 rotationAxis, float rotationAngle)
    {
        Vector3 startPoint = points[0];
        var transformedPoints = new List<Vector3> { startPoint }; // 起点保持不变

        // 创建旋转四元数
        Quaternion rotation = Quaternion.AngleAxis(rotationAngle, rotationAxis.normalized);

        // 对每个控制点（除了起点）应用缩放和旋转
        for (int i = 1; i < points.Count; i++)
        {
            Vector3 point = points[i];
            Vector3 vectorFromStart = point - startPoint;

            // 先缩放，再旋转
            Vector3 scaledVector = vectorFromStart * scaleFactor;
            Vector3 rotatedAndScaledVector = rotation * scaledVector;

            Vector3 newPoint = startPoint + rotatedAndScaledVector;
            transformedPoints.Add(newPoint);
        }

        return transformedPoints;
    }

    #endregion

    /// <summary>
    /// 变换点位（单个轴缩放）
    /// </summary>
    /// <param name="points">原始点位数组</param>
    /// <param name="rotationAngle">旋转角度（度）</param>
    /// <param name="rotationAxis">旋转轴</param>
    /// <param name="scaleAxis">要缩放的轴 (0=X, 1=Y, 2=Z)</param>
    /// <param name="scaleValue">缩放值</param>
    /// <returns>变换后的点位数组</returns>
    public static Vector3[] TransformPointsSingleAxis(Vector3[] points, float rotationAngle, Vector3 rotationAxis, ScaleAxisType scaleAxis, float scaleValue)
    {
        // 创建旋转四元数
        Quaternion rotation = Quaternion.AngleAxis(rotationAngle, rotationAxis.normalized);

        // 创建缩放向量（只在指定轴缩放）
        Vector3 scale = Vector3.one;
        switch (scaleAxis)
        {
            case ScaleAxisType.X: // X轴
                scale.x = scaleValue;
                break;
            case ScaleAxisType.Y: // Y轴
                scale.y = scaleValue;
                break;
            case ScaleAxisType.Z: // Z轴
                scale.z = scaleValue;
                break;
            default:
                Debug.LogWarning("scaleAxis参数应为0(X),1(Y),2(Z)，使用默认值0");
                scale.x = scaleValue;
                break;
        }

        // 变换每个点
        Vector3[] transformedPoints = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            // 先缩放，后旋转
            Vector3 scaledPoint = Vector3.Scale(points[i], scale);
            transformedPoints[i] = rotation * scaledPoint;
        }

        return transformedPoints;
    }

    /// <summary>
    /// 变换点位（所有轴缩放）
    /// </summary>
    /// <param name="points">原始点位数组</param>
    /// <param name="rotationAngle">旋转角度（度）</param>
    /// <param name="rotationAxis">旋转轴</param>
    /// <param name="scale">缩放比例（XYZ分别缩放）</param>
    /// <returns>变换后的点位数组</returns>
    public static Vector3[] TransformPointsAllAxes(Vector3[] points, float rotationAngle, Vector3 rotationAxis, Vector3 scale)
    {
        // 创建旋转四元数
        Quaternion rotation = Quaternion.AngleAxis(rotationAngle, rotationAxis.normalized);

        // 变换每个点
        Vector3[] transformedPoints = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            // 先缩放，后旋转
            Vector3 scaledPoint = Vector3.Scale(points[i], scale);
            transformedPoints[i] = rotation * scaledPoint;
        }

        return transformedPoints;
    }

    /// <summary>
    /// 变换点位（所有轴统一缩放）
    /// </summary>
    /// <param name="points">原始点位数组</param>
    /// <param name="rotationAngle">旋转角度（度）</param>
    /// <param name="rotationAxis">旋转轴</param>
    /// <param name="uniformScale">统一缩放比例</param>
    /// <returns>变换后的点位数组</returns>
    public static Vector3[] TransformPointsUniformScale(Vector3[] points, float rotationAngle, Vector3 rotationAxis, float uniformScale)
    {
        // 创建旋转四元数
        Quaternion rotation = Quaternion.AngleAxis(rotationAngle, rotationAxis.normalized);

        // 变换每个点
        Vector3[] transformedPoints = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            // 先缩放，后旋转
            Vector3 scaledPoint = points[i] * uniformScale;
            transformedPoints[i] = rotation * scaledPoint;
        }

        return transformedPoints;
    }
}