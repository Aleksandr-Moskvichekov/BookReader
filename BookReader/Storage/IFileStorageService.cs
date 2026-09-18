namespace BookReader.Storage
{
    public interface IFileStorageService
    {
        Task<string> UploadAsync(Stream stream, string fileName, string contentType, string folder = "books");
        Task<string> GetPresignedUrlAsync(string key, int expiryMinutes = 15);
        Task DeleteAsync(string key);
        Task EnsureBucketAsync();
    }
}
