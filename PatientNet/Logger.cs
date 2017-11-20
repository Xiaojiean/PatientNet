namespace PatientNet
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using Windows.Storage;

    public class Logger : ILogger
    {
        private StorageFile logFile;

        public Logger(string filename)
        {
            if (string.IsNullOrEmpty(filename))
            {
                throw new ArgumentNullException(nameof(filename));
            }

            LoadLogFile(filename);
        }

        public async void Log(string message)
        {
            Debug.WriteLine(message);

            try
            {
                await FileIO.WriteTextAsync(this.logFile, message);
            }
            catch
            {
                Debug.WriteLine("Could not write to log file...");
            }
        }

        private async void LoadLogFile(string filename)
        {
            StorageFolder storageFolder = ApplicationData.Current.LocalFolder;
            this.logFile = await storageFolder.CreateFileAsync(filename, CreationCollisionOption.ReplaceExisting);
        }
    }
}