using GalgameManager.Contracts.Services;
using GalgameManager.Enums;
using GalgameManager.Models;
using GalgameManager.WinApp.Base.Contracts;

namespace GalgameManager.Services;

public partial class PluginService
{
    public partial class PotatoVnApiHost : IPotatoVnApi
    {
        private readonly IStaffService _staffService = App.GetService<IStaffService>();

        public Staff? GetStaff(Guid? id) => _staffService.GetStaff(id);

        public Staff? GetStaff(StaffIdentifier identifier) => _staffService.GetStaff(identifier);

        public List<Staff> GetStaffs() => _staffService.GetStaffs();

        public List<Staff> GetStaffs(Galgame game) => _staffService.GetStaffs(game);

        public void SaveStaff(Staff staff, bool sync = true) => _staffService.Save(staff, sync);

        public Task<Staff> ParseStaffAsync(Staff staff, RssType rssType) =>
            _staffService.ParseStaffAsync(staff, rssType);

        public Task ParseGameStaffAsync(Galgame game) => _staffService.ParseStaffAsync(ResolveGame(game));

        public void DeleteStaff(Staff staff, bool sync = true) => _staffService.Delete(staff, sync);
    }
}
