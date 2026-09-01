namespace ACTGameEditor.Combat
{
    /// <summary>底层表现后端桥接。</summary>
    interface ICombatFxBridge
    {
        bool CanPlay(in CombatFxSpec spec);
        object Play(in CombatFxSpec spec);
        void Stop(object backendToken, CombatFxKind kind);
    }
}
