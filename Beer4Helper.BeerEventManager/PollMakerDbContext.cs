using Beer4Helper.BeerEventManager.Models;
using Microsoft.EntityFrameworkCore;

namespace Beer4Helper.BeerEventManager;

public class PollMakerDbContext(DbContextOptions<PollMakerDbContext> options) : DbContext(options)
{
    public DbSet<Poll> Polls { get; set; }
    public DbSet<PollOption> PollOptions { get; set; }
    public DbSet<UserVote> UserVotes { get; set; }
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<Poll>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.Property(p => p.ChatId).IsRequired();
            entity.Property(p => p.MessageId).IsRequired();
            
            entity.HasMany(p => p.Options)
                  .WithOne(o => o.Poll)
                  .HasForeignKey(o => o.PollId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<PollOption>(entity =>
        {
            entity.HasKey(o => o.Id);
            entity.Property(o => o.PollId).IsRequired();
            
            entity.HasOne(o => o.Poll)
                  .WithMany(p => p.Options)
                  .HasForeignKey(o => o.PollId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasMany(o => o.UserVotes)
                  .WithOne(uv => uv.PollOption)
                  .HasForeignKey(uv => uv.PollOptionId)
                  .OnDelete(DeleteBehavior.Cascade);
        });
        
        modelBuilder.Entity<UserVote>(entity =>
        {
            entity.HasKey(uv => uv.Id);
            entity.Property(uv => uv.PollId).IsRequired();
            entity.Property(uv => uv.UserId).IsRequired();
            entity.Property(uv => uv.PollOptionId).IsRequired();
            
            entity.HasOne(uv => uv.Poll)
                  .WithMany()
                  .HasForeignKey(uv => uv.PollId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasOne(uv => uv.PollOption)
                  .WithMany(o => o.UserVotes)
                  .HasForeignKey(uv => uv.PollOptionId)
                  .OnDelete(DeleteBehavior.Cascade);
            
            entity.HasIndex(uv => new { uv.PollId, uv.UserId })
                  .IsUnique();
        });
    }
}