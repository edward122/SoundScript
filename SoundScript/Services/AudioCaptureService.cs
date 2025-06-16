using System;
using System.Collections.Generic;
using System.IO;
using NAudio.Wave;

namespace SoundScript.Services
{
    public class AudioCaptureService : IDisposable
    {
        private WaveInEvent? _waveIn;
        private MemoryStream? _audioStream;
        private bool _isRecording;
        
        public event EventHandler<byte[]>? AudioDataReceived;
        public event EventHandler? RecordingStarted;
        public event EventHandler? RecordingStopped;
        
        public bool IsRecording => _isRecording;
        
        // 16 kHz mono PCM as specified in requirements
        private readonly WaveFormat _waveFormat = new WaveFormat(16000, 16, 1);

        public void StartRecording()
        {
            if (_isRecording) return;

            try
            {
                _audioStream = new MemoryStream();
                
                _waveIn = new WaveInEvent
                {
                    WaveFormat = _waveFormat,
                    BufferMilliseconds = 100 // Small buffer for responsiveness
                };
                
                _waveIn.DataAvailable += OnDataAvailable;
                _waveIn.RecordingStopped += OnRecordingStopped;
                
                _waveIn.StartRecording();
                _isRecording = true;
                
                RecordingStarted?.Invoke(this, EventArgs.Empty);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to start recording: {ex.Message}", ex);
            }
        }

        public byte[] StopRecording()
        {
            if (!_isRecording) return Array.Empty<byte>();

            _waveIn?.StopRecording();
            _isRecording = false;
            
            var audioData = _audioStream?.ToArray() ?? Array.Empty<byte>();
            
            _audioStream?.Dispose();
            _audioStream = null;
            
            RecordingStopped?.Invoke(this, EventArgs.Empty);
            
            return audioData;
        }

        private void OnDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_audioStream != null && e.BytesRecorded > 0)
            {
                _audioStream.Write(e.Buffer, 0, e.BytesRecorded);
                
                // Emit audio data for real-time processing if needed
                var audioChunk = new byte[e.BytesRecorded];
                Array.Copy(e.Buffer, audioChunk, e.BytesRecorded);
                AudioDataReceived?.Invoke(this, audioChunk);
            }
        }

        private void OnRecordingStopped(object? sender, StoppedEventArgs e)
        {
            _isRecording = false;
            
            if (e.Exception != null)
            {
                throw new InvalidOperationException($"Recording stopped with error: {e.Exception.Message}", e.Exception);
            }
        }

        public byte[] ConvertToWav(byte[] rawAudio)
        {
            using var memoryStream = new MemoryStream();
            using var writer = new WaveFileWriter(memoryStream, _waveFormat);
            
            writer.Write(rawAudio, 0, rawAudio.Length);
            writer.Flush();
            
            return memoryStream.ToArray();
        }

        public void Dispose()
        {
            if (_isRecording)
            {
                StopRecording();
            }
            
            _waveIn?.Dispose();
            _audioStream?.Dispose();
        }
    }
} 