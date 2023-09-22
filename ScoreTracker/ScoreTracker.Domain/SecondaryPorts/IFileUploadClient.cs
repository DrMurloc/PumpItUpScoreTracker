using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScoreTracker.Domain.SecondaryPorts
{
    public interface IFileUploadClient
    {
        Task<Uri> UploadFile(string path, Stream fileStream, CancellationToken cancellationToken = default);
    }
}