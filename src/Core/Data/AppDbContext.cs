﻿using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Core.Data
{
    public class AppDbContext : IdentityDbContext<AppUser>
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
        {
        }

        public DbSet<BlogPost> BlogPosts { get; set; }
        public DbSet<Author> Authors { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<CustomField> CustomFields { get; set; }
        public DbSet<HtmlWidget> HtmlWidgets { get; set; }
        public DbSet<Newsletter> Newsletters { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                AppSettings.DbOptions(optionsBuilder);
            }
        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.HasDefaultSchema("help");
            builder.Entity<BlogPost>(entity => 
            {
                entity.Property(r => r.DisplayOrder).HasDefaultValue(0);
            });
            base.OnModelCreating(builder);
        }
    }
}