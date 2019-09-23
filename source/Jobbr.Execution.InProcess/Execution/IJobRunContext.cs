using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jobbr.Execution.InProcess.Execution
{
    internal interface IJobRunContext
    {
        long JobRunId { get; }

        event EventHandler<JobRunEndedEventArgs> Ended;

        void Start();
    }
}
