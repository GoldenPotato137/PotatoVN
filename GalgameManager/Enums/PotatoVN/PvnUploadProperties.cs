namespace GalgameManager.Enums;

[Flags]
public enum PvnUploadProperties
{
    None = 0,
    Infos = 1 << 0,
    ImageLoc = 1 << 1,
    Review = 1 << 2,
    PlayTime = 1 << 3,
    All = int.MaxValue,
}
