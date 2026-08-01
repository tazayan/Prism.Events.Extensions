namespace Prism.Events.Extensions.Tests;

class MockDelegateReference : IDelegateReference
{
    public Delegate Target { get; set; }

    public MockDelegateReference()
    {

    }

    public MockDelegateReference(Delegate target)
    {
        Target = target;
    }
}
