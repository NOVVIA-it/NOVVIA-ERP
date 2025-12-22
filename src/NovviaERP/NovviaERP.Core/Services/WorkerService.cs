using System;
using System.Threading.Tasks;
using NovviaERP.Core.Data;
using Serilog;

namespace NovviaERP.Core.Services
{
    /// <summary>
    /// Hintergrund-Worker für automatisierte Aufgaben (STUB - TODO: Implementierung)
    /// </summary>
    public class WorkerService : IDisposable
    {
        private static readonly ILogger _log = Log.ForContext<WorkerService>();

        public WorkerService(JtlDbContext db, WorkflowService workflow, PaymentService payment,
            WooCommerceService wooCommerce, SteuerService steuer)
        {
            _log.Information("WorkerService initialisiert (Stub-Modus)");
        }

        public void Dispose() { }
        public void StartAll() => _log.Information("WorkerService.StartAll (Stub)");
        public void StopAll() => _log.Information("WorkerService.StopAll (Stub)");
    }
}
