namespace IL.AttributeBasedDI.Attributes;

public interface IImplementationInstanceProvider<out TService>
{
    static abstract TService GetImplementationInstance();
}
