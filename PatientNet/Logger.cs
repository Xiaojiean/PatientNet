namespace PatientNet
{
    using System.Diagnostics;

    public class Logger : ILogger
    {
        public void Log(string message)
        {
            Debug.WriteLine(message);
        }
    }
}