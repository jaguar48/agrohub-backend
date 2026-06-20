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
    ///   - InProgress sessions past duration + 2h grace   → flag as OverdueReview + notify
    ///   - Soft reminders for stuck-but-not-failed states → nudge notifications only
    ///
    /// Runs every 5 minutes. Register with:
    ///   builder.Services.AddHostedService&lt;ExpirySweepService&gt;();
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
            _logger.LogInformation("[ExpirySweep] Service started — sweeping every {Interval}.", Interval);

            using var timer = new PeriodicTimer(Interval);
            do
            {
                await RunSweepAsync(stoppingToken);
            }
            while (!stoppingToken.IsCancellationRequested
                   && await timer.WaitForNextTickAsync(stoppingToken));

            _logger.LogInformation("[ExpirySweep] Service stopping.");
        }

        private async Task RunSweepAsync(CancellationToken ct)
        {
            _logger.LogDebug("[ExpirySweep] Sweep cycle starting at {Time} UTC.", DateTime.UtcNow);

            using var scope = _scopeFactory.CreateScope();
            var svc = scope.ServiceProvider.GetRequiredService<IConsultationService>();

            await Run(svc.ProcessExpiredNoShowsAsync, "ProcessExpiredNoShows");
            await Run(svc.ProcessExpiredCustomerNoShowsAsync, "ProcessExpiredCustomerNoShows");
            await Run(svc.ProcessExpiredApprovalsAsync, "ProcessExpiredApprovals");
            await Run(svc.ProcessOverdueInProgressSessionsAsync, "ProcessOverdueInProgressSessions");
            await Run(svc.ProcessReminderNotificationsAsync, "ProcessReminderNotifications");

            _logger.LogDebug("[ExpirySweep] Sweep cycle complete.");
        }

        private async Task Run(Func<Task> fn, string name)
        {
            try
            {
                _logger.LogDebug("[ExpirySweep] Running {Name}…", name);
                await fn();
                _logger.LogDebug("[ExpirySweep] {Name} completed.", name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[ExpirySweep] {Name} failed.", name);
            }
        }
    }
}