using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Jobbr.ComponentModel.Execution;
using Jobbr.ComponentModel.Execution.Model;

namespace Jobbr.Execution.InProcess.Execution
{
    internal class JobRunContextFactory : IJobRunContextFactory
    {
        private readonly IJobRunProgressChannel progressChannel;

        public JobRunContextFactory(IJobRunProgressChannel progressChannel)
        {
            this.progressChannel = progressChannel;
        }

        public IJobRunContext CreateJobRunContext(JobRunInfo jobRunInfo)
        {
            return new JobRunContext(jobRunInfo, this.progressChannel);
        }
    }
}
