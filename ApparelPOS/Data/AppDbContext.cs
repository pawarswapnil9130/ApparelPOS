using System.IO;
using ApparelPOS.Models;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace ApparelPOS.Data;

public class AppDbContext : DbContext
{
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<ProductVariant> ProductVariants => Set<ProductVariant>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceItem> InvoiceItems => Set<InvoiceItem>();
    public DbSet<StoreSettings> StoreSettings => Set<StoreSettings>();

    public static string DatabasePath
    {
        get
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var dir = Path.Combine(appData, "ApparelPOS");
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return Path.Combine(dir, "apparel_pos.db");
        }
    }

    public AppDbContext()
    {
    }

    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        if (!optionsBuilder.IsConfigured)
        {
            var dbPath = DatabasePath;
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                DefaultTimeout = 5
            }.ToString();

            optionsBuilder.UseSqlite(connectionString);
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Product configurations
        modelBuilder.Entity<Product>(entity =>
        {
            entity.HasKey(p => p.Id);
            entity.HasIndex(p => p.Sku);
            entity.HasIndex(p => p.Barcode);
            entity.Property(p => p.CostPrice).HasPrecision(18, 2);
            entity.Property(p => p.SellingPrice).HasPrecision(18, 2);
            entity.Property(p => p.TaxRate).HasPrecision(5, 2);
        });

        // ProductVariant configurations
        modelBuilder.Entity<ProductVariant>(entity =>
        {
            entity.HasKey(v => v.Id);
            entity.HasIndex(v => v.BarcodeVariant);
            entity.HasIndex(v => v.SkuVariant);
            entity.Property(v => v.AdditionalPrice).HasPrecision(18, 2);

            entity.HasOne(v => v.Product)
                  .WithMany(p => p.Variants)
                  .HasForeignKey(v => v.ProductId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Invoice configurations
        modelBuilder.Entity<Invoice>(entity =>
        {
            entity.HasKey(i => i.Id);
            entity.HasIndex(i => i.InvoiceNumber).IsUnique();
            entity.HasIndex(i => i.InvoiceDate);
            entity.Property(i => i.SubTotal).HasPrecision(18, 2);
            entity.Property(i => i.DiscountAmount).HasPrecision(18, 2);
            entity.Property(i => i.TaxAmount).HasPrecision(18, 2);
            entity.Property(i => i.CgstAmount).HasPrecision(18, 2);
            entity.Property(i => i.SgstAmount).HasPrecision(18, 2);
            entity.Property(i => i.TotalAmount).HasPrecision(18, 2);
            entity.Property(i => i.CashReceived).HasPrecision(18, 2);
            entity.Property(i => i.ChangeDue).HasPrecision(18, 2);
        });

        // InvoiceItem configurations
        modelBuilder.Entity<InvoiceItem>(entity =>
        {
            entity.HasKey(it => it.Id);
            entity.Property(it => it.UnitPrice).HasPrecision(18, 2);
            entity.Property(it => it.CostPrice).HasPrecision(18, 2);
            entity.Property(it => it.TaxAmount).HasPrecision(18, 2);
            entity.Property(it => it.TotalPrice).HasPrecision(18, 2);

            entity.HasOne(it => it.Invoice)
                  .WithMany(i => i.Items)
                  .HasForeignKey(it => it.InvoiceId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // StoreSettings
        modelBuilder.Entity<StoreSettings>(entity =>
        {
            entity.HasKey(s => s.Id);
        });
    }

    /// <summary>
    /// Configures SQLite WAL (Write-Ahead Logging) and performance pragmas
    /// </summary>
    public void EnableWalMode()
    {
        try
        {
            Database.ExecuteSqlRaw("PRAGMA journal_mode = WAL;");
            Database.ExecuteSqlRaw("PRAGMA synchronous = NORMAL;");
            Database.ExecuteSqlRaw("PRAGMA busy_timeout = 5000;");
            Database.ExecuteSqlRaw("PRAGMA temp_store = MEMORY;");
        }
        catch
        {
            // Fail-safe if pragmas are restricted
        }
    }
}
