using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Abstractions;
using MongoDB.Driver;
using Octokit;
using SwiftDeploy.Data;

namespace SwiftDeploy.Services
{ 
    public class MongoDbService
    {
        private readonly IMongoDatabase _database;

        public MongoDbService(IOptions<MongoDbSettings> settings)
        {
            var client = new MongoClient(settings.Value.ConnectionString);
            _database = client.GetDatabase(settings.Value.DatabaseName);
        }

        public IMongoCollection<User> Users => _database.GetCollection<User>("Users");
        public IMongoCollection<Deployment> Deployments => _database.GetCollection<Deployment>("Deployments");
        public IMongoCollection<Repository> Repositories => _database.GetCollection<Repository>("Repositories");
       public IMongoCollection<LogEntry> Logs => _database.GetCollection<LogEntry>("Logs");

    }
}
