using Jobbr.ComponentModel.Execution;
using Jobbr.ComponentModel.Execution.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jobbr.Execution.InProcess.Logging;

namespace Jobbr.Execution.InProcess.Execution
{
    public class InProcessJobExecutor : IJobExecutor
    {
        private static readonly ILog Logger = LogProvider.For<InProcessJobExecutor>();

        private readonly List<PlannedJobRun> plannedJobRuns = new List<PlannedJobRun>();
        private bool isStarted;
        private readonly object syncRoot = new object();
        private readonly List<IJobRunContext> activeContexts = new List<IJobRunContext>();
        private readonly InProcessExecutorConfiguration configuration;
        private readonly IJobRunInformationService jobRunInformationService;
        private readonly IDateTimeProvider dateTimeProvider;
        private readonly IJobRunContextFactory jobRunContextFactory;

        public InProcessJobExecutor(InProcessExecutorConfiguration configuration, IJobRunInformationService jobRunInformationService, IDateTimeProvider dateTimeProvider, IJobRunContextFactory jobRunContextFactory)
        {
            this.configuration = configuration;
            this.jobRunInformationService = jobRunInformationService;
            this.dateTimeProvider = dateTimeProvider;
            this.jobRunContextFactory = jobRunContextFactory;
        }


        public void Dispose()
        {
            throw new NotImplementedException();
        }

        public bool OnJobRunCanceled(long id)
        {
            throw new NotImplementedException();
        }

        public void OnPlanChanged(List<PlannedJobRun> newPlan)
        {
            // throw if Start hasn't been called yet
            if(!this.isStarted)
            {
                throw new Exception("Executor hasn't been started yet.");
            }

            var hadChanges = 0;

            lock (this.syncRoot)
            {
                Logger.Info($"Got a plan with {newPlan.Count} scheduled JobRuns with an upcoming startdate");

                // Update startdates of existing
                foreach (var plannedJobRun in newPlan)
                {
                    var existing = this.plannedJobRuns.SingleOrDefault(e => e.Id == plannedJobRun.Id);

                    if (existing != null && existing.PlannedStartDateTimeUtc != plannedJobRun.PlannedStartDateTimeUtc)
                    {
                        existing.PlannedStartDateTimeUtc = plannedJobRun.PlannedStartDateTimeUtc;
                        hadChanges++;
                        Logger.Info($"Changed startdate of jobrun '{existing.Id}' to '{plannedJobRun.PlannedStartDateTimeUtc}'");
                    }
                }

                // Add only new
                var toAdd = newPlan.Where(newItem => this.plannedJobRuns.All(existingItem => existingItem.Id != newItem.Id) && this.activeContexts.All(c => c.JobRunId != newItem.Id)).ToList();
                this.plannedJobRuns.AddRange(toAdd);
                hadChanges += toAdd.Count;

                Logger.Info($"Added {toAdd.Count} new planned jobruns based on the new plan");

                // Remove non existing
                var toRemove = this.plannedJobRuns.Where(existingItem => newPlan.All(newItem => existingItem.Id != newItem.Id)).ToList();
                this.plannedJobRuns.RemoveAll(p => toRemove.Contains(p));
                hadChanges += toRemove.Count;

                Logger.Info($"Removed {toRemove.Count} previously planned jobruns.");
            }

            if (hadChanges > 0)
            {
                // Immediately handle changes
                this.StartReadyJobsFromQueue();
            }


        }

        public void Start()
        {
            // nothing much to do here buy set the isStarted flag to true to respect API usage
            this.isStarted = true;
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        private void startJob()
        {
            lock (this.syncRoot)
            {
                var possibleJobsToStart = this.configuration.MaxConcurrentProcesses - this.activeContexts.Count;
                var readyJobs = this.plannedJobRuns.Where(jr => jr.PlannedStartDateTimeUtc <= this.dateTimeProvider.GetUtcNow()).OrderBy(jr => jr.PlannedStartDateTimeUtc).ToList();

                var jobsToStart = readyJobs.Take(possibleJobsToStart).ToList();

                var queueCannotStartAll = readyJobs.Count > possibleJobsToStart;
                var showStatusInformationNow = (DateTime.Now.Second % 5) == 0;
                var canStartAllReadyJobs = jobsToStart.Count > 0 && jobsToStart.Count <= possibleJobsToStart;

                if ((queueCannotStartAll && showStatusInformationNow) || canStartAllReadyJobs)
                {
                    Logger.Info($"There are {readyJobs.Count} planned jobs in the queue and currently {this.activeContexts.Count} running jobs. Number of possible jobs to start: {possibleJobsToStart}");
                }

                foreach (var jobRun in jobsToStart)
                {
                    Logger.Debug($"Trying to start job with Id '{jobRun.Id}' which was planned for {jobRun.PlannedStartDateTimeUtc}.");

                    IJobRunContext wrapper = null;

                    try
                    {
                        Logger.Debug($"Getting Metadata for a job (Id '{jobRun.Id}') that needs to be started.");
                        var jobRunInfo = this.jobRunInformationService.GetByJobRunId(jobRun.Id);

                        wrapper = this.jobRunContextFactory.CreateJobRunContext(jobRunInfo);

                        this.activeContexts.Add(wrapper);
                        this.plannedJobRuns.Remove(jobRun);

                        wrapper.Ended += this.ContextOnEnded;
                        wrapper.Start();
                    }
                    catch (Exception e)
                    {
                        Logger.ErrorException($"Exception was thrown while starting a new JobRun with Id: {jobRun.Id}.", e);

                        if (wrapper != null)
                        {
                            wrapper.Ended -= this.ContextOnEnded;
                        }
                    }
                }
            }
        }
    }
}
