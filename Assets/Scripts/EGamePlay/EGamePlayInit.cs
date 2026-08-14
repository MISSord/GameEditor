using EGamePlay;
using EGamePlay.Combat;
using ET;
using UnityEngine;
using UnityEngine.InputSystem;
using ACTGameEditor;

#if UNITY
public class EGamePlayInit : MonoBehaviour
{
    public static EGamePlayInit Instance { get; private set; }
    public bool EntityLog;
    public InputActionAsset ActionsAsset;
    //public ReferenceCollector ConfigsCollector;

#if !EGAMEPLAY_ET
    private void Awake()
    {
        Instance = this;
        Physics.autoSimulation = false;

        ////进行输入操作绑定
        ConfigurableInputManager.Instance.InputActionsAsset = ActionsAsset;

        //这个用于ET多线程 Socket
        //SynchronizationContext.SetSynchronizationContext(ThreadSynchronizationContext.Instance);

        Entity.EnableLog = EntityLog;
        var ecsNode = ECSNode.Create();
        ecsNode.AddChildNoPool<ETTimerManager>();
        ecsNode.AddChildNoPool<CombatContext>();
        ecsNode.AddChildNoPool<GameObjectPool>();

        //快速记录
        FastStaticExecutor.Initialize<SkillMethod>();
        //ecsNode.AddComponent<ConfigManageComponent>(ConfigsCollector);
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
        //ThreadSynchronizationContext.Instance.Update();
        ETTimerManager.Instance.Update(GameTimeManager.WorldDelta);
        CombatContext.Instance.Update(GameTimeManager.WorldDelta);
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