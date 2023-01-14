// See https://aka.ms/new-console-template for more information
using ExtractFaces;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Mpeg;
using MetadataExtractor.Formats.Xmp;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;

var commandArgs = InitializeCommandLineArgs(args);

ExtractFacesFromImages(commandArgs.Source, commandArgs.Destination, commandArgs);

Prompt(commandArgs, "Done");

if (commandArgs.WaitKeystrokeToEnd)
{
    Console.WriteLine("Press any key ton continue.");
    Console.ReadKey();
}



CommandLineArgs InitializeCommandLineArgs(string[] args)
{
    CommandLineArgs commandArgs = new CommandLineArgs();
    foreach (string arg in args)
    {
        switch (arg.ToLower())
        {
            case "-showexif":
                commandArgs.ShowExif = true;
                break;

            case "-showxmp":
                commandArgs.ShowXmp = true;
                break;

            case "-waitkey":
                commandArgs.WaitKeystrokeToEnd = true;
                break;

            case "-flush":
                commandArgs.Flush = true;
                break;

            case "-verbose":
                commandArgs.Verbose = true;
                break;

            case "-square":
                commandArgs.Square = true;
                break;

            case "-recursive":
                commandArgs.Recursive = true;
                break;

            case string s when s.StartsWith("-person"):
                commandArgs.Persons = arg.Substring(8).Split('|').ToHashSet();
                break;

            case string s when s.StartsWith("-percent"):
                var percents = arg.Substring(9).Split('|').ToHashSet();
                commandArgs.Percents = percents.Select(_ => int.Parse(_)).ToList();
                break;

            case string s when s.StartsWith("-source"):
                commandArgs.Source = arg.Substring(8);
                break;

            case string s when s.StartsWith("-destination"):
                commandArgs.Destination = arg.Substring(13);
                break;

            default:
                Console.WriteLine(@"/!\ Warning: Parameter " + args + " not recognized");
                break;
        }
    }

    if (commandArgs.Percents == null)
    {
        commandArgs.Percents = new List<int> { 100 };
    }

    if (!commandArgs.Flush && !commandArgs.Square)
    {
        Console.WriteLine(@"/!\ Warning: Neither -flush nor -square parameter specified, the application will assume -flush");
        commandArgs.Flush = true;
    }

    if (commandArgs.Persons == null)
    {
        Console.WriteLine(@"/!\ Warning: Parameter -person not specified, we will generate all persons found");
    }

    if (string.IsNullOrWhiteSpace(commandArgs.Source))
    {
        Console.WriteLine(@"/!\ Warning: Parameter -source:""File or directory path"" not specified, assuming current directory: " + Environment.CurrentDirectory);
        commandArgs.Source = Environment.CurrentDirectory;
    }

    if (string.IsNullOrWhiteSpace(commandArgs.Destination))
    {
        Console.WriteLine(@"/!\ Warning: Parameter -destination:""File or directory path"" not specified, assuming current directory: " + Environment.CurrentDirectory);
        commandArgs.Destination = Environment.CurrentDirectory;
    }

    //todo: might add logic to validate that
    //    source exist and is a directory or an image file
    //    destination is a valid directory or one that can be created
    // ... but for the moment, those will generate inelegant .net error and I am fine with it.

    return commandArgs;
}

static void ShowExif(IReadOnlyList<MetadataExtractor.Directory> directories)
{
    Console.WriteLine("All tags");
    Console.WriteLine("=======================");
    foreach (var directory in directories)
    {
        foreach (var tag in directory.Tags)
        {
            Console.WriteLine($"{directory.Name} - {tag.Name} = {tag.Description}");
        }
    }

    Console.WriteLine();
    Console.WriteLine();
}

static void ShowXmp(IReadOnlyList<MetadataExtractor.Directory> directories)
{
    Console.WriteLine("XMP Data");
    Console.WriteLine("=======================");
    foreach (var directory in directories.OfType<XmpDirectory>())
    {
        foreach (var property in directory.XmpMeta.Properties)
        {
            Console.WriteLine($"Path={property.Path}");
            Console.WriteLine($"    Namespace={property.Namespace}");
            Console.WriteLine($"    Value={property.Value}");
            Console.WriteLine();
        }
    }
    Console.WriteLine();
    Console.WriteLine();
}

void ExtractFacesFromImages(string source, string destination, CommandLineArgs args)
{
    Prompt(args, "Visiting source directory " + source);
    if (File.Exists(source))
    {
        try
        {
            ExtractFacesFromImage(source, destination, args);
        }
        catch (Exception ex)
        {
            Console.WriteLine();
            Console.WriteLine(@"/!\ ERROR /!\");
            Console.WriteLine(@"/!\ ERROR while extracting faces from an image: " + source);
            Console.WriteLine(ex.ToString());
            Console.WriteLine(@"/!\ ERROR /!\");
            Console.WriteLine();
        }
    }

    if (!System.IO.Directory.Exists(destination))
    {
        Prompt(args, "Creating directory " + destination);
        System.IO.Directory.CreateDirectory(destination);
    }

    if (System.IO.Directory.Exists(source))
    {
        foreach (var filename in System.IO.Directory.EnumerateFiles(source).Where(_ => _.ToLower().EndsWith(".jpg")
                                                                                   || _.ToLower().EndsWith(".jpeg")
                                                                                   || _.ToLower().EndsWith(".png")))
        {
            try
            {
                ExtractFacesFromImage(filename, destination, args);
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine(@"/!\ ERROR /!\");
                Console.WriteLine(@"/!\ ERROR while extracting faces from an image: " + filename);
                Console.WriteLine(ex.ToString());
                Console.WriteLine(@"/!\ ERROR /!\");
                Console.WriteLine();
            }
        }

        if (args.Recursive)
        {
            foreach (var directory in System.IO.Directory.EnumerateDirectories(source))
            {
                try
                {
                    ExtractFacesFromImages(directory, destination, args);
                }
                catch (Exception ex)
                {
                    Console.WriteLine();
                    Console.WriteLine(@"/!\ ERROR /!\");
                    Console.WriteLine(@"/!\ ERROR while enumerating directory: " + directory);
                    Console.WriteLine(ex.ToString());
                    Console.WriteLine(@"/!\ ERROR /!\");
                    Console.WriteLine();
                }
            }
        }
    }

}

void ExtractFacesFromImage(string source, string destination, CommandLineArgs args)
{
    // Read all metadata from the image
    Prompt(args, "Reading Metadata from " + source);
    var directories = ImageMetadataReader.ReadMetadata(source);

    if (args.ShowExif || args.Verbose)
        ShowExif(directories);

    if (args.ShowXmp || args.Verbose)
        ShowXmp(directories);

    Dictionary<int, XmpPerson> dic = new Dictionary<int, XmpPerson>();
    foreach (var directory in directories.OfType<XmpDirectory>())
    {
        foreach (var property in directory.XmpMeta.Properties.Where(_ => (_?.Path?.StartsWith(@"MP:RegionInfo/MPRI:Regions") ?? false) && ((_?.Path?.EndsWith(@"/MPReg:PersonDisplayName") ?? false) || (_?.Path?.EndsWith(@"/MPReg:Rectangle") ?? false))).OrderBy(_ => _.Path))
        {
            int index = int.Parse(property.Path.Replace("MP:RegionInfo/MPRI:Regions[", string.Empty).Replace("]/MPReg:Rectangle", string.Empty).Replace("]/MPReg:PersonDisplayName", string.Empty));
            if (!dic.ContainsKey(index))
                dic.Add(index, new XmpPerson());

            if (property.Path.EndsWith("]/MPReg:Rectangle"))
            {
                var x = property.Value.Split(',').Select(_ => float.Parse(_, NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat)).ToArray();
                dic[index].Rectangle = new RectangleF(x[0], x[1], x[2], x[3]);
            }

            if (property.Path.EndsWith("]/MPReg:PersonDisplayName"))
            {
                dic[index].PersonDisplayName = property.Value;
            }
        }
    }

    // https://stackoverflow.com/questions/180030/how-can-i-find-out-when-a-picture-was-actually-taken-in-c-sharp-running-on-vista
    // Find the so-called Exif "SubIFD" (which may be null)
    var subIfdDirectory = directories.OfType<ExifSubIfdDirectory>().FirstOrDefault();

    // Read the DateTime tag value
    var dateTaken = (DateTime)subIfdDirectory?.GetDateTime(ExifDirectoryBase.TagDateTimeOriginal);

    Prompt(args, "XMP person found in " + source);
    foreach (var x in dic)
    {
        Prompt(args, x.Key + ": " + x.Value.PersonDisplayName + " --> " + x.Value.Rectangle.ToString());
    }

    Prompt(args, "Loading file content " + source);
    using (var bmpSource = System.Drawing.Bitmap.FromFile(source) as Bitmap)
    {
        foreach (var percentInt in commandArgs.Percents)
        {
            var percent = percentInt / 100.0;

            var persons = args.Persons == null ? dic.Values : dic.Values.Where(x => args.Persons.Contains(x.PersonDisplayName));

            foreach (var person in persons)
            {
                Prompt(args, "Generating faces for " + person.PersonDisplayName);
                Point center = new Point((int)((person.Rectangle.X + person.Rectangle.Width / 2) * bmpSource.Width),
                                         (int)((person.Rectangle.Y + person.Rectangle.Height / 2) * bmpSource.Height));

                Point flushDelta = new Point((int)(person.Rectangle.Width * percent / 2 * bmpSource.Width),
                                             (int)(person.Rectangle.Height * percent / 2 * bmpSource.Height));

                var flushRect = new Rectangle(center.X - flushDelta.X,
                                              center.Y - flushDelta.Y,
                                              (int)(person.Rectangle.Width * percent * bmpSource.Width),
                                              (int)(person.Rectangle.Height * percent * bmpSource.Height));

                NormalizeRectInBorders(ref flushRect, bmpSource.Width, bmpSource.Height, false);

                int longestSide = Math.Max(flushRect.Width, flushRect.Height);

                var squareRect = new Rectangle(center.X - longestSide / 2,
                                               center.Y - longestSide / 2,
                                               longestSide,
                                               longestSide);

                NormalizeRectInBorders(ref squareRect, bmpSource.Width, bmpSource.Height, true);


                if (commandArgs.Flush)
                    SaveFaceImage(args, destination, dateTaken, bmpSource, percentInt, person, flushRect, "Flush");

                if (commandArgs.Square)
                    SaveFaceImage(args, destination, dateTaken, bmpSource, percentInt, person, squareRect, "Square");
            }
        }
    }
}

static void NormalizeRectInBorders(ref Rectangle rect, int width, int height, bool square)
{
    if (rect.Width > width)
    {
        if (square)
        {
            int newWidth = Math.Min(Math.Min(width, height), rect.Height);
            rect.X = rect.X + (rect.Width - newWidth) / 2;
            rect.Width = newWidth;
            rect.Y = rect.Y + (rect.Height - newWidth) / 2;
            rect.Height = newWidth;
        }
        else
        {
            rect.X = rect.X + (rect.Width - width) / 2;
            rect.Width = width;
        }
    }

    if (rect.Height > height)
    {
        if (square)
        {
            int newHeight = Math.Min(Math.Min(width, height), rect.Width);
            rect.Y = rect.Y + (rect.Height - newHeight) / 2;
            rect.Height = newHeight;
            rect.X = rect.X + (rect.Width - newHeight) / 2;
            rect.Width = newHeight;
        }
        else
        {
            rect.Y = rect.Y + (rect.Height - height) / 2;
            rect.Height = height;
        }
    }

    if (rect.X < 0)
    {
        rect.X = 0;
    }

    if (rect.Y < 0)
    {
        rect.Y = 0;
    }

    if (rect.X + rect.Width > width)
    {
        rect.X -= rect.Width - width;
    }

    if (rect.Y + rect.Height > height)
    {
        rect.Y -= rect.Height - height;
    }
}

static void SaveFaceImage(CommandLineArgs args, string destination, DateTime dateTaken, Bitmap? bmpSource, int percentInt, XmpPerson person, Rectangle rect, string cropMode)
{
    string filename = Path.Combine(destination,
                                    dateTaken.ToString("yyyy-MM-dd HH-mm-ss") +
                                    " - " + person.PersonDisplayName +
                                    " - " + cropMode +
                                    " - Zoom " + percentInt +
                                    " (" + args.TakeDestinationFilePrefix() + ")" +
                                    ".jpg");

    Prompt(args, "Saving face file " + filename);
    Prompt(args, "    Person " + person.PersonDisplayName);
    Prompt(args, "    Source " + rect);
    Prompt(args, "    Percent " + percentInt +"%");
    Prompt(args, "    Crop " + cropMode);
    using (var bmpFace = bmpSource.Clone(rect, bmpSource.PixelFormat) as Bitmap)
    {

        bmpFace.Save(filename, ImageFormat.Jpeg);
    }
}

static void Prompt(CommandLineArgs args, string message)
{
    if (args.Verbose)
        Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message);
}