// See https://aka.ms/new-console-template for more information
using ExtractFaces;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Mpeg;
using MetadataExtractor.Formats.Xmp;
using System.Drawing;
using System.Drawing.Imaging;
using System.Formats.Tar;
using System.Globalization;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

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

            case string s when s.StartsWith("-minsize"):
                var sizes = arg.Substring(9).Split('x');
                if(sizes.Length > 1)
                {
                    commandArgs.MinWidth = int.Parse(sizes[0]);
                    commandArgs.MinHeight = int.Parse(sizes[1]);
                }
                else
                {
                    commandArgs.MinWidth = int.Parse(sizes[0]);
                    commandArgs.MinHeight = int.Parse(sizes[0]);
                }
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
    StringBuilder sb = new StringBuilder();
    sb.AppendLine("All tags");
    sb.AppendLine("=======================");
    foreach (var directory in directories)
    {
        foreach (var tag in directory.Tags)
        {
            sb.AppendLine($"{directory.Name} - {tag.Name} = {tag.Description}");
        }
    }

    sb.AppendLine();
    sb.AppendLine();
    Console.WriteLine(sb.ToString());
}

static void ShowXmp(IReadOnlyList<MetadataExtractor.Directory> directories)
{
    StringBuilder sb = new StringBuilder();
    sb.AppendLine("XMP Data");
    sb.AppendLine("=======================");
    foreach (var directory in directories.OfType<XmpDirectory>())
    {
        foreach (var property in directory.XmpMeta.Properties)
        {
            sb.AppendLine($"Path={property.Path}");
            sb.AppendLine($"    Namespace={property.Namespace}");
            sb.AppendLine($"    Value={property.Value}");
            sb.AppendLine();
        }
    }
    sb.AppendLine();
    sb.AppendLine();
    Console.WriteLine(sb.ToString());
}

void ExtractFacesFromImages(string source, string destination, CommandLineArgs args)
{
    if (!System.IO.Directory.Exists(destination))
    {
        Prompt(args, "Creating directory " + destination);
        System.IO.Directory.CreateDirectory(destination);
    }

    Prompt(args, "Visiting source directory " + source);
    Parallel.ForEach(GetFiles(source), file =>
    {
        if (File.Exists(file))
        {
            try
            {
                ExtractFacesFromImage(file, destination, args);
            }
            catch (Exception ex)
            {
                StringBuilder sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine(@"/!\ ERROR /!\");
                sb.AppendLine(@"/!\ ERROR while extracting faces from an image: " + source);
                sb.AppendLine(ex.ToString());
                sb.AppendLine(@"/!\ ERROR /!\");
                sb.AppendLine();
                Console.WriteLine(sb.ToString());
            }
        }
    });
}

void ExtractFacesFromImage(string source, string destination, CommandLineArgs args)
{
    // Read all metadata from the image
    Prompt(args, "Reading Metadata from " + source);
    using (MemoryStream fileMemoryStream = new MemoryStream(File.ReadAllBytes(source)))
    {
        fileMemoryStream.Position = 0;
        var directories = ImageMetadataReader.ReadMetadata(fileMemoryStream);

        //if (args.ShowExif || args.Verbose)
        //    ShowExif(directories);

        //if (args.ShowXmp || args.Verbose)
        //    ShowXmp(directories);

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
        DateTime dateTaken = GetDateTaken(source, subIfdDirectory);

        Prompt(args, "XMP person found in " + source);
        foreach (var x in dic)
        {
            Prompt(args, x.Key + ": " + x.Value.PersonDisplayName + " --> " + x.Value.Rectangle.ToString());
        }

        Prompt(args, "Loading file content " + source);
        fileMemoryStream.Position = 0;
        using (var bmpSource = System.Drawing.Bitmap.FromStream(fileMemoryStream) as Bitmap)
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
                        SaveFaceImage(args, directories, destination, dateTaken, bmpSource, percentInt, person, flushRect, "Flush");

                    if (commandArgs.Square)
                        SaveFaceImage(args, directories, destination, dateTaken, bmpSource, percentInt, person, squareRect, "Square");
                }
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

static void SaveFaceImage(CommandLineArgs args, IReadOnlyList<MetadataExtractor.Directory>  directories, string destination, DateTime dateTaken, Bitmap? bmpSource, int percentInt, XmpPerson person, Rectangle rect, string cropMode)
{
    if((args.MinWidth != -1 && args.MinWidth > rect.Width) || (args.MinHeight != -1 && args.MinHeight > rect.Height))
    {
        Prompt(args, "Skiping generating face file since rect to extract is smaller than minimum size");
        Prompt(args, "    Person " + person.PersonDisplayName);
        Prompt(args, "    Source " + rect);
        Prompt(args, "    Percent " + percentInt + "%");
        Prompt(args, "    Crop " + cropMode);
        Prompt(args, "    MinSize " + args.MinWidth + "x" + args.MinHeight);

        return;
    }

    if(rect.Right > bmpSource.Width || rect.Bottom > bmpSource.Height)
    {
        Prompt(args, "Skiping generating face file since rect to extract larger then the source material");
        Prompt(args, "    Person " + person.PersonDisplayName);
        Prompt(args, "    Source " + rect);
        Prompt(args, "    Percent " + percentInt + "%");
        Prompt(args, "    Crop " + cropMode);
        Prompt(args, "    MinSize " + args.MinWidth + "x" + args.MinHeight);

        return;
    }

    string dir = Path.Combine(destination, person.PersonDisplayName);

    if (!System.IO.Directory.Exists(dir))
    {
        Prompt(args, "Creating directory " + dir);
        System.IO.Directory.CreateDirectory(dir);
    }


    string filename = Path.Combine(dir,
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
    using (var bmpFace = ImageOrientation(directories, bmpSource.Clone(rect, bmpSource.PixelFormat) as Bitmap))
    {

        bmpFace.Save(filename, ImageFormat.Jpeg);
    }
}

static void Prompt(CommandLineArgs args, string message)
{
    if (args.Verbose)
        Console.WriteLine(DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + message);
}

static DateTime GetDateTaken(string source, ExifSubIfdDirectory? subIfdDirectory)
{
    DateTime dateTaken;

    // Read the DateTime tag value from the first with one.  Using logical or (||) ensure that it will stop evaluation with 1st true
    if (subIfdDirectory != null && 
        (subIfdDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTimeOriginal, out dateTaken) ||
        subIfdDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTimeDigitized, out dateTaken)  ||
        subIfdDirectory.TryGetDateTime(ExifDirectoryBase.TagDateTime, out dateTaken)))
    {
        return dateTaken;
    }

    return System.IO.File.GetCreationTime(source);
}



// Adapted from: http://www.csharphelper.com/howtos/howto_orient_image.html
// And from: https://stackoverflow.com/questions/71628565/problem-obtaining-orientation-using-metadataextractor-getdescription
static Bitmap ImageOrientation(IReadOnlyList<MetadataExtractor.Directory> directories, Bitmap img)
{
    var ifd0Directory = directories.OfType<ExifIfd0Directory>().FirstOrDefault();
    var orientation = ExifOrientations.Unknown;
    if (ifd0Directory != null)
    {
        orientation = ifd0Directory.TryGetInt32(ExifDirectoryBase.TagOrientation, out int value) ? (ExifOrientations) value : ExifOrientations.Unknown;
    }

    // Orient the image.
    switch (orientation)
    {
        case ExifOrientations.Unknown:
        case ExifOrientations.TopLeft:
            break;
        case ExifOrientations.TopRight:
            img.RotateFlip(RotateFlipType.RotateNoneFlipX);
            break;
        case ExifOrientations.BottomRight:
            img.RotateFlip(RotateFlipType.Rotate180FlipNone);
            break;
        case ExifOrientations.BottomLeft:
            img.RotateFlip(RotateFlipType.RotateNoneFlipY);
            break;
        case ExifOrientations.LeftTop:
            img.RotateFlip(RotateFlipType.Rotate90FlipX);
            break;
        case ExifOrientations.RightTop:
            img.RotateFlip(RotateFlipType.Rotate90FlipNone);
            break;
        case ExifOrientations.RightBottom:
            img.RotateFlip(RotateFlipType.Rotate90FlipY);
            break;
        case ExifOrientations.LeftBottom:
            img.RotateFlip(RotateFlipType.Rotate270FlipNone);
            break;
    }

    return img;
}

// Adapted from: https://stackoverflow.com/questions/929276/how-to-recursively-list-all-the-files-in-a-directory-in-c
static IEnumerable<string> GetFiles(string path)
{
    Queue<string> directoriesQueue = new Queue<string>();
    directoriesQueue.Enqueue(path);
    while (directoriesQueue.Count > 0)
    {
        path = directoriesQueue.Dequeue();
        
        try
        {
            foreach (string subDir in System.IO.Directory.GetDirectories(path))
            {
                directoriesQueue.Enqueue(subDir);
            }
        }
        catch (Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(@"/!\ ERROR /!\");
            sb.AppendLine(@"/!\ ERROR while enumerating directory: " + path);
            sb.AppendLine(ex.ToString());
            sb.AppendLine(@"/!\ ERROR /!\");
            sb.AppendLine();
            Console.WriteLine(sb.ToString());
        }

        IEnumerable<string> files = null;
        try
        {
            files = System.IO.Directory.GetFiles(path).Where(_ => _.ToLower().EndsWith(".jpg")
                                                                    || _.ToLower().EndsWith(".jpeg")
                                                                    || _.ToLower().EndsWith(".png"));
        }
        catch (Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine(@"/!\ ERROR /!\");
            sb.AppendLine(@"/!\ ERROR while enumerating files in a directory: " + path);
            sb.AppendLine(ex.ToString());
            sb.AppendLine(@"/!\ ERROR /!\");
            sb.AppendLine();
            Console.WriteLine(sb.ToString());
        }
        
        if (files != null)
        {
            foreach(string file in files) 
            {
                yield return file;
            }
        }
    }
}