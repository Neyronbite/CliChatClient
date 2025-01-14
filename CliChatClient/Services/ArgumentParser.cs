using CommandLine;
using Microsoft.Extensions.FileSystemGlobbing.Internal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CliChatClient.Services
{
    public class ArgumentParser
    {
        const string usernamePattern = @"^[a-zA-Z0-9]+([._]?[a-zA-Z0-9]+)*$";
        public Options Parse(string[] args)
        {
            var optionsRes = Parser.Default.ParseArguments<Options>(args)
                    // options validation
                   .WithParsed<Options>(o =>
                   {
                       if (o.Login && o.Register)
                       {
                           Console.WriteLine($"Cant login and register at the same time");
                       }
                       else if (!o.Login && !o.Register)
                       {
                           Console.WriteLine($"--login or --register flags needed");
                       }

                       if (!o.Server.StartsWith(@"http://") && !o.Server.StartsWith(@"https://"))
                       {
                           o.Server = @"https://" + o.Server;
                       }
                       if (!Regex.IsMatch(o.Username, usernamePattern) || o.Username.Length < 3)
                       {
                           Console.WriteLine("username is not valid");
                           Environment.Exit(0);
                       }
                   });


            if (optionsRes.Errors != null && optionsRes.Errors.Count() > 0)
            {
                Console.WriteLine("there are errors in parsed arguments");
                Environment.Exit(0);
            }

            return optionsRes.Value;
        }
    }
    public class Options
    {
        [Option('l', "login", Required = false, HelpText = "To initiate login request")]
        public bool Login { get; set; }
        [Option('r', "register", Required = false, HelpText = "To initiate register request")]
        public bool Register { get; set; }
        [Option('u', "username", Required = true, HelpText = "your username, minimal length is 3 characters, must contain only letters, digits and '_', '-' symbols")]
        public string Username { get; set; }
        [Option('s', "server", Required = true, HelpText = "server's ip and port: 1.1.1.1:5000")]
        public string Server { get; set; }
        [Option('i', "ignore-ssl", Required = false, HelpText = "ignores ssl certificate issues")]
        public bool IgnoreSSL { get; set; }
    }
}
