using Microsoft.EntityFrameworkCore;
using Beer4Helper.ReactionCounter.Models;

namespace Beer4Helper.ReactionCounter.Data
{
    public class ReactionDbContext(DbContextOptions<ReactionDbContext> options) : DbContext(options)
    {
        public DbSet<Reaction> Reactions { get; set; }
        public DbSet<UserStats> UserStats { get; set; }
        public DbSet<PhotoMessage> PhotoMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserStats>().Property(u => u.Id)
                .ValueGeneratedNever();
            modelBuilder.Entity<Reaction>()
                .HasIndex(r => new { r.ChatId, r.MessageId, r.UserId })
                .IsUnique(false); 
            
            base.OnModelCreating(modelBuilder);
        }
    }
}