using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 运动组件，在这里管理战斗实体的移动、跳跃、击飞等运动功能
    /// </summary>
    public sealed class MotionComponent : Component
    {
        public override bool IsNeedFixedUpdate { get; protected set; } = true;
        public override bool DefaultEnable { get; set; } = true;
        public Vector3 Position { get => GetEntity<CombatEntity>().Position; set => GetEntity<CombatEntity>().Position = value; }
        public Quaternion Rotation { get => GetEntity<CombatEntity>().Rotation; set => GetEntity<CombatEntity>().Rotation = value; }
        public bool CanMove { get; set; }
        public Vector3 MoveVector { get; set; }

        private Vector3 _moveTarget;

        private float _moveSpeed;

        public override void Awake()
        {
            base.Awake();
            Entity.Subscribe<AttributeUpdateEvent>(UpdateMoveSpeed);
            _moveSpeed = Entity.GetComponent<AttributeComponent>().MoveSpeed.Value;
        }

        public override void OnDestroy()
        {
            base.OnDestroy();
            Entity.UnSubscribe<AttributeUpdateEvent>(UpdateMoveSpeed);
        }

        private void UpdateMoveSpeed(AttributeUpdateEvent event_)
        {
            if(event_.Numeric.AttributeType == AttributeType.MoveSpeed)
            {
                _moveSpeed = (float)event_.Numeric.Value;
            }
        }

        /// <summary>
        /// 这个是与当前位置的偏移
        /// </summary>
        /// <param name="target"></param>
        public void SetMoveDir(Vector3 target)
        {
            _moveTarget = target;
        }

        /// <summary>
        /// 这个是用于直接赋值目标点
        /// </summary>
        /// <param name="target"></param>
        public void SetMoveTarget(Vector3 target)
        {
            _moveTarget = target;
            if (Vector3.Distance(_moveTarget, Position) > 0.1f)
            {
                Vector3 vec2 = -(Position - _moveTarget);
                vec2.Normalize();
                var right = new Vector3(1, 0, 0);
                var y = VectorAngle(right, vec2);
                Rotation = Quaternion.Euler(0, y, 0);
                MoveVector = new Vector3(vec2.x, 0, vec2.z) / 100f;
            }
            else
            {
                MoveVector = Vector3.zero;
            }
        }

        private float VectorAngle(Vector3 from, Vector3 to)
        {
            var angle = 0f;
            var cross = Vector3.Cross(from, to);
            angle = Vector3.Angle(from, to);
            return cross.z > 0 ? angle : -angle;
        }

        public override void FixedUpdate(float fixDeltaTime)
        {
            Position += MoveVector * _moveSpeed * 5;
        }
    }
}