using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CliChatClient.UI
{
    public class Inputs
    {
        public static string ReadPass()
        {
            var sb = new StringBuilder();
            ConsoleKeyInfo key;

            do
            {
                key = Console.ReadKey(true);

                switch(key.Key)
                {
                    case ConsoleKey.Backspace:
                        sb.Remove(sb.Length - 2, 1);
                        break;
                    case ConsoleKey.Enter:
                        break;
                    default:
                        sb.Append(key.Key);
                        break;
                }
            }
            // Stops Receving Keys Once Enter is Pressed
            while (key.Key != ConsoleKey.Enter);

            return sb.ToString();
        }

        public static string ReadPass(string messageBeforePass)
        {
            Console.Write(messageBeforePass + ": ");
            return ReadPass();
        }

        public static string ReadOnXY(int x, int y)
        {
            var sb = new StringBuilder();
            ConsoleKeyInfo key;

            do
            {
                if (!Console.KeyAvailable)
                {
                    continue;
                }
                key = Console.ReadKey(true);

                switch(key.Key)
                {
                    case ConsoleKey.Backspace:
                        sb.Remove(sb.Length - 2, 1);
                        break;
                    case ConsoleKey.Enter:
                        break;
                    default:
                        sb.Append(key.Key);
                        break;
                }
            }
            // Stops Receving Keys Once Enter is Pressed
            while (true);

            return sb.ToString();
        }
    }
}
