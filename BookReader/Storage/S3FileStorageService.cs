using S3Storage;

namespace BookReader.Storage
{
    public class S3FileStorageService : IFileStorageService
    {
        private readonly IS3Client _s3;
        private readonly string _bucket;

        public S3FileStorageService(IS3Client s3, IConfiguration configuration)
        {
            _s3 = s3;
            _bucket = configuration["S3:Bucket"] ?? "books";
        }

        public async Task EnsureBucketAsync()
        {
            var exists = await _s3.BucketExistsAsync(_bucket);
            if (!exists)
                await _s3.CreateBucketAsync(_bucket);
        }

        public async Task<string> UploadAsync(Stream stream, string fileName,
            string contentType, string folder = "books")
        {
           
            var safeFileName = Path.GetFileName(fileName);
            var key = $"{folder}/{Guid.NewGuid():N}-{safeFileName}";

            await _s3.UploadAsync(_bucket, key, stream, contentType);

            return key;
        }

        public Task<string> GetPresignedUrlAsync(string key, int expiryMinutes = 15)
        {
            return Task.FromResult(_s3.GetPresignedUrl(_bucket, key, expiryMinutes * 60));
        }

        public async Task<(byte[] Content, string ContentType)> DownloadAsync(string key)
        {
            var objectInfo = await _s3.GetObjectInfoAsync(_bucket, key);
            var content = await _s3.DownloadBytesAsync(_bucket, key);

            return (content, objectInfo.ContentType ?? "application/octet-stream");
        }

        public async Task DeleteAsync(string key)
        {
            await _s3.DeleteObjectAsync(_bucket, key);
        }
    }
}
