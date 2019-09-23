using Jobbr.ComponentModel.Execution;
using Jobbr.ComponentModel.Execution.Model;
using Jobbr.Execution.InProcess.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Jobbr.Execution.InProcess.Execution
{
    internal class JobRunContext : IJobRunContext
    {
        private static readonly ILog Logger = LogProvider.For<JobRunContext>();

        private readonly JobRunInfo jobRunInfo;
        private readonly IJobRunProgressChannel progressChannel;

        public JobRunContext(JobRunInfo jobRunInfo, IJobRunProgressChannel progressChannel)
        {
            this.jobRunInfo = jobRunInfo;
            this.progressChannel = progressChannel;
        }

        public long JobRunId => this.jobRunInfo.Id;

        public event EventHandler<JobRunEndedEventArgs> Ended;


        public void Start()
        {
            Logger.Info($"[{this.jobRunInfo.Id}] A new JobRunContext is starting for JobRun with id: '{this.jobRunInfo.Id}'. (JobId: '{this.jobRunInfo.JobId}', TriggerId: '{this.jobRunInfo.TriggerId}'");

            try
            {
                var task = this.startTask(this.jobRunInfo);
                task.ContinueWith(OnCompletion);

                this.progressChannel.PublishStatusUpdate(this.jobRunInfo.Id, JobRunStates.Started);
            }
            catch (Exception e)
            {
                Logger.ErrorException($"[{this.jobRunInfo.Id}] Exception thrown while starting JobRun with id: '{this.jobRunInfo.Id}'. (JobId: '{this.jobRunInfo.JobId}', TriggerId: '{this.jobRunInfo.TriggerId}'", e);
            }
        }

        private void OnCompletion(Task antecedent)
        {
            if(antecedent.IsFaulted)
            {
                var exception = antecedent.Exception;
                this.progressChannel.PublishStatusUpdate(this.jobRunInfo.Id, JobRunStates.Failed);
                Logger.ErrorException($"[{this.jobRunInfo.Id}] Exception thrown while running JobRun with id: '{this.jobRunInfo.Id}'. (JobId: '{this.jobRunInfo.JobId}', TriggerId: '{this.jobRunInfo.TriggerId}'", exception);

            }
            else if (antecedent.IsCompleted)
            {
                this.progressChannel.PublishStatusUpdate(this.jobRunInfo.Id, JobRunStates.Completed);
                Logger.Info($"[{this.jobRunInfo.Id}] Execution finished for JobRun with id: '{this.jobRunInfo.Id}'. (JobId: '{this.jobRunInfo.JobId}', TriggerId: '{this.jobRunInfo.TriggerId}'");
            }
        }

        private Task startTask(JobRunInfo jobRun)
        {
            // get type
            var type = Type.GetType(jobRun.Type, throwOnError: true, ignoreCase:true);

            // create instance of job to run
            var jobRunnerClassInstance = Activator.CreateInstance(type) as dynamic;

            // Run and place into task
            if (jobRunnerClassInstance.Run is Action action)
            {
                Logger.Info($"[{jobRun.Id}] Preparing to start the runner of type {jobRun.Type}");
                return Task.Run(action);
            }

            throw new Exception("Public parameterless method 'Run' not found");
        }
    }
}
