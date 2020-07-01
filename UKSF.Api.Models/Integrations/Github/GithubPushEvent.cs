using Newtonsoft.Json;

namespace UKSF.Api.Models.Integrations.Github {
    public class GithubPushEvent {
        public string after;
        [JsonProperty("base_ref")] public string baseBranch;
        public string before;
        [JsonProperty("ref")] public string branch;
        [JsonProperty("head_commit")] public GithubCommit commit;
        public GithubRepository repository;
    }

    public class GithubRepository {
        [JsonProperty("full_name")] public string name;
    }

    public class GithubCommit {
        public GithubCommitAuthor author;
        public string id;
        public string message;
    }

    public class GithubCommitAuthor {
        public string email;
        public string username;
    }
}
