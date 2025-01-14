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

// Heading text
Console.ForegroundColor = ConsoleColor.DarkMagenta;
//Console.WriteLine("""
//    ooo        ooooo  o8o    .o    oooo o.     .oooooo.   oooo                      .   
//    `88.       .888'  `"'   .8'    `888 `8.   d8P'  `Y8b  `888                    .o8   
//     888b     d'888  oooo  .8'      888  `8. 888           888 .oo.    .oooo.   .o888oo 
//     8 Y88. .P  888  `888  88       888   88 888           888P"Y88b  `P  )88b    888   
//     8  `888'   888   888  88       888   88 888           888   888   .oP"888    888   
//     8    Y     888   888  `8.      888  .8' `88b    ooo   888   888  d8(  888    888 . 
//    o8o        o888o o888o  `8. .o. 88P .8'   `Y8bood8P'  o888o o888o `Y888""8o   "888" 
//                             `" `Y888P  "'                                              
//    """);
Console.WriteLine("""
    ███╗   ███╗██╗     ██╗     ██╗██╗      ██████╗██╗  ██╗ █████╗ ████████╗
    ████╗ ████║██║    ██╔╝     ██║╚██╗    ██╔════╝██║  ██║██╔══██╗╚══██╔══╝
    ██╔████╔██║██║    ██║      ██║ ██║    ██║     ███████║███████║   ██║   
    ██║╚██╔╝██║██║    ██║ ██   ██║ ██║    ██║     ██╔══██║██╔══██║   ██║   
    ██║ ╚═╝ ██║██║    ╚██╗╚█████╔╝██╔╝    ╚██████╗██║  ██║██║  ██║   ██║   
    ╚═╝     ╚═╝╚═╝     ╚═╝ ╚════╝ ╚═╝      ╚═════╝╚═╝  ╚═╝╚═╝  ╚═╝   ╚═╝   
                                                                           

    """);
Console.ResetColor();

// Parsing arguments 
var argParser = new ArgumentParser();
var options = argParser.Parse(args);

// Context class, that contains main properties for services (username, url ...)
var context = new Context()
{
    LoggedUsername = options.Username,
    BaseUrl = @"https://" + options.Server
};

// main services
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
    messageService.Init(
        mainWin.AddMessage,
        mainWin.SetError,
        mainWin.SetWarning);

    // TODO get queued messages from server
    var messages = new List<MessageModel>();

    // Initializing main UI class
    mainWin.Init(async m =>
    // Handling input taken from UI, delivering to MessageService
    {
        if (string.IsNullOrWhiteSpace(m))
        {
            mainWin.SetWarning("Empty string input!!!");
            return;
        }

        try
        {
            // TODO validate m using regex
            var mArr = m.Split(' ');
            var to = mArr[0];
            var message = m.Replace(to + " ", "");
            await messageService.SendMessage(to, message);
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