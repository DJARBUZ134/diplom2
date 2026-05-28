using System.ComponentModel.DataAnnotations;

namespace LinuxTrainer.Data.Entities;

public class Quest
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = "";

    [MaxLength(2048)]
    public string Description { get; set; } = "";

    [MaxLength(32)]
    public string Difficulty { get; set; } = "";

    public int Order { get; set; }

    [MaxLength(1024)]
    public string Question { get; set; } = "";

    [MaxLength(512)]
    public string ExpectedAnswer { get; set; } = "";

    public string InitialFileSystemJson { get; set; } = "";

    public int? AuthorId { get; set; }
    public User? Author { get; set; }
}
