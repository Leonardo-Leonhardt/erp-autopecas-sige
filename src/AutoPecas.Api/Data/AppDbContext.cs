using AutoPecas.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace AutoPecas.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Peca> Pecas => Set<Peca>();
    public DbSet<Fornecedor> Fornecedores => Set<Fornecedor>();
    public DbSet<Cliente> Clientes => Set<Cliente>();
    public DbSet<Venda> Vendas => Set<Venda>();
    public DbSet<ItemVenda> ItensVenda => Set<ItemVenda>();
    public DbSet<PedidoCompra> PedidosCompra => Set<PedidoCompra>();
    public DbSet<ItemCompra> ItensCompra => Set<ItemCompra>();
    public DbSet<Movimentacao> Movimentacoes => Set<Movimentacao>();

    protected override void ConfigureConventions(ModelConfigurationBuilder builder)
    {
        builder.Properties<decimal>().HavePrecision(12, 2);
        builder.Properties<string>().HaveMaxLength(200);
    }

    protected override void OnModelCreating(ModelBuilder b)
    {
        b.Entity<Peca>(e =>
        {
            e.HasIndex(p => p.Codigo).IsUnique();
            e.Property(p => p.Codigo).HasMaxLength(30);
            e.Property(p => p.Saldo);
            e.Property(p => p.Reservado);
            e.Property(p => p.CustoMedio);
            e.Property(p => p.UltimaVenda);
            e.Property(p => p.Versao).IsConcurrencyToken();
            e.Ignore(p => p.Disponivel);
        });

        b.Entity<Venda>(e =>
        {
            e.Property(v => v.Status).HasConversion<string>().HasMaxLength(20);
            e.HasMany(v => v.Itens).WithOne(i => i.Venda).HasForeignKey(i => i.VendaId);
            e.HasIndex(v => v.FinalizadaEm);
        });

        b.Entity<PedidoCompra>(e =>
        {
            e.Property(p => p.Status).HasConversion<string>().HasMaxLength(20);
            e.HasMany(p => p.Itens).WithOne(i => i.PedidoCompra).HasForeignKey(i => i.PedidoCompraId);
        });

        b.Entity<Movimentacao>(e =>
        {
            e.Property(m => m.Tipo).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(m => new { m.PecaId, m.Data });
            e.HasIndex(m => m.Data);
        });
    }
}
