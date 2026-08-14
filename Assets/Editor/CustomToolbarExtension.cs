using System;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[InitializeOnLoad]
public static class CustomToolbarExtension
{
    static ScriptableObject currentToolbar;
    private static VisualElement _container;
    private static GUIStyle _customButtonStyle;
    static Type toolbarType = typeof(Editor).Assembly.GetType("UnityEditor.Toolbar");

    static CustomToolbarExtension()
    {
        EditorApplication.update += OnUpdate;
    }

    static void OnUpdate()
    {
        if (currentToolbar == null)
        {
            var toolbars = Resources.FindObjectsOfTypeAll(toolbarType);
            currentToolbar = toolbars.Length > 0 ? (ScriptableObject)toolbars[0] : null;

            if (currentToolbar != null)
            {
                FieldInfo roots = currentToolbar.GetType().GetField("m_Root", BindingFlags.NonPublic | BindingFlags.Instance);
                var rootObj = roots.GetValue(currentToolbar);
                var toolbar = rootObj as VisualElement;
                if (toolbar == null) return;

                // 创建容器
                _container = new IMGUIContainer(OnContainerGUI)
                {
                    style = {
                    flexGrow = 1,
                    flexDirection = FlexDirection.Row,
                    }
                };

                var root = toolbar.parent;
                root.Add(_container);
            }
        }
    }

    private static void OnContainerGUI()
    {
        // 现在在安全的IMGUI上下文中
        GUILayout.BeginHorizontal();

        if (GUILayout.Button("工具1", GetButtonStyle()))
        {
            Debug.Log("自定义工具1被点击");
        }

        if (GUILayout.Button("工具2", GetButtonStyle()))
        {
            Debug.Log("自定义工具2被点击");
        }

        GUILayout.EndHorizontal();
    }

    private static GUIStyle GetButtonStyle()
    {
        if (_customButtonStyle == null)
        {
            _customButtonStyle = new GUIStyle(EditorStyles.toolbarButton)
            {
                fixedHeight = 20f,
                margin = new RectOffset(2, 2, 2, 2)
            };
        }
        return _customButtonStyle;
    }
}