namespace AscendantEmpire.Domain.Entities.Assets;

internal class ValueComparer<T>
{
    private Func<object, object, bool> value1;
    private Func<object, object> value2;
    private Func<object, object> value3;

    public ValueComparer(Func<object, object, bool> value1, Func<object, object> value2, Func<object, object> value3)
    {
        this.value1 = value1;
        this.value2 = value2;
        this.value3 = value3;
    }
}