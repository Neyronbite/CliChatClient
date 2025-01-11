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
                        if (sb.Length > 0)
                        {
                            sb.Remove(sb.Length - 1, 1);
                        }
                        break;
                    case ConsoleKey.Enter:
                        break;
                    default:
                        sb.Append(key.KeyChar);
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
    }
}
