using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jobbr.Execution.InProcess.Execution
{
    public interface IDateTimeProvider
    {
        DateTime GetUtcNow();
    }
}
