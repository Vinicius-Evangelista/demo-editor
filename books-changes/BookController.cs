using System.ComponentModel.DataAnnotations;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.JsonPatch;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Driver;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

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
        Database = new MongoClient(ConnectionString).GetDatabase("books");
    }

    [HttpPatch]
    [Route("{entityId}")]
    public async Task<IActionResult> Patch(string entityId, [FromBody] JsonPatchDocument patch,
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


    [HttpPost]
    public async Task<IActionResult> Create([FromBody] Book book, [FromQuery] DateTimeOffset? providedValueDate = null)
    {
        if (string.IsNullOrEmpty(book.EntityId))
            book.EntityId = Guid.NewGuid().ToString("N");

        JsonPatchDocument equivPatches = JSONPatchHelper.CreatePatch(new Book() { EntityId = book.EntityId }, book,
            new DefaultContractResolver() { NamingStrategy = new CamelCaseNamingStrategy() });

        return await Patch(book.EntityId, equivPatches, providedValueDate);
    }


    [HttpGet]
    public IActionResult Get(
        [FromQuery(Name = "$orderby")] string orderby = "",
        [FromQuery(Name = "$skip")] int skip = 0,
        [FromQuery(Name = "$top")] int top = 20)
    {
        var query = Database.GetCollection<Book>("books-bestsofar").Find(r =>
            r.Editing == null || r.Editing.Status == null || r.Editing.Status.Value != "archived").ToList();
        return new JsonResult(query);
    }

    [HttpGet]
    [Route("$count")]
    public long GetBooksCount()
    {
        // The count is of course done on the best so far collection, otherwise, it would add up historical states
        return Database.GetCollection<Book>("books-bestsofar").CountDocuments(r => true);
    }

    [HttpGet]
    [Route("{entityId}")]
    public IActionResult GetUnique(string entityId)
    {
        // As long as no parameter is added, the behavior is simply to find and retrieve the best-known state
        var result = Database.GetCollection<Book>("books-bestsofar").Find(item => item.EntityId == entityId);
        if (result.CountDocuments() == 0)
            return new NotFoundResult();
        else
            return new JsonResult(result.First());
    }

    [HttpDelete]
    [Route("{entityId}")]
    public async Task<IActionResult> Delete(
        string entityId,
        [FromQuery] DateTimeOffset? providedValueDate = null,
        [FromQuery] bool fullDeleteIncludingHistory = false)
    {
        if (fullDeleteIncludingHistory)
        {
            // If this flag is activated (and it could be linked to a special authorization), the object is indeed deleted
            await Database.GetCollection<ChangeUnit>("books-changes")
                .DeleteManyAsync(Builders<ChangeUnit>.Filter.Eq(item => item.EntityId, entityId));
            await Database.GetCollection<ObjectState<Book>>("books-states")
                .DeleteManyAsync(Builders<ObjectState<Book>>.Filter.Eq(item => item.EntityId, entityId));
            await Database.GetCollection<Book>("books-bestsofar")
                .DeleteManyAsync(Builders<Book>.Filter.Eq(item => item.EntityId, entityId));
            return new OkResult();
        }
        else
        {
            var result = Database.GetCollection<Book>("books-states").Find(item => item.EntityId == entityId);

            if (result.CountDocuments() == 0)
            {
                return new NotFoundResult();
            }

            Book book = result.First();
            Book modified = result.First();

            if (modified.Editing is null) modified.Editing = new EditingPetal();
            if (modified.Editing.Status is null) modified.Editing.Status = new Status();

            modified.Editing.Status.Value = "archived";

            JsonPatchDocument equivPatches = JSONPatchHelper.CreatePatch(book, modified, new DefaultContractResolver()
            {
                NamingStrategy = new CamelCaseNamingStrategy()
            });

            DateTimeOffset valueDate = providedValueDate.GetValueOrDefault(DateTimeOffset.UtcNow);
            return await Patch(book.EntityId, equivPatches, valueDate);
        }
    }

}

public class JSONPatchHelper
{
    public static JsonPatchDocument CreatePatch(object originalPatch, object modifiedObject,
        IContractResolver contractResolver)
    {
        var original = JObject.FromObject(originalPatch);
        var modified = JObject.FromObject(modifiedObject);

        var patch = new JsonPatchDocument()
        {
            ContractResolver = contractResolver,
        };
        FillPatchForObject(original, modified, patch, "/");

        return patch;
    }

    static void FillPatchForObject(JObject orig, JObject mod, JsonPatchDocument patch, string path)
    {
        var origNames = orig.Properties().Select(x => x.Name).ToArray();
        var modNames = mod.Properties().Select(x => x.Name).ToArray();

        foreach (var k in origNames.Except(modNames))
        {
            var prop = orig.Property(k);
            patch.Remove(path + prop.Name);
        }

        // Names added in modified
        foreach (var k in modNames.Except(origNames))
        {
            var prop = mod.Property(k);
            patch.Add(path + prop.Name, prop.Value);
        }

        foreach (var k in origNames.Intersect(modNames))
        {
            var origProp = orig.Property(k);
            var modProp = mod.Property(k);

            if (origProp.Value.Type != modProp.Value.Type)
            {
                patch.Replace(path + modProp.Name, modProp.Value);
            }
            else if (!string.Equals(origProp.Value.ToString(Newtonsoft.Json.Formatting.None),
                         modProp.Value.ToString(Newtonsoft.Json.Formatting.None)))
            {
                if (origProp.Value.Type == JTokenType.Object)
                {
                    FillPatchForObject(origProp.Value as JObject, modProp.Value as JObject, patch,
                        path + modProp.Name + "/");
                }
                else
                {
                    patch.Replace(path + modProp.Name, modProp.Value);
                }
            }
        }
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
    public ObjectId TechnicalId { get; set; }

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