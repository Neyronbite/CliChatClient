using CommandLine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.Services
{
    public class ArgumentParser
    {
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

                       //TODO server validation
                       //TODO username validation
                   });
            return optionsRes.Value;
        }
    }
    public class Options
    {
        [Option('l', "login", Required = false, HelpText = "To initiate login request")]
        public bool Login { get; set; }
        [Option('r', "register", Required = false, HelpText = "To initiate register request")]
        public bool Register { get; set; }
        [Option('u', "username", Required = true, HelpText = "your username, minimal length is 6 characters")]
        public string Username { get; set; }
        [Option('s', "server", Required = true, HelpText = "server's ip and port: 1.1.1.1:5000")]
        public string Server { get; set; }
    }
}
