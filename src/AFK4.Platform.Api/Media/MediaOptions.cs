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

        /// <summary>
        /// Хранилище готово принимать файлы. Без него загрузка отказывает всегда, и сказать об этом
        /// надо прямо: «не удалось загрузить файл» отправляет искать проблему в файле, которого в
        /// нём нет.
        /// </summary>
        public bool IsConfigured =>
            !string.IsNullOrWhiteSpace(Endpoint)
            && !string.IsNullOrWhiteSpace(Bucket)
            && !string.IsNullOrWhiteSpace(AccessKey)
            && !string.IsNullOrWhiteSpace(SecretKey)
            && !string.IsNullOrWhiteSpace(PublicBaseUri);
    }
}
