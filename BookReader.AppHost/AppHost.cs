var builder = DistributedApplication.CreateBuilder(args);

builder.AddProject<Projects.BookReader>("bookreader");

builder.Build().Run();
