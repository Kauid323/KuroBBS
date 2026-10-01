using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Media.Core;
using Windows.Media.MediaProperties;
using Windows.Security.Cryptography;
using Windows.Storage.Streams;

namespace KuroBBS.Services
{
    public class HlsSegment
    {
        public int Index { get; set; }
        public string Url { get; set; }
        public double Duration { get; set; }
        public double StartTime { get; set; }
    }

    public class HlsPlaylist
    {
        public string BaseUrl { get; set; }
        public List<HlsSegment> Segments { get; set; }
        public double TotalDuration { get; set; }

        public HlsPlaylist()
        {
            Segments = new List<HlsSegment>();
        }

        public static HlsPlaylist Parse(string m3u8Content, string m3u8Url)
        {
            var playlist = new HlsPlaylist();
            if (string.IsNullOrEmpty(m3u8Content) || string.IsNullOrEmpty(m3u8Url)) return playlist;

            string baseUrl = m3u8Url.Substring(0, m3u8Url.LastIndexOf('/') + 1);
            playlist.BaseUrl = baseUrl;

            var lines = m3u8Content.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            double currentStart = 0;
            double nextDuration = 0;
            int segIndex = 0;

            foreach (var line in lines)
            {
                string trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed)) continue;

                if (trimmed.StartsWith("#EXTINF:", StringComparison.OrdinalIgnoreCase))
                {
                    string durStr = trimmed.Substring(8).Split(',')[0].Trim();
                    double dur;
                    if (double.TryParse(durStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out dur))
                    {
                        nextDuration = dur;
                    }
                    else
                    {
                        nextDuration = 10.0;
                    }
                }
                else if (!trimmed.StartsWith("#"))
                {
                    string segUrl = trimmed;
                    if (!segUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                        !segUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                    {
                        segUrl = baseUrl + segUrl;
                    }

                    var seg = new HlsSegment
                    {
                        Index = segIndex++,
                        Url = segUrl,
                        Duration = nextDuration > 0 ? nextDuration : 10.0,
                        StartTime = currentStart
                    };

                    playlist.Segments.Add(seg);
                    currentStart += seg.Duration;
                    nextDuration = 0;
                }
            }

            playlist.TotalDuration = currentStart;
            return playlist;
        }
    }

    public class MediaSampleItem
    {
        public byte[] Data { get; set; }
        public TimeSpan Pts { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsKeyFrame { get; set; }
    }

    public class TsDemuxer
    {
        public static void DemuxSegment(
            byte[] tsData,
            double baseTimeOffset,
            List<MediaSampleItem> outVideoSamples,
            List<MediaSampleItem> outAudioSamples,
            ref uint videoWidth,
            ref uint videoHeight,
            ref uint audioSampleRate,
            ref uint audioChannels)
        {
            if (tsData == null || tsData.Length < 188 || outVideoSamples == null || outAudioSamples == null) return;

            int pmtPid = -1;
            int videoPid = -1;
            int audioPid = -1;

            var videoPesBuffer = new MemoryStream();
            long currentVideoPts = -1;
            long firstSegVideoPts = -1;
            long lastVideoPts = -1;

            var audioPesBuffer = new MemoryStream();
            long currentAudioPts = -1;
            long firstSegAudioPts = -1;
            long lastAudioPts = -1;

            int i = 0;
            while (i + 188 <= tsData.Length)
            {
                if (tsData[i] != 0x47)
                {
                    i++;
                    continue;
                }

                byte b1 = tsData[i + 1];
                byte b2 = tsData[i + 2];
                byte b3 = tsData[i + 3];

                bool pusi = (b1 & 0x40) != 0;
                int pid = ((b1 & 0x1F) << 8) | b2;
                int afc = (b3 & 0x30) >> 4;

                int payloadOffset = i + 4;
                if (afc == 2 || afc == 3)
                {
                    int afLen = tsData[i + 4];
                    payloadOffset += 1 + afLen;
                    if (afc == 2)
                    {
                        i += 188;
                        continue;
                    }
                }

                if (payloadOffset >= i + 188)
                {
                    i += 188;
                    continue;
                }

                int payloadLen = (i + 188) - payloadOffset;

                // PAT
                if (pid == 0 && pusi)
                {
                    int ptr = tsData[payloadOffset];
                    int tableOffset = payloadOffset + 1 + ptr;
                    if (tableOffset + 11 < i + 188)
                    {
                        pmtPid = ((tsData[tableOffset + 10] & 0x1F) << 8) | tsData[tableOffset + 11];
                    }
                }
                // PMT
                else if (pid == pmtPid && pmtPid != -1 && pusi)
                {
                    int ptr = tsData[payloadOffset];
                    int tableOffset = payloadOffset + 1 + ptr;
                    if (tableOffset + 11 < i + 188)
                    {
                        int sectionLen = ((tsData[tableOffset + 1] & 0x0F) << 8) | tsData[tableOffset + 2];
                        int progInfoLen = ((tsData[tableOffset + 10] & 0x0F) << 8) | tsData[tableOffset + 11];
                        int esOffset = tableOffset + 12 + progInfoLen;
                        int endOffset = tableOffset + 3 + sectionLen - 4;

                        while (esOffset + 5 <= endOffset && esOffset + 5 <= tsData.Length)
                        {
                            byte stype = tsData[esOffset];
                            int epid = ((tsData[esOffset + 1] & 0x1F) << 8) | tsData[esOffset + 2];
                            int eiLen = ((tsData[esOffset + 3] & 0x0F) << 8) | tsData[esOffset + 4];

                            if (stype == 0x1B) // H.264
                            {
                                videoPid = epid;
                            }
                            else if (stype == 0x0F) // AAC
                            {
                                audioPid = epid;
                            }
                            esOffset += 5 + eiLen;
                        }
                    }
                }
                // Video PES
                else if (pid == videoPid && videoPid != -1)
                {
                    if (pusi)
                    {
                        if (videoPesBuffer.Length > 0 && currentVideoPts >= 0)
                        {
                            FlushVideoPes(videoPesBuffer.ToArray(), currentVideoPts, firstSegVideoPts, baseTimeOffset, outVideoSamples, ref videoWidth, ref videoHeight);
                        }
                        videoPesBuffer.SetLength(0);

                        if (payloadLen >= 9 && tsData[payloadOffset] == 0 && tsData[payloadOffset + 1] == 0 && tsData[payloadOffset + 2] == 1)
                        {
                            byte ptsDtsFlags = (byte)((tsData[payloadOffset + 7] & 0xC0) >> 6);
                            int pesHeaderDataLen = tsData[payloadOffset + 8];
                            int pesDataOffset = payloadOffset + 9 + pesHeaderDataLen;

                            currentVideoPts = -1;

                            if ((ptsDtsFlags & 0x02) != 0 && payloadOffset + 13 < tsData.Length)
                            {
                                currentVideoPts = ParsePts(tsData, payloadOffset + 9);
                                if (firstSegVideoPts < 0) firstSegVideoPts = currentVideoPts;
                                lastVideoPts = currentVideoPts;
                            }
                            else if (lastVideoPts >= 0)
                            {
                                currentVideoPts = lastVideoPts + 3600; // ~40ms default
                                lastVideoPts = currentVideoPts;
                            }

                            if (pesDataOffset < i + 188)
                            {
                                videoPesBuffer.Write(tsData, pesDataOffset, (i + 188) - pesDataOffset);
                            }
                        }
                    }
                    else
                    {
                        videoPesBuffer.Write(tsData, payloadOffset, payloadLen);
                    }
                }
                // Audio PES
                else if (pid == audioPid && audioPid != -1)
                {
                    if (pusi)
                    {
                        if (audioPesBuffer.Length > 0 && currentAudioPts >= 0)
                        {
                            FlushAudioPes(audioPesBuffer.ToArray(), currentAudioPts, firstSegAudioPts, baseTimeOffset, outAudioSamples, ref audioSampleRate, ref audioChannels);
                        }
                        audioPesBuffer.SetLength(0);

                        if (payloadLen >= 9 && tsData[payloadOffset] == 0 && tsData[payloadOffset + 1] == 0 && tsData[payloadOffset + 2] == 1)
                        {
                            byte ptsDtsFlags = (byte)((tsData[payloadOffset + 7] & 0xC0) >> 6);
                            int pesHeaderDataLen = tsData[payloadOffset + 8];
                            int pesDataOffset = payloadOffset + 9 + pesHeaderDataLen;

                            currentAudioPts = -1;
                            if ((ptsDtsFlags & 0x02) != 0 && payloadOffset + 13 < tsData.Length)
                            {
                                currentAudioPts = ParsePts(tsData, payloadOffset + 9);
                                if (firstSegAudioPts < 0) firstSegAudioPts = currentAudioPts;
                                lastAudioPts = currentAudioPts;
                            }
                            else if (lastAudioPts >= 0)
                            {
                                currentAudioPts = lastAudioPts + 2088; // ~23.2ms
                                lastAudioPts = currentAudioPts;
                            }

                            if (pesDataOffset < i + 188)
                            {
                                audioPesBuffer.Write(tsData, pesDataOffset, (i + 188) - pesDataOffset);
                            }
                        }
                    }
                    else
                    {
                        audioPesBuffer.Write(tsData, payloadOffset, payloadLen);
                    }
                }

                i += 188;
            }

            // Flush final PES packets
            if (videoPesBuffer.Length > 0 && currentVideoPts >= 0)
            {
                FlushVideoPes(videoPesBuffer.ToArray(), currentVideoPts, firstSegVideoPts, baseTimeOffset, outVideoSamples, ref videoWidth, ref videoHeight);
            }
            if (audioPesBuffer.Length > 0 && currentAudioPts >= 0)
            {
                FlushAudioPes(audioPesBuffer.ToArray(), currentAudioPts, firstSegAudioPts, baseTimeOffset, outAudioSamples, ref audioSampleRate, ref audioChannels);
            }
        }

        private static long ParsePts(byte[] data, int offset)
        {
            long b0 = data[offset];
            long b1 = data[offset + 1];
            long b2 = data[offset + 2];
            long b3 = data[offset + 3];
            long b4 = data[offset + 4];

            return ((b0 & 0x0E) << 29) |
                   ((b1 & 0xFF) << 22) |
                   ((b2 & 0xFE) << 14) |
                   ((b3 & 0xFF) << 7) |
                   ((b4 & 0xFE) >> 1);
        }

        private static void FlushVideoPes(
            byte[] pesData,
            long ptsTicks,
            long firstSegPts,
            double baseTimeOffset,
            List<MediaSampleItem> outSamples,
            ref uint videoWidth,
            ref uint videoHeight)
        {
            if (pesData == null || pesData.Length == 0 || outSamples == null) return;

            bool isKeyFrame = false;
            // Check NAL units
            for (int k = 0; k + 4 < pesData.Length; k++)
            {
                if (pesData[k] == 0 && pesData[k + 1] == 0 && (pesData[k + 2] == 1 || (pesData[k + 2] == 0 && pesData[k + 3] == 1)))
                {
                    int nalOffset = (pesData[k + 2] == 1) ? k + 3 : k + 4;
                    if (nalOffset < pesData.Length)
                    {
                        int nalType = pesData[nalOffset] & 0x1F;
                        if (nalType == 5 || nalType == 7) // IDR or SPS
                        {
                            isKeyFrame = true;
                            break;
                        }
                    }
                }
            }

            double ptsSeconds = baseTimeOffset;
            if (firstSegPts >= 0 && ptsTicks >= firstSegPts)
            {
                ptsSeconds = baseTimeOffset + (ptsTicks - firstSegPts) / 90000.0;
            }

            var sampleTime = TimeSpan.FromSeconds(Math.Max(0, ptsSeconds));

            outSamples.Add(new MediaSampleItem
            {
                Data = pesData,
                Pts = sampleTime,
                Duration = TimeSpan.FromMilliseconds(40), // ~25 fps default
                IsKeyFrame = isKeyFrame
            });
        }

        private static void FlushAudioPes(
            byte[] pesData,
            long ptsTicks,
            long firstSegPts,
            double baseTimeOffset,
            List<MediaSampleItem> outSamples,
            ref uint audioSampleRate,
            ref uint audioChannels)
        {
            if (pesData == null || pesData.Length < 7 || outSamples == null) return;

            double ptsSeconds = baseTimeOffset;
            if (firstSegPts >= 0 && ptsTicks >= firstSegPts)
            {
                ptsSeconds = baseTimeOffset + (ptsTicks - firstSegPts) / 90000.0;
            }

            int offset = 0;

            // Extract ADTS frames
            while (offset + 7 <= pesData.Length)
            {
                if ((pesData[offset] == 0xFF) && ((pesData[offset + 1] & 0xF0) == 0xF0))
                {
                    int srIdx = (pesData[offset + 2] & 0x3C) >> 2;
                    int ch = ((pesData[offset + 2] & 0x01) << 2) | ((pesData[offset + 3] & 0xC0) >> 6);

                    uint detectedRate = 44100;
                    switch (srIdx)
                    {
                        case 0: detectedRate = 96000; break;
                        case 1: detectedRate = 88200; break;
                        case 2: detectedRate = 64000; break;
                        case 3: detectedRate = 48000; break;
                        case 4: detectedRate = 44100; break;
                        case 5: detectedRate = 32000; break;
                        case 6: detectedRate = 24000; break;
                        case 7: detectedRate = 22050; break;
                        case 8: detectedRate = 16000; break;
                    }

                    if (detectedRate > 0) audioSampleRate = detectedRate;
                    if (ch > 0) audioChannels = (uint)ch;

                    int frameLen = ((pesData[offset + 3] & 0x03) << 11) |
                                   (pesData[offset + 4] << 3) |
                                   ((pesData[offset + 5] & 0xE0) >> 5);

                    if (frameLen < 7 || offset + frameLen > pesData.Length)
                    {
                        break;
                    }

                    byte[] frameData = new byte[frameLen];
                    System.Buffer.BlockCopy(pesData, offset, frameData, 0, frameLen);

                    double frameDurationSec = 1024.0 / (audioSampleRate > 0 ? audioSampleRate : 44100.0);

                    outSamples.Add(new MediaSampleItem
                    {
                        Data = frameData,
                        Pts = TimeSpan.FromSeconds(Math.Max(0, ptsSeconds)),
                        Duration = TimeSpan.FromSeconds(frameDurationSec)
                    });

                    ptsSeconds += frameDurationSec;
                    offset += frameLen;
                }
                else
                {
                    offset++;
                }
            }
        }
    }

    public class KuroHlsMediaStreamSource : IDisposable
    {
        private HlsPlaylist _playlist;
        private MediaStreamSource _mss;
        private VideoStreamDescriptor _videoDesc;
        private AudioStreamDescriptor _audioDesc;

        private List<MediaSampleItem> _videoQueue = new List<MediaSampleItem>();
        private List<MediaSampleItem> _audioQueue = new List<MediaSampleItem>();
        private object _lock = new object();

        private int _currentSegmentIndex = 0;
        private bool _isDownloading = false;
        private bool _isDisposed = false;
        private uint _videoWidth = 1280;
        private uint _videoHeight = 720;
        private uint _audioSampleRate = 44100;
        private uint _audioChannels = 2;
        private TimeSpan _currentPosition = TimeSpan.Zero;
        private CancellationTokenSource _downloadCts;

        private MediaStreamSourceSampleRequestDeferral _videoDeferral;
        private MediaStreamSourceSampleRequest _pendingVideoRequest;
        private MediaStreamSourceSampleRequestDeferral _audioDeferral;
        private MediaStreamSourceSampleRequest _pendingAudioRequest;

        public MediaStreamSource MediaStreamSource
        {
            get { return _mss; }
        }

        public static async Task<KuroHlsMediaStreamSource> CreateAsync(string m3u8Url, uint width = 1280, uint height = 720)
        {
            try
            {
                KuroLogger.Loading("HLS_FETCH", "Fetching playlist: " + m3u8Url);

                using (var http = new HttpClient())
                {
                    http.DefaultRequestHeaders.Add("Origin", "https://www.kurobbs.com");
                    http.DefaultRequestHeaders.Add("Referer", "https://www.kurobbs.com/");

                    string m3u8Content = await http.GetStringAsync(m3u8Url);
                    var playlist = HlsPlaylist.Parse(m3u8Content, m3u8Url);
                    KuroLogger.Loading("HLS_PARSED", string.Format("Parsed {0} segments, TotalDuration={1:F1}s", playlist.Segments.Count, playlist.TotalDuration));

                    var source = new KuroHlsMediaStreamSource();
                    source._videoWidth = width > 0 ? width : 1280;
                    source._videoHeight = height > 0 ? height : 720;
                    source._playlist = playlist;

                    // Pre-buffer segment 0 before creating MediaStreamSource
                    if (playlist.Segments.Count > 0)
                    {
                        var seg0 = playlist.Segments[0];
                        KuroLogger.Loading("HLS_PRELOAD", "Downloading segment 0: " + seg0.Url);
                        byte[] seg0Data = await http.GetByteArrayAsync(seg0.Url);
                        if (seg0Data != null && seg0Data.Length > 0)
                        {
                            TsDemuxer.DemuxSegment(
                                seg0Data,
                                seg0.StartTime,
                                source._videoQueue,
                                source._audioQueue,
                                ref source._videoWidth,
                                ref source._videoHeight,
                                ref source._audioSampleRate,
                                ref source._audioChannels);
                            source._currentSegmentIndex = 1;

                            KuroLogger.Loading("TS_DEMUX_OK", string.Format("Segment 0 demuxed: VideoSamples={0}, AudioSamples={1}, W={2}, H={3}, AudioRate={4}Hz, Channels={5}",
                                source._videoQueue.Count, source._audioQueue.Count, source._videoWidth, source._videoHeight, source._audioSampleRate, source._audioChannels));
                        }
                    }

                    source.InitMediaStreamSource();
                    return source;
                }
            }
            catch (Exception ex)
            {
                KuroLogger.Error("HLS_CREATE_FAIL", "Error in KuroHlsMediaStreamSource.CreateAsync: " + ex.Message, ex);
                throw;
            }
        }

        private void InitMediaStreamSource()
        {
            var videoProps = VideoEncodingProperties.CreateH264();
            videoProps.Width = _videoWidth > 0 ? _videoWidth : 1280;
            videoProps.Height = _videoHeight > 0 ? _videoHeight : 720;
            _videoDesc = new VideoStreamDescriptor(videoProps);

            var audioProps = AudioEncodingProperties.CreateAacAdts(
                _audioSampleRate > 0 ? _audioSampleRate : 44100,
                _audioChannels > 0 ? _audioChannels : 2,
                128000);
            _audioDesc = new AudioStreamDescriptor(audioProps);

            _mss = new MediaStreamSource(_videoDesc, _audioDesc);
            _mss.CanSeek = true;
            _mss.Duration = TimeSpan.FromSeconds(_playlist.TotalDuration > 0 ? _playlist.TotalDuration : 10);
            _mss.Starting += Mss_Starting;
            _mss.SampleRequested += Mss_SampleRequested;
            _mss.Closed += Mss_Closed;

            KuroLogger.Loading("MSS_INIT_OK", string.Format("MediaStreamSource ready: Duration={0}, Video={1}x{2}, Audio={3}Hz",
                _mss.Duration, _videoWidth, _videoHeight, _audioSampleRate));
        }

        private void Mss_Starting(MediaStreamSource sender, MediaStreamSourceStartingEventArgs args)
        {
            var req = args.Request;
            if (req.StartPosition.HasValue && req.StartPosition.Value > TimeSpan.Zero)
            {
                var target = req.StartPosition.Value;
                var actualStart = SeekTo(target);
                req.SetActualStartPosition(actualStart);
                KuroLogger.Loading("MSS_START_SEEK", string.Format("Starting from Seek Target={0}, ActualStart={1}", target, actualStart));
            }
            else
            {
                req.SetActualStartPosition(TimeSpan.Zero);
                if (_downloadCts == null)
                {
                    _downloadCts = new CancellationTokenSource();
                }
                StartDownloadLoop(_downloadCts.Token);
                KuroLogger.Loading("MSS_START_HEAD", "Starting from head (00:00)");
            }
        }

        public TimeSpan SeekTo(TimeSpan position)
        {
            TimeSpan actualStart = TimeSpan.Zero;
            lock (_lock)
            {
                if (_downloadCts != null)
                {
                    try { _downloadCts.Cancel(); _downloadCts.Dispose(); } catch { }
                    _downloadCts = null;
                }
                _downloadCts = new CancellationTokenSource();

                _videoQueue.Clear();
                _audioQueue.Clear();

                double targetSec = position.TotalSeconds;
                int segIndex = 0;
                for (int i = 0; i < _playlist.Segments.Count; i++)
                {
                    if (_playlist.Segments[i].StartTime <= targetSec &&
                        _playlist.Segments[i].StartTime + _playlist.Segments[i].Duration > targetSec)
                    {
                        segIndex = i;
                        break;
                    }
                }

                _currentSegmentIndex = segIndex;
                _currentPosition = position;
                if (segIndex < _playlist.Segments.Count)
                {
                    actualStart = TimeSpan.FromSeconds(_playlist.Segments[segIndex].StartTime);
                }
            }

            StartDownloadLoop(_downloadCts.Token);
            return actualStart;
        }

        private void Mss_SampleRequested(MediaStreamSource sender, MediaStreamSourceSampleRequestedEventArgs args)
        {
            var req = args.Request;
            if (_isDisposed) return;

            lock (_lock)
            {
                if (req.StreamDescriptor is VideoStreamDescriptor)
                {
                    if (_videoQueue.Count > 0)
                    {
                        var item = _videoQueue[0];
                        _videoQueue.RemoveAt(0);

                        var buf = CryptographicBuffer.CreateFromByteArray(item.Data);
                        var sample = MediaStreamSample.CreateFromBuffer(buf, item.Pts);
                        sample.Duration = item.Duration;
                        sample.KeyFrame = item.IsKeyFrame;
                        req.Sample = sample;
                    }
                    else
                    {
                        if (_currentSegmentIndex >= _playlist.Segments.Count)
                        {
                            req.Sample = null;
                        }
                        else
                        {
                            _pendingVideoRequest = req;
                            _videoDeferral = req.GetDeferral();
                        }
                    }
                }
                else if (req.StreamDescriptor is AudioStreamDescriptor)
                {
                    if (_audioQueue.Count > 0)
                    {
                        var item = _audioQueue[0];
                        _audioQueue.RemoveAt(0);

                        var buf = CryptographicBuffer.CreateFromByteArray(item.Data);
                        var sample = MediaStreamSample.CreateFromBuffer(buf, item.Pts);
                        sample.Duration = item.Duration;
                        req.Sample = sample;
                    }
                    else
                    {
                        if (_currentSegmentIndex >= _playlist.Segments.Count)
                        {
                            req.Sample = null;
                        }
                        else
                        {
                            _pendingAudioRequest = req;
                            _audioDeferral = req.GetDeferral();
                        }
                    }
                }
            }

            if (_videoQueue.Count < 20 || _audioQueue.Count < 20)
            {
                if (_downloadCts != null && !_downloadCts.IsCancellationRequested)
                {
                    StartDownloadLoop(_downloadCts.Token);
                }
            }
        }

        private async void StartDownloadLoop(CancellationToken token)
        {
            if (_isDownloading || _isDisposed || token.IsCancellationRequested) return;
            _isDownloading = true;

            try
            {
                while (!_isDisposed && !token.IsCancellationRequested && _currentSegmentIndex < _playlist.Segments.Count)
                {
                    lock (_lock)
                    {
                        if (_videoQueue.Count >= 60 && _audioQueue.Count >= 60)
                        {
                            break;
                        }
                    }

                    int idx = _currentSegmentIndex;
                    var seg = _playlist.Segments[idx];

                    byte[] segData = null;
                    using (var http = new HttpClient())
                    {
                        http.DefaultRequestHeaders.Add("Origin", "https://www.kurobbs.com");
                        http.DefaultRequestHeaders.Add("Referer", "https://www.kurobbs.com/");

                        using (var response = await http.GetAsync(seg.Url, HttpCompletionOption.ResponseContentRead, token))
                        {
                            response.EnsureSuccessStatusCode();
                            segData = await response.Content.ReadAsByteArrayAsync();
                        }
                    }

                    if (token.IsCancellationRequested || _isDisposed) break;

                    if (segData != null && segData.Length > 0)
                    {
                        var newVideo = new List<MediaSampleItem>();
                        var newAudio = new List<MediaSampleItem>();

                        TsDemuxer.DemuxSegment(
                            segData,
                            seg.StartTime,
                            newVideo,
                            newAudio,
                            ref _videoWidth,
                            ref _videoHeight,
                            ref _audioSampleRate,
                            ref _audioChannels);

                        if (token.IsCancellationRequested || _isDisposed) break;

                        lock (_lock)
                        {
                            _videoQueue.AddRange(newVideo);
                            _audioQueue.AddRange(newAudio);
                            _currentSegmentIndex++;

                            // Complete video deferrals if any
                            if (_videoDeferral != null && _videoQueue.Count > 0)
                            {
                                var item = _videoQueue[0];
                                _videoQueue.RemoveAt(0);

                                var buf = CryptographicBuffer.CreateFromByteArray(item.Data);
                                var sample = MediaStreamSample.CreateFromBuffer(buf, item.Pts);
                                sample.Duration = item.Duration;
                                sample.KeyFrame = item.IsKeyFrame;
                                _pendingVideoRequest.Sample = sample;

                                var d = _videoDeferral;
                                _videoDeferral = null;
                                _pendingVideoRequest = null;
                                d.Complete();
                            }

                            // Complete audio deferrals if any
                            if (_audioDeferral != null && _audioQueue.Count > 0)
                            {
                                var item = _audioQueue[0];
                                _audioQueue.RemoveAt(0);

                                var buf = CryptographicBuffer.CreateFromByteArray(item.Data);
                                var sample = MediaStreamSample.CreateFromBuffer(buf, item.Pts);
                                sample.Duration = item.Duration;
                                _pendingAudioRequest.Sample = sample;

                                var d = _audioDeferral;
                                _audioDeferral = null;
                                _pendingAudioRequest = null;
                                d.Complete();
                            }
                        }
                    }
                }

                // If finished and queues empty, complete any remaining deferrals with null
                lock (_lock)
                {
                    if (!token.IsCancellationRequested && _currentSegmentIndex >= _playlist.Segments.Count)
                    {
                        if (_videoDeferral != null && _videoQueue.Count == 0)
                        {
                            _pendingVideoRequest.Sample = null;
                            var d = _videoDeferral;
                            _videoDeferral = null;
                            _pendingVideoRequest = null;
                            d.Complete();
                        }
                        if (_audioDeferral != null && _audioQueue.Count == 0)
                        {
                            _pendingAudioRequest.Sample = null;
                            var d = _audioDeferral;
                            _audioDeferral = null;
                            _pendingAudioRequest = null;
                            d.Complete();
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Clean cancellation on seek
            }
            catch (Exception ex)
            {
                KuroLogger.Warn("HLS_DOWNLOAD_ERR", "Segment download error: " + ex.Message);
            }
            finally
            {
                _isDownloading = false;
            }
        }

        private void Mss_Closed(MediaStreamSource sender, MediaStreamSourceClosedEventArgs args)
        {
            KuroLogger.Loading("MSS_CLOSED", "MediaStreamSource closed");
            Dispose();
        }

        public void Dispose()
        {
            _isDisposed = true;
            lock (_lock)
            {
                if (_downloadCts != null)
                {
                    try { _downloadCts.Cancel(); _downloadCts.Dispose(); } catch { }
                    _downloadCts = null;
                }

                _videoQueue.Clear();
                _audioQueue.Clear();

                if (_videoDeferral != null)
                {
                    try { _videoDeferral.Complete(); } catch { }
                    _videoDeferral = null;
                    _pendingVideoRequest = null;
                }
                if (_audioDeferral != null)
                {
                    try { _audioDeferral.Complete(); } catch { }
                    _audioDeferral = null;
                    _pendingAudioRequest = null;
                }
            }
        }
    }
}
