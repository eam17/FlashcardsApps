using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using SpanishFlashcards;
using SpanishFlashcards.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
builder.Services.AddScoped<WordRepository>();
builder.Services.AddScoped<VerbRepository>();
builder.Services.AddScoped<DictionaryRepository>();
builder.Services.AddScoped<ProgressStore>();
builder.Services.AddScoped<SongService>();
builder.Services.AddScoped<ClaudeService>();

await builder.Build().RunAsync();
