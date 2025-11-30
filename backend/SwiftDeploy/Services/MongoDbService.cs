using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Abstractions;
using MongoDB.Driver;
using SwiftDeploy.Data;
using SwiftDeploy.Models;
using SwiftDeploy.Models.SwiftDeploy.Models;

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
        public IMongoCollection<UserTokens> UserTokens => _database.GetCollection<UserTokens>("UserTokens");
        public IMongoCollection<Deployment> Deployments => _database.GetCollection<Deployment>("Deployments");
        public IMongoCollection<Project> Projects => _database.GetCollection<Project>("Projects");
        public IMongoCollection<Repository> Repositories => _database.GetCollection<Repository>("Repositories");
       public IMongoCollection<Models.LogEntry> Logs => _database.GetCollection<Models.LogEntry>("Logs");

    }
}
