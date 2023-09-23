using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IAdminNotificationClient
    {
        Task NotifyAdmin(string message, CancellationToken cancellationToken);
    }
}