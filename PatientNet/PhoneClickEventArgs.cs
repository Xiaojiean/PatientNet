using System;

public class PhoneClickEventArgs : EventArgs
{
    public PhoneClickEventArgs(string number_)
    {
        Number = number_;
    }

    public string Number { get; }
}