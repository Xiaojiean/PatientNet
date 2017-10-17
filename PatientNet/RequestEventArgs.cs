namespace PatientNet
{
    using System;
    using System.Collections.Generic;

    public class RequestEventArgs : EventArgs
    {
        public RequestEventArgs(HashSet<string> set_, string content_)
        {
            this.Set = set_;
            this.Content = content_;
        }

        public HashSet<string> Set { get; }

        public string Content { get; }
    }
}