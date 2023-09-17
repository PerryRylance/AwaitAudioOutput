using System;
using System.Collections.Generic;
using System.Threading;
using CommandLine.Text;
using CommandLine;
using NAudio.Wave;

namespace AwaitAudioOutput
{
    internal class Options
    {
        [Option('w', "wait", Required = false, Default = 20, HelpText = "Time to wait for audio output, in seconds.")]
        public int wait { get; set; }

        [Option('t', "threshold", Required = false, Default = 4, HelpText = "Absolute amplitude threshold for positive detection, between 0 - 32767.")]
        public int threshold { get; set; }
    }
    internal class Program
    {
        private static WasapiLoopbackCapture capture;
        private static DateTime recordingStarted;

        private static int secondsToWait = 20;
        private static int amplitudeThreshold = 4;

        private static bool audioDetected = false;

        static void Main(string[] args)
        {
            CommandLine.Parser.Default.ParseArguments<Options>(args).MapResult(
                (Options opts) => RunWithOptions(opts), //in case parser sucess
                (IEnumerable<Error> errs) => HandleParseError(errs)
            );
        }

        private static int RunWithOptions(Options opts)
        {
            secondsToWait = opts.wait;
            amplitudeThreshold = opts.threshold;

            StartRecording();

            while (
                capture.CaptureState != NAudio.CoreAudioApi.CaptureState.Stopped
                &&
                GetRecordedSeconds() < secondsToWait
                &&
                !audioDetected
            )
            {
                Thread.Sleep(1000);
            }

            Console.Write(audioDetected ? "1" : "0");

            return 0;
        }

        private static int HandleParseError(IEnumerable<Error> errors)
        {
            foreach (Error error in errors)
                Console.WriteLine(error.ToString());

            return 1;
        }

        private static void StartRecording()
        {
            capture = new WasapiLoopbackCapture();
            capture.WaveFormat = new WaveFormat(); // NB: Ensure 16-bit

            capture.DataAvailable += (s, a) =>
            {
                if (a.Buffer.Length % 2 != 0)
                    throw new Exception("Expected an even buffer length");

                short value;

                for (int i = 0; i < a.Buffer.Length; i += 2)
                {
                    value = BitConverter.ToInt16(a.Buffer, i);

                    if (Math.Abs(value) >= amplitudeThreshold)
                    {
                        audioDetected = true;
                        capture.StopRecording();

                        break;
                    }
                        
                }
            };

            capture.RecordingStopped += (s, a) =>
            {
                capture.Dispose();
            };

            capture.StartRecording();

            recordingStarted = DateTime.Now;
        }

        private static double GetRecordedSeconds()
        {
            DateTime now = DateTime.Now;
            long elapsedTicks = now.Ticks - recordingStarted.Ticks;
            TimeSpan elapsedSpan = new TimeSpan(elapsedTicks);

            return elapsedSpan.TotalSeconds;
        }
    }
}
