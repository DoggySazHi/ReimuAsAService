using System.Diagnostics;
using System.Text.RegularExpressions;

namespace ReimuAsAService;

public record ImageRequest(int Order, string Query);

public interface IInvokeAIController
{
    public int QueueImage(string input);
    public Task<string> WaitForImage(int key);
}

public partial class InvokeAIController : IInvokeAIController, IDisposable
{
    private readonly Queue<ImageRequest> _requests = new();
    private readonly Dictionary<int, string> _generated = new();
    private Process? _cli;
    private readonly Thread _thread;
    private readonly Timer _timer;
    private volatile bool _checking;
    private volatile int _next;

    private ImageRequest? _currentRequest;
    private readonly ILogger<InvokeAIController> _logger;

    public InvokeAIController(ILogger<InvokeAIController> logger, IConfiguration config)
    {
        _logger = logger;
        
        var startInfo = new ProcessStartInfo("cmd.exe", $"/c {config["InvokeAIScript"]!}/invokecli.bat")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            // Set up redirects
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            WorkingDirectory = config["InvokeAIScript"]!
        };


        var thread = new ThreadStart(() =>
        {
            _cli = Process.Start(startInfo);
            if (_cli == null)
            {
                _logger.LogCritical("Failed to start new process");
                return;
            }
            
            _cli.OutputDataReceived += CliOnOutputDataReceived;
            _cli.ErrorDataReceived += CliOnErrorDataReceived;
            _cli.BeginOutputReadLine();
            _cli.BeginErrorReadLine();

            _cli.WaitForExit();
        });
        _thread = new Thread(thread);
        _thread.Start();

        _timer = new Timer(CheckProcess, null, TimeSpan.Zero, TimeSpan.FromSeconds(1));
    }

    private void CliOnErrorDataReceived(object sender, DataReceivedEventArgs e)
    {
        _logger.LogError("ai> {}", e.Data);
    }

    private void CliOnOutputDataReceived(object sender, DataReceivedEventArgs e)
    {
        _logger.LogInformation("ai> {}", e.Data);
        if (e.Data == null || _currentRequest == null)
            return;
        
        var match = PathRegex().Match(e.Data);
        
        if (match.Success)
        {
            var path = match.Groups[1].Value;
            _generated[_currentRequest.Order] = path;
            _currentRequest = null;
        }
    }

    private void CheckProcess(object? state)
    {
        if (_checking || _cli == null)
            return;
        _checking = true;
        
        // no sanitization here!
        if (_currentRequest == null && _requests.TryDequeue(out var request))
        {
            _currentRequest = request;
            
            var stripped = request.Query.ReplaceLineEndings("");
            _cli.StandardInput.Write(stripped);
            _cli.StandardInput.Write('\n');
        }

        _checking = false;
    }

    public int QueueImage(string input)
    {
        var temp = _next + 1;
        _next = temp;
        
        _requests.Enqueue(new ImageRequest(temp, input));
        return temp;
    }

    public async Task<string> WaitForImage(int key)
    {
        while (!_generated.ContainsKey(key))
        {
            await Task.Delay(100);
        }

        return _generated[key];
    }

    public void Dispose()
    {
        _cli?.StandardInput.Close();
        _thread.Join(10000);
        _cli?.Dispose();
        _timer.Dispose();
        
        GC.SuppressFinalize(this);
    }

    [GeneratedRegex("\\] ([A-Za-z\\/\\\\:.0-9]+png)")]
    private static partial Regex PathRegex();
}