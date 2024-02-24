namespace GalgameManager.Server.Helpers;

public static class DtoHelper
{
    public static List<TDto> ToDtoList<TDto, TModel>(this IEnumerable<TModel> models, Func<TModel, TDto> converter)
        where TDto : class
        where TModel : class
    {
        return models.Select(converter).ToList();
    }
}