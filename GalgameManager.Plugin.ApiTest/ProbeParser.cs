using GalgameManager.Contracts.Phrase;
using GalgameManager.Enums;
using GalgameManager.Models;

namespace GalgameManager.Plugin.ApiTest;

internal sealed class ProbeParser : IGalInfoPhraser, IGalCharacterPhraser, IGalCoversParser,
    IGalHeadersParser, IGalStaffParser
{
    internal static ProbeParser Instance { get; } = new();
    internal static RssType RssType => (RssType)ApiProbePlugin.ParserTypeValue;

    public Task<Galgame?> GetGalgameInfo(Galgame game)
    {
        Galgame result = new(game.Name.Value ?? "Plugin API Probe Game")
        {
            RssType = RssType,
            Id = $"probe-{game.Uuid:N}",
        };
        return Task.FromResult<Galgame?>(result);
    }

    public RssType GetPhraseType() => RssType;

    public Task<GalgameCharacter?> GetGalgameCharacter(GalgameCharacter character) =>
        Task.FromResult<GalgameCharacter?>(new GalgameCharacter
        {
            Name = string.IsNullOrWhiteSpace(character.Name) ? "Probe Character" : character.Name,
            Summary = "Parsed by Plugin API Probe",
        });

    public Task<List<string>> GetGalCoversAsync(Galgame game) =>
        Task.FromResult(new List<string> { "https://probe.invalid/cover.png" });

    public Task<List<string>> GetGalHeadersAsync(Galgame game) =>
        Task.FromResult(new List<string> { "https://probe.invalid/header.png" });

    public Task<Staff?> GetStaffAsync(Staff staff) => Task.FromResult<Staff?>(new Staff
    {
        JapaneseName = "Probe Parsed Staff",
        EnglishName = staff.EnglishName,
        Description = "Parsed by Plugin API Probe",
    });

    public Task<List<StaffRelation>> GetStaffsAsync(Galgame game) =>
        Task.FromResult(new List<StaffRelation>());
}
