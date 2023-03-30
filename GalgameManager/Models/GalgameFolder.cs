namespace GalgameManager.Models;

public class GalgameFolder 
{
    public string Path
    {
        get;
        set;
    }
    
    public GalgameFolder(string path)
    {
        Path = path;
    }
}
