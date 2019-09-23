using Jobbr.ComponentModel.Execution;
using Jobbr.ComponentModel.Execution.Model;
using Jobbr.Execution.InProcess.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Jobbr.Execution.InProcess.Execution
{
    internal class JobRunContext : IJobRunContext
    {
        private static readonly ILog Logger = LogProvider.For<JobRunContext>();

        private readonly JobRunInfo jobRunInfo;

        private readonly IJobRunProgressChannel progressChannel;

        private bool didReportProgress;

        public JobRunContext()
        {

        }

        public long JobRunId => this.jobRunInfo.Id;

        public event EventHandler<JobRunEndedEventArgs> Ended;


        public void Start()
        {
            Logger.Info($"[{this.jobRunInfo.Id}] A new JobRunContext is starting for JobRun with id: '{this.jobRunInfo.Id}'. (JobId: '{this.jobRunInfo.JobId}', TriggerId: '{this.jobRunInfo.TriggerId}'");

            try
            {
                var workDir = this.SetupDirectories(this.jobRunInfo);

                this.StartProcess(this.jobRunInfo, workDir);

                this.progressChannel.PublishStatusUpdate(this.jobRunInfo.Id, JobRunStates.Started);
            }
            catch (Exception e)
            {
                Logger.ErrorException($"[{this.jobRunInfo.Id}] Exception thrown while starting JobRun with id: '{this.jobRunInfo.Id}'. (JobId: '{this.jobRunInfo.JobId}', TriggerId: '{this.jobRunInfo.TriggerId}'", e);
            }
        }

        private void StartProcess(JobRunInfo jobRun, string workDir)
        {
            var runnerFileExe = Path.GetFullPath(this.configuration.JobRunnerExecutable);
            Logger.Info($"[{jobRun.Id}] Preparing to start the runner from '{runnerFileExe}' in '{workDir}'");

            var proc = new Process { EnableRaisingEvents = true, StartInfo = { FileName = runnerFileExe } };

            var arguments = $"--jobRunId {jobRun.Id} --server {this.configuration.BackendAddress}";

            if (this.configuration.IsRuntimeWaitingForDebugger)
            {
                arguments += " --debug";
            }

            if (this.configuration.AddJobRunnerArguments != null)
            {
                var model = new JobRunStartInfo
                {
                    JobType = jobRun.Type,
                    UniqueName = jobRun.UniqueName,
                    JobRunId = jobRun.Id,
                    JobId = jobRun.JobId,
                    TriggerId = jobRun.TriggerId,
                    UserId = jobRun.UserId
                };

                var additionalArguments = this.configuration.AddJobRunnerArguments(model);

                foreach (var additionalArgument in additionalArguments)
                {
                    if (additionalArgument.Value.Contains(" "))
                    {
                        arguments += $" --{additionalArgument.Key} \"{additionalArgument.Value}\"";
                    }
                    else
                    {
                        arguments += $" --{additionalArgument.Key} {additionalArgument.Value}";
                    }
                }
            }

            proc.StartInfo.Arguments = arguments;
            proc.StartInfo.WorkingDirectory = workDir;
            proc.StartInfo.RedirectStandardOutput = true;
            proc.StartInfo.UseShellExecute = false;
            proc.StartInfo.CreateNoWindow = true;

            // Wire events
            proc.OutputDataReceived += this.ProcOnOutputDataReceived;
            proc.Exited += (o, args) => this.OnEnded(new JobRunEndedEventArgs() { ExitCode = proc.ExitCode, JobRun = jobRun, ProcInfo = proc, DidReportProgress = this.didReportProgress });

            this.progressChannel.PublishStatusUpdate(this.jobRunInfo.Id, JobRunStates.Starting);
            Logger.Info($"[{jobRun.Id}] Starting '{runnerFileExe} {arguments}' in '{workDir}'");
            proc.Start();

            Logger.Info($"[{jobRun.Id}] Started Runner with ProcessId '{proc.Id}' at '{proc.StartTime}'");
            this.progressChannel.PublishPid(jobRun.Id, proc.Id, Environment.MachineName);

            proc.BeginOutputReadLine();
        }
    }
}
