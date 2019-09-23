using Jobbr.ComponentModel.Execution.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jobbr.Execution.InProcess.Execution
{
    internal interface IJobRunContextFactory
    {
        IJobRunContext CreateJobRunContext(JobRunInfo jobRunInfo);
    }
}
