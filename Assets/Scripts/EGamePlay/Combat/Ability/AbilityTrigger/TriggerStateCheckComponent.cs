using DynamicExpresso;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace EGamePlay.Combat
{
    /// <summary>
    /// 表达式上下文：Caster、Target、Skill，供 ExpressionCondition 使用。
    /// </summary>
    public class ExpressionContext
    {
        //创建者
        public Entity Caster;
        //目标
        public Entity Target;
        //技能或者Buff信息
        public Entity Skill;
    }

    public class ExpressionCondition
    {
        [TextArea]
        public string Expression; // 策划输入: "Target.HP < 0.5 && Caster.Level >= 10"

        // 缓存编译后的 Lambda，避免每帧解析字符串（关键性能点！）
        private Func<Entity, Entity, bool> _compiledFunc;

        public void Compile()
        {
            var interpreter = new Interpreter();

            // 注册变量类型，让策划在字符串里能有代码提示（如果编辑器支持）或者类型安全
            // 定义参数：caster, target
            _compiledFunc = interpreter.ParseAsDelegate<Func<Entity, Entity, bool>>(Expression, "Caster", "Target");
        }

        public bool Check(ExpressionContext ctx)
        {
            if (_compiledFunc == null) Compile();
            try
            {
                return _compiledFunc(ctx.Caster, ctx.Target);
            }
            catch
            {
                return false; // 容错处理
            }
        }
    }
}