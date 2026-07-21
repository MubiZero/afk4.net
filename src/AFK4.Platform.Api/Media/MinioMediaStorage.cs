using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Media;

public sealed class MinioMediaStorage : IMediaStorage
{
    private readonly MediaOptions.S3Options s3;
    private readonly IAmazonS3 client;

    public MinioMediaStorage(IOptions<MediaOptions> options)
    {
        s3 = options.Value.S3;
        var config = new AmazonS3Config
        {
            ServiceURL = s3.Endpoint,
            ForcePathStyle = true,               // MinIO: path-style bucket addressing
            AuthenticationRegion = s3.Region
        };
        client = new AmazonS3Client(s3.AccessKey, s3.SecretKey, config);
    }

    public async Task<string> PutAsync(string objectKey, string contentType, Stream content, CancellationToken ct)
    {
        await client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = s3.Bucket,
            Key = objectKey,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false
        }, ct);
        return PublicUrlFor(objectKey);
    }

    public async Task DeleteAsync(string objectKey, CancellationToken ct)
        => await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = s3.Bucket, Key = objectKey }, ct);

    public string PublicUrlFor(string objectKey)
        => $"{s3.PublicBaseUri.TrimEnd('/')}/{objectKey}";
}
