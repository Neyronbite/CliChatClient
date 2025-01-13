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

// credentials
string username = string.Empty;
string password = string.Empty;

// base url from server
//string baseUrl = @"https://localhost:7183";
string baseUrl = string.Empty;
// authentication jwt token
string token = string.Empty;

//main services
var httpService = new HTTPService(baseUrl);
var dataAccess = new DataAccess();
var messageService = new MessageService(dataAccess);
var mainWin = new MainWindow();

// finalization function, that closes connection to server, saves db's etc
Action finalizingAction = async () =>
{
    Console.WriteLine("Finalizing");
    await messageService.DisposeAsync();
    Console.WriteLine("Disconnected from SignalR hub");
    Environment.Exit(0);
};

//TODO welcome text

// operations on parsed arguments
try
{
    if (options.Register)
    {
        // getting password for registration
        var passwordRepeat = string.Empty;
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
        token = await httpService.Register(username, password, keys.publicKey);

        Console.WriteLine();
        Console.WriteLine("Welcome " + options.Username);
    }
    else if (options.Login)
    {
        password = Inputs.ReadPass("Enter Password");

        // getting token
        token = await httpService.Authenticate(options.Username, password);

        Console.WriteLine();
        Console.WriteLine("Successfully loged in");
    }

    username = options.Username;
    baseUrl = $"https://{options.Server}";
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
    //Initializing local database access class
    await dataAccess.Init();

    // Initializing signal r communication service
    messageService.Init(baseUrl, token,
    //Handling received message delivering to UI
    (u, m) =>
    {
        var message = new MessageModel()
        {
            From = u,
            To = username,
            Message = m
        };

        mainWin.AddMessage(message);
    });

    //TODO get queued messages from server
    var messages = new List<MessageModel>();

    //Initializing main UI class
    mainWin.Init(messages,
    //Handling message taken from UI deliver to MessageService
    async m =>
    {
        if (string.IsNullOrWhiteSpace(m))
        {
            mainWin.SetWarning("Empty string input!!!");
            return;
        }

        try
        {
            //TODO validate m
            var mArr = m.Split(' ');
            var to = mArr[0];
            var message = m.Replace(to + " ", "");
            await messageService.SendMessage(to, message);

            mainWin.AddMessage(new MessageModel() { From = username, To = to, Message = m });
        }
        catch (Exception e)
        {
            mainWin.SetError("Invalid message format");
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

// Handle Ctrl+C to perform custom cleanup
//static void CancelKeyHandler(object sender, ConsoleCancelEventArgs args)
//{
//    // Ignore the default Ctrl+C behavior
//    args.Cancel = true;
//}

//messageService.Init(baseUrl, token, (user, message) =>
//{
//    Console.WriteLine($"{user}: {message}");
//});

//// Start the connection
//try
//{
//    // Loop to send messages
//    Console.Write("Username to sent: ");
//    while (true)
//    {
//        Console.Write("Username to sent: ");
//        var to = Console.ReadLine();
//        Console.WriteLine();
//        Console.Write("Enter message: ");
//        var message = Console.ReadLine();

//        if (message?.ToLower() == "exit")
//            break;

//        // Send message to the SignalR hub
//        await connection.InvokeAsync("SendMessage", message, to);
//    }
//}
//catch (Exception ex)
//{
//    Console.WriteLine($"Error: {ex.Message}");
//}
//finally
//{
//    await connection.StopAsync();
//    Console.WriteLine("Disconnected from SignalR hub");
//}