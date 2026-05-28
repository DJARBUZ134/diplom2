namespace LinuxTrainer.Data.Entities;

public class TerminalState
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public string Cwd { get; set; } = "/home";

    public string HistoryJson { get; set; } = "[]";

    public string FileSystemJson { get; set; } = "";
}
