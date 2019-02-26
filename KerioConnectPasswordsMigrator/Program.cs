using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using CommandLine;
using CommandLine.Text;

namespace KerioConnectPasswordsMigrator
{
    class Program
    {
        public class Options
        {
            [Option('s',
                "source",
                Required = true,
                HelpText = "Source users.cfg file.")]
            public string Source { get; set; }

            [Option('t',
                "target",
                Required = true,
                HelpText = "Target users.cfg file.")]
            public string Target { get; set; }

            [Option('a',
                "skipAdmin",
                Required = false,
                Default = true,
                HelpText = "Should skip Admin user.")]
            public bool SkipAdmin { get; set; }
        }

        public class KerioUserPassword
        {
            public string Domain;
            public string Name;
            public string Password;
        }

        static void Main(string[] args)
        {
            var parser = new Parser(settings =>
            {
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = Console.Out;
            });

            parser.ParseArguments<Options>(args)
                .WithParsed<Options>(TryMigratePasswords).WithNotParsed(OutputErrorsAndExit);
        }

        private static void OutputErrorsAndExit(IEnumerable<Error> errors)
        {
            Console.WriteLine("Error parsing arguments. Details below:");
            foreach (var error in errors)
            {
                Console.WriteLine(error);
            }
            Environment.Exit(1);
        }

        public static void TryMigratePasswords(Options o)
        {
            var sourceFilePath = Path.GetFullPath(o.Source);
            var sourceFileExistis = File.Exists(sourceFilePath);
            if (!sourceFileExistis)
            {
                Console.WriteLine($"Source users.cfg not found at {sourceFilePath}. Can't proceed, quiting...");
                Environment.Exit(1);
            }

            var targetFilePath = Path.GetFullPath(o.Target);
            var targetFileExistis = File.Exists(targetFilePath);
            if (!targetFileExistis)
            {
                Console.WriteLine($"Target users.cfg not found at {sourceFilePath}. Can't proceed, quiting...");
                Environment.Exit(1);
            }

            //Open target, read all users that are there.
            XDocument xDocTarget = XDocument.Load(targetFilePath);
            var targetUsersNode = xDocTarget.Descendants("list").Where(e => e.Attributes().Any(a => a.Name == "name" && a.Value == "User"));

            var usersInTargetFileCount = targetUsersNode.Descendants("listitem").Count();

            if (usersInTargetFileCount > 0)
            {

                var listOfUsersThatRequireAPasswordUpdate = new List<KerioUserPassword>();

                Console.WriteLine($"Found {usersInTargetFileCount} users in a target users.cfg.");
                Console.WriteLine("Will get their passwords updated with the one's from the source users.cfg...");

                var targetListItems = targetUsersNode.Descendants("listitem");

                foreach (var targetItem in targetListItems)
                {
                    var currentDomain = targetItem.Descendants("variable").First(e => e.Attributes().Any(a => a.Name == "name" && a.Value == "Domain")).Value;
                    var currentName = targetItem.Descendants("variable").First(e => e.Attributes().Any(a => a.Name == "name" && a.Value == "Name")).Value;

                    if (currentName == "Admin" && o.SkipAdmin)
                    {
                        continue;
                    }

                    listOfUsersThatRequireAPasswordUpdate.Add(new KerioUserPassword { Name = currentName, Domain = currentDomain });
                }

                XDocument xDocSource = XDocument.Load(sourceFilePath);
                var sourceUsersNode = xDocSource.Descendants("list").Where(e => e.Attributes().Any(a => a.Name == "name" && a.Value == "User"));

                var sourceListItems = sourceUsersNode.Descendants("listitem");

                var foundPasswordFor = 0;

                foreach (var userEntry in listOfUsersThatRequireAPasswordUpdate)
                {
                    foreach (var sourceItem in sourceListItems)
                    {
                        var currentDomain = sourceItem.Descendants("variable").First(e => e.Attributes().Any(a => a.Name == "name" && a.Value == "Domain")).Value;
                        var currentName = sourceItem.Descendants("variable").First(e => e.Attributes().Any(a => a.Name == "name" && a.Value == "Name")).Value;

                        if (currentDomain == userEntry.Domain && currentName == userEntry.Name)
                        {
                            foundPasswordFor++;
                            userEntry.Password = sourceItem.Descendants("variable").First(e => e.Attributes().Any(a => a.Name == "name" && a.Value == "Password")).Value;
                            break;
                        }
                    }
                }

                Console.WriteLine($"Found new passford for {foundPasswordFor} accounts...");

                foreach (var targetItem in targetListItems)
                {
                    var currentDomain = targetItem.Descendants("variable").First(e => e.Attributes().Any(a => a.Name == "name" && a.Value == "Domain")).Value;
                    var currentName = targetItem.Descendants("variable").First(e => e.Attributes().Any(a => a.Name == "name" && a.Value == "Name")).Value;

                    if (currentName == "Admin" && o.SkipAdmin)
                    {
                        continue;
                    }

                    targetItem.Descendants("variable").First(e => e.Attributes().Any(a => a.Name == "name" && a.Value == "Password")).Value = listOfUsersThatRequireAPasswordUpdate.First(u => u.Name == currentName && u.Domain == currentDomain).Password;
                }

                xDocTarget.Save(targetFilePath);
            }
            else
            {
                Console.WriteLine("Didn't find any users in the target users.cfg. Nothing to update...");
                Environment.Exit(1);
            }

            Console.ReadKey();
        }
    }
}
