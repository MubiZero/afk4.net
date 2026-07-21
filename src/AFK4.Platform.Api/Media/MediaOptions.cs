namespace AFK4.Platform.Api.Media;

public sealed class MediaOptions
{
    public const string SectionName = "Media";
    public S3Options S3 { get; set; } = new();
    public long MaxBytes { get; set; } = 10 * 1024 * 1024; // 10 MB

    public sealed class S3Options
    {
        public string Endpoint { get; set; } = string.Empty;
        public string Bucket { get; set; } = string.Empty;
        public string AccessKey { get; set; } = string.Empty;
        public string SecretKey { get; set; } = string.Empty;
        public string PublicBaseUri { get; set; } = string.Empty;
        public string Region { get; set; } = "us-east-1";
    }
}
