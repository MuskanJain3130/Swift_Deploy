using Microsoft.AspNetCore.SignalR;
using MongoDB.Driver;
using SwiftDeploy.Models;
using SwiftDeploy.Services;
using System.Text.Json;

public class DeploymentSchedulerWorker : BackgroundService
{
    private readonly IMongoCollection<ScheduledDeployment> _scheduledCollection;
    private readonly IMongoCollection<Deployment> _deploymentCollection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeploymentSchedulerWorker> _logger;
    private readonly IHubContext<DeploymentHub> _hubContext;

    public DeploymentSchedulerWorker(
        IMongoDatabase db,
        IServiceProvider serviceProvider,
        ILogger<DeploymentSchedulerWorker> logger,
        IHubContext<DeploymentHub> hubContext)
    {
        _scheduledCollection = db.GetCollection<ScheduledDeployment>("scheduled_deployments");
        _deploymentCollection = db.GetCollection<Deployment>("Deployments");
        _serviceProvider = serviceProvider;
        _logger = logger;
        _hubContext = hubContext;
    }
    private string? ExtractGitHubUrl(string payloadJson)
    {
        try
        {
            var obj = JsonSerializer.Deserialize<GitHubDeployRequest>(payloadJson);
            return obj?.GitHubRepo;
        }
        catch
        {
            return null;
        }
    }
    private string? ExtractRepoId(ScheduledDeployment job)
    {
        try
        {
            if (job.DeploymentType == "github")
            {
                var payload = JsonSerializer.Deserialize<GitHubDeployRequest>(job.PayloadJson);
                return payload?.GitHubRepo;
            }

            return job.ProjectId; // fallback for upload
        }
        catch
        {
            return job.ProjectId;
        }
    }
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var filter = Builders<ScheduledDeployment>.Filter.And(
                    Builders<ScheduledDeployment>.Filter.Eq(x => x.IsExecuted, false),
                    Builders<ScheduledDeployment>.Filter.Lte(x => x.ScheduledTime, DateTime.UtcNow)
                );

                var jobs = await _scheduledCollection.Find(filter).ToListAsync(stoppingToken);

                foreach (var job in jobs)
                {
                    try
                    {
                        // 🔒 LOCK JOB (prevent duplicate execution)
                        var lockedJob = await _scheduledCollection.FindOneAndUpdateAsync(
                            x => x.Id == job.Id && x.IsExecuted == false,
                            Builders<ScheduledDeployment>.Update.Set(x => x.Status, "Running")
                        );

                        if (lockedJob == null)
                            continue;

                        _logger.LogInformation($"🚀 Executing scheduled deployment {job.Id}");

                        using var scope = _serviceProvider.CreateScope();
                        var service = scope.ServiceProvider.GetRequiredService<IUnifiedDeploymentService>();

                        DeploymentResponse result;

                        switch (job.DeploymentType?.ToLower())
                        {
                            case "upload":
                                var uploadPayload = JsonSerializer.Deserialize<UploadProjectRequest>(job.PayloadJson);

                                if (uploadPayload == null)
                                    throw new Exception("Invalid upload payload");

                                result = await service.ExecuteUploadDeployment(uploadPayload, job.UserId);
                                break;

                            case "github":
                                var githubPayload = JsonSerializer.Deserialize<GitHubDeployRequest>(job.PayloadJson);

                                if (githubPayload == null)
                                    throw new Exception("Invalid GitHub payload");

                                result = await service.ExecuteGitHubDeployment(githubPayload);
                                break;

                            default:
                                throw new Exception($"Unknown deployment type: {job.DeploymentType}");
                        }

                        // ✅ Save deployment result
                        var deployment = new Deployment
                        {
                            // 🔥 CORE
                            RepoId = ExtractRepoId(job), // FIXED
                            Status = result.Success ? "success" : "failed",

                            // 🔥 USER + PLATFORM
                            UserId = job.UserId,
                            Platform = job.Platform,

                            // 🔥 SERVICE INFO
                            ServiceId = result.Success ? result.ProjectId : null,
                            ServiceUrl = result.Success ? result.DeploymentUrl : null,

                            // 🔥 GITHUB
                            GitHubRepoUrl = ExtractGitHubUrl(job.PayloadJson),

                            // 🔥 INTERNAL LINKING
                            InternalProjectId = job.ProjectId,
                            PlatformProjectId = job.PlatformProjectId,

                            DeployedAt = DateTime.UtcNow
                        };

                        await _deploymentCollection.InsertOneAsync(deployment, cancellationToken: stoppingToken);

                        // ✅ Update job status
                        await _scheduledCollection.UpdateOneAsync(
                            x => x.Id == job.Id,
                            Builders<ScheduledDeployment>.Update
                                .Set(x => x.Status, result.Success ? "Completed" : "Failed")
                                .Set(x => x.IsExecuted, true),
                            cancellationToken: stoppingToken
                        );

                        // 📡 Notify frontend via SignalR
                        await _hubContext.Clients.Group(job.UserId).SendAsync(
                            "ScheduledDeploymentCompleted",
                            new
                            {
                                jobId         = job.Id,
                                status        = result.Success ? "Completed" : "Failed",
                                platform      = job.Platform,
                                deploymentUrl = result.Success ? result.DeploymentUrl : null,
                                message       = result.Success
                                    ? $"✅ Scheduled deployment on {job.Platform} succeeded!"
                                    : $"❌ Scheduled deployment on {job.Platform} failed.",
                                completedAt   = DateTime.UtcNow
                            },
                            cancellationToken: stoppingToken
                        );

                        _logger.LogInformation($"✅ Deployment finished: {job.Id}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"❌ Scheduled deployment failed for job {job.Id}");

                        await _scheduledCollection.UpdateOneAsync(
                            x => x.Id == job.Id,
                            Builders<ScheduledDeployment>.Update.Set(x => x.Status, "Failed"),
                            cancellationToken: stoppingToken
                        );

                        // 📡 Notify frontend of failure via SignalR
                        try
                        {
                            await _hubContext.Clients.Group(job.UserId).SendAsync(
                                "ScheduledDeploymentCompleted",
                                new
                                {
                                    jobId         = job.Id,
                                    status        = "Failed",
                                    platform      = job.Platform,
                                    deploymentUrl = (string?)null,
                                    message       = $"❌ Scheduled deployment on {job.Platform} failed: {ex.Message}",
                                    completedAt   = DateTime.UtcNow
                                },
                                cancellationToken: stoppingToken
                            );
                        }
                        catch (Exception hubEx)
                        {
                            _logger.LogWarning(hubEx, "Failed to send SignalR failure notification for job {JobId}", job.Id);
                        }
                    }
                }

                await Task.Delay(10000, stoppingToken); // polling interval
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "🔥 Worker loop error");
                await Task.Delay(10000, stoppingToken);
            }
        }
    }
}