using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Octokit;
using UKSF.Api.Interfaces.Integrations.Github;

namespace UKSF.Api.Services.Integrations.Github {
    public class GithubService : IGithubService {
        private readonly IConfiguration configuration;

        public GithubService(IConfiguration configuration) => this.configuration = configuration;

        public bool VerifySignature(string signature, string body) {
            string secret = configuration.GetSection("Secrets")["githubSecret"];
            byte[] data = Encoding.UTF8.GetBytes(body);
            byte[] secretData = Encoding.UTF8.GetBytes(secret);
            using HMACSHA1 hmac = new HMACSHA1(secretData);
            byte[] hash = hmac.ComputeHash(data);
            string sha1 = $"sha1={BitConverter.ToString(hash).ToLower().Replace("-", "")}";
            return string.Equals(sha1, signature);
        }

        public async Task<string> GetCommitVersion(string branch) {
            GitHubClient client = new GitHubClient(new ProductHeaderValue("uksf-api-integration"));
            byte[] contentBytes = await client.Repository.Content.GetRawContentByRef("uksf", "modpack", $"addons/main/script_version.hpp", branch);
            string content = Encoding.UTF8.GetString(contentBytes);
            IEnumerable<string> lines = content.Split("\n").Take(3);
            string version = string.Join('.', lines.Select(x => x.Split(' ')[^1]));
            return version;
        }
    }
}
