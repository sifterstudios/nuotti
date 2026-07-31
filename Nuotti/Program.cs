using Projects;
var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("nuotti-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);
var database = postgres.AddDatabase("nuotti");
var realtime = builder.AddRedis("realtime");
var storage = builder.AddAzureStorage("storage")
    .RunAsEmulator(emulator => emulator.WithDataVolume("nuotti-storage-data"));
var assets = storage.AddBlobs("assets");

// Venue hardware remains part of one-command local development but is packaged separately.
var audioEngine = builder
    .AddProject<Nuotti_AudioEngine>("show-agent")
    .ExcludeFromManifest();
var backend = builder
    .AddProject<Nuotti_Backend>("backend")
    .WithReference(database)
    .WithReference(realtime)
    .WithReference(assets)
    .WithEnvironment("Nuotti__InfrastructureProofEnabled", "true")
    .WaitFor(database)
    .WaitFor(realtime)
    .WaitFor(assets)
    .WithExternalHttpEndpoints();
var projector = builder
    .AddProject<Nuotti_Projector>("projector")
    .ExcludeFromManifest();
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
