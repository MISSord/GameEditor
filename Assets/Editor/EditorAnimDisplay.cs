using UnityEditor;
using UnityEngine;

namespace Assets.Editor
{
    public class EditorAnimDisplay : EditorWindow
    {
        #region init
        private static EditorAnimDisplay _instance;
        [MenuItem("Tools/XiaoCao/动画预览窗口")]
        static void PrefabWrapTool()
        {
            //获取当前窗口实例
            _instance = EditorWindow.GetWindow<EditorAnimDisplay>();
            _instance.Show();
            //ShowUtility() 实体窗口样式
        }
        #endregion

        public AnimationClip[] clips;
        public GameObject player;
        Vector2 pos = Vector2.zero;
        public string Fitter = "";
        public string NoFitter = "";
        
        private AnimationClip _curAnimClip;
        private float Timer = 0;
        private int _playCount = 0;
        private bool _isStop = true;
        
        void OnGUI()
        {
            var obj = new SerializedObject(new UnityEngine.Object[] { this }, this);
            XiaoCao.XiaoCaoWindow.DrawHeader(obj);

            player = EditorGUILayout.ObjectField("player", player, typeof(GameObject), true) as GameObject;
            Fitter= EditorGUILayout.TextField("包含",Fitter );
            NoFitter = EditorGUILayout.TextField("不包含", NoFitter);
            if (player)
            {
                clips = player.GetComponent<Animator>().runtimeAnimatorController.animationClips;
                pos = GUILayout.BeginScrollView(pos, false, false);
                foreach (var item in clips)
                {

                    if (IsShow(item.name)&&GUILayout.Button(item.name))
                    {
                        //复制
                        GUIUtility.systemCopyBuffer = item.name;
                        PlayAnim(item);
                    }
                }
                GUILayout.EndScrollView();
            }
        }


        private bool IsShow(string clipName)
        {
            bool isInFitter = false;

            if (Fitter.IsEmpty())
            {
                isInFitter = true;
            }
            else
            {
                isInFitter = clipName.ToLower().Contains(Fitter.ToLower());
            }

            bool isNoInFitter = false;
            if (NoFitter.IsEmpty())
            {
                isNoInFitter = true;
            }
            else
            {
                isNoInFitter = ! clipName.ToLower().Contains(NoFitter.ToLower());
            }
            return isInFitter && isNoInFitter;
        }

        private void PlayAnim(AnimationClip clip)
        {
            Timer = 0;
            _playCount = 0;
            _curAnimClip = clip;
            Selection.activeObject = clip;
            _isStop = false;
        }

        private void Update()
        {
            //if(Timer<10)
            UpdateAnim(Time.deltaTime);
        }

        private void UpdateAnim(float delta)
        {
            if (_curAnimClip != null)
            {

                if (!_isStop)
                {
                    Timer += delta;

                    if (Timer > _curAnimClip.length)
                    {
                        _playCount++;
                        Timer = 0;
                    }

                    if(_playCount < 2)
                    {
                        _curAnimClip.SampleAnimation(player, Timer);                
                    }
                    else
                    {
                        _isStop = true;
                    }
                }
            }
        }

    }
}
