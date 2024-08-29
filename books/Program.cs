using System.Collections.Immutable;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

var statuses = new[]
{
    "Idea", "AuthorChosen", "ContentDefined", "Writing", "Editing", "ReadyToPrint", "avaliable", "RetiredFromSalesl", "Archived", "Archived"
};

var books =Enumerable.Range(1, 3).Select(index => 
        new Book()
        {
            BusinessId = index.ToString(),
            ISBN = "978-2-409-03806-" + index.ToString(),
            NumberOfPages = Random.Shared.Next(400, 850),
            PublishDate = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
            Editing = new EditingPetal()
            {
                NumberOfChapters = Random.Shared.Next(5, 30),
                Status = new Status() { Value = statuses[Random.Shared.Next(statuses.Length)]}
            },
            Sales = new SalesPetal()
            {
                Price = new MonetaryAmount()
                {
                    Value = new decimal(Random.Shared.Next(1000, 10000) / 100),
                    MonetaryUnit = "USD"
                },
                Weight = new decimal(Random.Shared.Next(200, 3500))
            }
        })
    .ToArray();


app.MapGet("/books", () =>

    books
)
.WithName("GetBooks")
.WithOpenApi();

app.MapGet("/books/{id}", (string id) =>
{
    return books.FirstOrDefault(book => book!.BusinessId == id, null);
})
.WithName("GetBook")
.WithOpenApi();


// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();



app.Run();