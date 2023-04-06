using CommunityToolkit.Mvvm.ComponentModel;

namespace GalgameManager.Models;

public partial class LockableProperty<T> : ObservableObject
{
    private T? _value;

    public T? Value
    {
        get => _value;
        set
        {
            if (!_isLock)
            {
                SetProperty(ref _value, value);
            }
        }
    }
        
    [ObservableProperty]
    private bool _isLock;

    // 隐式类型转换
    public static implicit operator LockableProperty<T>(T value)
    {
        return new LockableProperty<T> { Value = value };
    }

    // 反向隐式类型转换
    public static implicit operator T?(LockableProperty<T> lockableProperty)
    {
        return lockableProperty._value;
    }
}