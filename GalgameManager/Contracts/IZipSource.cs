using GalgameManager.Enums;

namespace GalgameManager.Contracts;

public interface IZipSource: IGalgameSource
{
    public GalgameZipProtocol GetProtocol();
}