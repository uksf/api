namespace UKSF.Api.ArmaMissions.Services;

public interface ISqmDecompiler
{
    Task<bool> IsBinarized(string sqmPath);
    Task Decompile(string sqmPath);
}
