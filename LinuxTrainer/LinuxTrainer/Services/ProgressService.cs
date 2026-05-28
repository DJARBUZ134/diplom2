using LinuxTrainer.Data;
using LinuxTrainer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinuxTrainer.Services;

public record QuestRow(
    int QuestId,
    string Name,
    string Description,
    string Difficulty,
    string Status,
    double Rating,
    bool HasQuestion,
    string? LastAnswer,
    string? ReviewComment);

public record QuestProgressDetails(
    int Id,
    string Name,
    string Description,
    string Question,
    int Order,
    string Status,
    string? LastAnswer,
    string? ReviewComment);

public record PendingReview(
    int ProgressId,
    int UserId,
    string UserDisplay,
    int QuestId,
    string QuestName,
    string Question,
    string ExpectedAnswer,
    string LastAnswer);

public interface IProgressService
{
    Task<List<QuestRow>> ForUserAsync(int userId);
    Task<QuestProgressDetails?> ForUserQuestAsync(int userId, int questId);
    Task<List<Quest>> AllQuestsAsync();
    Task<Quest?> GetAsync(int questId);
    Task<Quest> CreateAsync(string name, string description, string question, string expectedAnswer, string difficulty, int authorId);
    Task<bool> UpdateAsync(int questId, string name, string description, string question, string expectedAnswer, string difficulty);
    Task<bool> DeleteAsync(int questId);
    Task SetStatusAsync(int userId, int questId, string status);
    Task<string> SubmitAnswerAsync(int userId, int questId, string answer);
    Task<List<PendingReview>> ListPendingAsync();
    Task<bool> ApproveAsync(int progressId, double rating);
    Task<bool> RejectAsync(int progressId, string comment);
}

public class ProgressService : IProgressService
{
    private readonly AppDbContext _db;
    public ProgressService(AppDbContext db) => _db = db;

    public async Task<List<QuestRow>> ForUserAsync(int userId)
    {
        var quests = await _db.Quests.OrderBy(q => q.Order).ToListAsync();
        var progress = await _db.Progress.Where(p => p.UserId == userId).ToListAsync();

        var rows = new List<QuestRow>();
        var prevCompleted = true;
        foreach (var q in quests)
        {
            var p = progress.FirstOrDefault(x => x.QuestId == q.Id);
            var status = p?.Status ?? QuestStatus.NotStarted;

            if (!prevCompleted &&
                status != QuestStatus.Completed &&
                status != QuestStatus.PendingReview)
            {
                status = QuestStatus.Locked;
            }

            rows.Add(new QuestRow(
                q.Id, q.Name, q.Description, q.Difficulty, status,
                p?.Rating ?? 0,
                !string.IsNullOrWhiteSpace(q.Question),
                p?.LastAnswer,
                p?.ReviewComment));

            prevCompleted = status == QuestStatus.Completed;
        }
        return rows;
    }

    public async Task<QuestProgressDetails?> ForUserQuestAsync(int userId, int questId)
    {
        var quest = await _db.Quests.FindAsync(questId);
        if (quest is null) return null;

        var rows = await ForUserAsync(userId);
        var row = rows.FirstOrDefault(r => r.QuestId == questId);
        var status = row?.Status ?? QuestStatus.NotStarted;

        return new QuestProgressDetails(
            quest.Id, quest.Name, quest.Description, quest.Question, quest.Order,
            status, row?.LastAnswer, row?.ReviewComment);
    }

    public Task<List<Quest>> AllQuestsAsync() =>
        _db.Quests.OrderBy(q => q.Order).ToListAsync();

    public Task<Quest?> GetAsync(int questId) =>
        _db.Quests.FirstOrDefaultAsync(q => q.Id == questId);

    public async Task<Quest> CreateAsync(string name, string description, string question, string expectedAnswer, string difficulty, int authorId)
    {
        var maxOrder = await _db.Quests.AnyAsync()
            ? await _db.Quests.MaxAsync(q => q.Order)
            : 0;
        var quest = new Quest
        {
            Name = name,
            Description = description,
            Question = question,
            ExpectedAnswer = expectedAnswer,
            Difficulty = string.IsNullOrWhiteSpace(difficulty) ? "Средняя" : difficulty,
            Order = maxOrder + 1,
            AuthorId = authorId
        };
        _db.Quests.Add(quest);
        await _db.SaveChangesAsync();
        return quest;
    }

    public async Task<bool> UpdateAsync(int questId, string name, string description, string question, string expectedAnswer, string difficulty)
    {
        var quest = await _db.Quests.FindAsync(questId);
        if (quest is null) return false;
        quest.Name = name;
        quest.Description = description;
        quest.Question = question;
        quest.ExpectedAnswer = expectedAnswer;
        quest.Difficulty = string.IsNullOrWhiteSpace(difficulty) ? quest.Difficulty : difficulty;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteAsync(int questId)
    {
        var quest = await _db.Quests.FindAsync(questId);
        if (quest is null) return false;
        var progress = _db.Progress.Where(p => p.QuestId == questId);
        _db.Progress.RemoveRange(progress);
        var states = _db.QuestTerminalStates.Where(s => s.QuestId == questId);
        _db.QuestTerminalStates.RemoveRange(states);
        _db.Quests.Remove(quest);
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task SetStatusAsync(int userId, int questId, string status)
    {
        var p = await _db.Progress.FirstOrDefaultAsync(x => x.UserId == userId && x.QuestId == questId);
        if (p is null)
        {
            p = new UserQuestProgress { UserId = userId, QuestId = questId, Status = status };
            _db.Progress.Add(p);
        }
        else
        {
            p.Status = status;
        }
        await _db.SaveChangesAsync();
    }

    public async Task<string> SubmitAnswerAsync(int userId, int questId, string answer)
    {
        var quest = await _db.Quests.FindAsync(questId);
        if (quest is null) return "Задание не найдено";

        var rows = await ForUserAsync(userId);
        var row = rows.FirstOrDefault(r => r.QuestId == questId);
        if (row?.Status == QuestStatus.Locked) return "Задание ещё закрыто";
        if (row?.Status == QuestStatus.Completed) return "Задание уже выполнено";

        var trimmed = (answer ?? "").Trim();
        var expected = (quest.ExpectedAnswer ?? "").Trim();
        var matches = !string.IsNullOrEmpty(expected) &&
                      string.Equals(trimmed, expected, StringComparison.OrdinalIgnoreCase);

        var progress = await _db.Progress.FirstOrDefaultAsync(p => p.UserId == userId && p.QuestId == questId);
        if (progress is null)
        {
            progress = new UserQuestProgress { UserId = userId, QuestId = questId };
            _db.Progress.Add(progress);
        }

        progress.LastAnswer = trimmed;
        progress.ReviewComment = null;

        if (!matches)
        {
            progress.Status = QuestStatus.InProgress;
            await _db.SaveChangesAsync();
            return "Ответ неверный, попробуйте ещё раз";
        }

        progress.Status = QuestStatus.PendingReview;
        await _db.SaveChangesAsync();
        return "Ответ принят и отправлен преподавателю на проверку";
    }

    public async Task<List<PendingReview>> ListPendingAsync()
    {
        var pending = await _db.Progress
            .Where(p => p.Status == QuestStatus.PendingReview)
            .Join(_db.Users, p => p.UserId, u => u.Id, (p, u) => new { p, u })
            .Join(_db.Quests, x => x.p.QuestId, q => q.Id, (x, q) => new { x.p, x.u, q })
            .ToListAsync();

        return pending.Select(x => new PendingReview(
            x.p.Id, x.u.Id,
            string.IsNullOrWhiteSpace(x.u.DisplayName) ? x.u.Login : x.u.DisplayName,
            x.q.Id, x.q.Name, x.q.Question, x.q.ExpectedAnswer,
            x.p.LastAnswer ?? "")).ToList();
    }

    public async Task<bool> ApproveAsync(int progressId, double rating)
    {
        var p = await _db.Progress.FindAsync(progressId);
        if (p is null) return false;
        p.Status = QuestStatus.Completed;
        p.Rating = Math.Clamp(rating, 0, 5);
        p.ReviewComment = null;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> RejectAsync(int progressId, string comment)
    {
        var p = await _db.Progress.FindAsync(progressId);
        if (p is null) return false;
        p.Status = QuestStatus.InProgress;
        p.ReviewComment = comment;
        await _db.SaveChangesAsync();
        return true;
    }
}
