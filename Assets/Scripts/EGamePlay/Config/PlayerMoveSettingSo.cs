using UnityEngine;

namespace EGamePlay
{
    [CreateAssetMenu(menuName = "MyAsset/PlayerMoveSettingSo")]
    public class PlayerMoveSettingSo : ScriptableObject
    {
        [Header("地面")]
        [XCLabel("地面层级")]
        public LayerMask GroundLayers;

        [Header("转向")]
        [XCLabel("转向速度(度/秒)")]
        public float m_MovingTurnSpeed = 600;
        [XCLabel("站立转向速度（未接入）")]
        public float m_StationaryTurnSpeed = 250;

        [Header("移动")]
        [XCLabel("走路速度")]
        public float NorMoveSpeed = 2.5f;
        [XCLabel("慢跑速度")]
        public float RunMoveSpeed = 5f;
        [XCLabel("快跑速度")]
        public float SprintMoveSpeed = 7.5f;
        [XCLabel("加速时间")]
        public float Acceleration = 0.08f;
        [XCLabel("减速时间")]
        public float Deceleration = 0.05f;
        [XCLabel("最短迈步时间")]
        public float MinimumStepTime = 0.08f;
        [XCLabel("走跑摇杆阈值(已废弃)")]
        public float WalkStickThreshold = 0.55f;

        [Header("锁定")]
        [XCLabel("锁定敌人半径（未接入）")]
        public float LookEnemyRad = 12;
        [XCLabel("锁定敌人角度（未接入）")]
        public float LockEnemyAngle = 60;

        [Header("击飞")]
        [XCLabel("击飞倍率（未接入）")]
        public float AddYRate = 1;
        [XCLabel("击退倍率（未接入）")]
        public float AddHorRate = 1;

        [Header("重力")]
        [XCLabel("重力")]
        public float Gravity = -9.8f;
        [XCLabel("落地重力倍率")]
        public float GravityOnGrondRate = 0.8f;
        [XCLabel("滞空重力增强")]
        public float GravityOnAirAddRate = 1f;
        [XCLabel("重力上限倍率")]
        public float GravityMaxRate = 4f;

        [Header("跳跃")]
        [XCLabel("起跳高度(米)")]
        public float JumpHeight = 1.2f;
        [XCLabel("空中方向控制")]
        [Range(0f, 1f)]
        public float AirControl = 0.65f;
        [XCLabel("空中移速倍率")]
        public float AirMoveSpeedScale = 1f;
        [XCLabel("土狼时间(秒)")]
        public float CoyoteTime = 0.1f;
        [XCLabel("起跳缓冲(秒)")]
        public float JumpBufferTime = 0.12f;
        [XCLabel("落地顿时长(秒)")]
        public float LandSlowTime = 0.1f;
        [XCLabel("落地顿移速倍率")]
        [Range(0.2f, 1f)]
        public float LandSlowScale = 0.55f;
    }
}
