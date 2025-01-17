﻿using CliChatClient.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.UI
{
    public class MainWindow
    {
        private Action<string> handleUserInput;

        private string error = string.Empty;
        private string warning = string.Empty;

        private int width = Console.WindowWidth;
        private int height = Console.WindowHeight;
        private int defaulMargin = 6;

        // List to store messages
        private List<MessageModel> messages = new List<MessageModel>();
        // Dict to store colors
        private Dictionary<string, ConsoleColor> colors = new Dictionary<string, ConsoleColor>();

        private List<ConsoleColor> colorList;
        string spaceArr = string.Empty;
        string defaultPtefixString = string.Empty;

        private StringBuilder stringBuilder = new StringBuilder();
        private int scrollPosition = 0;

        // update indicators
        private bool toBeUpdated = true;
        private bool messagesToBeRerendered = true;
        private bool inputToBeRerendered = true;
        private bool warningsToBeRendered = true;

        public MainWindow()
        {
            //main colors
            colorList = new List<ConsoleColor>()
            {
                //ConsoleColor.Red,
                //ConsoleColor.Green,
                //ConsoleColor.DarkGreen,
                ConsoleColor.Blue,
                ConsoleColor.DarkBlue,
                ConsoleColor.Cyan,
                ConsoleColor.DarkCyan,
                //ConsoleColor.Yellow,
                ConsoleColor.Magenta,
                ConsoleColor.DarkMagenta,
                //ConsoleColor.Gray,
            };

            InitSpaces();
        }

        public void Init(Action<string> handleUserInput, List<MessageModel> startMessages)
        {
            this.handleUserInput = handleUserInput;
            messages = startMessages;

            scrollPosition = messages.Count - 1;

            //Console.SetBufferSize(Console.BufferWidth * 4, Console.BufferHeight * 4);
            Console.CursorVisible = false;

            Console.Clear();
            Update();

            ConsoleKeyInfo exKey = new ConsoleKeyInfo();
            // Infinite loop to simulate the chat interface
            while (true)
            {
                if (toBeUpdated || warningsToBeRendered || inputToBeRerendered || messagesToBeRerendered)
                {
                    Update();
                     
                    {
                        toBeUpdated = false;
                    }
                }

                if (Console.KeyAvailable)
                {
                    exKey = CheckInputs(exKey);
                }
            }
        }

        public void SetWarning(string warning)
        {
            this.warning = warning;
            toBeUpdated = true;
            warningsToBeRendered = true;
        }

        public void SetError(string error)
        {
            this.error = error;
            toBeUpdated = true;
            warningsToBeRendered = true;
        }

        public void AddMessage(MessageModel message)
        {
            messages.Add(message);
            scrollPosition = messages.Count - 1;
            toBeUpdated = true; 
            messagesToBeRerendered = true;
        }

        private ConsoleKeyInfo CheckInputs(ConsoleKeyInfo exKey)
        {
            ConsoleKeyInfo key = Console.ReadKey(true);

            switch (key.Key)
            {
                // Deleting last character
                case ConsoleKey.Backspace:
                    if (stringBuilder.Length > 0)
                    {
                        stringBuilder.Remove(stringBuilder.Length - 1, 1);
                        inputToBeRerendered = true;
                    }
                    break;
                // Refreshing
                case ConsoleKey.F5:
                    InitSpaces();
                    Console.Clear();
                    width = Console.WindowWidth;
                    height = Console.WindowHeight;
                    toBeUpdated = true;
                    messagesToBeRerendered = true;
                    warningsToBeRendered = true;
                    inputToBeRerendered = true;
                    break;
                // Scrolling (4 cases)
                case ConsoleKey.UpArrow:
                    scrollPosition -= scrollPosition > height - defaulMargin ? 1 : 0;
                    messagesToBeRerendered = true;
                    break;
                case ConsoleKey.PageUp:
                    scrollPosition = scrollPosition - 10 > height - defaulMargin ? scrollPosition - 10 : height - defaulMargin;
                    messagesToBeRerendered = true;
                    break;
                case ConsoleKey.DownArrow:
                    scrollPosition += scrollPosition != messages.Count - 1 ? 1 : 0;
                    messagesToBeRerendered = true;
                    break;
                case ConsoleKey.PageDown:
                    scrollPosition = scrollPosition + 10 < messages.Count ? scrollPosition + 10 : messages.Count - 1;
                    messagesToBeRerendered = true;
                    break;
                // Cashing prefix string
                case ConsoleKey.Tab:
                    defaultPtefixString = stringBuilder.ToString();
                    break;
                // Removeing cashed prefix string
                case ConsoleKey.Escape:
                    defaultPtefixString = string.Empty;
                    break;
                // Submiting input
                case ConsoleKey.Enter:
                    var str = stringBuilder.ToString();
                    Task.Run(() =>
                    {
                        handleUserInput(str);
                    });
                    stringBuilder = new StringBuilder(defaultPtefixString);
                    inputToBeRerendered = true;
                    break;
                // Appending char 
                default:
                    stringBuilder.Append(key.KeyChar);
                    inputToBeRerendered = true;
                    break;
            }
            toBeUpdated = true;
            return key;
        }

        
        private void Update()
        {
            // Clear the console and redraw the UI
            //Console.Clear();

            if (warningsToBeRendered) 
            {
                // Display warnings and errors at the top
                DisplayWarnings();
                warningsToBeRendered = false;
            }

            if (messagesToBeRerendered)
            {
                // Display messages (scrollable)
                DisplayMessages();
                messagesToBeRerendered = false;
            }

            if (inputToBeRerendered)
            {
                // Display input bar at the bottom
                DisplayInputBar();
                inputToBeRerendered = false;
            }

            Console.SetCursorPosition(0, height - 1);
        }

        // Display the warnings and errors at the top
        private void DisplayWarnings()
        {
            //Clearing
            Console.SetCursorPosition(0, 0);
            Console.WriteLine(spaceArr);
            Console.WriteLine(spaceArr);

            //Printing
            Console.SetCursorPosition(0, 0); // Move cursor to top
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"{warning}");
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"{error}");
            Console.ResetColor();
        }

        // Display the scrollable messages in the center
        private void DisplayMessages()
        {
            // Display messages in reverse order to show the most recent messages
            int displayStart = Math.Max(0, scrollPosition - height + defaulMargin);
            var visibleMessages = messages.Skip(displayStart).Take(height - defaulMargin + 1);

            // Start displaying messages at line 3 (after warnings/errors)
            int yPos = 3;
            int currentY = yPos;
            Console.SetCursorPosition(0, yPos);

            foreach (var message in visibleMessages)
            {
                if (!colors.TryGetValue(message.From, out var fromColor))
                {
                    fromColor = colorList[Random.Shared.Next(0, colorList.Count)];
                    colors.Add(message.From, fromColor);
                }
                if (!colors.TryGetValue(message.To, out var toColor))
                {
                    toColor = colorList[Random.Shared.Next(0, colorList.Count)];
                    colors.Add(message.To, toColor);
                }

                //Clearing
                Console.WriteLine(spaceArr);
                Console.SetCursorPosition(0, currentY);

                //Printing
                Console.ForegroundColor = fromColor;
                Console.Write(message.From);
                Console.ResetColor();
                Console.Write(" -> ");
                Console.ForegroundColor = toColor;
                Console.Write(message.To);
                Console.ResetColor();
                Console.Write(": ");

                if (message.HasError)
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine(message.Message);
                    Console.ResetColor();
                }
                else if (message.IsNotification)
                {
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine(message.Message);
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine(message.Message);
                }

                currentY++;
            }
        }

        // Display the input bar at the bottom of the console
        private void DisplayInputBar()
        {
            //Clearing
            Console.SetCursorPosition(0, height - 2); // Position at the bottom
            Console.WriteLine(spaceArr);

            //Printing
            Console.SetCursorPosition(0, height - 2); // Position at the bottom
            Console.Write("message -> {username} {message}: " + stringBuilder.ToString());
        }

        private void InitSpaces()
        {
            using (var sw = new StringWriter())
            {
                for (int i = 0; i < width; i++)
                {
                    sw.Write(' ');
                }
                spaceArr = sw.ToString();
            }
        }
    }
}
