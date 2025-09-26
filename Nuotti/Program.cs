var builder = DistributedApplication.CreateBuilder(args);

var backend = builder
    .AddProject<Projects.Nuotti_Backend>("backend")
    .WithExternalHttpEndpoints();
var projector = builder.AddProject<Projects.Nuotti_Projector>("projector");
var audience = builder
    .AddProject<Projects.Nuotti_Audience>("audience")
    .WithReference(backend)
    .WithExternalHttpEndpoints();
var audioEngine = builder.AddProject<Projects.Nuotti_AudioEngine>("audioEngine");

builder.Build().Run();