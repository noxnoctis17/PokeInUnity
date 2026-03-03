
public class CustomLogSession
{
    private System.Text.StringBuilder _sb = new();

    public void Add( string line )
    {
        _sb.AppendLine( line );
    }

    public override string ToString()
    {
        return _sb.ToString();
    }

    public void Clear()
    {
        _sb.Clear();
    }
}
