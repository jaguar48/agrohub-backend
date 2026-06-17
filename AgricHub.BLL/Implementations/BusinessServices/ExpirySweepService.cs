using AgricHub.BLL.Interfaces.IBusinessServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AgricHub.BLL.Implementations.BusinessServices
{
    /// <summary>
    /// Periodically sweeps the database for time-based consultation transitions:
    ///   - Consultant no-show grace periods expired       → full refund to customer
    ///   - Customer no-show grace periods expired         → 50/50 split
    ///   - Pending-approval windows (72h) expired         → auto-release escrow to consultant
    ///   - InProgress sessions past duration + 2h grace   → flag as OverdueReview + notify consultant
    ///
    /// Runs every 5 minutes inside this process for its entire lifetime.
    /// Register with: builder.Services.AddHostedService&lt;ExpirySweepService&gt;();
    /// </summary>
    public class ExpirySweepService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ExpirySweepService> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        public ExpirySweepService(IServiceScopeFactory scopeFactory, ILogger<ExpirySweepService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger       = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ExpirySweep] Started — running every {Interval}.", Interval);
            using var timer = new PeriodicTimer(Interval);

            // Run once immediately on startup, then on each tick.
            do
            {
                await RunSweepAsync(stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested
                   && await timer.WaitForNextTickAsync(stoppingToken));
        }

        private async Task RunSweepAsync(CancellationToken ct)
        {
            // Each iteration gets its own scope — IConsultationService and its
            // dependencies (DbContext, IUnitOfWork, etc.) are scoped services,
            // and BackgroundService runs outside the normal request scope.
            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IConsultationService>();

            try
            {
                await svc.ProcessExpiredNoShowsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpirySweep] ProcessExpiredNoShowsAsync failed.");
            }

            try
            {
                await svc.ProcessExpiredCustomerNoShowsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpirySweep] ProcessExpiredCustomerNoShowsAsync failed.");
            }

            try
            {
                await svc.ProcessExpiredApprovalsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpirySweep] ProcessExpiredApprovalsAsync failed.");
            }

            try
            {
                await svc.ProcessOverdueInProgressSessionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpirySweep] ProcessOverdueInProgressSessionsAsync failed.");
            }
        }
    }
}