// See https://aka.ms/new-console-template for more information
using CliChatClient.Data;
using CliChatClient.Helpers;
using CliChatClient.Models;
using CliChatClient.Services;
using CliChatClient.UI;
using Microsoft.AspNetCore.SignalR.Client;
using Newtonsoft.Json;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Terminal.Gui;

/*
var messages = new List<MessageModel>();

for (int i = 0; i < 64; i++)
{
    messages.Add(new MessageModel() { From = i.ToString(), To = i.ToString() + 55, Message = $"Test message - {i}" });
}

var mainWin = new MainWindow();
mainWin.Init(messages, m =>
{
    if (string.IsNullOrWhiteSpace(m))
    {
        mainWin.SetWarning("Empty string input!!!");
    }
    mainWin.AddMessage(new MessageModel() { From = "neo", To = "brat", Message = m });
});
*/

// Parsing arguments 
var argParser = new ArgumentParser();
var options = argParser.Parse(args);

//Context class, that contains main properties for services (username, url ...)
var context = new Context()
{
    LoggedUsername = options.Username,
    BaseUrl = @"https://" + options.Server
};

//main services
var httpService = new HTTPService(context);
var dataAccess = new DataAccess(context);
var messageService = new MessageService(dataAccess, context, httpService);
var mainWin = new MainWindow();

// finalization function, that closes connection to server, saves db's etc
Action finalizingAction = async () =>
{
    Console.WriteLine("Finalizing");
    await messageService.DisposeAsync();
    Console.WriteLine("Disconnected from SignalR hub");
    Environment.Exit(0);
};

// Initializing local database access class
// we are initializing this service earlyer than others,
// because after registration we need to save then into db
await dataAccess.Init();

//TODO welcome text

// operations on parsed arguments
try
{
    if (options.Register)
    {
        // getting password for registration
        var passwordRepeat = string.Empty;
        var password = string.Empty;
        do
        {
            Console.WriteLine();
            password = Inputs.ReadPass("Enter Password");
            Console.WriteLine();
            passwordRepeat = Inputs.ReadPass("Enter Password again");

            if (passwordRepeat != password)
            {
                Console.WriteLine("Passwords dont match");
            }
        } while (string.IsNullOrEmpty(password) || password != passwordRepeat);

        // generating public and private RSA keys for key exchannge
        var keys = RSAEncryptionHelper.GenerateKeys();

        // getting token
        context.Token = await httpService.Register(context.LoggedUsername, password, keys.publicKey);

        // inserting new user data to db
        await dataAccess.UserKey.Insert(new UserKey() 
        {
            PrivateKey = keys.privateKey,
            PublicKey = keys.publicKey,
            Username = context.LoggedUsername,
            UsersPublicKeys = new Dictionary<string, string>(),
            UsersSymetricKeys = new Dictionary<string, string>()
        }, context.LoggedUsername);

        Console.WriteLine();
        Console.WriteLine("Welcome " + options.Username);
    }
    else if (options.Login)
    {
        var password = Inputs.ReadPass("Enter Password");

        // getting token
        context.Token = await httpService.Authenticate(options.Username, password);

        Console.WriteLine();
        Console.WriteLine("Successfully loged in");
    }
}
catch (Exception e)
{
    Console.WriteLine();
    Console.WriteLine("An error occured while processing request");
    Console.WriteLine(e.Message);
    Environment.Exit(0);
}

// Register the custom Ctrl+C handler
// It overrides default program closing, and finalizing things
Console.CancelKeyPress += new ConsoleCancelEventHandler((sender, e) => { finalizingAction(); });

try
{
    // Initializing signal r communication service
    // Handling received message delivering to UI
    messageService.Init((u, m) =>
    {
        var message = new MessageModel()
        {
            From = u,
            To = context.LoggedUsername,
            Message = m
        };

        mainWin.AddMessage(message);
    });

    // TODO get queued messages from server
    var messages = new List<MessageModel>();

    // Initializing main UI class
    mainWin.Init(messages,
    // Handling message taken from UI deliver to MessageService
    async m =>
    {
        if (string.IsNullOrWhiteSpace(m))
        {
            mainWin.SetWarning("Empty string input!!!");
            return;
        }

        try
        {
            // TODO validate m
            var mArr = m.Split(' ');
            var to = mArr[0];
            var message = m.Replace(to + " ", "");
            await messageService.SendMessage(to, message);

            mainWin.AddMessage(new MessageModel() { From = context.LoggedUsername, To = to, Message = m });
        }
        catch (Exception e)
        {
            mainWin.SetError(e.Message);
        }
    });
}
catch (Exception e)
{
    Console.WriteLine("Critical Error Occured");
    Console.WriteLine(e.ToString());
}
finally
{
    finalizingAction();
}

// Start the connection
// For Testing
//try
//{
//    messageService.Init((u, m) =>
//    {
//        Console.WriteLine("message from " + u + " : " + m);
//    });

//    // Loop to send messages
//    while (true)
//    {
//        var m = Console.ReadLine();
//        var mArr = m.Split(' ');
//        var to = mArr[0];
//        var message = m.Replace(to + " ", "");

//        await messageService.SendMessage(to, message);
//    }
//}
//catch (Exception ex)
//{
//    Console.WriteLine($"Error: {ex.Message}");
//}
//finally
//{
//    finalizingAction();
//}