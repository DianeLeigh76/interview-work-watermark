namespace InterviewFingerprint.Classes;

internal static class InteractiveMenu
{
    public static Int32 Run(Func<String[], Int32> execute)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine("=== InterviewFingerprint ===");
            Console.WriteLine("1. Mark an image");
            Console.WriteLine("2. Verify an image watermark");
            Console.WriteLine("3. Fingerprint C# source");
            Console.WriteLine("4. Compare C# source files");
            Console.WriteLine("5. Create evidence archive");
            Console.WriteLine("6. Verify evidence archive");
            Console.WriteLine("0. Exit");
            Console.Write("Select an option: ");
            String? selection = Console.ReadLine();
            if (selection is null || selection.Trim() == "0")
            {
                return 0;
            }

            String[]? arguments = selection.Trim() switch
            {
                "1" => Collect("mark", ["in", "out", "id"], ["strength"]),
                "2" => Collect("verify", ["in", "id"], ["reference"]),
                "3" => Collect("code-fingerprint", ["in"], []),
                "4" => Collect("compare-code", ["a", "b"], []),
                "5" => Collect("archive", ["in", "out", "id"], []),
                "6" => Collect("check-archive", ["in", "manifest"], ["sha256"]),
                _ => null
            };

            if (arguments is null)
            {
                Console.WriteLine("Invalid selection or cancelled input.");
                continue;
            }

            Console.WriteLine();
            Int32 result = execute(arguments);
            Console.WriteLine("Command exit code: " + result);
        }
    }

    private static String[]? Collect(String command, String[] required, String[] optional)
    {
        List<String> arguments = [command];
        foreach (String name in required)
        {
            Console.Write(name + ": ");
            String? value = Console.ReadLine();
            if (String.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine("Required value missing. Returning to menu.");
                return null;
            }

            arguments.Add("--" + name);
            arguments.Add(value.Trim().Trim('"'));
        }

        foreach (String name in optional)
        {
            Console.Write(name + " (optional, press Enter to skip): ");
            String? value = Console.ReadLine();
            if (value is null)
            {
                return null;
            }

            if (!String.IsNullOrWhiteSpace(value))
            {
                arguments.Add("--" + name);
                arguments.Add(value.Trim().Trim('"'));
            }
        }

        return arguments.ToArray();
    }
}
