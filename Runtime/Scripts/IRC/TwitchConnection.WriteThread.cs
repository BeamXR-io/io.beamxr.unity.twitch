using System;
using UnityEngine;
using System.Threading;
using System.Collections.Concurrent;
using BeamXR.Streaming.Core;

namespace BeamXR.Streaming.Twitch
{
    internal partial class TwitchConnection
    {
        private ConcurrentQueue<string> priorityWriteQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<string> writeQueue = new ConcurrentQueue<string>();
        private ConcurrentQueue<DateTime> writeTimestamps = new ConcurrentQueue<DateTime>();

        private void WriteThreadLoop()
        {
            BeamLogger.LogDev($"{IRCMessageTag.Thread()} Write thread started");

            var stream = tcpClient.GetStream();
            RateLimit currentRateLimit;

            while (ThreadsRunning)
            {
                // Process priority write queue (no ratelimiting)
                while (!priorityWriteQueue.IsEmpty)
                {
                    if (priorityWriteQueue.TryDequeue(out string message))
                    {
                        stream.WriteLine(message, showIRCDebug);
                    }
                }

                // Get the current rate limit
                lock (rateLimitLock)
                    currentRateLimit = rateLimit;

                // Remove old timestamps (ratelimiting)
                var minTime = DateTime.Now - rateLimit.timeSpan;
                while (writeTimestamps.TryPeek(out var timestamp) && timestamp < minTime)
                    writeTimestamps.TryDequeue(out _);

                // Process the write queue (with IRC ratelimiting in mind)
                while (!writeQueue.IsEmpty && writeTimestamps.Count < rateLimit.count)
                {
                    if (writeQueue.TryDequeue(out var message))
                    {
                        stream.WriteLine(message, showIRCDebug);
                        writeTimestamps.Enqueue(DateTime.Now);
                    }
                }

                // Sleep to prevent high CPU usage
                Thread.Sleep(writeInterval);

            }

            BeamLogger.LogDev($"{IRCMessageTag.Thread()} Write thread stopped");
        }

        public void SendCommand(string command, bool priority = false)
        {
            if (priority)
                priorityWriteQueue.Enqueue(command);
            else
                writeQueue.Enqueue(command);
        }

        /// <summary>
        /// Sends a chat message to the channel.
        /// </summary>
        public void SendChatMessage(string message)
        {
            if (message.Length <= 0)
            {
                BeamLogger.LogWarning($"{IRCMessageTag.Write()} Tried sending an empty chat message");
                return;
            }

            // Place the chat message into the write queue
            SendCommand("PRIVMSG #" + channel + " :" + message);
        }
    }
}
