

var builder = DistributedApplication.CreateBuilder(args);

var rustfs = builder.AddRustFs("rustfs")
    .WithDataVolume();
var booksBucket = rustfs.AddBucket("books");
var imageBucket = rustfs.AddBucket("image");

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("BookReaderPlatform_PostgresData")
    .WithPgAdmin();

var bookDB = postgres.AddDatabase("bookdb");

var api = builder.AddProject<Projects.BookReader>("api")
    .WithReference(bookDB)
    .WaitFor(bookDB)
    .WithReference(rustfs).WaitFor(rustfs);

builder.Build().Run();
