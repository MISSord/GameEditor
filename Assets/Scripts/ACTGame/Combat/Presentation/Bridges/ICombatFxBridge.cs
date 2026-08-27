namespace ACTGameEditor.Combat
{
    interface ICombatFxBridge
    {
        bool CanPlay(in CombatFxSpec spec);
        object Play(in CombatFxSpec spec);
        void Stop(object backendToken, CombatFxKind kind);
    }
}
