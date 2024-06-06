using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

namespace WhatsappChatBot.Models;

public partial class WhatsAppDbContext : DbContext
{
    private readonly IConfiguration _configuration;
    public WhatsAppDbContext(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public WhatsAppDbContext(DbContextOptions<WhatsAppDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<Employee> Employees { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.UseSqlServer(_configuration.GetConnectionString("WhatsAppDB"));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Employee>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__Employee__3213E83F6093E586");

            entity.ToTable("Employee");

            entity.Property(e => e.Id)
                .ValueGeneratedNever()
                .HasColumnName("id");
            entity.Property(e => e.DepartmentName)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("department_name");
            entity.Property(e => e.Name)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasColumnName("name");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
