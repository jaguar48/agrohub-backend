using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Threading.Tasks;

namespace AgricHub.Contracts
{
    public interface IUnitOfWork : IDisposable
    {
        IRepository<TEntity> GetRepository<TEntity>() where TEntity : class;
        int SaveChanges();
        Task<int> SaveChangesAsync();

        // Transaction management
        Task<IDbContextTransaction> BeginTransactionAsync();
        Task CommitTransactionAsync();
        Task RollbackTransactionAsync();
    }
}
