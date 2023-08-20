using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using SQLite;

namespace GalgameManager.Helpers.Phrase;

public static class PhraseHelper
{
    private const string DbFile = @"Assets\Data\vn_mapper.db";
    private static bool _init;
    private static SQLiteAsyncConnection? _db;

    private static void Init()
    {
        Assembly assembly = Assembly.GetExecutingAssembly();
        var file = Path.Combine(Path.GetDirectoryName(assembly.Location)!, DbFile);
        if (!File.Exists(file)) return;
        _db = new SQLiteAsyncConnection(file);
        _init = true;
    }

    public static async Task<int?> TryGetVndbIdAsync(string name)
    {
        if (_init == false) Init();
        if (_db is null) return null;
        List<TitleModel>? games = await _db.Table<TitleModel>().ToListAsync();
        int? result = null, minDis = int.MaxValue;
        await Task.Run(() =>
        {
            foreach (TitleModel game in games.Where(g => g.Title!.JaroWinkler(name) > 0.5))
                if (game.Title is not null && name.Levenshtein(game.Title) < minDis)
                {
                    minDis = name.Levenshtein(game.Title);
                    result = game.VndbId;
                    if (minDis == 0) break;
                }
        });
        return minDis < 1 ? result : null;
    }

    public static async Task<int?> TryGetBgmIdAsync(string name)
    {
        if(_init == false) Init();
        if (_db is null) return null;
        var vndbId = await TryGetVndbIdAsync(name);
        if (vndbId is null) return null;
        MapModel result = await _db.FindAsync<MapModel>(vndbId);
        if(result is not null && result.BgmDistance < 3)
            return result.BgmId;
        return null;
    }

    [Table("title")]
    private class TitleModel
    {
        public int VndbId
        {
            get;
            set;
        }

        [PrimaryKey]
        public string? Title
        {
            get;
            set;
        }

        public static bool operator ==(TitleModel x, TitleModel y)
        {
            return x.VndbId == y.VndbId && x.Title == y.Title;
        }

        public static bool operator !=(TitleModel x, TitleModel y)
        {
            return !(x == y);
        }

        public override bool Equals(object? obj)
        {
            if (obj is TitleModel titleModel)
                return this == titleModel;
            return false;
        }

        [SuppressMessage("ReSharper", "NonReadonlyMemberInGetHashCode")]
        public override int GetHashCode()
        {
            return HashCode.Combine(VndbId, Title);
        }
    }

    [Table("map")]
    private class MapModel
    {
        [PrimaryKey, AutoIncrement]
        public int VndbId
        {
            get;
            set;
        }

        public int BgmId
        {
            get;
            set;
        }

        public int BgmDistance
        {
            get;
            set;
        } = int.MaxValue;

        public MapModel(int vndbId)
        {
            VndbId = vndbId;
        }

        public MapModel()
        {
        }
    }
}