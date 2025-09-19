using System;
using System.Linq;

namespace GalgameManager.Helpers;

public class ExUri : Uri, IEquatable<ExUri>
{
    private int _depth = -1;
    public int Depth
    {
        get
        {
            if (_depth != -1) return _depth;
            
            _depth = 0;
            Uri current = this;
            while (true)
            {
                Uri parent = new(current, "..");
                if (parent.Equals(current)) break;
                _depth++;
                current = parent;
            }
            return _depth;
        }
    }
    
    private string? _name;
    /// <summary>
    /// 获取 URI 路径的最后一部分作为名称。<br/>
    /// 例如："ftp://host/a/b/c" -> "c"<br/>
    /// 例如："ftp://host/a/b/c/" -> "c"<br/>
    /// 例如："ftp://host/" -> "/"
    /// </summary>
    public string Name
    {
        get
        {
            if (_name != null) return _name;
            var lastSegment = Segments.LastOrDefault();
            if (string.IsNullOrEmpty(lastSegment))
                _name = string.Empty;
            else if (lastSegment == "/")
                _name = "/";
            else
                _name = lastSegment.TrimEnd('/');
            _name = UnescapeDataString(_name);
            return _name;
        }
    }

    #region Constructors
    
    /// <summary>
    /// 当isFolder为true时，如果uriString不以'/'结尾，则会自动添加'/'。<br/>
    /// </summary>
    /// <param name="uriString"></param>
    /// <param name="isFolder"></param>
    public ExUri(string uriString, bool isFolder = true) : base(!uriString.EndsWith('/') && isFolder
        ? uriString + '/' : uriString) { }
    
    private ExUri(Uri baseUri, string? relativeUri) : base(baseUri, relativeUri) { }
    #endregion

    public ExUri Parent => new(this, "..");
    
    public bool IsRoot => Depth == 0;

    public ExUri Lca(ExUri other)
    {
        ExUri a = this;
        ExUri b = other;
        while (a.Depth > b.Depth) a = a.Parent;
        while (b.Depth > a.Depth) b = b.Parent;
        while (a != b)
        {
            a = a.Parent;
            b = b.Parent;
        }
        return a;
    }
    
    public bool IsAncestorOf(ExUri other) => Lca(other) == this;

    #region CMP
    
    public override int GetHashCode() => base.GetHashCode();
    
    public override bool Equals(object? obj) => Equals(obj as ExUri);
    public bool Equals(ExUri? other)
    {
        if (other is null) return false;
        if (ReferenceEquals(this, other)) return true;
        return base.Equals(other);
    }
    
    public static bool operator ==(ExUri? left, ExUri? right)
    {
        if (left is null) return right is null;
        return left.Equals(right);
    }

    public static bool operator !=(ExUri? left, ExUri? right) => !(left == right);

    #endregion
}
