var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

var statuses = new[]
{
    "Idea", "AuthorChosen", "ContentDefined", "Writing", "Editing", "ReadyToPrint", "avaliable", "RetiredFromSalesl", "Archived", "Archived"
};

app.MapGet("/books", () =>
{
    var books = Enumerable.Range(1, 3).Select(index =>
    new Book
    {
        BusinessId = index.ToString(),
        ISBN = "978-2-409-03806-" + index.ToString(),
        NumberOfPages =





    }

    );




});



app.MapGet("/weatherforecast", () =>
{
    var forecast = Enumerable.Range(1, 5).Select(index =>
        new WeatherForecast
        (
            DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Random.Shared.Next(-20, 55),
            summaries[Random.Shared.Next(summaries.Length)]
        ))
        .ToArray();
    return forecast;
})
.WithName("GetWeatherForecast")
.WithOpenApi();

app.Run();

record Book(string BusinessId, string? ISBN, int? NumberOfPages, DateOnly? PublishDate, EditingPetal? EditingProps, SalesPetal SalesProps)
{
}

record EditingPetal(int? NumberOfChapters, string? Status)
{ }

record SalesPetal(decimal? Price, decimal? Weight)
{ }