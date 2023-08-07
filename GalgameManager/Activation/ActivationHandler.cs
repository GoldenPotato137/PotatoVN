namespace GalgameManager.Activation;

public abstract class ActivationHandler<T> : IActivationHandler where T : class
{
    // Override this method to add the logic for whether to handle the activation.
    protected abstract bool CanHandleInternal(T args);

    // Override this method to add the logic for your activation handler.
    protected abstract Task HandleInternalAsync(T args);

    public bool CanHandle(object args) => args is T tmp && CanHandleInternal(tmp);

    public async Task HandleAsync(object args) => await HandleInternalAsync((args as T)!);
}
