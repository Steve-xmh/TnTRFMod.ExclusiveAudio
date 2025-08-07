using System.Collections.Concurrent;
using System.Net;
using System.Text;
using TnTRFMod.ExclusiveAudio.Wasapi;

namespace TnTRFMod.ExclusiveAudio;

public static class HTTPAudioServer
{
    private static Task? _serverTask;
    private static CancellationTokenSource? _cancellationTokenSource;
    private static HttpListener? _listener;
    private static readonly ConcurrentQueue<byte[]> AudioBufferQueue = new();
    private static readonly object QueueLock = new();
    private static int _port = 8090;
    private static bool _isRunning;
    private static WaveFormat? _currentFormat;

    public static void Start()
    {
        if (_isRunning) return;
        _port = ExclusiveAudioPlugin.Instance.ConfigAudioStreamPort.Value;
        if (_port == 0) return;

        _cancellationTokenSource = new CancellationTokenSource();

        Logger.Info($"Starting Audio HTTP Server via port: {_port}");

        _serverTask = Task.Run(() => RunServer(_cancellationTokenSource.Token));

        // 订阅OnAudioData事件
        CriWareEnableExclusiveModePatch.OnAudioData += OnAudioDataHandler;

        _isRunning = true;
    }

    public static void Stop()
    {
        if (!_isRunning) return;

        Logger.Info("Stopping Audio HTTP Server...");

        _cancellationTokenSource?.Cancel();
        CriWareEnableExclusiveModePatch.OnAudioData -= OnAudioDataHandler;

        _listener?.Stop();
        _listener = null;

        lock (QueueLock)
        {
            AudioBufferQueue.Clear();
        }

        _isRunning = false;
    }

    private static void OnAudioDataHandler(CriWareEnableExclusiveModePatch.OnAudioDataArgs args)
    {
        if (args.Data.Length == 0) return;

        // 保存当前音频格式
        _currentFormat = args.Format;

        // 将音频数据添加到队列
        AudioBufferQueue.Enqueue(args.Data);

        // 如果队列太长，删除旧数据
        while (AudioBufferQueue.Count > 100) AudioBufferQueue.TryDequeue(out _);
    }

    private static async Task RunServer(CancellationToken cancellationToken)
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();

            Logger.Message($"Audio HTTP Server has started, access this link to fetch audio stream: http://localhost:{_port}/audio.wav");

            while (!cancellationToken.IsCancellationRequested)
            {
                var context = await _listener.GetContextAsync();
                if (cancellationToken.IsCancellationRequested) break;

                // 处理请求
                HandleRequest(context, cancellationToken);
            }
        }
        catch (HttpListenerException ex)
        {
            Logger.Error($"Audio HTTP Server Error: {ex.Message}");
        }
        catch (OperationCanceledException)
        {
            // 正常取消
        }
        catch (Exception ex)
        {
            Logger.Error($"Audio HTTP Server Unhandled Error: {ex}");
        }
        finally
        {
            _listener?.Close();
        }
    }

    private static async void HandleRequest(HttpListenerContext context, CancellationToken cancellationToken)
    {
        try
        {
            var request = context.Request;
            var response = context.Response;

            // 仅支持GET请求
            if (request.HttpMethod != "GET")
            {
                response.StatusCode = 405;
                response.Close();
                return;
            }

            var requestPath = request.Url?.AbsolutePath ?? "/";

            if (requestPath == "/")
            {
                // 显示简单的信息页面
                SendInfoPage(response);
            }
            else if (requestPath == "/audio.wav")
            {
                // 发送WAV流
                await SendAudioStreamAsync(response, cancellationToken);
            }
            else
            {
                // 404 Not Found
                response.StatusCode = 404;
                response.Close();
            }
        }
        catch (Exception ex)
        {
            Logger.Error($"处理HTTP请求时出错: {ex.Message}");
            try
            {
                context.Response.StatusCode = 500;
                context.Response.Close();
            }
            catch
            {
                // 忽略关闭响应时的错误
            }
        }
    }

    private static void SendInfoPage(HttpListenerResponse response)
    {
        var html = $@"<!DOCTYPE html>
<html>
<head>
    <title>TnTRFMod Exclusive Audio Streaming Server</title>
    <meta charset=""utf-8"">
</head>
<body>
    <h1>TnTRFMod Exclusive Audio Streaming Server</h1>
    <p>Status: {(_isRunning ? "Running" : "Stopped")}</p>
    <p>Audio Stream URL: <a href=""http://localhost:{_port}/audio.wav"">http://localhost:{_port}/audio.wav</a></p>
    <p>Add this URL as media source to OBS to capture/record game audio.</p>
</body>
</html>";

        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }

    private static async Task SendAudioStreamAsync(HttpListenerResponse response, CancellationToken cancellationToken)
    {
        try
        {
            response.ContentType = "audio/wav";
            response.SendChunked = true;
            response.KeepAlive = true;

            await using var outputStream = response.OutputStream;
            // 写入WAV头
            await WriteWavHeaderAsync(outputStream);

            while (_isRunning)
                if (AudioBufferQueue.TryDequeue(out var audioData))
                {
                    await outputStream.WriteAsync(audioData, cancellationToken);
                    await outputStream.FlushAsync(cancellationToken);
                }
                else
                {
                    // 没有数据时短暂等待
                    await Task.Delay(5, cancellationToken);
                }
        }
        catch (HttpListenerException)
        {
            // 客户端可能断开连接，正常处理
        }
        catch (Exception ex)
        {
            Logger.Error($"发送音频流时出错: {ex.Message}");
        }
        finally
        {
            try
            {
                response.Close();
            }
            catch
            {
            }
        }
    }

    private static async Task WriteWavHeaderAsync(Stream stream)
    {
        _currentFormat ??= new WaveFormat
        {
            waveFormatTag = WaveFormatEncoding.Pcm,
            channels = 2,
            sampleRate = 48000,
            bitsPerSample = 16,
            blockAlign = 4, // channels * (bitsPerSample / 8)
            averageBytesPerSecond = 192000 // sampleRate * blockAlign
        };

        // RIFF头
        var header = new byte[44];

        // "RIFF"标识
        header[0] = (byte)'R';
        header[1] = (byte)'I';
        header[2] = (byte)'F';
        header[3] = (byte)'F';

        // 文件大小，未知，设为最大值
        header[4] = 0xFF;
        header[5] = 0xFF;
        header[6] = 0xFF;
        header[7] = 0xFF;

        // "WAVE"格式
        header[8] = (byte)'W';
        header[9] = (byte)'A';
        header[10] = (byte)'V';
        header[11] = (byte)'E';

        // "fmt "子块
        header[12] = (byte)'f';
        header[13] = (byte)'m';
        header[14] = (byte)'t';
        header[15] = (byte)' ';

        // 子块1大小(16字节)
        header[16] = 16;
        header[17] = 0;
        header[18] = 0;
        header[19] = 0;

        // 音频格式(PCM = 1)
        header[20] = 1;
        header[21] = 0;

        // 声道数
        header[22] = (byte)_currentFormat.channels;
        header[23] = 0;

        // 采样率
        var sampleRate = BitConverter.GetBytes(_currentFormat.sampleRate);
        header[24] = sampleRate[0];
        header[25] = sampleRate[1];
        header[26] = sampleRate[2];
        header[27] = sampleRate[3];

        // 字节率 = 采样率 * 块对齐
        var byteRate = BitConverter.GetBytes(_currentFormat.averageBytesPerSecond);
        header[28] = byteRate[0];
        header[29] = byteRate[1];
        header[30] = byteRate[2];
        header[31] = byteRate[3];

        // 块对齐 = 声道数 * 每个样本的字节数
        var blockAlign = BitConverter.GetBytes(_currentFormat.blockAlign);
        header[32] = blockAlign[0];
        header[33] = blockAlign[1];

        // 每个样本的位数
        var bitsPerSample = BitConverter.GetBytes(_currentFormat.bitsPerSample);
        header[34] = bitsPerSample[0];
        header[35] = bitsPerSample[1];

        // "data"子块
        header[36] = (byte)'d';
        header[37] = (byte)'a';
        header[38] = (byte)'t';
        header[39] = (byte)'a';

        // 数据大小，未知，设为最大值
        header[40] = 0xFF;
        header[41] = 0xFF;
        header[42] = 0xFF;
        header[43] = 0xFF;

        await stream.WriteAsync(header, 0, header.Length);
        await stream.FlushAsync();
    }
}