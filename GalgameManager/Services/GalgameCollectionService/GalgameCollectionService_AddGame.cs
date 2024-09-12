using GalgameManager.Enums;

namespace GalgameManager.Services;

public partial class GalgameCollectionService
{
    public async Task<AddGalgameResult> AddLocalGameAsync(string path, bool force)
    {
        await Task.CompletedTask;
        return AddGalgameResult.Success;
    }
}