using Azure.Storage.Blobs;
using Microsoft.Azure.Cosmos;
using FileNetPOC.Api.Infrastructure;
using MediatR;
using FluentValidation;
using FileNetPOC.Shared.Interfaces;
using FileNetPOC.Shared.Behaviors;
using FileNetPOC.Core.Repositories;
using FileNetPOC.Services.Features.Documents.Commands.UploadDocument;

var builder = WebApplication.CreateBuilder(args);

// 1. Load the auto-generated Terraform configuration
builder.Configuration.AddJsonFile("appsettings.Terraform.json", optional: true, reloadOnChange: true);

// 2. Add Base Services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// 3. Add Exception Handling
builder.Services.AddExceptionHandler<CustomExceptionHandler>();
builder.Services.AddProblemDetails();

// 4. Bind the configuration section & Register Azure SDK Clients
var fileNetConfig = builder.Configuration.GetSection("FileNet");

builder.Services.AddSingleton(x => 
{
    var connectionString = fileNetConfig["StorageConnectionString"];
    return new BlobServiceClient(connectionString);
});

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

// 5. REGISTER MEDIATR, BEHAVIORS, VALIDATORS, AND REPOSITORY
builder.Services.AddMediatR(cfg => {
    cfg.RegisterServicesFromAssembly(typeof(UploadDocumentCommand).Assembly);
});

builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(LoggingBehavior<,>));
builder.Services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));

builder.Services.AddValidatorsFromAssembly(typeof(UploadDocumentCommandValidator).Assembly);

builder.Services.AddScoped(typeof(IRepository<>), typeof(CosmosRepository<>));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseExceptionHandler();
app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();