using System;
using System.Collections.Generic;
using ACTGameEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using EGamePlay;

// 关于GameObject的对象池
public class RunTimePoolManager : MonoSingleton<RunTimePoolManager>
{
    private Vector3 _outPosition = new Vector3(999, 999, 999);

    public Dictionary<string , RunTimePool> ResPool = new Dictionary<string, RunTimePool>();

    public static string GetResPath(string bundle, string asset)
    {
        return bundle + "|" + asset;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="bundle"></param>
    /// <param name="asset"></param>
    /// <param name="AutoRecycel">自动回收时间</param>
    /// <returns></returns>
    public GameObject LoadResPoolObj(string bundle, string asset, float AutoRecycel = 0)
    {
        if(bundle.IsEmpty() || asset.IsEmpty()) 
            return null;

        string resPath = bundle + "|" + asset;
        if (!ResPool.ContainsKey(resPath))
        {
            GameObject pool = new GameObject(resPath);
            pool.transform.SetParent(transform);
            ResPool.Add(resPath, new RunTimePool(bundle, asset, pool.transform));
        }

        var obj = ResPool[resPath].GetObj();
        if (AutoRecycel > 0 && ETTimerManager.Instance != null)
        {
            long tillTimeMs = TimeHelper.ClientNow() + (long)(AutoRecycel * 1000);
            long timerId = ETTimerManager.Instance.NewOnceTimer(tillTimeMs, () => ReCycle(resPath, obj));
            ResPool[resPath].TimerDic.Add(obj.GetHashCode(), timerId);
        }
        return obj;
    }

    public void ReCycle(string resPath, GameObject game)
    {
        if (ResPool.ContainsKey(resPath))
        {
            var pool = ResPool[resPath];
            pool.Recycle(game);
            //回收后立马挪远点
            game.transform.position = _outPosition;
        }
    }

    public AudioClip GetAudioClip(string ID, bool isHit)
    {
        (string, string) bundle = isHit? PrefabPath.GetHitMp3Path(ID) : PrefabPath.GetMp3Path(ID);

        AudioClip res = AssetBundleManager.Instance.LoadAssetSync<AudioClip>(bundle.Item1, bundle.Item2);
        if (res == null)
        {
            Debug.LogError("yns no " + bundle);
        }
        return res;
    }

    public Transform HitEffectParent;

    /// <summary>运行时场景物体根节点，切场景不销毁。</summary>
    public Transform SceneObjLayer { get; private set; }

    public override void Init()
    {
        base.Init();
        HitEffectParent = new GameObject("HitEffectParent").transform;
        EnsureSceneObjLayer();
    }

    /// <summary>将物体挂到 SceneObjLayer 下，保留世界坐标。</summary>
    public void AttachToSceneLayer(Transform child)
    {
        if (child == null)
            return;

        EnsureSceneObjLayer();
        if (child.parent == SceneObjLayer)
            return;

        child.SetParent(SceneObjLayer, true);
    }

    void EnsureSceneObjLayer()
    {
        if (SceneObjLayer != null)
            return;

        var go = new GameObject("SceneObjLayer");
        Transform ddolRoot = transform.parent;
        if (ddolRoot != null)
            go.transform.SetParent(ddolRoot, false);
        else
            DontDestroyOnLoad(go);

        go.transform.localPosition = Vector3.zero;
        go.transform.localRotation = Quaternion.identity;
        go.transform.localScale = Vector3.one;
        SceneObjLayer = go.transform;
    }
    public Dictionary<string, TypeLoopPool<GameObject>> LoopPoolDic = new Dictionary<string, TypeLoopPool<GameObject>>();

    public GameObject GetHitEffect(string ID)
    {
        if (ID.IsEmpty())
        {
            ID = "0";
        }

        if (!LoopPoolDic.ContainsKey(ID))
        {
            var Prefab = AssetBundleManager.Instance.LoadAssetSync<GameObject>(PrefabPath.HitEffectPath, ID);
            if (Prefab == null)
            {
                Debug.LogError($"yns path null {ID}");
                Prefab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Prefab.name = "ErrorCube";
            }
            TypeLoopPool<GameObject> newPool = new TypeLoopPool<GameObject>(Prefab, 13);
            LoopPoolDic.Add(ID, newPool);
        }

        GameObject one = LoopPoolDic[ID].GetOne();
        one.transform.SetParent(HitEffectParent,true);
        return one;
    }

}


public class RunTimePool
{
    public RunTimePool(string bundle, string asset, Transform poolTF)
    {
        BundlePath = bundle;
        AssetPath = asset;
        Prefab = AssetBundleManager.Instance.LoadAssetSync<GameObject>(bundle, asset);
        if (Prefab == null)
        {
            Debug.LogError($"no res {bundle} {asset}");
        }
        PoolTransform = poolTF;
    }

    /// <summary> 对象实例 HashCode -> ETTimerManager 定时器 ID（用于自动回收） </summary>
    public Dictionary<int, long> TimerDic = new Dictionary<int, long>();

    public string BundlePath;
    public string AssetPath;
    private GameObject Prefab;
    private Transform PoolTransform;

    public List<GameObject> UsingObj = new List<GameObject>();
    public List<GameObject> PoolObj = new List<GameObject>();

    public GameObject GetObj()
    {
        if (Prefab == null)
            return null;

        GameObject obj = null;
        while (PoolObj.Count > 0)
        {
            int last = PoolObj.Count - 1;
            obj = PoolObj[last];
            PoolObj.RemoveAt(last);
            if (obj != null)
            {
                UsingObj.Add(obj);
                return obj;
            }
        }

        obj = GameObject.Instantiate(Prefab);
        UsingObj.Add(obj);
        return obj;
    }

    //放回池
    public void Recycle(GameObject obj)
    {
        if (obj == null)
            return;

        if (PoolObj.Contains(obj))
            return;

        UsingObj.Remove(obj);
        PoolObj.Add(obj);
        obj.transform.SetParent(PoolTransform, false);
        obj.transform.localPosition = Vector3.zero;
        obj.transform.localRotation = Quaternion.identity;
        if (Prefab != null)
            obj.transform.localScale = Prefab.transform.localScale;
        obj.SetActive(false);

        int hash = obj.GetHashCode();
        if (TimerDic.TryGetValue(hash, out long timerId))
        {
            ETTimerManager.Instance?.Remove(timerId);
            TimerDic.Remove(hash);
        }
    }

}


public class TypePool<T> where T: Behaviour
{
    public T prefab;

    public TypePool(T prefab, Action<T> recyleAction)
    {
        RecyleAction = recyleAction;
        this.prefab = prefab;
    }
    
    public Action<T> RecyleAction;

    Queue<T> unUseQue = new Queue<T>();

    public T GetOne()
    {
        if (unUseQue.Count > 0)
        {
            return unUseQue.Dequeue();
        }
        else
        {
           return GameObject.Instantiate(prefab);
        }
    }

    public void Recyle(T rec)
    {
        unUseQue.Enqueue(rec);
        RecyleAction?.Invoke(rec);
    }

}

public class TypeLoopPool<T> where T : Object
{
    public T prefab;

    public int Max = 5; //大于0

    public int curIndex;

    public int genCount;

    List<T> totalList = new List<T>();

    public TypeLoopPool(T prefab,int Max)
    {
        this.prefab = prefab;
        this.Max = Max;
    }

    public T GetOne()
    {
        if (genCount < Max)
        {
            T newOne = GameObject.Instantiate(prefab);
            totalList.Add(newOne);
            genCount++;
            curIndex++;
            return newOne;
        }
       
        if (curIndex >= Max)
        {
            curIndex = curIndex % Max;    
        }
        return totalList[curIndex++];
    }

}