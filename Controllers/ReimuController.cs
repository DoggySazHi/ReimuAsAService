using Microsoft.AspNetCore.Mvc;

namespace ReimuAsAService.Controllers;

[ApiController]
[Route("reimu")]
public class ReimuController : ControllerBase
{
    private readonly ILogger<ReimuController> _logger;
    private readonly IInvokeAIController _invoke;

    public ReimuController(ILogger<ReimuController> logger, IInvokeAIController invoke)
    {
        _logger = logger;
        _invoke = invoke;
    }

    [HttpGet]
    public async Task<IActionResult> Get()
    {
        const string reimuQuery = "masterpiece, best quality, masterpiece, professional, hakurei reimu, 1girl, looking at viewer [lowres, bad anatomy, bad hands, text, error, missing fingers, extra digit, fewer digits, cropped, worst quality, low quality, normal quality, jpeg artifacts,signature, watermark, username, blurry, artist name] -s 50 -W 512 -H 512 -C 7.5 -A k_lms";
        var order = _invoke.QueueImage(reimuQuery);
        _logger.LogInformation("Received query #{}", order);
        var file = await _invoke.WaitForImage(order);
        _logger.LogInformation("#{} receives path {}", order, file);
        return File(System.IO.File.OpenRead(file), "image/png");
    }
}