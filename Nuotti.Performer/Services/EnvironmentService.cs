namespace Nuotti.Performer.Services;

public interface IEnvironmentService
{
    bool IsDevelopment { get; }
    string EnvironmentName { get; }
}

public sealed class EnvironmentService : IEnvironmentService
{
    private readonly IWebHostEnvironment _env;
    public EnvironmentService(IWebHostEnvironment env)
    {
        _env = env;
    }

    public bool IsDevelopment => _env.IsDevelopment();
    public string EnvironmentName => _env.EnvironmentName;
}
