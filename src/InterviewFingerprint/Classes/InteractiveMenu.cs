namespace InterviewFingerprint.Classes;

internal static class InteractiveMenu
{
    public static Int32 Run(Func<String[], Int32> execute)
    {
        while (true)
        {
            ShowMainMenu();
            String? selection = Console.ReadLine();
            if (selection is null || selection.Trim() is "0" or "Q" or "q")
            {
                return 0;
            }

            if (selection.Trim() is not ("1" or "2" or "3" or "4" or "5" or "6"))
            {
                Console.WriteLine("That is not a menu choice. Please enter a number from 0 to 6.");
                continue;
            }

            String[]? arguments = ChooseOperation(selection.Trim());

            if (arguments is null)
            {
                Console.WriteLine("Cancelled. No files were changed by this menu selection.");
                continue;
            }

            Console.WriteLine();
            Int32 result = execute(arguments);
            ShowOperationResult(result);
            Console.WriteLine("Press Enter to return to the menu.");
            if (Console.ReadLine() is null)
            {
                return result;
            }
        }
    }

    private static String[]? ChooseOperation(String selection)
    {
        return selection.Trim() switch
        {
            "1" => Collect("mark", "Creates a NEW PNG image with a hidden identifying mark. Your original is not replaced. The mark does not stop others from copying or editing the image.",
                [("in", "Full path to the original image (for example C:\\Pictures\\art.png)"),
                 ("out", "Full path for the NEW marked PNG image"),
                 ("id", "A label you will remember for this submission (for example Company-2026)")],
                [("strength", "Advanced: mark strength from 8 to 60 (press Enter for the default)")]),
            "2" => Collect("verify", "Checks an image for a mark created with your private secret and submission label. A match is not proof of ownership.",
                [("in", "Full path to the image you want to check"),
                 ("id", "The exact submission label used when marking the image")],
                [("reference", "Optional: full path to the originally marked image (helps with some resized or cropped images)")]),
            "3" => Collect("code-fingerprint", "Displays identifying hashes for a C# code file. This does not change the file or prove authorship.",
                [("in", "Full path to the C# code file (for example C:\\Code\\Example.cs)")], []),
            "4" => Collect("compare-code", "Compares two C# code files. Similarity can occur naturally and is not proof of copying.",
                [("a", "Full path to YOUR original C# code file"),
                 ("b", "Full path to the OTHER C# code file")], []),
            "5" => Collect("archive", "Creates a NEW JSON record of files in a folder, so changes can be checked later. It does not copy or protect those files.",
                [("in", "Full path to the folder containing the files"),
                 ("out", "Full path for the NEW record file (for example C:\\Records\\submission.json)"),
                 ("id", "A label for this submission (for example Company-2026)")], []),
            "6" => Collect("check-archive", "Checks files against a previously saved record. A record alone does not prove when files were created.",
                [("in", "Full path to the folder of files you want to check"),
                 ("manifest", "Full path to the JSON record file created earlier")],
                [("sha256", "Optional: the 64-character record checksum you saved separately")]),
            _ => null
        };
    }

    private static void ShowMainMenu()
    {
        Console.WriteLine();
        Console.WriteLine("==============================================");
        Console.WriteLine("           INTERVIEW FINGERPRINT");
        Console.WriteLine("==============================================");
        Console.WriteLine("What would you like to do?");
        Console.WriteLine();
        Console.WriteLine("1. Add a hidden identifying mark to an image");
        Console.WriteLine("2. Check whether an image contains your mark");
        Console.WriteLine("3. Create a record of a C# code file");
        Console.WriteLine("4. Compare two C# code files");
        Console.WriteLine("5. Create a record of a folder of files");
        Console.WriteLine("6. Check whether recorded files have changed");
        Console.WriteLine();
        Console.WriteLine("0. Exit");
        Console.Write("Enter a number (0-6), or Q to exit: ");
    }

    private static void ShowOperationResult(Int32 result)
    {
        Console.WriteLine();
        String message = result switch
        {
            0 => "Operation completed. Review the details above.",
            1 => "The check did not confirm a match, or found changes. Review the details above.",
            _ => "The operation could not be completed. Review the error above."
        };
        Console.WriteLine(message);
    }

    private static String[]? Collect(
        String command,
        String explanation,
        (String Name, String Prompt)[] required,
        (String Name, String Prompt)[] optional)
    {
        Console.WriteLine();
        Console.WriteLine(explanation);
        if (command is "mark" or "verify")
        {
            ShowImageSecretGuidance();
        }

        Console.WriteLine("You can paste a full file or folder path. Surrounding quotes are accepted.");
        Console.WriteLine("Enter Q at any prompt to return to the menu.");
        List<String> arguments = [command];
        foreach ((String name, String prompt) in required)
        {
            String? value = Ask(prompt, true);
            if (value is null)
            {
                return null;
            }

            arguments.Add("--" + name);
            arguments.Add(value);
        }

        foreach ((String name, String prompt) in optional)
        {
            String? value = AskOptionalOption(name, prompt);
            if (value is null)
            {
                return null;
            }

            if (value.Length > 0)
            {
                arguments.Add("--" + name);
                arguments.Add(value);
            }
        }

        return arguments.ToArray();
    }

    private static void ShowImageSecretGuidance()
    {
        String? secret = Environment.GetEnvironmentVariable("IFP_SECRET");
        if (String.IsNullOrWhiteSpace(secret) || secret.Length < 16)
        {
            Console.WriteLine("A private image-marking secret is not configured.");
            Console.WriteLine("Before using image marking or checking, set IFP_SECRET to a private value");
            Console.WriteLine("of at least 16 characters, then restart this program.");
            Console.WriteLine("Keep a secure copy: you need the same secret to check your marks later.");
            return;
        }

        Console.WriteLine("Your private image-marking secret is configured.");
        Console.WriteLine("Use the SAME secret when checking a marked image. Never share it.");
    }

    private static String? AskOptionalOption(String name, String prompt)
    {
        while (true)
        {
            String? value = Ask(prompt, false);
            if (value is null || value.Length == 0)
            {
                return value;
            }

            if (name == "strength")
            {
                if (Double.TryParse(value, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out Double strength) &&
                    Double.IsFinite(strength) && strength is >= 8 and <= 60)
                {
                    return value;
                }

                Console.WriteLine("Enter a number from 8 to 60, such as 24, or press Enter for the default.");
                continue;
            }

            if (name == "sha256" && (value.Length != 64 || !value.All(Uri.IsHexDigit)))
            {
                Console.WriteLine("Enter exactly 64 hexadecimal characters (0-9 or A-F), or press Enter to skip.");
                continue;
            }

            return value;
        }
    }

    private static String? Ask(String prompt, Boolean required)
    {
        while (true)
        {
            Console.WriteLine();
            Console.WriteLine(prompt);
            Console.Write(required ? "Required: " : "Optional (press Enter to skip): ");
            String? input = Console.ReadLine();
            if (input is null || String.Equals(input.Trim(), "Q", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            String value = input.Trim().Trim('"');
            if (required && String.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine("This information is required. Try again, or enter Q to cancel.");
                continue;
            }

            return value;
        }
    }
}
