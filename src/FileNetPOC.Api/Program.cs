using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using FileNetPOC.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// 1. Load the auto-generated Terraform configuration (Crucial for your local/cloud hybrid setup)
builder.Configuration.AddJsonFile("appsettings.Terraform.json", optional: true, reloadOnChange: true);

// 2. Add Base Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Add Exception Handling (Your new additions)
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddProblemDetails();

// 4. Bind the configuration section & Register Azure SDK Clients
var fileNetConfig = builder.Configuration.GetSection("FileNet");

// Blob Storage Client
builder.Services.AddSingleton(x => 
{
    var connectionString = fileNetConfig["StorageConnectionString"];
    return new BlobServiceClient(connectionString);
});

// Cosmos DB Client
builder.Services.AddSingleton(x =>
{
    var connectionString = fileNetConfig["CosmosConnectionString"];
    var options = new CosmosClientOptions()
    {
        SerializerOptions = new CosmosSerializationOptions()
        {
            PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
        },
        ConnectionMode = ConnectionMode.Direct
    };
    return new CosmosClient(connectionString, options);
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

// 5. Enable the Exception Handler Middleware (Your new addition)
app.UseExceptionHandler();

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();