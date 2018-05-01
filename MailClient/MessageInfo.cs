using System.Collections.Generic;
using System.IO;

namespace MailClient
{
    class MessageInfo
    {
        public readonly string Account;
        public readonly string Password;
        public readonly IEnumerable<string> receivers;
        public readonly string Subject;
        public readonly string text;
        public readonly IEnumerable<FileInfo> Files;

        public MessageInfo(string account, string password, IEnumerable<string> receivers, string subject, string text, IEnumerable<FileInfo> files)
        {
            Account = account;
            Password = password;
            this.receivers = receivers;
            this.text = text;
            Files = files;
            Subject = subject;
        }
    }
}
