using Microsoft.EntityFrameworkCore;
using SmartVestFinancialAdvisor.Components.Models;

namespace SmartVestFinancialAdvisor.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
    }
}
