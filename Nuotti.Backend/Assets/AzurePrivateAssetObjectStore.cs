using Azure.Storage.Blobs;
using Azure.Storage.Sas;
using System.Security.Cryptography;

namespace Nuotti.Backend.Assets;

public sealed class AzurePrivateAssetObjectStore(BlobServiceClient service, TimeProvider? timeProvider = null)
    : IPrivateAssetObjectStore
{
    readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    public async Task<(Uri Uri, DateTimeOffset ExpiresAt)> CreateUploadGrantAsync(
        string objectKey, string contentType, CancellationToken cancellationToken = default)
    {
        var blob = await BlobAsync(objectKey, cancellationToken);
        if (!blob.CanGenerateSasUri) throw new PrivateAssetGrantUnavailableException();
        var expires = _time.GetUtcNow().AddMinutes(5);
        return (blob.GenerateSasUri(BlobSasPermissions.Create | BlobSasPermissions.Write, expires), expires);
    }

    public async Task<(Uri Uri, DateTimeOffset ExpiresAt)> CreateDownloadGrantAsync(
        string objectKey, CancellationToken cancellationToken = default)
    {
        var blob = await SealedBlobAsync(objectKey, cancellationToken);
        if (!blob.CanGenerateSasUri) throw new PrivateAssetGrantUnavailableException();
        var expires = _time.GetUtcNow().AddMinutes(2);
        return (blob.GenerateSasUri(BlobSasPermissions.Read, expires), expires);
    }

    public async Task<SealedPrivateObject?> SealAsync(string objectKey, CancellationToken cancellationToken = default)
    {
        var blob = await BlobAsync(objectKey, cancellationToken);
        if (!await blob.ExistsAsync(cancellationToken)) return null;
        var snapshot = (await blob.CreateSnapshotAsync(cancellationToken: cancellationToken)).Value.Snapshot;
        var sealedBlob = blob.WithSnapshot(snapshot);
        var properties = (await sealedBlob.GetPropertiesAsync(cancellationToken: cancellationToken)).Value;
        await using var content = await sealedBlob.OpenReadAsync(cancellationToken: cancellationToken);
        var sha256 = Convert.ToHexString(await SHA256.HashDataAsync(content, cancellationToken)).ToLowerInvariant();
        return new(new SealedReference(objectKey, snapshot).ToString(),
            new(properties.ContentLength, properties.ContentType, sha256, properties.ETag.ToString()));
    }

    async Task<BlobClient> BlobAsync(string objectKey, CancellationToken ct)
    {
        var container = service.GetBlobContainerClient("private-song-assets");
        await container.CreateIfNotExistsAsync(cancellationToken: ct);
        return container.GetBlobClient(objectKey);
    }

    async Task<BlobClient> SealedBlobAsync(string reference, CancellationToken ct)
    {
        var parsed = SealedReference.Parse(reference);
        return (await BlobAsync(parsed.ObjectKey, ct)).WithSnapshot(parsed.Snapshot);
    }

    public async Task DiscardSealedAsync(string objectKey, CancellationToken cancellationToken = default) =>
        await (await SealedBlobAsync(objectKey, cancellationToken)).DeleteIfExistsAsync(cancellationToken: cancellationToken);

    sealed record SealedReference(string ObjectKey, string Snapshot)
    {
        public override string ToString() => $"{ObjectKey}|{Snapshot}";
        public static SealedReference Parse(string value)
        {
            var separator = value.LastIndexOf('|');
            if (separator <= 0 || separator == value.Length - 1) throw new PrivateAssetGrantUnavailableException();
            return new(value[..separator], value[(separator + 1)..]);
        }
    }
}

public sealed class PrivateAssetGrantUnavailableException : System.Exception;
