using EGamePlay;
using EGamePlay.Combat;
using UnityEngine;
using UnityEngine.InputSystem;
using ACTGameEditor;

#if UNITY
public class EGamePlayInit : MonoBehaviour
{
    public static EGamePlayInit Instance { get; private set; }
    public bool EntityLog;
    [Tooltip("开：入队不扣 CD，时间轴启动后才转。关：Idle 入队立刻扣 CD。")]
    public bool UseAbilityGate = true;
    public InputActionAsset ActionsAsset;

#if !EGAMEPLAY_ET
    private void Awake()
    {
        Instance = this;
        Physics.autoSimulation = false;

        ConfigurableInputManager.Instance.InputActionsAsset = ActionsAsset;

        Entity.EnableLog = EntityLog;
        var ecsNode = ECSNode.Create();
        ecsNode.AddChildNoPool<ETTimerManager>();
        ecsNode.AddChildNoPool<CombatContext>();
        CombatContext.Instance.UseAbilityGate = UseAbilityGate;
        ecsNode.AddChildNoPool<GameObjectPool>();

        FastStaticExecutor.Initialize<SkillMethod>();
    }

    private void Start()
    {
        //输入系统注册
        ConfigurableInputManager.Instance.Start();
        ConfigurableInputManager.Instance.InitListener();        
        //标签系统初始化
        TagCollection.Instance.Initialize();
        //创建角色
        PlayerManager.Instance.AddTruePlayer();
        //绑定角色
        ConfigurableInputManager.Instance.ChangeCurPlayer();
    }

    private void Update()
    {
        TimeScaleEffectManager.Tick();
        GameTimeManager.Tick();
        ETTimerManager.Instance.Update(GameTimeManager.WorldDelta);
        var combatContext = CombatContext.Instance;
        if (combatContext != null)
        {
            combatContext.UseAbilityGate = UseAbilityGate;
            combatContext.Update(GameTimeManager.WorldDelta);
        }
        ConfigurableInputManager.Instance.Update();
    }

    private void FixedUpdate()
    {
        GameTimeManager.FixedTick();
        if (!GameTimeManager.IsWorldPaused)
        {
            float step = Time.fixedDeltaTime * GameTimeManager.WorldScale;
            if (step > 0f)
                Physics.Simulate(step);
        }
        CombatContext.Instance.FixedUpdate(Time.fixedDeltaTime * GameTimeManager.WorldScale);
    }

    private void OnApplicationQuit()
    {
        ECSNode.Destroy();
    }

#endif
}
#endif