using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

namespace NasInterface.Python
{
    public enum RunnerType
    {
        Encoder,
        Decoder
    }

    public class PyRunner
    {
        public event EventHandler<ProcessCompletionEventArgs> ProcessCompleted;

        public async Task RunAsync(RunnerType runnerType, string filepath)
        {
            string projectDirectory = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
            string scriptsDirectory = Path.Combine(projectDirectory, "Python");
            string scriptPath = scriptsDirectory;

            switch (runnerType)
            {
                case RunnerType.Encoder:
                    scriptPath = Path.Combine(scriptsDirectory, "encoder.py");
                    break;
                case RunnerType.Decoder:
                    scriptPath = Path.Combine(scriptsDirectory, "decoder.py");
                    break;
                default:
                    break;
            }

            string arguments = $"\"{scriptPath}\" \"{filepath}\" \"{Path.Combine(projectDirectory, "Temp")}\"";

            ProcessStartInfo start = new ProcessStartInfo();
            start.FileName = "python"; 
            start.Arguments = arguments;
            start.UseShellExecute = false;
            start.CreateNoWindow = true; 
            start.RedirectStandardOutput = true;
            start.RedirectStandardError = true;

            using (Process process = Process.Start(start))
            {
                Task<string> outputTask = process.StandardOutput.ReadToEndAsync();
                Task<string> errorTask = process.StandardError.ReadToEndAsync();

                string output = await outputTask;
                string errors = await errorTask;

                OnProcessCompleted(new ProcessCompletionEventArgs(output, errors));
            }
        }

        protected virtual void OnProcessCompleted(ProcessCompletionEventArgs e)
        {
            ProcessCompleted?.Invoke(this, e);
        }
    }

    public class ProcessCompletionEventArgs : EventArgs
    {
        public string Output { get; }
        public string Errors { get; }

        public ProcessCompletionEventArgs(string output, string errors)
        {
            Output = output;
            Errors = errors;
        }
    }
}
