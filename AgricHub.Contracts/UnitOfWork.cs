using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AgricHub.Contracts
{
    public class UnitOfWork<TContext> : IUnitOfWork where TContext : DbContext
    {
        private Dictionary<Type, object> _repositories;
        private readonly TContext _context;
        private IDbContextTransaction _transaction;

        public UnitOfWork(TContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        public IRepository<TEntity> GetRepository<TEntity>() where TEntity : class
        {
            _repositories ??= new Dictionary<Type, object>();

            var type = typeof(TEntity);
            if (!_repositories.ContainsKey(type))
                _repositories[type] = new Repository<TEntity>(_context);

            return (IRepository<TEntity>)_repositories[type];
        }

        public int SaveChanges() => _context.SaveChanges();
        public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

        // --- Transaction Handling ---
        public async Task<IDbContextTransaction> BeginTransactionAsync()
        {
            if (_transaction == null)
                _transaction = await _context.Database.BeginTransactionAsync();
            return _transaction;
        }

        public async Task CommitTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.CommitAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        public void Dispose()
        {
            _transaction?.Dispose();
            _context?.Dispose();
        }
    }
}
