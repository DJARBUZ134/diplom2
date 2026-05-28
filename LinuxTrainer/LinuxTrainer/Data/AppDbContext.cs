using LinuxTrainer.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace LinuxTrainer.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Quest> Quests => Set<Quest>();
    public DbSet<UserQuestProgress> Progress => Set<UserQuestProgress>();
    public DbSet<TerminalState> TerminalStates => Set<TerminalState>();
    public DbSet<QuestTerminalState> QuestTerminalStates => Set<QuestTerminalState>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => u.Login).IsUnique();
        b.Entity<TerminalState>().HasIndex(t => t.UserId).IsUnique();
        b.Entity<UserQuestProgress>().HasIndex(p => new { p.UserId, p.QuestId }).IsUnique();
        b.Entity<QuestTerminalState>().HasIndex(t => new { t.UserId, t.QuestId }).IsUnique();
        b.Entity<Quest>()
            .HasOne(q => q.Author)
            .WithMany()
            .HasForeignKey(q => q.AuthorId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
