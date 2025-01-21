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
    BaseUrl = options.Server,
    IgnoreSSL = options.IgnoreSSL

};

// main services
var httpService = new HTTPService(context);
var dataAccess = new DataAccess(context);
var messageService = new MessageService(dataAccess, context, httpService);
var mainWin = new MainWindow();

// finalization function, that closes connection to server, saves db's etc
Func<Task> finalizingAction = async () =>
{
    Console.CursorVisible = true;
    Console.WriteLine("Finalizing");
    await messageService.DisposeAsync();
    Console.WriteLine("Disconnected from SignalR hub");
    Environment.Exit(0);
};

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
        context.Password = password;

        // Initializing dataAccess after gaining password, to encrypt new data with password
        await dataAccess.Init();

        // inserting new user data to db
        await dataAccess.UserKeys.Update(context.LoggedUsername,
            new UserKey()
            {
                PrivateKey = keys.privateKey,
                PublicKey = keys.publicKey,
                Username = context.LoggedUsername,
                UsersPublicKeys = new Dictionary<string, string>(),
                UsersSymetricKeys = new Dictionary<string, string>()
            });

        Console.WriteLine();
        Console.WriteLine("Welcome " + options.Username);
    }
    else if (options.Login)
    {
        var password = Inputs.ReadPass("Enter Password");

        // getting token
        context.Token = await httpService.Authenticate(options.Username, password);
        context.Password = password;

        // Initializing dataAccess after gaining password, to decrypt it with password
        await dataAccess.Init();

        Console.WriteLine();
        Console.WriteLine("Successfully loged in");
    }
}
catch (Exception e)
{
    Console.WriteLine();
    Console.WriteLine("An error occured while processing request");
    Console.WriteLine(e.Message);
    if (e.InnerException != null)
    {
        Console.WriteLine(e.InnerException.Message);
    }
    Environment.Exit(0);
}

// Register the custom Ctrl+C handler
// It overrides default program closing, and finalizing things
Console.CancelKeyPress += new ConsoleCancelEventHandler((sender, e) => 
{
    var task = finalizingAction();
    Task.WaitAny(task);
});
Console.InputEncoding = System.Text.Encoding.Unicode;
Console.OutputEncoding = System.Text.Encoding.Unicode;

try
{

    // queued messages
    var messages = new List<MessageModel>();

    // Initializing signal r communication service
    // Handling received message delivering to UI
    await messageService.Init(
        mainWin.AddMessage,
        mainWin.SetError,
        mainWin.SetWarning,
        messages);


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
    }, messages);
}
catch (Exception e)
{
    Console.WriteLine("Critical Error Occured");
    Console.WriteLine(e.ToString());
}
finally
{
    var task = finalizingAction();
    Task.WaitAny(task);
}