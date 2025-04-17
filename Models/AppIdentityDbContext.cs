using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography.X509Certificates;

namespace GoogleLogin.Models
{
    public class AppIdentityDbContext : IdentityDbContext<AppUser>
    {
        public AppIdentityDbContext(DbContextOptions<AppIdentityDbContext> options) : base(options) { }
        public DbSet<AppUser> AspNetUsers { get; set; }
        public DbSet<TbEmail> TbEmails { get; set; }
        public DbSet<TbOrder> TbOrders{ get; set; }
        public DbSet<TbShopifyLog> TbLogs{ get; set; }
        public DbSet<TbShopifyToken> TbTokens{ get; set; }
        public DbSet<TbShopifyUser> TbShopifyUsers{ get; set; }        
        public DbSet<TbSms> TbSmss { get; set; }
        public DbSet<TbMailAccount> TbMailAccount { get; set; }
        public DbSet<TbTwilio> TbTwilios { get; set; }
        public DbSet<TbCompany> TbCompanies { get; set; }
        public DbSet<TbMember> TbMembers { get; set; }
        public DbSet<TbPlan> TbPlans { get; set; }
        public DbSet<TbUserPlan> TbUserPlans { get; set; }
    }
}
