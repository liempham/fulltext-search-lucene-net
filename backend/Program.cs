// Program.cs
using LuceneApi.Services; // Add necessary using

var builder = WebApplication.CreateBuilder(args);

// --- Configuration ---
// Add configuration source if needed (e.g., appsettings.json is added by default)
// Example: builder.Configuration.AddJsonFile("myconfig.json", optional: true);

// --- Add services to the container ---

// Configure Lucene Search Service as Singleton
// Singleton is generally appropriate here as IndexReader/Searcher are thread-safe for reads
// and IndexWriter access is synchronized internally in our SearchService.
builder.Services.AddSingleton<ISearchService, SearchService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // Swagger for API testing

// --- Configure CORS ---
var reactAppOrigin = builder.Configuration["AllowedOrigins:ReactApp"] ?? "http://localhost:5173"; // Default Vite port
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowReactApp", policy =>
    {
        policy.WithOrigins(reactAppOrigin) // Allow requests from React dev server
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// --- Configure the HTTP request pipeline ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
    app.UseDeveloperExceptionPage(); // More detailed errors in dev
}

app.UseHttpsRedirection();

app.UseCors("AllowReactApp"); // Apply CORS policy **before** UseAuthorization/UseEndpoints

app.UseAuthorization();

app.MapControllers();

// --- Initial Lucene Index Check/Build ---
// Perform this after the app is built but before it starts listening
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        // Triggering EnsureIndexExists via the service instance
        var searchService = services.GetRequiredService<ISearchService>();
        // The check & potential build happens in the SearchService constructor or EnsureIndexExists method now
        // Force initial check/build if constructor doesn't handle it robustly enough for startup
        // Example: searchService.EnsureIndexExists(); // (If you moved the logic to a separate method)

        // Or Trigger a full rebuild on every startup (for demo purposes)
        // searchService.BuildIndex(SearchService.GetSampleData(), recreateIndex: true);
        app.Logger.LogInformation("Initial Lucene index check/build completed.");
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "An error occurred during initial Lucene index setup.");
        // Decide if you want the app to fail startup if indexing fails
        // throw;
    }
}

app.Run(); // Start the application