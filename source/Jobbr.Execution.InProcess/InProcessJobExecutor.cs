using Jobbr.ComponentModel.Execution;
using Jobbr.ComponentModel.Execution.Model;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Jobbr.Execution.InProcess.Logging;

namespace Jobbr.Execution.InProcess
{
    public class InProcessJobExecutor : IJobExecutor
    {
        private static readonly ILog Logger = LogProvider.For<InProcessJobExecutor>();

        private readonly List<PlannedJobRun> plannedJobRuns = new List<PlannedJobRun>();
        private bool isStarted;
        private readonly object syncRoot = new object();
        private readonly List<Task> _activeContexts = new List<Task>();


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
            isStarted = true;
        }

        public void Stop()
        {
            throw new NotImplementedException();
        }

        private void determineNextRun()
        {
            this.plannedJobRuns.

        }
    }
}
