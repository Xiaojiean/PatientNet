namespace PatientNet
{
    using System;

    public class EnterEventArgs : EventArgs
    {
        public EnterEventArgs(MessageType type_)
        {
            this.Type = type_;
        }

        public MessageType Type { get; }
    }
}