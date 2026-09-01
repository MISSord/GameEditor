using System;
using System.Collections.Generic;
using System.Reflection;
using EGamePlay;

namespace EGamePlay.Combat
{
    public class BuffStateCheckComponent: Component
    {
        public ExpressionCondition CheckStateExpress;
        public string CheckStateMethod;

        ////下面是每帧判断的
        //public ExpressionCondition UpdateCheckStateExpress;
        //public string UpdateCheckStateMethod;
        //public ExpressionCondition UpdateRemoveCheckStateExpress;
        //public string UpdateRemoveCheckStateMethod;

        public override void Awake()
        {
            var effectConfig = Entity.As<Buff>().Setting;
            if (string.IsNullOrEmpty(effectConfig.TriggerFormula) == false)
            {
                string method = effectConfig.TriggerFormula;
                if(method.IndexOf("#") == 0) //是方法
                {
                    CheckStateMethod = method.Substring(1);
                }
                else
                {
                    CheckStateExpress = AddNewExpression(method);
                }
            }

            //if (string.IsNullOrEmpty(effectConfig.EffectRemoveFormula) == false)
            //{
            //    string method = effectConfig.EffectRemoveFormula;
            //    if (method.IndexOf("#") == 0) //是方法
            //    {
            //        RemoveCheckStateMethod = method.Substring(1);
            //    }
            //    else
            //    {
            //        RemoveCheckStateExpress = AddNewExpression(method);
            //    }
            //}
        }

        private ExpressionCondition AddNewExpression(string conditionStr)
        {
            //创建解析方法
            ExpressionCondition condition = new ExpressionCondition();
            condition.Expression = conditionStr;
            condition.Compile();
            return condition;
        }

        public override void OnDestroy()
        {
            CheckStateExpress = null;
            CheckStateMethod = null;

            //RemoveCheckStateExpress = null;
            //RemoveCheckStateMethod = null;
        }

        public bool CheckTargetState(Entity target)
        {
            GameLog.Debug("BuffTriggerStateCheckComponent CheckTargetState");
            // 这里是状态判断，状态判断是判断目标的状态是否满足条件，满足则触发效果

            //判断方法
            if (CheckStateMethod != null)
            {
                return BuffStateCheck.CallStaticMethodEfficiently(CheckStateMethod, target, GetEntity<Buff>());
            }
            
            //判断简单表达式
            var conditionCheckResult = true;
            ExpressionContext context = new ExpressionContext();
            context.Target = target;
            context.Caster = Entity.As<Buff>().OwnerEntity?.Entity;
            context.Skill = Entity;

            if (CheckStateExpress.Check(context) == false)
            {
                conditionCheckResult = false;
            }
            return conditionCheckResult;
        }

        //public bool CheckRemoveTargetState(Entity target)
        //{
        //    Log.Debug("BuffTriggerStateCheckComponent CheckRemoveTargetState");
        //    // 这里是状态判断，状态判断是判断目标的状态是否满足条件，满足则触发效果

        //    //判断方法
        //    if (RemoveCheckStateMethod != null)
        //    {
        //        return BuffStateCheck.CallStaticMethodEfficiently(RemoveCheckStateMethod, target, GetEntity<Buff>());
        //    }

        //    //判断简单表达式
        //    var conditionCheckResult = true;
        //    ExpressionContext context = new ExpressionContext();
        //    context.Target = target;
        //    context.Caster = Entity.As<Buff>().OwnerEntity;
        //    context.Skill = Entity;

        //    if (RemoveCheckStateExpress.Check(context) == false)
        //    {
        //        conditionCheckResult = false;
        //    }
        //    return conditionCheckResult;
        //}
    }

    public class BuffStateCheck
    {
        // 定义一个委托来匹配你的静态方法签名
        private delegate bool BuffStateCheckDelegate(Entity target, Buff buff);

        private static Dictionary<string, Delegate> _methodCache = new Dictionary<string, Delegate>();

        public static bool CallStaticMethodEfficiently(string name, Entity target, Buff buff)
        {
            string className = "EGamePlay.Combat.BuffMethod";
            string methodName = name;
            string cacheKey = $"{className}.{methodName}";

            // 1. 尝试从缓存中获取委托
            if (!_methodCache.TryGetValue(cacheKey, out var methodDelegate))
            {
                // 2. 缓存中没有，使用反射获取方法信息（同上）
                Type targetType = Type.GetType(className);
                MethodInfo methodInfo = targetType?.GetMethod(methodName,
                    BindingFlags.Public | BindingFlags.Static,
                    null,
                    new Type[] { typeof(Entity), typeof(Buff) },
                    null
                );

                if (methodInfo == null) 
                    return false;

                // 3. 创建强类型委托
                methodDelegate = methodInfo.CreateDelegate(typeof(BuffStateCheckDelegate));

                // 4. 存入缓存
                _methodCache[cacheKey] = methodDelegate;
            }

            // 5. 转换为具体委托类型并调用（此步速度极快，接近直接调用）
            var typedDelegate = (BuffStateCheckDelegate)methodDelegate;
            return typedDelegate.Invoke(target, buff);
        }
    }

    public class BuffMethod
    {
        /// <summary>
        /// 检查是否有火焰类型的buff
        /// </summary>
        /// <param name="target"></param>
        /// <param name="buff"></param>
        /// <returns></returns>
        public static bool CheckIsHadFireBigBuff(Entity target, Buff buff)
        {
            if (target != null
                && target.GetComponent<StatusComponent>()?.HasBigBuffType(4) == true)
            {
                return true;
            }
            return false;
        }

        /// <summary>
        /// 本次行动是否为“释放普攻或大招”。被动/Buff 伤害不会走施法 Session，因此不会命中。
        /// </summary>
        /// <summary>
        /// 本次伤害是否为主动技能真实命中。Buff/点燃跳字不会再触发，闪避与免疫也不算命中。
        /// </summary>
        public static bool IsActiveSkillHit(Entity target, Buff buff)
        {
            if (target is not DamageAction damage)
                return false;
            if (damage.DamageSource != DamageSource.Skill)
                return false;
            if (damage.DamageActionEffect.HasFlag(DamageActionEffect.Interrupt)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Dodge)
                || damage.DamageActionEffect.HasFlag(DamageActionEffect.Immunity))
                return false;
            if (damage.Target == null || damage.Target.IsDisposed || damage.Target.IsDead)
                return false;

            var config = damage.TriggerContext.SourceAbility?.Definition?.Config;
            if (config != null && config.Type == AbilityType.PassiveSkill.ToString())
                return false;
            return true;
        }
    }
}
