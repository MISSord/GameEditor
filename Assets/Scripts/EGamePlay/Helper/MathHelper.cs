using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;

namespace EGamePlay.Combat
{
    public static class MathHelper
    {
        /// <summary>
        /// 静态方法：基于位置和旋转计算角度
        /// </summary>
        /// <param name="objectPosition">物体世界坐标</param>
        /// <param name="objectYRotation">物体Y轴旋转角度（度）</param>
        /// <param name="targetPoint">目标点世界坐标</param>
        /// <returns>角度值，范围[-180, 180]度</returns>
        public static float GetAngleOnXZPlane(Vector3 objectPosition, Vector3 targetPoint)
        {
            // 计算从物体指向目标点的方向向量（在XZ平面上）
            Vector3 direction = targetPoint - objectPosition;
            direction.y = 0; // 忽略Y轴，只在XZ平面上计算

            // 如果目标点与物体位置重合，返回0度
            if (direction.magnitude < 0.001f)
                return 0f;

            direction.Normalize();

            // 计算世界坐标系下的角度（以世界X轴正方向为0度）
            float worldAngle = Mathf.Atan2(direction.z, direction.x) * Mathf.Rad2Deg;

            // 减去物体的旋转角度，得到相对于物体自身坐标系的角度
            float relativeAngle = worldAngle; //- objectYRotation;

            // 将角度标准化到[-180, 180]范围
            relativeAngle %= 360f;
            if (relativeAngle > 180f)
                relativeAngle -= 360f;
            else if (relativeAngle < -180f)
                relativeAngle += 360f;

            return relativeAngle;
        }

        /// <summary>
        /// 计算围绕目标旋转的当前位置
        /// </summary>
        /// <param name="targetPosition">目标世界坐标</param>
        /// <param name="initialAngle">初始角度（度）</param>
        /// <param name="rotationAxis">旋转轴（世界坐标）</param>
        /// <param name="rotationSpeed">每秒的转速（度/秒）正数逆时针 负数顺时针</param>
        /// <param name="elapsedTime">从旋转开始到现在的时间长度（秒）</param>
        /// <param name="orbitRadius">旋转半径</param>
        /// <returns>当前应该所在的世界坐标</returns>
        public static Vector3 CalculateOrbitPosition(
            Vector3 targetPosition,
            float initialAngle,
            Vector3 rotationAxis,
            float rotationSpeed,
            float elapsedTime,
            float orbitRadius)
        {
            // 计算总旋转角度（初始角度 + 随时间旋转的角度）
            float totalAngle = initialAngle + rotationSpeed * elapsedTime;

            // 创建一个旋转四元数，表示绕指定轴旋转指定角度
            Quaternion rotation = Quaternion.AngleAxis(totalAngle, rotationAxis.normalized);

            // 计算初始位置（假设初始位置在旋转轴的垂直平面内）
            // 我们需要一个垂直于旋转轴的向量作为初始方向
            Vector3 initialDirection = GetPerpendicularVector(rotationAxis.normalized);
            Vector3 initialOffset = initialDirection * orbitRadius;

            // 应用旋转到初始偏移向量
            Vector3 rotatedOffset = rotation * initialOffset;

            // 计算最终位置：目标位置 + 旋转后的偏移
            Vector3 finalPosition = targetPosition + rotatedOffset;

            return finalPosition;
        }

        /// <summary>
        /// 获取与给定向量垂直的向量
        /// </summary>
        private static Vector3 GetPerpendicularVector(Vector3 axis)
        {
            // 如果旋转轴接近世界向上向量，使用世界向前向量作为垂直向量
            if (Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f)
            {
                return Vector3.right;
            }

            // 否则使用世界向上向量与旋转轴的叉积得到垂直向量
            return Vector3.Cross(axis, Vector3.up).normalized;
        }

        //返回面前的目标距离的世界坐标
        public static Vector3 GetPositionInFront(Vector3 position, Vector3 rotation, float distance)
        {
            // 最精简的实现，但保持了可读性
            return position + (Quaternion.Euler(rotation) * Vector3.forward) * distance;
        }

        /// <summary>
        /// 使用四元数作为参数的版本（如果已经计算过四元数）
        /// </summary>
        public static Vector3 GetPositionInFront(Vector3 position, Quaternion rotation, float distance)
        {
            // 避免重复计算四元数
            return position + (rotation * Vector3.forward) * distance;
        }
    }
}
