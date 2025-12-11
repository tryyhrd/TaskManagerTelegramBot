using Microsoft.EntityFrameworkCore;

namespace TaskManagerTelegramBot_Ozhgibesov.Classes.Common
{
    public class Connect: DbContext
    {
        public DbSet<Users> Users { get; set; }
        public DbSet<Events> Events { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer(
                "Server=10.0.201.112;" +
                "Database=base1_ISP_22_4_12;" +
                "Integrated Security=false;" +
                "User=ISP_22_4_12;" +
                "Pwd=7m4tIyDMeybp_;" +
                "MultipleActiveResultSets=true;");
        }
    }
}
