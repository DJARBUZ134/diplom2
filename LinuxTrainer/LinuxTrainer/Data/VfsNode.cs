using System.Text.Json.Serialization;

namespace LinuxTrainer.Data;

public class VfsNode
{
    public string Name { get; set; } = "";
    public bool IsDir { get; set; }
    public string? Content { get; set; }
    public List<VfsNode> Children { get; set; } = new();

    [JsonIgnore]
    public VfsNode? Parent { get; set; }

    public VfsNode? Find(string name) =>
        Children.FirstOrDefault(c => string.Equals(c.Name, name, StringComparison.Ordinal));

    public static VfsNode Dir(string name, params VfsNode[] children)
    {
        var d = new VfsNode { Name = name, IsDir = true };
        foreach (var c in children) d.Children.Add(c);
        return d;
    }

    public static VfsNode File(string name, string content) =>
        new() { Name = name, IsDir = false, Content = content };
}
