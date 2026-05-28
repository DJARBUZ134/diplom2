using System.ComponentModel.DataAnnotations;

namespace LinuxTrainer.Data.Entities;

public static class QuestStatus
{
    public const string Locked = "Заблокировано";
    public const string NotStarted = "Не начато";
    public const string InProgress = "В процессе";
    public const string PendingReview = "На проверке";
    public const string Completed = "Завершено";
    public const string Rejected = "Отклонено";
}

public class UserQuestProgress
{
    public int Id { get; set; }

    public int UserId { get; set; }
    public User? User { get; set; }

    public int QuestId { get; set; }
    public Quest? Quest { get; set; }

    [MaxLength(32)]
    public string Status { get; set; } = QuestStatus.NotStarted;

    public double Rating { get; set; }

    [MaxLength(512)]
    public string? LastAnswer { get; set; }

    [MaxLength(512)]
    public string? ReviewComment { get; set; }
}
