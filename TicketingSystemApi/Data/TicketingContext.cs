using Microsoft.EntityFrameworkCore;

namespace TicketingSystemApi.Data
{
    public enum Status
    {
        OPEN,
        ON_PROGRESS,
        DONE
    }

    public class TicketingContext(DbContextOptions<TicketingContext> options) : DbContext(options)
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Ticket> Tickets { get; set; }
        public DbSet<WorkOrder> WorkOrders { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            var adminUsername = "admin";
            var adminPassword = "admin123";

            var adminUser = new User
            {
                Id = 1,
                Username = adminUsername,
                Password = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(adminPassword))
            };

            modelBuilder.Entity<User>().HasData(adminUser);

            base.OnModelCreating(modelBuilder);
        }

    }

    public class User
    {
        public int Id { get; set; }
        public required string Username { get; set; }
        public required string Password { get; set; }
    }

    public class Ticket
    {
        public int Id { get; set; }
        public required string Title { get; set; }
        public required string Description { get; set; }
        public Status Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class WorkOrder
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public required string AssignedTo { get; set; }
        public required string Details { get; set; }
        public Status Status { get; set; }
        public DateTime CreatedAt { get; set; }

        public bool Internal { get; set; }
        public bool NotificationSent { get; set; }

        public Ticket Ticket { get; set; }
    }
}
