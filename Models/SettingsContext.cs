using Microsoft.EntityFrameworkCore;

namespace Coflnet.Sky.Settings.Models
{
    /// <summary>
    /// <see cref="DbContext"/> Settings saving
    /// </summary>
    public class SettingsDbContext : DbContext
    {
        /// <summary>
        /// Users 
        /// </summary>
        /// <value></value>
        public DbSet<User> Users { get; set; }
        /// <summary>
        /// Settings table representation
        /// </summary>
        /// <value></value>
        public DbSet<Setting> Settings { get; set; }

        /// <summary>
        /// Creates a new instance of <see cref="SettingsDbContext"/>
        /// </summary>
        /// <param name="options"></param>
        public SettingsDbContext(DbContextOptions<SettingsDbContext> options)
        : base(options)
        {
        }

        /// <summary>
        /// Configures additional relations and indexes
        /// </summary>
        /// <param name="modelBuilder"></param>
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasIndex(e => e.ExternalId);
            });
        }
    }
}