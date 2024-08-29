using Microsoft.AspNetCore.Mvc;
namespace books_controller.Controllers;

[ApiController]
[Route("[controller]")]
public class BooksController : ControllerBase
{
    private readonly ILogger<BooksController> _logger;
    public BooksController(ILogger<BooksController> logger)
    {
        _logger = logger;
    }
    // The operations will be described step by step in this chapter
}