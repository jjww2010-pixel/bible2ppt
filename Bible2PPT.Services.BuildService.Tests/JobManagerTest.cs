using Bible2PPT.PPT;
using Xunit;

namespace Bible2PPT.Services.BuildService.Tests;

public sealed class JobManagerTest
{
    [Fact]
    public async Task PendingFlagIncludesQueuedJobsAndClearsAfterCancel()
    {
        await using var manager = new WaitingManager();
        var job = new Job();
        manager.Queue(job);
        Assert.True(manager.HasPendingJobs);
        await manager.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        manager.Cancel(job);
        for (var attempt = 0; attempt < 100 && manager.HasPendingJobs; attempt++) await Task.Delay(10);
        Assert.False(manager.HasPendingJobs);
    }

    private sealed class WaitingManager : JobManager
    {
        public TaskCompletionSource<bool> Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task ProcessAsync(Job job, CancellationToken token)
        {
            Started.TrySetResult(true);
            await Task.Delay(Timeout.Infinite, token);
        }
    }
}
