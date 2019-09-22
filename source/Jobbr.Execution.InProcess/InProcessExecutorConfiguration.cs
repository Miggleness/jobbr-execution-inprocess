using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jobbr.Execution.InProcess
{
    public class InProcessExecutorConfiguration
    {
        public int MaxConcurrentProcesses { get; set; } = 4;
    }
}
