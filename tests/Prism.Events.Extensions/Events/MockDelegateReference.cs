namespace Prism.Events.Extensions.Tests;

class MockDelegateReference : DelegateReference
{
    public MockDelegateReference(Delegate target) : base(target, true)
    {
    }

    public MockDelegateReference(Delegate target, bool preventTargetSet) : base(target, preventTargetSet)
    {
    }

    public new Delegate Target
    {
        get => base.Target;
        set => SetTarget(value);
    }

    private void SetTarget(Delegate target)
    {
        WeakReference _weakReference = typeof(DelegateReference).GetField("_weakReference", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.GetValue(this) as WeakReference;

        if (_weakReference != null) 
        {
            _weakReference.Target = target?.Target;
        }
    }
}
