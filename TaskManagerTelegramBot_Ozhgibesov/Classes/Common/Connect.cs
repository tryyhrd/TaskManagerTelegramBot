using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace TaskManagerTelegramBot_Ozhgibesov.Classes.Common
{
    public class Connect: DbContext
    {
        public DbSet<Users> Users { get; set; }
        public DbSet<Events> Events { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            //optionsBuilder.UseSqlServer(
            //    "Server=10.0.201.112;" +
            //    "Database=base1_ISP_22_4_12;" +
            //    "Integrated Security=false;" +
            //    "User=ISP_22_4_12;" +
            //    "Pwd=7m4tIyDMeybp_;" +
            //    "MultipleActiveResultSets=true;");
            optionsBuilder.UseSqlServer(
                "Server=DESKTOP-E07VVT6\\SQLEXPRESS;" +
                "Database=TaskManager;" +
                "Integrated Security=true;" +
                "User=;" +
                "Pwd=" +
                "MultipleActiveResultSets=true;");
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Users>()
                        .HasKey(u => u.Id);

            modelBuilder.Entity<Events>()
                        .Property(e => e.RecurringDays)
                        .HasConversion(
                        v => JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
                        v => JsonSerializer.Deserialize<List<DayOfWeek>>(v, (JsonSerializerOptions?)null)
                        ?? new List<DayOfWeek>()
        );

            modelBuilder.Entity<Events>()
                .HasOne(e => e.User)
                .WithMany(u => u.Events)
                .HasForeignKey(e => e.UserId)
                .HasPrincipalKey(u => u.IdUser);
        }
    }
}
