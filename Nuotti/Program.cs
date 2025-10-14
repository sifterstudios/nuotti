using Projects;
var builder = DistributedApplication.CreateBuilder(args);

var audioEngine = builder.AddProject<Nuotti_AudioEngine>("audioEngine");
var backend = builder
    .AddProject<Nuotti_Backend>("backend")
    .WithExternalHttpEndpoints();
var projector = builder.AddProject<Nuotti_Projector>("projector");
var audience = builder
    .AddProject<Nuotti_Audience>("audience")
    .WithReference(backend)
    .WithReference(projector)
    .WithExternalHttpEndpoints();
var performer = builder
    .AddProject<Nuotti_Performer>("performer")
    .WithReference(backend)
    .WithReference(projector)
    .WithReference(audioEngine)
    .WithExternalHttpEndpoints();

builder.Build().Run();