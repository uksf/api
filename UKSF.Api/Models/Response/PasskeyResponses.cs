namespace UKSF.Api.Models.Response;

public class PasskeyOptionsResponse<T>
{
    public string FlowId { get; set; }
    public T Options { get; set; }
}

public class PasskeyResponse
{
    public string Id { get; set; }
    public string Name { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastUsed { get; set; }
    public bool IsBackedUp { get; set; }
}

public class PasskeysResponse
{
    public bool HasPassword { get; set; }
    public List<PasskeyResponse> Passkeys { get; set; } = [];
}
