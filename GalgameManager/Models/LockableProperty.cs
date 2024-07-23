using CommunityToolkit.Mvvm.ComponentModel;
using GalgameManager.Helpers;

namespace GalgameManager.Models;

public partial class LockableProperty<T> : ObservableObject, IComparable
{
    public event GenericDelegate<T?>? OnValueChanged;
    
    private T? _value;

    public T? Value
    {
        get => _value;
        set
        {
            if (IsLock) return;
            SetProperty(ref _value, value);
            OnValueChanged?.Invoke(value);
        }
    }
        
    [ObservableProperty]
    private bool _isLock;

    public LockableProperty(T value)
    {
        _value = value;
    }

    // 隐式类型转换
    public static implicit operator LockableProperty<T>(T value)
    {
        return new LockableProperty<T>(value);
    }

    // 反向隐式类型转换
    public static implicit operator T?(LockableProperty<T> lockableProperty)
    {
        return lockableProperty._value;
    }
    
    public void ForceSet(T? value)
    {
        SetProperty(ref _value, value);
        OnValueChanged?.Invoke(value);
    }

    public int CompareTo(object? obj)
    {
        if (Value is IComparable value)
        {
            if (obj is LockableProperty<T> lockableProperty) return value.CompareTo(lockableProperty._value);
            return value.CompareTo(obj);
        }

        throw new ArgumentException($"{nameof(T)} is not comparable");
    }
}