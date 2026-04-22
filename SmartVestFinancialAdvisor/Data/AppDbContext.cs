using Microsoft.EntityFrameworkCore;
using SmartVestFinancialAdvisor.Components.Models;

namespace SmartVestFinancialAdvisor.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }

        public DbSet<SurveySubmission> SurveySubmissions { get; set; }

        public DbSet<SurveyResult> SurveyResults { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>()
                .HasMany(u => u.SurveySubmissions)
                .WithOne(s => s.User)
                .HasForeignKey(s => s.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            modelBuilder.Entity<User>()
                .HasMany(u => u.SurveyResults)
                .WithOne(r => r.User)
                .HasForeignKey(r => r.UserId)
                .OnDelete(DeleteBehavior.Restrict);


            modelBuilder.Entity<SurveyResult>()
                .HasOne(r => r.SurveySubmission)
                .WithMany()
                .HasForeignKey(r => r.SurveySubmissionId)
                .OnDelete(DeleteBehavior.Cascade); 

            modelBuilder.Entity<SurveySubmission>()
                .HasIndex(s => s.UserId)
                .HasDatabaseName("IX_SurveySubmissions_UserId");

            modelBuilder.Entity<SurveySubmission>()
                .HasIndex(s => new { s.UserId, s.SubmittedAt })
                .HasDatabaseName("IX_SurveySubmissions_UserId_SubmittedAt")
                .IsDescending(false, true); 

            modelBuilder.Entity<SurveyResult>()
                .HasIndex(r => r.UserId)
                .HasDatabaseName("IX_SurveyResults_UserId");

            modelBuilder.Entity<SurveyResult>()
                .HasIndex(r => new { r.UserId, r.ComputedAt })
                .HasDatabaseName("IX_SurveyResults_UserId_ComputedAt")
                .IsDescending(false, true); 
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique()
                .HasDatabaseName("IX_Users_Email_Unique");
        }
    }
}
