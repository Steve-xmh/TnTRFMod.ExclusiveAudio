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

    private static readonly List<ConcurrentQueue<CriWareEnableExclusiveModePatch.OnAudioDataArgs>> AudioBufferQueues =
        new(1);

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
            AudioBufferQueues.Clear();
        }

        _isRunning = false;
    }

    private static void OnAudioDataHandler(CriWareEnableExclusiveModePatch.OnAudioDataArgs args)
    {
        if (args.Data.Length == 0) return;

        // 只在格式变化时更新音频格式，减少不必要的赋值操作
        if (_currentFormat == null || !_currentFormat.Equals(args.Format)) _currentFormat = args.Format;

        // 将音频数据添加到队列
        lock (QueueLock)
        {
            foreach (var queue in AudioBufferQueues)
                queue.Enqueue(args);
        }
    }

    private static async Task RunServer(CancellationToken cancellationToken)
    {
        try
        {
            _listener = new HttpListener();
            _listener.Prefixes.Add($"http://localhost:{_port}/");
            _listener.Prefixes.Add($"http://127.0.0.1:{_port}/");
            _listener.Start();

            Logger.Message(
                $"Audio HTTP Server has started, access this link to fetch audio stream: http://localhost:{_port}/audio.wav");

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
                Logger.Message($"Sending audio stream to client {context.Request.RemoteEndPoint}");
                await SendAudioStreamAsync(response, cancellationToken);
                Logger.Message($"Client {context.Request.RemoteEndPoint} disconnected");
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
                // ���略关闭响应时的错误
            }
        }
    }

    private static void SendInfoPage(HttpListenerResponse response)
    {
        var html = $"""
                    <!DOCTYPE html>
                    <html>
                    <head>
                        <title>TnTRFMod Exclusive Audio Streaming Server</title>
                        <meta charset="utf-8">
                    </head>
                    <body>
                        <h1>TnTRFMod Exclusive Audio Streaming Server</h1>
                        <p>Status: {(_isRunning ? "Running" : "Stopped")}</p>
                        <p>Audio Stream URL: <a href="http://localhost:{_port}/audio.wav">http://localhost:{_port}/audio.wav</a></p>
                        <p>Add this URL as media source to OBS to capture/record game audio.</p>
                    </body>
                    </html>
                    """;

        var buffer = Encoding.UTF8.GetBytes(html);
        response.ContentType = "text/html; charset=utf-8";
        response.ContentLength64 = buffer.Length;
        response.OutputStream.Write(buffer, 0, buffer.Length);
        response.Close();
    }

    private static async Task SendAudioStreamAsync(HttpListenerResponse response, CancellationToken cancellationToken)
    {
        var queue = new ConcurrentQueue<CriWareEnableExclusiveModePatch.OnAudioDataArgs>();
        lock (QueueLock)
        {
            AudioBufferQueues.Add(queue);
        }

        try
        {
            response.ContentType = "audio/wav";
            response.SendChunked = true;
            response.KeepAlive = true;

            await using var outputStream = response.OutputStream;
            // 写入WAV头
            await WriteWavHeaderAsync(outputStream, cancellationToken);
            var dataChunkHeader = new byte[8];
            var transferLatency = ExclusiveAudioPlugin.Instance.ConfigAudioStreamTransferLatency.Value;

            while (_isRunning)
                if (queue.TryDequeue(out var audioData))
                {
                    if (DateTime.Now.Millisecond - audioData.Timestamp.Milliseconds >
                        transferLatency)
                    {
                        Logger.Warn(
                            $"Audio buffer is empty for over {transferLatency}ms, skipping audio buffer to keep up with current audio...");
                        continue;
                    }

                    // 发送 "data" 区块头
                    dataChunkHeader[0] = (byte)'d';
                    dataChunkHeader[1] = (byte)'a';
                    dataChunkHeader[2] = (byte)'t';
                    dataChunkHeader[3] = (byte)'a';

                    // 数据大小，以 audioData 的大小为准
                    var dataSize = BitConverter.GetBytes(audioData.Data.Length);
                    dataChunkHeader[4] = dataSize[0];
                    dataChunkHeader[5] = dataSize[1];
                    dataChunkHeader[6] = dataSize[2];
                    dataChunkHeader[7] = dataSize[3];

                    // 发送 data 区块头
                    await outputStream.WriteAsync(dataChunkHeader, cancellationToken);

                    // 发送音频数据
                    await outputStream.WriteAsync(audioData.Data, cancellationToken);
                    await outputStream.FlushAsync(cancellationToken);
                }
                else
                {
                    await Task.Yield();
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

            try
            {
                lock (QueueLock)
                {
                    AudioBufferQueues.Remove(queue);
                }
            }
            catch
            {
            }
        }
    }

    private static async Task WriteWavHeaderAsync(Stream stream, CancellationToken cancellationToken)
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
        var header = new byte[36]; // 修改为36字节，去掉data区块

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

        await stream.WriteAsync(header, cancellationToken);
        await stream.FlushAsync(cancellationToken);
    }
}