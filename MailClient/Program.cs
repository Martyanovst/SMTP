using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;

namespace MailClient
{
    class Program
    {
        private static readonly Encoding encoding = Encoding.GetEncoding("windows-1251");
        private const string configFileName = "config.txt";
        private const string textFileName = "text.txt";
        private static readonly Regex receiverPattern = new Regex("receivers: ((\".+?\" ?)+)", RegexOptions.Compiled);
        private static readonly Regex filesPattern = new Regex("files: ((\".+?\" ?)*)", RegexOptions.Compiled);
        private static readonly Regex subjectPattern = new Regex("subject: ((\".+?\")+)", RegexOptions.Compiled);

        static void Main(string[] args)
        {
            Console.WriteLine("Enter your account mail.ru:");
            var account = Console.ReadLine();

            Console.WriteLine("Enter your password:");
           var password = Console.ReadLine();

            using (var sslStream = new SslStream(new TcpClient("smtp.mail.ru", 465).GetStream(), false,
                ValidateServerCertificate, null))
            {
                sslStream.AuthenticateAsClient("smtp.mail.ru");

                var data = new byte[1024];
                sslStream.Read(data, 0, data.Length);
                data = new byte[1024];
                Console.WriteLine(Encoding.Default.GetString(data));
                Console.WriteLine("Enter path to message directory:");
                var path = Console.ReadLine();
                data = new byte[1024];
                var input = GetMessage(ReadMessageInfoFromDirectory(account, password, path));
                Authenticate(sslStream, account, password, data);
                foreach (var s in input)
                {
                    sslStream.Write(encoding.GetBytes(s + "\r\n"));
                    sslStream.Read(data, 0, 1024);
                    Console.WriteLine(encoding.GetString(data));
                    data = new byte[1024];
                }
            }
        }

        public static string Authenticate(SslStream stream, string account, string password, byte[] buffer)
        {
            if (string.IsNullOrEmpty(account) || string.IsNullOrEmpty(password))
                throw new ArgumentException("INCORRECT PARAMETERS");
            var authentication = new[]
            {
                "EHLO hackerman.ru",
                "AUTH LOGIN",
                Convert.ToBase64String(encoding.GetBytes(account)),
                Convert.ToBase64String(encoding.GetBytes(password)),
            };
            foreach (var input in authentication)
                stream.Write(encoding.GetBytes(input + "\r\n"));
            stream.Read(buffer, 0, buffer.Length);
            return encoding.GetString(buffer);
        }

        private static MessageInfo ReadMessageInfoFromDirectory(string account, string password, string path)
        {
            var config = Path.Combine(path, configFileName);
            var textFile = Path.Combine(path, textFileName);
            if (!Directory.Exists(path) || !File.Exists(config) || !File.Exists(textFile))
                throw new ArgumentException("Expected directory with files: \"config.txt\", \"text.txt\"");
            var data = File.ReadAllText(config, encoding).Split('\n');
            var subject = subjectPattern.Parse(data[0]);
            subject = subject.Substring(1, subject.Length - 2);
            var receivers = receiverPattern.Parse(data[1])
                .Split(' ')
                .Select(inQuotes => inQuotes.Substring(1, inQuotes.Length - 2));

            var files = filesPattern.Parse(data[2])
                .Split(' ')
                .Select(inQuotes => new FileInfo(Path.Combine(path, inQuotes.Substring(1, inQuotes.Length - 2))));

            var text = File.ReadAllText(textFile, encoding);
            return new MessageInfo(account, password, receivers, subject, text, files);
        }



        private static IEnumerable<string> GetMessage(MessageInfo messageInfo)
        {
            var builder1 = new StringBuilder();
            foreach (var receptiest in messageInfo.receivers)
                builder1.Append($"RCPT TO: {receptiest}\n");

            var builder2 = new StringBuilder();
            foreach (var receptiest in messageInfo.receivers)
                builder2.Append($"{receptiest},");

            var boundary = GenerateBoundary(messageInfo.text);
            var text = messageInfo.text.Replace("\n.\n", "\n. \n");
            var fileData = string.Concat(ConvertFiles(messageInfo.Files).Select(x => boundary + "\n" + x + "\n"));
            Console.WriteLine(builder1);
            return new[]
             {
               $"MAIL FROM:  {messageInfo.Account}",
               builder1.Remove(builder1.Length-1, 1).ToString(),
               "DATA",
               $"From: {messageInfo.Account} (ОАО Газпром)\n" +
               $"To: { builder2.Remove(builder2.Length-1, 1)}\n" +
               $"Subject: =?Windows-1251?B?{Convert.ToBase64String(encoding.GetBytes(messageInfo.Subject))}?=\n" +
               $"Content-Type: multipart/mixed; boundary={boundary.Substring(2)}\n\n"+

               $"{boundary}\n"+
               "Content-Type: text/plain; charset=windows-1251;\n"+
               "Content-Transfer-Encoding: base64\n\n"+
               text+'\n'+
               fileData+
               $"\n{boundary}--"+
                "\n.",
            "QUIT"
            };
        }

        private static IEnumerable<string> ConvertFiles(IEnumerable<FileInfo> files)
        {
            var builder = new StringBuilder();
            foreach (var file in files)
            {
                builder.Append($"Content-Type: {GetContentType(file)}\n");
                builder.Append("Content-Transfer-Encoding: base64\n");
                builder.Append($"Content-Disposition:attachment; filename={file.Name}\n\n");
                builder.Append(Convert.ToBase64String(File.ReadAllBytes(file.FullName)));
                yield return builder.ToString();
                builder.Clear();
            }
        }

        private static string GetContentType(FileInfo file)
        {
            var ext = Path.GetExtension(file.Name);
            switch (ext)
            {
                case ".jpeg": return "image/jpeg";
                case ".bmp": return "image/bmp";
                case ".png": return "image/png";
                case ".txt": return "text/plain";
                default:
                    throw new ArgumentException($"Incorrect file format: {ext}");
            }
        }

        private static string GenerateBoundary(string text)
        {
            var random = new Random();
            var boundary = "--#MyBoundary";
            while (text.Contains(boundary))
                boundary += random.Next(10);
            return boundary;
        }

        private static bool ValidateServerCertificate(object sender, X509Certificate certificate,
            X509Chain chain, SslPolicyErrors sslPolicyErrors) => true;
    }
}
