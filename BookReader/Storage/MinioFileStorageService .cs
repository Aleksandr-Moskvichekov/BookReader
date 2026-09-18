using Minio;
using Minio.DataModel.Args;

namespace BookReader.Storage
{
    public class MinioFileStorageService : IFileStorageService
    {
        private readonly IMinioClient _minio;
        private readonly string _bucket;

        public MinioFileStorageService(IMinioClient minio, IConfiguration configuration)
        {
            _minio = minio;
            _bucket = configuration["MinIO:Bucket"] ?? "books";
        }

        public async Task EnsureBucketAsync()
        {
            var exists = await _minio.BucketExistsAsync(
                new BucketExistsArgs().WithBucket(_bucket));
            if (!exists)
                await _minio.MakeBucketAsync(
                    new MakeBucketArgs().WithBucket(_bucket));
        }

        public async Task<string> UploadAsync(Stream stream, string fileName,
            string contentType, string folder = "books")
        {
           
            var key = $"{folder}/{Guid.NewGuid():N}-{fileName}";

            await _minio.PutObjectAsync(new PutObjectArgs()
                .WithBucket(_bucket)
                .WithObject(key)
                .WithStreamData(stream)
                .WithObjectSize(stream.Length)
                .WithContentType(contentType));

            return key; 
        }

        public async Task<string> GetPresignedUrlAsync(string key, int expiryMinutes = 15)
        {
            return await _minio.PresignedGetObjectAsync(new PresignedGetObjectArgs()
                .WithBucket(_bucket)
                .WithObject(key)
                .WithExpiry(expiryMinutes * 60));
        }

        public async Task DeleteAsync(string key)
        {
            await _minio.RemoveObjectAsync(new RemoveObjectArgs()
                .WithBucket(_bucket)
                .WithObject(key));
        }
    }
}
