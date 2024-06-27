namespace GalgameManager.Models.Sources;

public class GalgameAndPath
{
    public Galgame Galgame { get; set; }
    public string Path { get; set; }

    public GalgameAndPath(Galgame game, string path)
    {
        Galgame = game;
        Path = path;
    }
}