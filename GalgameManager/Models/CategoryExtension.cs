using GalgameManager.Helpers;

namespace GalgameManager.Models;

public static class CategoryExtension
{
    public static bool ApplySearchKey(this Category category, string searchKey)
    {
        return category.Name.ContainX(searchKey);
    }
}