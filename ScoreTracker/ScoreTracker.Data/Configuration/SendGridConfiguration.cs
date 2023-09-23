using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScoreTracker.Data.Configuration
{
    public sealed class SendGridConfiguration
    {
        public string ApiKey { get; set; }
        public string ToEmail { get; set; }
        public string FromEmail { get; set; }
    }
}