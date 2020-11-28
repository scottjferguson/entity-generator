using Pluralize.NET.Core;
using Sprocket.EntityFramework;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Sprocket.ConsoleApp
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var entityFileProcessor = new EntityFileProcessor(new Pluralizer());

            string directoryPath = SubstringBefore(typeof(Program).Assembly.Location, "bin");

            await entityFileProcessor.ProcessAsync(directoryPath, new CancellationToken());

            Console.WriteLine("Complete");
            Console.ReadLine();
        }

        private static string SubstringBefore(string str, string removeAfter, bool includeRemoveAfterString = false)
        {
            if (str == null)
            {
                return null;
            }

            try
            {
                return str.Substring(0, str.IndexOf(removeAfter, StringComparison.Ordinal) + (includeRemoveAfterString ? 1 : 0));
            }
            catch
            {
                return str;
            }
        }
    }
}
