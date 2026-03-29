using MongoDB.Driver;
using SwiftDeploy.Models;
using System.Text.Json;

public class DeploymentSchedulerWorker : BackgroundService
{
    private readonly IMongoCollection<ScheduledDeployment> _scheduledCollection;
    private readonly IMongoCollection<Deployment> _deploymentCollection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DeploymentSchedulerWorker> _logger;

    public DeploymentSchedulerWorker(
        IMongoDatabase db,
        IServiceProvider serviceProvider,
        ILogger<DeploymentSchedulerWorker> logger)
    {
        _scheduledCollection = db.GetCollection<ScheduledDeployment>("scheduled_deployments");
        _deploymentCollection = db.GetCollection<Deployment>("deployments");
        _serviceProvider = serviceProvider;
        _logger = logger;
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
                            RepoId = job.ProjectId,
                            Status = result.Success ? "success" : "failed",
                            ServiceUrl = result.DeploymentUrl,
                            ServiceId = result.ProjectId,
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