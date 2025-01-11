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

object lockObj = new object();
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
/*

var argParser = new ArgumentParser();
var options = argParser.Parse(args);

string username = options.Username;
string password = string.Empty;

string baseUrl = @"https://localhost:7183";
string token = string.Empty;

var httpService = new HTTPService(baseUrl);
var messageService = new MessageService();
var dataAccess = new DataAccess();

if (options.Register)
{
    var passwordRepeat = string.Empty;
    do
    {
        Console.WriteLine();
        password = Inputs.ReadPass("Enter Password");
        Console.WriteLine();
        passwordRepeat = Inputs.ReadPass("Enter Password to verify");
        if (passwordRepeat != password)
        {
            Console.WriteLine("Passwords dont match");
        }
    } while (string.IsNullOrEmpty(password) || password != passwordRepeat);

    var keys = RSAEncryptionHelper.GenerateKeys();
    token = await httpService.Register(username, password, keys.publicKey);
}
else if (options.Login)
{
    password = Inputs.ReadPass("Enter Password");
    token = await httpService.Authenticate(username, password);
}
else if (options.Help)
{
    //TODO help text
}

try
{
    
}
catch (Exception)
{

    throw;
}
finally
{

}

messageService.Init(baseUrl, token, (user, message) =>
{
    Console.WriteLine($"{user}: {message}");
});

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
*/