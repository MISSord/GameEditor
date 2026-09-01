using EGamePlay;
using EGamePlay.Combat;
using EGamePlay.Unity;
using UnityEngine;
using UnityEngine.InputSystem;
using ACTGameEditor;
using ACTGameEditor.Combat;

#if UNITY
/// <summary>
/// 战斗主循环。100：晚于默认 EventSystem（0）再 Sample，避免点按轮盘前把空轴锁进快照。
/// </summary>
[DefaultExecutionOrder(100)]
public class EGamePlayInit : MonoBehaviour
{
    public static EGamePlayInit Instance { get; private set; }
    public bool EntityLog;
    [Tooltip("开：入队不扣 CD，时间轴启动后才转。关：Idle 入队立刻扣 CD。")]
    public bool UseAbilityGate = true;
    [Tooltip("伤害 HitFlash / HitStop 默认参数；留空则使用内置默认值。")]
    public CombatFxPreset FxPreset;
    [Tooltip("表现 Package 目录；留空则使用内置默认包。")]
    public CombatFxPackageCatalog FxPackageCatalog;
    public InputActionAsset ActionsAsset;

#if !EGAMEPLAY_ET
    private void Awake()
    {
        Instance = this;
        if (FxPreset != null)
            CombatFxPreset.SetActive(FxPreset);
        if (FxPackageCatalog != null)
            CombatFxPackageCatalog.SetActive(FxPackageCatalog);
        Physics.autoSimulation = false;

        ConfigurableInputManager.Instance.InputActionsAsset = ActionsAsset;

        Entity.EnableLog = EntityLog;
        var ecsNode = ECSNode.Create();
        ecsNode.AddChildNoPool<ETTimerManager>();
        ecsNode.AddChildNoPool<CombatContext>();
        ecsNode.AddChildNoPool<GameObjectPool>();

        CombatContext.Instance.UseAbilityGate = UseAbilityGate;

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
        ConfigurableInputManager.Instance.Update();
        var combatContext = CombatContext.Instance;
        if (combatContext != null)
        {
            combatContext.UseAbilityGate = UseAbilityGate;
            combatContext.Update(GameTimeManager.WorldDelta);
        }
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
        CombatPresentationDirector.ClearAll();
        ActSkillTimelineLoader.Clear();
        AbilityDefinitionManager.DestroyInstance();
        ECSNode.Destroy();
    }

#endif
}
#endif