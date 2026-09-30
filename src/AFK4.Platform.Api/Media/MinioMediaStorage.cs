using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;

namespace AFK4.Platform.Api.Media;

public sealed class MinioMediaStorage : IMediaStorage
{
    private readonly MediaOptions.S3Options s3;
    private readonly Lazy<IAmazonS3> client;

    public MinioMediaStorage(IOptions<MediaOptions> options)
    {
        s3 = options.Value.S3;
        // Клиент строится при первом обращении, а не при создании хранилища: на стенде без MinIO
        // пустые ключи и адрес ронят конструктор, а хранилище получают все, кому оно нужно только
        // для проверки «настроено ли» (выметание сирот, отказ загрузки с понятным кодом).
        client = new Lazy<IAmazonS3>(() => new AmazonS3Client(
            s3.AccessKey,
            s3.SecretKey,
            new AmazonS3Config
            {
                ServiceURL = s3.Endpoint,
                ForcePathStyle = true,               // MinIO: path-style bucket addressing
                AuthenticationRegion = s3.Region
            }));
    }

    public async Task<string> PutAsync(string objectKey, string contentType, Stream content, CancellationToken ct)
    {
        await client.Value.PutObjectAsync(new PutObjectRequest
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
        => await client.Value.DeleteObjectAsync(new DeleteObjectRequest { BucketName = s3.Bucket, Key = objectKey }, ct);

    public string PublicUrlFor(string objectKey)
        => $"{s3.PublicBaseUri.TrimEnd('/')}/{objectKey}";
}
