using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;

namespace books_controller.Controllers;

[ApiController]
[Route("[controller]")]
public class BooksController : ControllerBase
{
    private readonly string ConnectionString;
    private readonly IMongoDatabase Database;
    private readonly ILogger<BooksController> _logger;

    public BooksController(ILogger<BooksController> logger, IConfiguration config)
    {
        _logger = logger;
        ConnectionString = config.GetValue<string>("BooksConnectionString") ?? "mongodb://localhost:27017";
    }

    [HttpPatch]
    [Route("{entityId}")]
    public async Task<IActionResult> UpdateEntity(string entityId, [FromBody] JsonPatchDocument patch,
        [FromQuery] DateTimeOffset? providedValueDate = null)
    {
        if (patch is null)
            return BadRequest();

        DateTimeOffset valueDate = providedValueDate.GetValueOrDefault(DateTimeOffset.UtcNow);

        ChangeUnit patchAction = new ChangeUnit()
        {
            EntityId = entityId,
            ValueDate = valueDate,
            PatchContent = new BsonArray(patch.Operations.ConvertAll<BsonDocument>(item => item.ToBsonDocument())),
        };

        var collectionActions = Database.GetCollection<BsonDocument>("books-changes");
        await collectionActions.InsertOneAsync(patchAction.ToBsonDocument());

        var result = Database.GetCollection<Book>("books-bestsofar").Find(item => item.EntityId == entityId);
        var book = result.FirstOrDefault();

        if (book == null)
        {
            book = new Book() { EntityId = entityId };
            var bestknown = Database.GetCollection<BsonDocument>("books-bestsofar");
            await bestknown.InsertOneAsync(book.ToBsonDocument());

            result = Database.GetCollection<Book>("books-bestsofar").Find(item => item.EntityId == entityId);

            book = result.First();
        }

        patch.ApplyTo(book);
        ObjectState<Book> nouvelEtat = new ObjectState<Book>()
        {
            EntityId = entityId,
            ValueDate = valueDate,
            State = book
        };
        
        var collectionEtats = Database.GetCollection<BsonDocument>("books-states");
        await collectionEtats.InsertOneAsync(nouvelEtat.ToBsonDocument());
        
        var collectionBK = Database.GetCollection<Book>("books-bestsofar");
        
        await collectionBK.FindOneAndReplaceAsync<Book>(p => p.EntityId == entityId, book);

        return new ObjectResult(book);

    }
}

public class ObjectState<T>
{
    [BsonId()]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonIgnore]
    public ObjectId TechnicalId { get; set; }

    [BsonElement("entityId")] [Required] public string EntityId { get; set; }
    [BsonElement("valueDate")] [Required] public DateTimeOffset ValueDate { get; set; }
    [BsonElement("state")] public T State { get; set; }
}

public class ChangeUnit
{
    [BsonId()]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonIgnore]
    public string TechnicalId { get; set; }

    [BsonElement("entityId")] [Required] public string EntityId { get; set; }

    [BsonElement("valueDate")] [Required] public DateTimeOffset ValueDate { get; set; }

    [BsonElement("patchContent")]
    [Required]
    public BsonArray PatchContent { get; set; }
}

public class Book
{
    [BsonId()]
    [BsonRepresentation(BsonType.ObjectId)]
    [JsonIgnore]
    public ObjectId TechnicalId { get; set; }

    [BsonElement("entityId")]
    [Required(ErrorMessage = "Business identifier of a book is mandatory")]
    public string EntityId { get; set; }

    [BsonElement("isbn")] public string? ISBN { get; set; }
    [BsonElement("title")] public string? Title { get; set; }
    [BsonElement("numberOfPages")] public int? NumberOfPages { get; set; }
    [BsonElement("publishDate")] public DateTime? PublishDate { get; set; }
    [BsonElement("editing")] public EditingPetal? Editing { get; set; }
    [BsonElement("sales")] public SalesPetal Sales { get; set; }
}

public class EditingPetal
{
    [BsonElement("numberOfChapters")] public int? NumberOfChapters { get; set; }
    [BsonElement("status")] public Status? Status { get; set; }
}

public class Status
{
    [BsonElement("value")] public string Value { get; set; }
}

public class SalesPetal
{
    [BsonElement("price")] public MonetaryAmount? Price { get; set; }
    [BsonElement("weightInGrams")] public decimal? WeightInGrams { get; set; }
}

public class MonetaryAmount
{
    [BsonElement("value")] public decimal Value { get; set; }
    [BsonElement("monetaryUnit")] public string MonetaryUnit { get; set; }
}